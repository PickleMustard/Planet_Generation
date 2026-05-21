using Godot;
using Structures.GameState;

namespace Structures.Resources;

/// <summary>
/// Placement behavior for atmosphere-dependent buildings (wind turbines).
/// Wraps a standard <see cref="DefaultPlacementBehavior"/> for biome / elevation /
/// slope checks, then additionally requires the owning body's
/// <see cref="IOrbitalBody.Atmosphere"/> to fall within
/// [<see cref="MinAtmosphere"/>, <see cref="MaxAtmosphere"/>].
///
/// Atmosphere bounds are set from the <c>configurable_behavior</c> inline config
/// in <c>placement_requirements</c> (not from BuildingDefinition.Power, which no
/// longer exists). The loader applies <c>min_atmosphere</c> / <c>max_atmosphere</c>
/// keys from that config after instantiation.
///
/// Small bodies (asteroids, comets, moons, dwarf planets) have atmosphere = 0
/// and fail the min check, disabling wind there. Gas/ice giants exceed the max
/// and are gated off until biome-adapted variants ship.
/// </summary>
public partial class AtmospherePlacementBehavior : RefCounted, IPlacementBehavior
{
    private readonly BuildingDefinition.PlacementRequirements _requirements;
    private readonly DefaultPlacementBehavior _inner;

    public AtmospherePlacementBehavior(BuildingDefinition.PlacementRequirements requirements)
    {
        _requirements = requirements;
        _inner = new DefaultPlacementBehavior(requirements);
    }

    /// <summary>
    /// Min atmosphere (atm) required on the owning body. Set from the
    /// <c>configurable_behavior</c> inline config in <c>placement_requirements</c>
    /// after construction (via the loader).
    /// </summary>
    public float MinAtmosphere { get; set; } = 0f;

    /// <summary>
    /// Max atmosphere (atm) allowed on the owning body.
    /// </summary>
    public float MaxAtmosphere { get; set; } = float.MaxValue;

    public bool IsValidPlacement(VoronoiCell cell)
    {
        // No body context — fall back to the cell-only checks. This path is hit by
        // legacy callers; new code should use the body-aware overload.
        return _inner.IsValidPlacement(cell);
    }

    public bool IsValidPlacement(VoronoiCell cell, IOrbitalBody? body)
    {
        if (!_inner.IsValidPlacement(cell))
            return false;
        if (body == null)
            return false;
        float atm = body.Atmosphere;
        return atm >= MinAtmosphere && atm <= MaxAtmosphere;
    }
}
