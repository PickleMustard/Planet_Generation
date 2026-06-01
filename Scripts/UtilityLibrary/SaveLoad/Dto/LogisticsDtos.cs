using System.Collections.Generic;

namespace UtilityLibrary.SaveLoad.Dto;

/// <summary>
/// Phase 2 (save_version 2) logistics-unit DTOs. A unit's host body and any trajectory body
/// references are stored by node name (the same cross-reference key used by <see cref="BodyDto"/>);
/// the loader resolves them once the bodies are built. Engine modifiers are an ordered list so they
/// can be re-applied deterministically. Full transit state is captured so a mid-transfer unit resumes
/// its Kepler-propagated arc and burn-profile fuel consumption identically after load.
/// </summary>
public sealed class LogisticsUnitDto
{
    public string Name { get; set; } = "";

    /// <summary>
    /// Stable runtime identifier preserved across save/load. Backward-compatible: pre-Id saves
    /// load with <c>Id = ""</c>, and <see cref="Constructables.LogisticsUnit._EnterTree"/>
    /// assigns a fresh Guid in that case.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>Name of the celestial body this unit orbits, or null when adrift in the system
    /// container (in transit / stranded).</summary>
    public string? HostBodyName { get; set; }

    /// <summary>LogisticsUnitState enum name.</summary>
    public string State { get; set; } = "Idle";

    public bool IsActive { get; set; } = true;
    public bool IsStationary { get; set; }
    public int BandIndex { get; set; }

    public float Fuel { get; set; }
    public float MaxFuel { get; set; }
    public float DryMass { get; set; }

    /// <summary>Local position under the host body's SatellitesContainer (or the system container
    /// when adrift). Re-derived next physics frame for orbiting/transit units; defensive otherwise.</summary>
    public Vec3Dto Position { get; set; } = new();
    public Vec3Dto Velocity { get; set; } = new();

    public float OrbitalAngle { get; set; }
    public float OrbitalRadius { get; set; }
    public float OrbitalSpeed { get; set; }
    public float HostMass { get; set; }

    /// <summary>Cargo manifest contents (resource id → whole-unit quantity).</summary>
    public Dictionary<string, int> Cargo { get; set; } = new();

    /// <summary>Null when no engine is installed.</summary>
    public EngineDto? Engine { get; set; }

    /// <summary>Null unless the unit is mid-transfer (movement controller actively transferring).</summary>
    public TransitStateDto? Transit { get; set; }
}

public sealed class EngineDto
{
    public float BaseSpecificImpulse { get; set; }
    public float BaseThrust { get; set; }

    /// <summary>Active modifiers in application order. Re-applied in order on load.</summary>
    public List<EngineModifierDto> Modifiers { get; set; } = new();
}

public sealed class EngineModifierDto
{
    /// <summary>ModifierType enum name ("Additive" or "Multiplicative").</summary>
    public string Type { get; set; } = "Additive";
    public string Source { get; set; } = "";
    public float IspValue { get; set; }
    public float ThrustValue { get; set; }
}

public sealed class TransitStateDto
{
    public string? DestinationBodyName { get; set; }
    public string? OriginBodyName { get; set; }
    public string? CentralBodyName { get; set; }

    public float TransferTime { get; set; }
    public float TimeInTransfer { get; set; }

    public Vec3Dto DeparturePosition { get; set; } = new();
    public Vec3Dto TargetPosition { get; set; } = new();
    public Vec3Dto DeparturePositionGlobal { get; set; } = new();

    public float GravitationalParameter { get; set; }

    /// <summary>TransitPhase enum name.</summary>
    public string CurrentTransitPhase { get; set; } = "Coasting";
    public float FuelConsumedThisTransfer { get; set; }

    /// <summary>LogisticsMovementController.SimulationMode enum name.</summary>
    public string SimulationMode { get; set; } = "FullKepler";

    public TrajectoryDto Trajectory { get; set; } = new();

    /// <summary>Null when the transfer had no burn profile (e.g. no engine at initiation).</summary>
    public BurnProfileDto? BurnProfile { get; set; }
}

/// <summary>
/// Serializable subset of <c>TrajectorySolution</c> sufficient to resume Kepler propagation
/// (classical elements + μ + TOF) and complete arrival (destination band). Origin/destination bodies
/// are resolved by name from the enclosing <see cref="TransitStateDto"/>.
/// </summary>
public sealed class TrajectoryDto
{
    public Vec3Dto InitialVelocity { get; set; } = new();
    public Vec3Dto FinalVelocity { get; set; } = new();

    public float TimeOfFlight { get; set; }
    public float DeltaVRequired { get; set; }
    public float DepartureDeltaV { get; set; }
    public float ArrivalDeltaV { get; set; }

    public float SemiMajorAxis { get; set; }
    public float Eccentricity { get; set; }
    public float Inclination { get; set; }
    public float AscendingNodeLongitude { get; set; }
    public float ArgumentOfPeriapsis { get; set; }
    public float MeanAnomaly { get; set; }

    public int Revolutions { get; set; }

    /// <summary>TransferType enum name.</summary>
    public string TransferType { get; set; } = "Direct";

    public float GravitationalParameter { get; set; }

    public Vec3Dto PredictedOriginPosition { get; set; } = new();
    public Vec3Dto PredictedDestinationPosition { get; set; } = new();
    public Vec3Dto OriginOrbitalVelocity { get; set; } = new();
    public Vec3Dto DestinationOrbitalVelocity { get; set; } = new();

    public int OriginBandIndex { get; set; } = -1;
    public int DestinationBandIndex { get; set; } = -1;

    public float DepartureTime { get; set; }
    public float FuelRequired { get; set; }
}

/// <summary>
/// Serialized <c>BurnProfile</c>. All values are stored verbatim (rather than recomputed via
/// <c>BurnProfile.Calculate</c>) so resume is exact regardless of the ship's current mass, which may
/// differ from the mass at transfer initiation due to fuel already consumed.
/// </summary>
public sealed class BurnProfileDto
{
    public float AccelBurnDuration { get; set; }
    public float CoastDuration { get; set; }
    public float DecelBurnDuration { get; set; }
    public float TotalDuration { get; set; }
    public float AccelFuelBudget { get; set; }
    public float DecelFuelBudget { get; set; }
    public float TotalFuelBudget { get; set; }
    public float AccelFuelRate { get; set; }
    public float DecelFuelRate { get; set; }
    public float AccelEndTime { get; set; }
    public float DecelStartTime { get; set; }
}
