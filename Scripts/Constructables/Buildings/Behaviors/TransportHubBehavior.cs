using System.Collections.Generic;
using Godot;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Routes packages to remote destinations via a bulk buffer. Drains the buffer into
/// connected Export or Flex links during each manufacture tick.
/// </summary>
public partial class TransportHubBehavior : RefCounted, IBuildingBehavior
{
    private Building? _owner;

    public Building? Owner => _owner;

    public List<Building> Destinations { get; set; } = new();
    public Dictionary<string, float> BulkBuffer { get; set; } = new();

    public void OnAttach(Building owner) => _owner = owner;
    public void OnRegister() { }
    public void OnUnregister() { }
    public void OnDetach() => _owner = null;

    public void OnManufactureTick(float delta, Building owner)
    {
        DrainBulkBuffer(owner);
    }

    private void DrainBulkBuffer(Building owner)
    {
        if (BulkBuffer.Count == 0)
            return;

        var exportNodes = new List<ResourceNode>();
        foreach (var node in owner.Nodes)
        {
            if ((node.Kind == ResourceNodeKind.Export || node.Kind == ResourceNodeKind.Flex) && node.Link != null)
                exportNodes.Add(node);
        }

        if (exportNodes.Count == 0)
            return;

        var resourcesToDrain = new Dictionary<string, float>(BulkBuffer);
        foreach (var kvp in resourcesToDrain)
        {
            string resourceId = kvp.Key;
            float amountRemaining = kvp.Value;
            if (amountRemaining <= 0)
                continue;

            foreach (var node in exportNodes)
            {
                if (amountRemaining <= 0)
                    break;
                if (node.Link == null)
                    continue;

                float enqueued = node.Link.TryEnqueueAmount(resourceId, amountRemaining);
                if (enqueued > 0)
                {
                    BulkBuffer[resourceId] -= enqueued;
                    amountRemaining -= enqueued;
                }
            }
        }

        var keysToRemove = new List<string>();
        foreach (var kvp in BulkBuffer)
        {
            if (kvp.Value <= 0)
                keysToRemove.Add(kvp.Key);
        }
        foreach (var key in keysToRemove)
            BulkBuffer.Remove(key);
    }
}
