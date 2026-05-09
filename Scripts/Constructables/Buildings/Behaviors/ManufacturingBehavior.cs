using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Discrete-cycle manufacturing for a Building. Pulls inputs from InputStorage and connected
/// links, runs a work timer, then deposits outputs into the building's OutputStorage.
/// External ticking (ContinentEconomy/StationEconomy) drives StartCycle / OnManufactureTick
/// based on the building's ManufacturingState.
/// </summary>
public partial class ManufacturingBehavior : RefCounted, IBuildingBehavior
{
    private Building? _owner;

    public Building? Owner => _owner;

    public ManufacturingState State { get; private set; } = ManufacturingState.Idle;
    public float WorkProgress { get; private set; }
    public float WorkRequired { get; private set; }

    /// <summary>
    /// Manufacturing buildings sleep while waiting for inputs — re-registration is driven by
    /// the building's storage-event handler, not by per-tick polling.
    /// </summary>
    public bool WantsTick => State != ManufacturingState.WaitingForInputs;

    public Dictionary<string, float> InputsHeld { get; private set; } = new();
    public Dictionary<string, float> ExpectedOutputs { get; private set; } = new();

    private Dictionary<string, float> _pendingInputs = new();

    public int Priority { get; set; } = 5;

    public void OnAttach(Building owner)
    {
        _owner = owner;
    }

    public void OnRegister() { }
    public void OnUnregister() {}

    public void OnDetach()
    {
        _owner = null;
    }

    public void StartCycle(RecipeDefinition recipe, float productionSpeed)
    {
        if (_owner == null)
            return;

        if (State != ManufacturingState.Idle && State != ManufacturingState.Outputting)
            return;

        EnsureSlotsForRecipe(recipe);

        WorkRequired = recipe.WorkRequired / productionSpeed;
        WorkProgress = 0f;
        InputsHeld.Clear();
        ExpectedOutputs.Clear();
        _pendingInputs.Clear();

        foreach (var output in recipe.OutputResources)
        {
            if (output.Key != "power")
                ExpectedOutputs[output.Key] = output.Value;
        }

        var missingInputs = new Dictionary<string, float>();
        foreach (var input in recipe.InputResources)
        {
            if (input.Key != "power")
            {
                float amountNeeded = input.Value;
                if (amountNeeded > 0)
                {
                    float available = _owner.InputStorage.GetQuantity(input.Key);
                    float toWithdraw = Mathf.Min(available, amountNeeded);
                    if (toWithdraw > 0)
                    {
                        _owner.InputStorage.Withdraw(input.Key, toWithdraw);
                        InputsHeld[input.Key] = toWithdraw;
                        amountNeeded -= toWithdraw;
                    }

                    if (amountNeeded > 0)
                        missingInputs[input.Key] = amountNeeded;
                }
            }
        }

        if (missingInputs.Count > 0)
        {
            State = ManufacturingState.WaitingForInputs;
            _pendingInputs = new Dictionary<string, float>(missingInputs);
        }
        else
        {
            State = ManufacturingState.Manufacturing;
        }
    }

    public void SetState(ManufacturingState newState)
    {
        State = newState;
    }

    /// <summary>
    /// Aborts any in-flight manufacturing cycle and returns the behavior to Idle.
    /// When <paramref name="returnHeldInputs"/> is true, deposits each <see cref="InputsHeld"/>
    /// entry back into the owner's InputStorage; if a slot rejects part of the deposit
    /// (slot full / capacity exceeded), the remainder is logged and discarded.
    /// Existing recipe-locked storage slots are not removed — slot residue is preserved
    /// across recipe swaps per the documented behavior of <see cref="EnsureSlotsForRecipe"/>.
    /// </summary>
    public void CancelCycle(bool returnHeldInputs)
    {
        if (returnHeldInputs && _owner != null)
        {
            foreach (var kvp in InputsHeld)
            {
                float held = kvp.Value;
                if (held <= 0)
                    continue;
                float deposited = _owner.InputStorage.Deposit(kvp.Key, held);
                if (deposited < held)
                {
                    GameLogger.Warning(
                        $"ManufacturingBehavior.CancelCycle: discarded {held - deposited} of '{kvp.Key}' (InputStorage full)."
                    );
                }
            }
        }

        InputsHeld.Clear();
        ExpectedOutputs.Clear();
        _pendingInputs.Clear();
        WorkProgress = 0f;
        WorkRequired = 0f;
        State = ManufacturingState.Idle;
    }

