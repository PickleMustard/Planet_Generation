using System;
using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Behavior for a Building that starts with a stockpile of resources.
/// On attachment, the stockpile is populated with the resources defined in the Building's configuration.
/// No ticking is required.
/// </summary>
public partial class InitialStockpileBehavior : IBuildingBehavior
{
    private Building? _owner;

    public Building? Owner => _owner;

    public bool WantsTick => false;

    public void OnAttach(Building owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void OnRegister()
    {
        if (_owner == null)
            return;

        var definition = _owner.Definition;
        if (definition?.StartingStockpiles == null || definition.StartingStockpiles.Count == 0)
            return;

        foreach (var kvp in definition.StartingStockpiles)
        {
            string resourceId = kvp.Key;
            int requested = kvp.Value;

            if (!ResourceDatabase.Instance.TryGetResource(resourceId, out _))
            {
                GameLogger.Warning(
                    $"InitialStockpileBehavior: Skipping unknown resource '{resourceId}' for building '{_owner.Name}'"
                );
                continue;
            }

            float deposited = _owner.BulkStorage.Deposit(resourceId, requested);

            if (Mathf.Abs(deposited - requested) > 0.001f)
            {
                GameLogger.Warning(
                    $"InitialStockpileBehavior: Insufficient capacity for '{resourceId}' — added {deposited}/{requested} for building '{_owner.Name}'"
                );
            }
        }
    }

    public void OnDetach() { }

    public void OnManufactureTick(float delta, Building owner) { }

    public void OnUnregister() { }
}
