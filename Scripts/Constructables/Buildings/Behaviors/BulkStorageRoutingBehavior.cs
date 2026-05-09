using System.Collections.Generic;
using Godot;
using Structures.Logistics;
using Structures.Resources;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Routes resource flow through <see cref="Building.BulkStorage"/> when a building exposes
/// any bulk slots. Owns four steps:
///   1. Incoming link deliveries land in BulkStorage (fallback to InputStorage only when a
///      recipe-locked input slot exists for the resource).
///   2. Each tick: top up recipe-locked InputStorage slots from BulkStorage so manufacturing
///      keeps running.
///   3. Each tick: drain freshly produced OutputStorage residue into BulkStorage.
///   4. Each tick: enqueue BulkStorage contents into outgoing export/flex links.
/// Auto-attached by <see cref="Building.ApplyDefinition"/> when the definition declares
/// <c>StorageCapacity &gt; 0</c>. Runs at <see cref="Priority"/> -1 so its pull/push happens
/// before <see cref="ManufacturingBehavior"/>'s state machine on the same tick.
/// </summary>
public partial class BulkStorageRoutingBehavior : RefCounted, IBuildingBehavior
{
    private Building? _owner;

    public Building? Owner => _owner;

    public int Priority => -1;

    public bool WantsTick =>
        _owner != null
        && (_owner.BulkStorage.Slots.Count > 0 || _owner.OutputStorage.Slots.Count > 0);

    public void OnAttach(Building owner) => _owner = owner;

    public void OnRegister() { }

    public void OnUnregister() { }

    public void OnDetach() => _owner = null;

    /// <summary>
    /// Routes an incoming link package. Returns true when the package was fully absorbed
    /// (by BulkStorage, or by InputStorage as a recipe-locked fallback). Returns false to
    /// signal the link to retain the package in <c>ArrivalBuffer</c> for retry.
    /// </summary>
    public bool TryRouteIncoming(ResourcePackage package)
    {
        if (_owner == null || package == null)
            return false;

        var bulk = _owner.BulkStorage;
        if (bulk.Slots.Count == 0)
            return false;

        string resourceId = package.ResourceId;
        float amount = package.Quantity;

        if (bulk.HasSpace(resourceId, amount))
        {
            bulk.Deposit(resourceId, amount);
            return true;
        }

        if (
            HasRecipeLockedSlot(_owner.InputStorage, resourceId)
            && _owner.InputStorage.HasSpace(resourceId, amount)
        )
        {
            _owner.InputStorage.Deposit(resourceId, amount);
            return true;
        }

        return false;
    }

    private static bool HasRecipeLockedSlot(Storage storage, string resourceId)
    {
        foreach (var slot in storage.Slots)
        {
            if (
                slot.Filter.Kind == SlotFilterKind.Resource
                && string.Equals(slot.Filter.ResourceId, resourceId, System.StringComparison.Ordinal)
            )
                return true;
        }
        return false;
    }

    public void OnManufactureTick(float delta, Building owner)
    {
        if (!owner.PoweredOn)
            return;

        PullInputsFromBulk(owner);
        PushOutputsToBulk(owner);
        DrainBulkStorageToLinks(owner);
    }

    /// <summary>
    /// For every recipe-locked InputStorage slot, top it up from BulkStorage to its free
    /// capacity. No-op when InputStorage has no resource-filtered slots (pure-bulk hubs).
    /// </summary>
    private static void PullInputsFromBulk(Building owner)
    {
        var bulk = owner.BulkStorage;
        foreach (var slot in owner.InputStorage.Slots)
        {
            if (slot.Filter.Kind != SlotFilterKind.Resource)
                continue;

            string? resourceId = slot.Filter.ResourceId;
            if (string.IsNullOrEmpty(resourceId))
                continue;

            float room = owner.InputStorage.GetFreeSpace(resourceId);
            if (room <= 0f)
                continue;

            float available = bulk.GetQuantity(resourceId);
            float toMove = Mathf.Min(available, room);
            if (toMove <= 0f)
                continue;

            float withdrawn = bulk.Withdraw(resourceId, toMove);
            if (withdrawn <= 0f)
                continue;

            float deposited = owner.InputStorage.Deposit(resourceId, withdrawn);
            if (deposited < withdrawn)
                bulk.Deposit(resourceId, withdrawn - deposited);
        }
    }

    /// <summary>
    /// Moves every resource currently in OutputStorage into BulkStorage, capped by bulk's
    /// free space. Anything bulk can't accept stays in OutputStorage and retries next tick.
    /// </summary>
    private static void PushOutputsToBulk(Building owner)
    {
        var snapshot = owner.OutputStorage.GetAllQuantities();
        if (snapshot.Count == 0)
            return;

        foreach (var kvp in snapshot)
        {
            string resourceId = kvp.Key;
            float available = kvp.Value;
            if (available <= 0f)
                continue;

            float room = owner.BulkStorage.GetFreeSpace(resourceId);
            if (room <= 0f)
                continue;

            float toMove = Mathf.Min(available, room);
            float withdrawn = owner.OutputStorage.Withdraw(resourceId, toMove);
            if (withdrawn <= 0f)
                continue;

            float deposited = owner.BulkStorage.Deposit(resourceId, withdrawn);
            if (deposited < withdrawn)
                owner.OutputStorage.Deposit(resourceId, withdrawn - deposited);
        }
    }

    /// <summary>
    /// Enqueues BulkStorage contents into export/flex links, mirroring
    /// <see cref="ManufacturingBehavior"/>.PushOutputs but reading from bulk.
    /// </summary>
    private static void DrainBulkStorageToLinks(Building owner)
    {
        var bulk = owner.BulkStorage;
        var quantities = bulk.GetAllQuantities();
        if (quantities.Count == 0)
            return;

        var exportNodes = new List<ResourceNode>();
        foreach (var node in owner.Nodes)
        {
            if (
                (node.Kind == ResourceNodeKind.Export || node.Kind == ResourceNodeKind.Flex)
                && node.Link != null
            )
                exportNodes.Add(node);
        }

        if (exportNodes.Count == 0)
            return;

        foreach (var kvp in quantities)
        {
            string resourceId = kvp.Key;
            float available = kvp.Value;
            if (available <= 0f)
                continue;

            foreach (var node in exportNodes)
            {
                if (available <= 0f)
                    break;
                if (node.Link == null)
                    continue;

                float enqueued = node.Link.TryEnqueueAmount(resourceId, available);
                if (enqueued > 0f)
                {
                    bulk.Withdraw(resourceId, enqueued);
                    available -= enqueued;
                }
            }
        }
    }
}
