namespace Constructables.Buildings;

/// <summary>
/// Per-tick behavior component attached to a Building. Buildings compose capabilities
/// (manufacturing, extraction, storage, transport, power) by holding a list of these.
/// Lifecycle:
///  - OnAttach: building constructed (no placement yet)
///  - OnRegister: building placed; behavior may now query the owner's cells
///  - OnManufactureTick: every 60Hz tick of the ManufactureTickEngine
///  - OnUnregister / OnDetach: cleanup
/// </summary>
public interface IBuildingBehavior
{
    Building? Owner { get; }

    void OnAttach(Building owner);
    void OnRegister();
    void OnUnregister();
    void OnDetach();

    /// <summary>
    /// Per-tick hook called by <see cref="Tick.ManufactureTickEngine"/>. Override for
    /// behavior-specific manufacture logic.
    /// </summary>
    void OnManufactureTick(float delta, Building owner);

    /// <summary>
    /// Whether this behavior currently needs ticks. Returning false from every behavior on a
    /// building lets the building unregister from the tick engine until something wakes it
    /// (e.g. a <c>Storage.StorageUpdated</c> event). Default is true.
    /// </summary>
    bool WantsTick => true;

    /// <summary>
    /// Sort order within a building's tick. Lower values run first. Default 0; behaviors that
    /// must run before or after the default block override this. <see cref="Building.Register"/>
    /// sorts <see cref="Building.Behaviors"/> by this value once at registration time.
    /// </summary>
    int Priority => 0;
}