    /// <summary>
    /// Ensures InputStorage and OutputStorage have a resource-locked slot for every
    /// non-power input/output of the given recipe. Slot capacity is dynamic and equals
    /// the resource's MaxStackSize. Existing slots for the same resources are kept
    /// (so residue from a previous recipe is not lost on swap).
    /// </summary>
    public void EnsureSlotsForRecipe(RecipeDefinition recipe)
    {
        if (_owner == null)
            return;

        var existingIn = new HashSet<string>();
        foreach (var slot in _owner.InputStorage.Slots)
        {
            if (slot.Filter.Kind == SlotFilterKind.Resource && slot.Filter.ResourceId != null)
                existingIn.Add(slot.Filter.ResourceId);
        }
        foreach (var input in recipe.InputResources.Keys)
        {
            if (input == "power") continue;
            if (existingIn.Contains(input)) continue;
            _owner.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(input)));
        }

        var existingOut = new HashSet<string>();
        foreach (var slot in _owner.OutputStorage.Slots)
        {
            if (slot.Filter.Kind == SlotFilterKind.Resource && slot.Filter.ResourceId != null)
                existingOut.Add(slot.Filter.ResourceId);
        }
        foreach (var output in recipe.OutputResources.Keys)
        {
            if (output == "power") continue;
            if (existingOut.Contains(output)) continue;
            _owner.OutputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(output)));
        }
    }

    public void OnManufactureTick(float delta, Building owner)
    {
        if (!owner.PoweredOn)
            return;

        if (State == ManufacturingState.WaitingForInputs)
        {
            TryFulfillInputsFromStorage(owner);
        }

        if (State == ManufacturingState.Manufacturing)
        {
            WorkProgress += delta;

            if (WorkProgress >= WorkRequired)
                FinishCycle(owner);
        }

        PushOutputs(owner);
    }

    private void TryFulfillInputsFromStorage(Building owner)
    {
        if (_pendingInputs.Count == 0)
            return;

        var stillMissing = new Dictionary<string, float>();
        foreach (var kvp in _pendingInputs)
        {
            string resourceId = kvp.Key;
            float needed = kvp.Value;

            float inStorage = owner.InputStorage.GetQuantity(resourceId);
            float toWithdraw = Mathf.Min(inStorage, needed);
            if (toWithdraw > 0)
            {
                owner.InputStorage.Withdraw(resourceId, toWithdraw);
                InputsHeld[resourceId] = InputsHeld.GetValueOrDefault(resourceId) + toWithdraw;
                needed -= toWithdraw;
            }

            if (needed > 0)
                stillMissing[resourceId] = needed;
        }

        _pendingInputs = stillMissing;
        if (_pendingInputs.Count == 0)
            State = ManufacturingState.Manufacturing;
    }

    private void FinishCycle(Building owner)
    {
        State = ManufacturingState.Outputting;

        foreach (var output in ExpectedOutputs)
            owner.OutputStorage.Deposit(output.Key, output.Value);

        InputsHeld.Clear();
        ExpectedOutputs.Clear();
        _pendingInputs.Clear();
        WorkProgress = 0f;
        State = ManufacturingState.Idle;
    }

    private void PushOutputs(Building owner)
    {
        // BulkStorageRoutingBehavior owns the OutputStorage→Bulk→link drain on bulk-equipped
        // buildings. Skipping here avoids two paths racing to withdraw the same OutputStorage.
        if (owner.GetBehavior<BulkStorageRoutingBehavior>() != null
            && owner.BulkStorage.Slots.Count > 0)
            return;

        var quantities = owner.OutputStorage.GetAllQuantities();
        if (quantities.Count == 0)
            return;

        var exportNodes = new List<ResourceNode>();
        foreach (var node in owner.Nodes)
        {
            if ((node.Kind == ResourceNodeKind.Export || node.Kind == ResourceNodeKind.Flex) && node.Link != null)
                exportNodes.Add(node);
        }

        if (exportNodes.Count == 0)
            return;

        foreach (var kvp in quantities)
        {
            string resourceId = kvp.Key;
            float amountAvailable = kvp.Value;
            if (amountAvailable <= 0)
                continue;

            foreach (var node in exportNodes)
            {
                if (amountAvailable <= 0)
                    break;
                if (node.Link == null)
                    continue;

                float enqueued = node.Link.TryEnqueueAmount(resourceId, amountAvailable);
                if (enqueued > 0)
                {
                    owner.OutputStorage.Withdraw(resourceId, enqueued);
                    amountAvailable -= enqueued;
                }
            }
        }
    }
}
