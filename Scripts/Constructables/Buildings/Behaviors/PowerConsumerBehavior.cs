using System.Collections.Generic;
using Constructables.Power;
using Godot;
using Structures.Enums;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Draws power from the containing <see cref="PowerGrid"/> in two tiers: full
/// <see cref="BaseDraw"/> while the owner is actively manufacturing, and a smaller
/// <see cref="StandbyDraw"/> whenever it is powered on but idle / waiting for inputs, so a
/// connected consumer always contributes load and active load spikes when it runs. Only
/// powered-off (and under-construction, filtered by the grid) buildings draw zero.
/// Registration with a grid is driven by <see cref="BodyPowerGridManager"/>; when no grid
/// covers the owner's cell, <see cref="Grid"/> is null.
/// </summary>
public partial class PowerConsumerBehavior : RefCounted, IBuildingBehavior, IPowerConsumer, IBehaviorConfigurable
{
    private const float STANDBY_FRACTION = 0.2f;

    private Building? _owner;

    public Building? Owner => _owner;

    public float BaseDraw { get; set; }
    public float StandbyDraw { get; set; }
    public PowerGrid? Grid { get; set; }

    public void OnAttach(Building owner) => _owner = owner;
    public void OnRegister() { }
    public void OnUnregister() { }
    public void OnDetach() => _owner = null;

    public void OnManufactureTick(float delta, Building owner) { }

    public float GetCurrentDraw()
    {
        if (_owner == null || !_owner.PoweredOn)
            return 0f;

        bool manufacturing = _owner.ProductionState == ManufacturingState.Manufacturing;
        return manufacturing ? BaseDraw : StandbyDraw;
    }

    public void Configure(Dictionary<string, object> config)
    {
        BaseDraw = BehaviorConfigHelper.ReadFloat(config, "base_draw", 0f);
        StandbyDraw = BehaviorConfigHelper.ReadFloat(config, "standby_draw", BaseDraw * STANDBY_FRACTION);
    }
}
