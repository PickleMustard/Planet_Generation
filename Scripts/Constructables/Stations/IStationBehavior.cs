namespace Constructables.Stations;

/// <summary>
/// Per-tick behavior component attached to a StationSatellite. Stations compose capabilities
/// (storage, transfer, construction, shipbuilding) by holding a list of these.
/// Lifecycle:
///  - OnAttach: station instance created (no registration yet)
///  - OnRegister: station placed; behavior may now query the owner's scene tree
///  - OnManufactureTick: every 60Hz tick of the ManufactureTickEngine
///  - OnUnregister / OnDetach: cleanup
/// </summary>
public interface IStationBehavior
{
    StationSatellite? Owner { get; }

    void OnAttach(StationSatellite owner);
    void OnRegister();
    void OnUnregister();
    void OnDetach();

    /// <summary>
    /// Per-tick hook called by <see cref="Tick.ManufactureTickEngine"/>. Override for
    /// behavior-specific manufacture logic.
    /// </summary>
    void OnManufactureTick(float delta, StationSatellite owner);

    /// <summary>
    /// Whether this behavior currently needs ticks. Returning false from every behavior on a
    /// station lets the station unregister from the tick engine until something wakes it
    /// (e.g. a <see cref="Structures.Logistics.Storage.StorageUpdated"/> event). Default is true.
    /// </summary>
    bool WantsTick => true;

    /// <summary>
    /// Sort order within a station's tick. Lower values run first. Default 0; behaviors that
    /// must run before or after the default block override this. <see cref="StationSatellite.RegisterBehaviors"/>
    /// sorts <see cref="StationSatellite.Behaviors"/> by this value once at registration time.
    /// </summary>
    int Priority => 0;
}
