namespace Constructables.Power;

/// <summary>
/// Marker interface for behaviors that can establish or extend a <see cref="PowerGrid"/>.
/// Implemented by power producers and batteries.
/// </summary>
public interface IGridContributor
{
    /// <summary>
    /// Reach in cell hops measured from the owner's occupied cells. Coverage area is
    /// the set of cells within <c>Radius + 1</c> hops, per the design spec.
    /// </summary>
    int Radius { get; }

    /// <summary>
    /// Power output per tick when the owner is actively running. 0 for batteries.
    /// </summary>
    float Output { get; }

    /// <summary>
    /// Battery storage capacity. 0 for non-battery contributors.
    /// </summary>
    float BatteryCapacity { get; }

    /// <summary>
    /// Currently stored battery energy. 0 for non-battery contributors.
    /// </summary>
    float BatteryStored { get; set; }

    /// <summary>
    /// True if this contributor is currently producing power. Producers report based on
    /// their manufacturing state; batteries are always considered active when above zero.
    /// </summary>
    bool IsProducing { get; }

    Constructables.Building? Owner { get; }
}
