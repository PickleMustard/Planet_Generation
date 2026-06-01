using System.Collections.Generic;

namespace UtilityLibrary.SaveLoad.Dto;

/// <summary>
/// Phase 3 (save_version 3) DTOs for placed buildings, orbital stations, and the resource-transfer
/// orders/schedules they own. Buildings/stations reference their owning body by node name (the same
/// cross-reference key as <see cref="BodyDto"/>) and their cells by integer index. Definitions are
/// referenced by their static-registry id (BuildingDefinition.IdName / StationDefinition.Name) so
/// only mutable state is serialized. Manufacturing cycle progress is intentionally not captured —
/// recipes restart fresh on load.
/// </summary>
public sealed class BuildingDto
{
    /// <summary>Stable building id (GUID). Cross-referenced by transfer orders/schedules.</summary>
    public string Id { get; set; } = "";

    /// <summary>BuildingDefinition.IdName — looked up in BuildingDatabase on load.</summary>
    public string DefinitionId { get; set; } = "";

    /// <summary>Owning body node name.</summary>
    public string BodyName { get; set; } = "";

    /// <summary>Primary occupied cell index.</summary>
    public int PrimaryCellIndex { get; set; } = -1;

    /// <summary>Additional occupied cell indices (excludes the primary cell).</summary>
    public List<int> AdditionalCellIndices { get; set; } = new();

    public bool PoweredOn { get; set; } = true;
    public int Specifier { get; set; }
    public float SpeedModifier { get; set; } = 1f;

    /// <summary>Active recipe id, or null when the building has none.</summary>
    public string? ActiveRecipeId { get; set; }

    /// <summary>Storage contents (resource id → whole units). Fractional buffers are not saved.</summary>
    public Dictionary<string, int> InputStorage { get; set; } = new();
    public Dictionary<string, int> OutputStorage { get; set; } = new();
    public Dictionary<string, int> BulkStorage { get; set; } = new();

    /// <summary>Null unless the building has a transfer-station behavior.</summary>
    public TransferBehaviorStateDto? Transfer { get; set; }
}

public sealed class StationDto
{
    /// <summary>Stable station id (GUID). Cross-referenced by transfer orders/schedules.</summary>
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>StationDefinition.Name — looked up in StationDatabase on load.</summary>
    public string DefinitionName { get; set; } = "";

    /// <summary>Host body node name (the station orbits this body).</summary>
    public string BodyName { get; set; } = "";

    public int BandIndex { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsStationary { get; set; } = true;

    public float OrbitalAngle { get; set; }
    public float OrbitalRadius { get; set; }
    public float OrbitalSpeed { get; set; }
    public float HostMass { get; set; }

    /// <summary>Local position under the host's SatellitesContainer; re-derived from the angle each frame.</summary>
    public Vec3Dto Position { get; set; } = new();
    public Vec3Dto Velocity { get; set; } = new();

    public Dictionary<string, int> BulkStorage { get; set; } = new();

    /// <summary>Null unless the station has a transfer-hub behavior.</summary>
    public TransferBehaviorStateDto? Transfer { get; set; }
}

/// <summary>
/// Serialized state of a single transfer behavior (surface station or orbital hub): its accumulated
/// game-time clock plus the in-flight orders and standing schedules it owns.
/// </summary>
public sealed class TransferBehaviorStateDto
{
    public double TotalTime { get; set; }
    public List<TransferOrderDto> ActiveTransfers { get; set; } = new();
    public List<TransferScheduleDto> Schedules { get; set; } = new();
}

public sealed class TransferDestinationDto
{
    public string? BuildingId { get; set; }
    public string? StationSatelliteId { get; set; }
}

public sealed class TransferOrderDto
{
    public string OrderId { get; set; } = "";
    public string OriginBuildingId { get; set; } = "";
    public TransferDestinationDto Destination { get; set; } = new();

    /// <summary>Loaded cargo (resource id → whole units).</summary>
    public Dictionary<string, int> Manifest { get; set; } = new();

    /// <summary>Originally requested cargo.</summary>
    public Dictionary<string, int> RequestedManifest { get; set; } = new();

    /// <summary>SurfaceTransferState enum name.</summary>
    public string State { get; set; } = "InTransit";

    public float TravelTimeSeconds { get; set; }
    public float ElapsedTimeSeconds { get; set; }
    public double DispatchedAtTime { get; set; }
    public string? SourceScheduleId { get; set; }
}

public sealed class TransferScheduleDto
{
    public string ScheduleId { get; set; } = "";
    public string OriginBuildingId { get; set; } = "";
    public TransferDestinationDto Destination { get; set; } = new();

    public Dictionary<string, float> ResourceProportions { get; set; } = new();

    /// <summary>DepartureConditionMode enum name.</summary>
    public string DepartureMode { get; set; } = "AllResources";

    /// <summary>DepartureThreshold enum name.</summary>
    public string Threshold { get; set; } = "Full";

    /// <summary>TransferScheduleState enum name.</summary>
    public string State { get; set; } = "Idle";

    public string? ActiveTransferOrderId { get; set; }
    public int Priority { get; set; }
    public float? WaitSeconds { get; set; }
    public double LastDispatchTime { get; set; }
}
