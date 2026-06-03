using System.Collections.Generic;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary.SaveLoad;
using UtilityLibrary.SaveLoad.Dto;

namespace Constructables;

/// <summary>
/// Save side of <see cref="LogisticsUnit"/> (save_version 2). Builds a <see cref="LogisticsUnitDto"/>
/// capturing FSM state, fuel, cargo, engine (with ordered modifiers), mid-transfer controller state and
/// the active orbital schedule. The matching load path stays in <c>LogisticsMapper.Restore</c>: a unit
/// cannot rebuild itself (the loader instantiates and parents it, resolving body refs by name).
/// Discovered by the saver through <c>SystemData.GetAllShips()</c>, not the "save_serializable" group.
/// </summary>
public partial class LogisticsUnit : ISaveSerializable
{
    /// <summary>Section discriminator (the saver routes units via the ship registry, not by key).</summary>
    public string SaveKey => "logistics_unit";

    /// <summary>Snapshots this unit into a <see cref="LogisticsUnitDto"/>. See <see cref="ISaveSerializable"/>.</summary>
    public object Serialize()
    {
        var dto = new LogisticsUnitDto
        {
            Name = Name,
            Id = Id,
            HostBodyName = FindHostBodyName(),
            State = State.ToString(),
            IsActive = IsActive,
            IsStationary = IsStationary,
            BandIndex = BandIndex,
            Fuel = Fuel,
            MaxFuel = MaxFuel,
            DryMass = DryMass,
            Position = Vec3Dto.From(Position),
            Velocity = Vec3Dto.From(Velocity),
            OrbitalAngle = OrbitalAngle,
            OrbitalRadius = OrbitalRadius,
            OrbitalSpeed = OrbitalSpeed,
            HostMass = HostMass,
        };

        if (Cargo != null)
            foreach (var kv in Cargo.Resources)
                dto.Cargo[kv.Key] = kv.Value;

        if (CurrentEngine != null)
            dto.Engine = EngineToDto(CurrentEngine);

        var mc = MovementController;
        if (mc != null && mc.IsTransferring && mc.ActiveTrajectory != null)
            dto.Transit = TransitToDto(mc);

        var schedule = ScheduleExecutor?.ActiveSchedule;
        if (schedule != null && schedule.Legs.Count > 0)
            dto.Schedule = ScheduleToDto(schedule);

        return dto;
    }

    /// <summary>Walks the parent chain to the celestial body this unit orbits, returning its node name.
    /// Null when the unit is adrift (in transit / stranded) directly under the system container.</summary>
    private string? FindHostBodyName()
    {
        Node? parent = GetParent();
        while (parent != null)
        {
            if (parent is CelestialBody body)
                return body.Name;
            parent = parent.GetParent();
        }
        return null;
    }

    private static OrbitalScheduleDto ScheduleToDto(OrbitalTransferSchedule s)
    {
        var dto = new OrbitalScheduleDto
        {
            ScheduleId = s.ScheduleId,
            WaitPeriodBetweenLegs = s.WaitPeriodBetweenLegs,
            RetryPeriod = s.RetryPeriod,
            MaxRetries = s.MaxRetries,
            IsRepeating = s.IsRepeating,
            State = s.State.ToString(),
            CurrentLegIndex = s.CurrentLegIndex,
        };
        foreach (var leg in s.Legs)
            dto.Legs.Add(LegToDto(leg));
        return dto;
    }

    private static LegDto LegToDto(Leg leg) => new()
    {
        LegId = leg.LegId,
        Origin = EndpointToDto(leg.Origin),
        Destination = EndpointToDto(leg.Destination),
        PickupOrder = ManifestToDict(leg.PickupOrder),
        DropoffOrder = ManifestToDict(leg.DropoffOrder),
        DepartureConstraints = new DepartureConstraintsDto
        {
            BudgetMode = leg.DepartureConstraints.BudgetMode.ToString(),
            MinBudget = leg.DepartureConstraints.MinBudget,
            MaxBudget = leg.DepartureConstraints.MaxBudget,
            NumOptions = leg.DepartureConstraints.NumOptions,
            RankingCriteria = leg.DepartureConstraints.RankingCriteria.ToString(),
        },
        RefuelInstructions = new RefuelInstructionsDto
        {
            Policy = leg.RefuelInstructions.Policy.ToString(),
            FuelResourceId = leg.RefuelInstructions.FuelResourceId,
            Amount = leg.RefuelInstructions.Amount,
        },
        MaxWaitSeconds = leg.MaxWaitSeconds,
        IsClosingLeg = leg.IsClosingLeg,
        State = leg.State.ToString(),
    };

    private static LegEndpointDto EndpointToDto(LegEndpoint? e) => new()
    {
        BodyName = (e?.Body as Node)?.Name,
        StationId = e?.Station?.Id,
        BandIndex = e?.BandIndex ?? -1,
    };

    private static Dictionary<string, int>? ManifestToDict(CargoManifest? m)
    {
        if (m == null || m.ResourceCount == 0)
            return null;
        var d = new Dictionary<string, int>();
        foreach (var kv in m.Resources)
            d[kv.Key] = kv.Value;
        return d;
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
}
