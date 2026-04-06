using Constructables;

namespace Structures.Logistics;

/// <summary>
/// Represents an origin or destination point for an orbital transfer leg.
/// Can be either a station (with economy for cargo/refuel) or a bare orbital position.
/// </summary>
public class LegEndpoint
{
    /// <summary>
    /// The station at this endpoint, if any. Enables cargo and refuel operations.
    /// Null for non-station orbital positions.
    /// </summary>
    public StationSatellite? Station { get; set; }

    /// <summary>
    /// The orbital body at this endpoint. Always required for trajectory planning.
    /// </summary>
    public IOrbitalBody Body { get; set; } = null!;

    /// <summary>
    /// Which orbit band to target. -1 means auto-select based on approach speed.
    /// </summary>
    public int BandIndex { get; set; } = -1;

    /// <summary>
    /// Whether this endpoint is a station capable of cargo and refuel operations.
    /// </summary>
    public bool IsStation => Station != null;

    /// <summary>
    /// Creates an endpoint targeting a station orbiting a body.
    /// </summary>
    public static LegEndpoint ForStation(StationSatellite station, IOrbitalBody body, int bandIndex = -1)
    {
        return new LegEndpoint
        {
            Station = station,
            Body = body,
            BandIndex = bandIndex
        };
    }

    /// <summary>
    /// Creates an endpoint targeting an orbital position around a body with no station.
    /// </summary>
    public static LegEndpoint ForBody(IOrbitalBody body, int bandIndex = -1)
    {
        return new LegEndpoint
        {
            Body = body,
            BandIndex = bandIndex
        };
    }
}
