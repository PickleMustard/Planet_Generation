namespace Structures.Transfers;

/// <summary>
/// Identifies the destination of a surface transfer.
/// Either a continent (by index) or an orbital station (by ID), but not both.
/// </summary>
public class TransferDestination
{
    /// <summary>
    /// Surface transfer-station building id for surface-to-surface transfers.
    /// Null when targeting an orbital station.
    /// </summary>
    public string? BuildingId { get; set; }

    /// <summary>
    /// Station satellite identifier for surface-to-orbit transfers.
    /// Null when targeting a surface building.
    /// </summary>
    public string? StationSatelliteId { get; set; }

    /// <summary>
    /// Whether this destination targets an orbital station rather than a surface building.
    /// </summary>
    public bool IsOrbitalStation => !string.IsNullOrEmpty(StationSatelliteId);

    /// <summary>
    /// Creates a destination targeting a surface transfer-station building.
    /// </summary>
    public static TransferDestination ForBuilding(string buildingId)
    {
        return new TransferDestination { BuildingId = buildingId };
    }

    /// <summary>
    /// Creates a destination targeting an orbital station.
    /// </summary>
    public static TransferDestination ForStation(string stationId)
    {
        return new TransferDestination { StationSatelliteId = stationId };
    }

    public override string ToString()
    {
        if (IsOrbitalStation)
            return $"Station({StationSatelliteId})";
        return $"Building({BuildingId})";
    }
}
