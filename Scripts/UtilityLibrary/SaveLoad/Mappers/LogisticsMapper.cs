using System;
using System.Collections.Generic;
using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary.SaveLoad.Dto;

namespace UtilityLibrary.SaveLoad.Mappers;

/// <summary>
/// Load half for <see cref="LogisticsUnit"/> (save_version 2): rebuilds a unit and its mid-transfer
/// state / orbital schedule from a <see cref="LogisticsUnitDto"/>, resolving body references by node
/// name. The save half lives on the unit itself (<c>LogisticsUnit.Serialize()</c>); the loader keeps
/// the restore path because a unit cannot instantiate or re-parent itself.
/// </summary>
public static class LogisticsMapper
{
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

    // ---------- Schedule restore ----------

    /// <summary>
    /// Rebuilds an <see cref="OrbitalTransferSchedule"/> from its DTO. Endpoints are
    /// resolved by body node name and station id (callers supply resolvers populated
    /// after bodies and stations have been restored — loader pass 7). A Running
    /// schedule is downgraded to Stopped by the caller's executor restore so it never
    /// auto-departs on load. Returns null if a leg endpoint cannot be resolved.
    /// </summary>
    public static OrbitalTransferSchedule? RestoreSchedule(
        OrbitalScheduleDto dto,
        Func<string, IOrbitalBody?> resolveBody,
        Func<string, StationSatellite?> resolveStation)
    {
        if (dto == null || dto.Legs.Count == 0)
            return null;

        var schedule = new OrbitalTransferSchedule
        {
            ScheduleId = string.IsNullOrEmpty(dto.ScheduleId) ? Guid.NewGuid().ToString() : dto.ScheduleId,
            WaitPeriodBetweenLegs = dto.WaitPeriodBetweenLegs,
            RetryPeriod = dto.RetryPeriod,
            MaxRetries = dto.MaxRetries,
            IsRepeating = dto.IsRepeating,
            State = ParseScheduleState(dto.State),
            CurrentLegIndex = dto.CurrentLegIndex,
        };

        foreach (var legDto in dto.Legs)
        {
            var origin = RestoreEndpoint(legDto.Origin, resolveBody, resolveStation);
            var dest = RestoreEndpoint(legDto.Destination, resolveBody, resolveStation);
            if (origin == null || dest == null)
            {
                GameLogger.Warning(
                    $"[LogisticsMapper] Schedule {schedule.ScheduleId}: leg '{legDto.LegId}' has an unresolved endpoint; skipping schedule restore.");
                return null;
            }

            schedule.Legs.Add(new Leg
            {
                LegId = string.IsNullOrEmpty(legDto.LegId) ? Guid.NewGuid().ToString() : legDto.LegId,
                Origin = origin,
                Destination = dest,
                PickupOrder = DictToManifest(legDto.PickupOrder),
                DropoffOrder = DictToManifest(legDto.DropoffOrder),
                MaxWaitSeconds = legDto.MaxWaitSeconds,
                IsClosingLeg = legDto.IsClosingLeg,
                State = ParseLegState(legDto.State),
                DepartureConstraints = new DepartureConstraints
                {
                    BudgetMode = ParseBudgetMode(legDto.DepartureConstraints.BudgetMode),
                    MinBudget = legDto.DepartureConstraints.MinBudget,
                    MaxBudget = legDto.DepartureConstraints.MaxBudget,
                    NumOptions = legDto.DepartureConstraints.NumOptions,
                    RankingCriteria = ParseRanking(legDto.DepartureConstraints.RankingCriteria),
                },
                RefuelInstructions = new RefuelInstructions
                {
                    Policy = ParseRefuelPolicy(legDto.RefuelInstructions.Policy),
                    FuelResourceId = legDto.RefuelInstructions.FuelResourceId,
                    Amount = legDto.RefuelInstructions.Amount,
                },
            });
        }

        return schedule.Legs.Count > 0 ? schedule : null;
    }

    private static LegEndpoint? RestoreEndpoint(
        LegEndpointDto dto,
        Func<string, IOrbitalBody?> resolveBody,
        Func<string, StationSatellite?> resolveStation)
    {
        if (dto == null || string.IsNullOrEmpty(dto.BodyName))
            return null;
        var body = resolveBody(dto.BodyName!);
        if (body == null)
            return null;

        if (!string.IsNullOrEmpty(dto.StationId))
        {
            var station = resolveStation(dto.StationId!);
            if (station != null)
                return LegEndpoint.ForStation(station, body, dto.BandIndex);
        }
        return LegEndpoint.ForBody(body, dto.BandIndex);
    }

    private static CargoManifest? DictToManifest(Dictionary<string, int>? d)
    {
        if (d == null || d.Count == 0)
            return null;
        var m = new CargoManifest();
        foreach (var kv in d)
            m.LoadResource(kv.Key, kv.Value);
        return m;
    }

    private static OrbitalScheduleState ParseScheduleState(string s) =>
        Enum.TryParse<OrbitalScheduleState>(s, out var v) ? v : OrbitalScheduleState.Idle;

    private static LegState ParseLegState(string s) =>
        Enum.TryParse<LegState>(s, out var v) ? v : LegState.Pending;

    private static ExpenditureBudgetMode ParseBudgetMode(string s) =>
        Enum.TryParse<ExpenditureBudgetMode>(s, out var v) ? v : ExpenditureBudgetMode.TimeOfFlight;

    private static RefuelPolicy ParseRefuelPolicy(string s) =>
        Enum.TryParse<RefuelPolicy>(s, out var v) ? v : RefuelPolicy.None;

    private static TrajectorySolution.RankingCriteria ParseRanking(string s) =>
        Enum.TryParse<TrajectorySolution.RankingCriteria>(s, out var v)
            ? v
            : TrajectorySolution.RankingCriteria.MostEfficient;
}
