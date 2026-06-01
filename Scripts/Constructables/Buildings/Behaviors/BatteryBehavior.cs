using System.Collections.Generic;
using Constructables.Power;
using Godot;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Stores excess power and discharges it back into the grid when generation falls below
/// draw. Acts as an <see cref="IGridContributor"/>: a battery seeds and extends a grid
/// the same way a power producer does, but supplies no live generation — only stored
/// charge. The grid's resolveTick reads <see cref="Stored"/> / <see cref="Capacity"/>
/// directly.
/// </summary>
public partial class BatteryBehavior : RefCounted, IBuildingBehavior, IGridContributor, IBehaviorConfigurable
{
    private Building? _owner;

    public Building? Owner => _owner;

    public float Capacity { get; set; }
    public float Stored { get; set; }
    public int Radius { get; set; }

    // Batteries don't generate power live; they only discharge stored energy.
    public float Output => 0f;
    public float BatteryCapacity => Capacity;
    public float BatteryStored
    {
        get => Stored;
        set => Stored = value;
    }

    public bool IsProducing => false;

    public void OnAttach(Building owner) => _owner = owner;
    public void OnRegister() { }
    public void OnUnregister() { }
    public void OnDetach() => _owner = null;

    public void OnManufactureTick(float delta, Building owner) { }

    public void Configure(Dictionary<string, object> config)
    {
        Capacity = BehaviorConfigHelper.ReadFloat(config, "capacity", 0f);
        Radius = BehaviorConfigHelper.ReadInt(config, "grid_radius", 0);
    }
}
