namespace Structures.Transfers;

/// <summary>
/// Defines transfer station capabilities parsed from YAML building config.
/// </summary>
public class TransferStationDefinition
{
    /// <summary>
    /// Total cargo capacity units available per transfer.
    /// Resources consume capacity based on their TransportWeight.
    /// </summary>
    public float CargoCapacity { get; set; } = 500.0f;

    /// <summary>
    /// Base vehicle speed in distance units per second.
    /// Travel time = distance / VehicleSpeed.
    /// </summary>
    public float VehicleSpeed { get; set; } = 50.0f;

    /// <summary>
    /// Maximum number of transfers that can be in-flight simultaneously from this station.
    /// </summary>
    public int MaxConcurrentTransfers { get; set; } = 2;

    /// <summary>
    /// Stub definition for stations that are valid transfer destinations but cannot
    /// originate transfers. Origin-side dispatch checks reject zero capacity/speed/concurrency.
    /// </summary>
    public static TransferStationDefinition DestinationOnly() =>
        new() { CargoCapacity = 0f, VehicleSpeed = 0f, MaxConcurrentTransfers = 0 };
}
