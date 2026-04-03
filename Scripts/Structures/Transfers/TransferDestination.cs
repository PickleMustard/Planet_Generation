namespace Structures.Transfers;

/// <summary>
/// Identifies the destination of a surface transfer.
/// Either a continent (by index) or an orbital station (by ID), but not both.
/// </summary>
public class TransferDestination
{
    /// <summary>
    /// Continent index for continent-to-continent transfers.
    /// Null when targeting an orbital station.
    /// </summary>
    public int? ContinentIndex { get; set; }

    /// <summary>
    /// Station satellite identifier for continent-to-orbit transfers.
    /// Null when targeting a continent.
    /// </summary>
    public string? StationSatelliteId { get; set; }

    /// <summary>
    /// Whether this destination targets an orbital station rather than a continent.
    /// </summary>
    public bool IsOrbitalStation => !string.IsNullOrEmpty(StationSatelliteId);

    /// <summary>
    /// Creates a destination targeting a continent.
    /// </summary>
    public static TransferDestination ForContinent(int continentIndex)
    {
        return new TransferDestination { ContinentIndex = continentIndex };
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
        return $"Continent({ContinentIndex})";
    }
}
