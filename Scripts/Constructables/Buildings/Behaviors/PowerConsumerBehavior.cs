using Constructables.Power;
using Godot;
using Structures.Enums;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Draws power from the containing <see cref="PowerGrid"/> only while the owner is actively
/// manufacturing. Idle, asleep (waiting for inputs), or powered-off buildings draw zero, so
/// the grid budget reflects real load. Registration with a grid is driven by
/// <see cref="BodyPowerGridManager"/>; when no grid covers the owner's cell, <see cref="Grid"/>
/// is null.
/// </summary>
public partial class PowerConsumerBehavior : RefCounted, IBuildingBehavior, IPowerConsumer
{
    private Building? _owner;

    public Building? Owner => _owner;

    public float BaseDraw { get; set; }
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

        var mfg = _owner.GetBehavior<ManufacturingBehavior>();
        if (mfg == null)
            return 0f;

        return mfg.State == ManufacturingState.Manufacturing ? BaseDraw : 0f;
    }
}
