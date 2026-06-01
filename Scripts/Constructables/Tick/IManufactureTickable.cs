namespace Constructables.Tick;

/// <summary>
/// Implemented by anything that participates in the dedicated 60Hz manufacture tick.
/// Buildings, ContinentEconomy, and StationEconomy all implement this so they can be
/// scheduled by ManufactureTickEngine independently of Godot's physics frame.
/// </summary>
public interface IManufactureTickable
{
    void OnManufactureTick(float delta);

    /// <summary>
    /// Priority for tick ordering. Lower values tick first. Default is 0.
    /// </summary>
    int TickPriority { get => 0; }
}
