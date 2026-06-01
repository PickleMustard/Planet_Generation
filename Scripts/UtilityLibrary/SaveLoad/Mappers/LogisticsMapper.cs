using System;
using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary.SaveLoad.Dto;

namespace UtilityLibrary.SaveLoad.Mappers;

/// <summary>
/// Maps <see cref="LogisticsUnit"/> nodes to <see cref="LogisticsUnitDto"/> and back (save_version 2).
/// Captures FSM state, fuel, cargo, engine (with ordered modifiers) and — for a unit mid-transfer —
/// the full movement-controller transit state so the Kepler-propagated arc and burn-profile fuel
/// consumption resume identically. Body references are persisted/resolved by node name.
/// </summary>
public static class LogisticsMapper
{
    // ---------- Save ----------

    public static LogisticsUnitDto ToDto(LogisticsUnit unit)
    {
        var dto = new LogisticsUnitDto
        {
            Name = unit.Name,
            Id = unit.Id,
            HostBodyName = FindHostBodyName(unit),
            State = unit.State.ToString(),
            IsActive = unit.IsActive,
            IsStationary = unit.IsStationary,
            BandIndex = unit.BandIndex,
            Fuel = unit.Fuel,
            MaxFuel = unit.MaxFuel,
            DryMass = unit.DryMass,
            Position = Vec3Dto.From(unit.Position),
            Velocity = Vec3Dto.From(unit.Velocity),
            OrbitalAngle = unit.OrbitalAngle,
            OrbitalRadius = unit.OrbitalRadius,
            OrbitalSpeed = unit.OrbitalSpeed,
            HostMass = unit.HostMass,
        };

        if (unit.Cargo != null)
            foreach (var kv in unit.Cargo.Resources)
                dto.Cargo[kv.Key] = kv.Value;

        if (unit.CurrentEngine != null)
            dto.Engine = EngineToDto(unit.CurrentEngine);

        var mc = unit.MovementController;
        if (mc != null && mc.IsTransferring && mc.ActiveTrajectory != null)
            dto.Transit = TransitToDto(mc);

        return dto;
    }

    private static EngineDto EngineToDto(EngineDefinition engine)
    {
        var dto = new EngineDto
        {
            BaseSpecificImpulse = engine.BaseSpecificImpulse,
            BaseThrust = engine.BaseThrust,
        };

        foreach (var mod in engine.GetActiveModifiers())
        {
            dto.Modifiers.Add(new EngineModifierDto
            {
                Type = mod.Type.ToString(),
                Source = mod.Source,
                IspValue = mod.IspValue,
                ThrustValue = mod.ThrustValue,
            });
        }

        return dto;
    }

    private static TransitStateDto TransitToDto(LogisticsMovementController mc)
    {
        var traj = mc.ActiveTrajectory!;
        var dto = new TransitStateDto
        {
            DestinationBodyName = (mc.DestinationBody as Node)?.Name,
            OriginBodyName = (mc.OriginBody as Node)?.Name,
            CentralBodyName = (mc.CentralBody as Node)?.Name,
            TransferTime = mc.TransferTime,
            TimeInTransfer = mc.TimeInTransfer,
            DeparturePosition = Vec3Dto.From(mc.DeparturePosition),
            TargetPosition = Vec3Dto.From(mc.TargetPosition),
            DeparturePositionGlobal = Vec3Dto.From(mc.DeparturePositionGlobal),
            GravitationalParameter = mc.GravitationalParameter,
            CurrentTransitPhase = mc.CurrentTransitPhase.ToString(),
            FuelConsumedThisTransfer = mc.FuelConsumedThisTransfer,
            SimulationMode = mc.CurrentSimulationMode.ToString(),
            Trajectory = TrajectoryToDto(traj),
        };

        if (mc.ActiveBurnProfile != null)
            dto.BurnProfile = BurnProfileToDto(mc.ActiveBurnProfile);

        return dto;
    }

    private static TrajectoryDto TrajectoryToDto(TrajectorySolution t) => new()
    {
        InitialVelocity = Vec3Dto.From(t.InitialVelocity),
        FinalVelocity = Vec3Dto.From(t.FinalVelocity),
        TimeOfFlight = t.TimeOfFlight,
        DeltaVRequired = t.DeltaVRequired,
        DepartureDeltaV = t.DepartureDeltaV,
        ArrivalDeltaV = t.ArrivalDeltaV,
        SemiMajorAxis = t.SemiMajorAxis,
        Eccentricity = t.Eccentricity,
        Inclination = t.Inclination,
        AscendingNodeLongitude = t.AscendingNodeLongitude,
        ArgumentOfPeriapsis = t.ArgumentOfPeriapsis,
        MeanAnomaly = t.MeanAnomaly,
        Revolutions = t.Revolutions,
        TransferType = t.TransferType.ToString(),
        GravitationalParameter = t.GravitationalParameter,
        PredictedOriginPosition = Vec3Dto.From(t.PredictedOriginPosition),
        PredictedDestinationPosition = Vec3Dto.From(t.PredictedDestinationPosition),
        OriginOrbitalVelocity = Vec3Dto.From(t.OriginOrbitalVelocity),
        DestinationOrbitalVelocity = Vec3Dto.From(t.DestinationOrbitalVelocity),
        OriginBandIndex = t.OriginBandIndex,
        DestinationBandIndex = t.DestinationBandIndex,
        DepartureTime = t.DepartureTime,
        FuelRequired = t.FuelRequired,
    };

