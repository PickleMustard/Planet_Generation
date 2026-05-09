using Godot;
using UtilityLibrary;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Registers the owning building as a transfer-station endpoint with its parent body's
/// <see cref="BodyTransferManager"/>. Compose with <see cref="StorageHubBehavior"/> in
/// the YAML <c>behaviors:</c> list to give the building bulk-storage slots that back
/// the endpoint.
///
/// Drainage / fill is pull-driven: <see cref="BodyTransferManager"/> reads from the
/// endpoint each tick via <see cref="Structures.GameState.IResourceEndpoint"/>, so this
/// behavior reports <see cref="WantsTick"/> = false.
/// </summary>
public partial class TransferStationBehavior : RefCounted, IBuildingBehavior
{
    private Building? _owner;
    private BuildingResourceEndpoint? _endpoint;
    private BodyTransferManager? _registeredManager;

    public Building? Owner => _owner;

    public bool WantsTick => false;

    // Run after StorageHubBehavior so BulkStorage slots exist before the endpoint is exposed.
    public int Priority => 100;

    public void OnAttach(Building owner) => _owner = owner;

    public void OnRegister()
    {
        if (_owner == null)
            return;
        if (_owner.Definition?.TransferStation == null)
        {
            GameLogger.Warning(
                $"TransferStationBehavior on '{_owner.Name}' but definition has no transfer_station block; skipping registration."
            );
            return;
        }
        if (string.IsNullOrEmpty(_owner.Id))
        {
            GameLogger.Warning(
                $"TransferStationBehavior on '{_owner.Name}' has no Building.Id; skipping registration."
            );
            return;
        }

        var mgr = FindTransferManager(_owner);
        if (mgr == null)
        {
            GameLogger.Warning(
                $"TransferStationBehavior: could not find BodyTransferManager for '{_owner.Name}'."
            );
            return;
        }

        _endpoint = new BuildingResourceEndpoint(_owner);
        _registeredManager = mgr;
        mgr.RegisterEndpoint(
            _owner.Id,
            _endpoint,
            _owner.Definition.TransferStation,
            _owner
        );
    }

    public void OnUnregister()
    {
        if (_owner == null || _registeredManager == null || string.IsNullOrEmpty(_owner.Id))
            return;
        _registeredManager.UnregisterEndpoint(_owner.Id);
        _registeredManager = null;
        _endpoint = null;
    }

    public void OnDetach() => _owner = null;

    public void OnManufactureTick(float delta, Building owner) { }

    /// <summary>
    /// Walks the visual-node parent chain to find the owning <see cref="IOrbitalBody"/>
    /// and returns its <see cref="BodyTransferManager"/>. Returns null if the building
    /// has no visual node yet or the chain doesn't contain a body.
    /// </summary>
    private static BodyTransferManager? FindTransferManager(Building owner)
    {
        Node? cursor = owner.VisualNode;
        while (cursor != null)
        {
            if (cursor is IOrbitalBody body)
                return body.TransferMgr;
            cursor = cursor.GetParent();
        }
        return null;
    }
}
