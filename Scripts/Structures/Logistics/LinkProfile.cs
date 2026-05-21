using Structures.Enums;

namespace Structures.Logistics;

/// <summary>
/// Defines the characteristics of a logistics link (conveyor, pipe, etc.)
/// that governs how <see cref="ResourcePackage"/> instances move between nodes.
/// </summary>
public class LinkProfile
{
    /// <summary>
    /// Unique identifier for this link profile (e.g. "conveyor_belt_mk1").
    /// </summary>
    public string IdName { get; set; } = string.Empty;

    /// <summary>
    /// Physical state of matter this link can carry. One state per link — a Solid
    /// link cannot carry fluids and vice versa.
    /// </summary>
    public StateOfMatter StateOfMatter { get; set; }

    /// <summary>
    /// Technology tier of the link. Higher tiers generally offer better throughput.
    /// </summary>
    public int Tier { get; set; }

    /// <summary>
    /// Progress advanced per tick, in the range 0.0..1.0.
    /// </summary>
    public float TransportSpeed { get; set; }

    /// <summary>
    /// Maximum quantity of resource that can travel in a single package.
    /// </summary>
    public int PackageSize { get; set; }

    /// <summary>
    /// Number of ticks to accumulate resources before a package is dispatched.
    /// </summary>
    public int BundleTime { get; set; }

    /// <summary>
    /// Maximum number of packages that may be in flight concurrently.
    /// </summary>
    public int SlotCapacity { get; set; }
}