    private static BurnProfileDto BurnProfileToDto(BurnProfile b) => new()
    {
        AccelBurnDuration = b.AccelBurnDuration,
        CoastDuration = b.CoastDuration,
        DecelBurnDuration = b.DecelBurnDuration,
        TotalDuration = b.TotalDuration,
        AccelFuelBudget = b.AccelFuelBudget,
        DecelFuelBudget = b.DecelFuelBudget,
        TotalFuelBudget = b.TotalFuelBudget,
        AccelFuelRate = b.AccelFuelRate,
        DecelFuelRate = b.DecelFuelRate,
        AccelEndTime = b.AccelEndTime,
        DecelStartTime = b.DecelStartTime,
    };

    /// <summary>Walks the parent chain to the celestial body this unit orbits, returning its node
    /// name. Null when the unit is adrift (in transit / stranded) directly under the system container.</summary>
    private static string? FindHostBodyName(LogisticsUnit unit)
    {
        Node? parent = unit.GetParent();
        while (parent != null)
        {
            if (parent is CelestialBody body)
                return body.Name;
            parent = parent.GetParent();
        }
        return null;
    }

    // ---------- Load ----------

    /// <summary>
    /// Builds a configured (but not-yet-parented) LogisticsUnit from a DTO. Non-tree state (fuel,
    /// cargo, engine, orbital params, FSM state) is applied immediately; mid-transfer state is queued
    /// via <see cref="LogisticsUnit.SetPendingControllerRestore"/> and applied once the movement
    /// controller is ready. The caller parents the unit and sets its local Position.
    /// </summary>
    public static LogisticsUnit Restore(LogisticsUnitDto dto, Func<string, IOrbitalBody?> resolveBody)
    {
        var unit = new LogisticsUnit { Name = dto.Name };

        // Set persisted Id BEFORE _EnterTree fires (caller AddChild's after Restore returns).
        // The _EnterTree Guid branch is skipped when Id is non-empty, so the unit registers
        // under its saved Id. Pre-Id saves leave dto.Id == "" and fall through to the fresh Guid.
        unit.SetPersistedId(dto.Id);

        unit.IsActive = dto.IsActive;
        unit.IsStationary = dto.IsStationary;
        unit.BandIndex = dto.BandIndex;
        unit.SetFuelCapacity(dto.MaxFuel);
        unit.Fuel = dto.Fuel;
        unit.SetDryMass(dto.DryMass);
        unit.OrbitalAngle = dto.OrbitalAngle;
        unit.OrbitalRadius = dto.OrbitalRadius;
        unit.OrbitalSpeed = dto.OrbitalSpeed;
        unit.HostMass = dto.HostMass;
        unit.Velocity = dto.Velocity.ToVector3();
        unit.MarkInitialized();

        // Cargo
        unit.InitializeCargo();
        foreach (var kv in dto.Cargo)
            unit.Cargo?.LoadResource(kv.Key, kv.Value);

        // Engine + ordered modifiers
        if (dto.Engine != null)
            unit.SetEngine(RestoreEngine(dto.Engine));

        // FSM state — set directly (the saved state is already a valid resting/transit state).
        unit.State = ParseState(dto.State);

        // Mid-transfer state is restored onto the controller once it exists.
        if (dto.Transit != null)
        {
            var transit = dto.Transit;
            unit.SetPendingControllerRestore(controller =>
                RestoreTransit(controller, transit, resolveBody));
        }

        return unit;
    }

    private static EngineDefinition RestoreEngine(EngineDto dto)
    {
        var engine = new EngineDefinition(dto.BaseSpecificImpulse, dto.BaseThrust);
        foreach (var mod in dto.Modifiers)
        {
            var type = ParseModifierType(mod.Type);
            EngineModifier modifier = type == ModifierType.Multiplicative
                ? EngineModifier.Multiplicative(mod.Source, mod.IspValue, mod.ThrustValue)
                : EngineModifier.Additive(mod.Source, mod.IspValue, mod.ThrustValue);
            engine.ApplyModifier(modifier);
        }
        return engine;
    }

