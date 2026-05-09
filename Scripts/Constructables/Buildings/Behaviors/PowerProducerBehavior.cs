using Constructables.Power;
using Godot;
using Structures.Enums;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Generates power per tick at a fixed <see cref="Output"/> rate while the owner is
/// actively manufacturing. Acts as a <see cref="IGridContributor"/>: its
/// <see cref="Radius"/> seeds and extends the surrounding <see cref="PowerGrid"/>.
/// </summary>
public partial class PowerProducerBehavior : RefCounted, IBuildingBehavior, IGridContributor
{
    private Building? _owner;

    public Building? Owner => _owner;

    public float Output { get; set; }
    public int Radius { get; set; }

    /// <summary>
    /// Renewable producers (solar, wind, geothermal) generate continuously while powered on,
    /// independent of any manufacturing cycle. Fueled plants (default) gate generation on the
    /// owner's <see cref="ManufacturingState.Manufacturing"/> state so they only output while
    /// burning fuel.
    /// </summary>
    public bool IsRenewable { get; set; } = false;

    // Producers carry no battery storage.
    public float BatteryCapacity => 0f;
    public float BatteryStored
    {
        get => 0f;
        set { /* no-op for producers */ }
    }

    /// <summary>
    /// Producers contribute power only while a manufacturing cycle is running (fueled plants),
    /// or continuously while powered on (renewables). Brownout / construction always disables.
    /// </summary>
    public bool IsProducing
    {
        get
        {
            if (_owner == null || !_owner.PoweredOn || _owner.IsUnderConstruction)
                return false;
            if (IsRenewable)
                return true;
            var mfg = _owner.GetBehavior<ManufacturingBehavior>();
            return mfg != null && mfg.State == ManufacturingState.Manufacturing;
        }
    }

    public void OnAttach(Building owner) => _owner = owner;
    public void OnRegister() { }
    public void OnUnregister() { }
    public void OnDetach() => _owner = null;

    public void OnManufactureTick(float delta, Building owner) { }
}