    private static void RestoreTransit(
        LogisticsMovementController controller,
        TransitStateDto dto,
        Func<string, IOrbitalBody?> resolveBody)
    {
        var trajectory = RestoreTrajectory(dto.Trajectory, dto, resolveBody);

        BurnProfile? burnProfile = dto.BurnProfile == null
            ? null
            : BurnProfile.Restore(
                dto.BurnProfile.AccelBurnDuration,
                dto.BurnProfile.CoastDuration,
                dto.BurnProfile.DecelBurnDuration,
                dto.BurnProfile.TotalDuration,
                dto.BurnProfile.AccelFuelBudget,
                dto.BurnProfile.DecelFuelBudget,
                dto.BurnProfile.TotalFuelBudget,
                dto.BurnProfile.AccelFuelRate,
                dto.BurnProfile.DecelFuelRate,
                dto.BurnProfile.AccelEndTime,
                dto.BurnProfile.DecelStartTime);

        controller.RestoreTransitState(
            trajectory,
            ResolveOrNull(dto.OriginBodyName, resolveBody),
            ResolveOrNull(dto.DestinationBodyName, resolveBody),
            ResolveOrNull(dto.CentralBodyName, resolveBody),
            dto.TransferTime,
            dto.TimeInTransfer,
            dto.DeparturePosition.ToVector3(),
            dto.TargetPosition.ToVector3(),
            dto.DeparturePositionGlobal.ToVector3(),
            dto.GravitationalParameter,
            burnProfile,
            ParseTransitPhase(dto.CurrentTransitPhase),
            dto.FuelConsumedThisTransfer,
            ParseSimulationMode(dto.SimulationMode));
    }

    private static TrajectorySolution RestoreTrajectory(
        TrajectoryDto dto,
        TransitStateDto transit,
        Func<string, IOrbitalBody?> resolveBody)
    {
        return new TrajectorySolution
        {
            InitialVelocity = dto.InitialVelocity.ToVector3(),
            FinalVelocity = dto.FinalVelocity.ToVector3(),
            TimeOfFlight = dto.TimeOfFlight,
            DeltaVRequired = dto.DeltaVRequired,
            DepartureDeltaV = dto.DepartureDeltaV,
            ArrivalDeltaV = dto.ArrivalDeltaV,
            SemiMajorAxis = dto.SemiMajorAxis,
            Eccentricity = dto.Eccentricity,
            Inclination = dto.Inclination,
            AscendingNodeLongitude = dto.AscendingNodeLongitude,
            ArgumentOfPeriapsis = dto.ArgumentOfPeriapsis,
            MeanAnomaly = dto.MeanAnomaly,
            Revolutions = dto.Revolutions,
            TransferType = ParseTransferType(dto.TransferType),
            GravitationalParameter = dto.GravitationalParameter,
            PredictedOriginPosition = dto.PredictedOriginPosition.ToVector3(),
            PredictedDestinationPosition = dto.PredictedDestinationPosition.ToVector3(),
            OriginOrbitalVelocity = dto.OriginOrbitalVelocity.ToVector3(),
            DestinationOrbitalVelocity = dto.DestinationOrbitalVelocity.ToVector3(),
            OriginBandIndex = dto.OriginBandIndex,
            DestinationBandIndex = dto.DestinationBandIndex,
            DepartureTime = dto.DepartureTime,
            FuelRequired = dto.FuelRequired,
            OriginBody = ResolveOrNull(transit.OriginBodyName, resolveBody),
            DestinationBody = ResolveOrNull(transit.DestinationBodyName, resolveBody),
        };
    }

    private static IOrbitalBody? ResolveOrNull(string? name, Func<string, IOrbitalBody?> resolveBody) =>
        string.IsNullOrEmpty(name) ? null : resolveBody(name!);

    private static LogisticsUnitState ParseState(string s) =>
        Enum.TryParse<LogisticsUnitState>(s, out var v) ? v : LogisticsUnitState.Idle;

    private static ModifierType ParseModifierType(string s) =>
        Enum.TryParse<ModifierType>(s, out var v) ? v : ModifierType.Additive;

    private static TransitPhase ParseTransitPhase(string s) =>
        Enum.TryParse<TransitPhase>(s, out var v) ? v : TransitPhase.Coasting;

    private static TransferType ParseTransferType(string s) =>
        Enum.TryParse<TransferType>(s, out var v) ? v : TransferType.Direct;

    private static LogisticsMovementController.SimulationMode ParseSimulationMode(string s) =>
        Enum.TryParse<LogisticsMovementController.SimulationMode>(s, out var v)
            ? v
            : LogisticsMovementController.SimulationMode.FullKepler;
}
