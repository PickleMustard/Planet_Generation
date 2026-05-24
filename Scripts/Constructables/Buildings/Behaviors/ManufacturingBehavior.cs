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
public partial class ManufacturingBehavior : RefCounted, IBuildingBehavior, IBehaviorConfigurable
{
    private Building? _owner;

    /// <summary>Cached max resource tier from the owner's BuildingDefinition. Updated in OnAttach.</summary>
    private int _maxResourceTier;

    public Building? Owner => _owner;

    public ManufacturingState State { get; private set; } = ManufacturingState.Idle;
    public float WorkProgress { get; private set; }
    public float WorkRequired { get; private set; }

    /// <summary>
    /// Manufacturing buildings sleep while waiting for inputs — re-registration is driven by
    /// the building's storage-event handler, not by per-tick polling.
    /// </summary>
    public bool WantsTick => State != ManufacturingState.WaitingForInputs;

    public Dictionary<string, int> InputsHeld { get; private set; } = new();
    public Dictionary<string, float> ExpectedOutputs { get; private set; } = new();

    private Dictionary<string, int> _pendingInputs = new();

    /// <summary>Tag input key (e.g., "tag:ore") -> concrete resource id resolved at cycle start.</summary>
    public Dictionary<string, string> ResolvedTagInputs { get; private set; } = new();

    /// <summary>Tag output key (e.g., "tag:metal") -> concrete resource id resolved at cycle start.</summary>
    public Dictionary<string, string> ResolvedTagOutputs { get; private set; } = new();

    /// <summary>Material:* discriminator captured from the chosen tag-input resource. Null when no tag inputs were resolved.</summary>
    public string? CycleMaterialDiscriminator { get; private set; }

    /// <summary>Reference to the recipe active in the current cycle. Used for late tag-output resolution after delivery.</summary>
    private RecipeDefinition? _currentRecipe;

    /// <summary>The recipe of the cycle currently in flight, or null when no cycle is running.</summary>
    public RecipeDefinition? CurrentRecipe => _currentRecipe;

    public int Priority { get; set; } = 5;

    /// <summary>Default recipe read from YAML behavior config.</summary>
    public string? DefaultRecipe { get; private set; }

    /// <summary>Alternative recipes read from YAML behavior config.</summary>
    private List<string> _alternativeRecipes = new();
    public IReadOnlyList<string> AlternativeRecipes => _alternativeRecipes;

    /// <summary>Production speed multiplier read from YAML behavior config.</summary>
    public float ProductionSpeed { get; private set; } = 1.0f;

    /// <summary>Optional environment-driven output scaler, sampled at OnRegister.</summary>
    private EnvironmentalModifier? _environmentalModifier;

    /// <summary>
    /// Cached factor from the environmental modifier; 1.0 when none configured.
    /// Multiplies every non-power output (literal and tag-resolved) at deposit time.
    /// A value of 0 blocks new cycles from starting.
    /// </summary>
    public float EnvScaleFactor { get; private set; } = 1f;

    public void Configure(Dictionary<string, object> config)
    {
        DefaultRecipe = BehaviorConfigHelper.ReadString(config, "default_recipe", null);
        _alternativeRecipes = BehaviorConfigHelper.ReadStringList(config, "alternative_recipes");
        ProductionSpeed = BehaviorConfigHelper.ReadFloat(config, "production_speed", 1.0f);
        _environmentalModifier = EnvironmentalModifier.FromConfig(config);
    }

    public void OnAttach(Building owner)
    {
        _owner = owner;
        _maxResourceTier = owner.Definition?.MaxResourceTier ?? 0;
    }

    public void OnRegister()
    {
        if (_environmentalModifier == null)
        {
            EnvScaleFactor = 1f;
            return;
        }
        EnvScaleFactor = _environmentalModifier.ComputeFactor(_owner);
        GameLogger.Debug(
            $"ManufacturingBehavior: env modifier {_environmentalModifier.Type} -> scale {EnvScaleFactor:F3}");
    }
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

        if (EnvScaleFactor <= 0f)
        {
            GameLogger.Debug(
                $"ManufacturingBehavior: refusing cycle on recipe '{recipe.RecipeId}' — env scale factor is 0");
            return;
        }

        EnsureSlotsForRecipe(recipe);

        WorkRequired = recipe.WorkRequired / productionSpeed;
        WorkProgress = 0f;
        InputsHeld.Clear();
        ExpectedOutputs.Clear();
        _pendingInputs.Clear();
        ResolvedTagInputs.Clear();
        ResolvedTagOutputs.Clear();
        CycleMaterialDiscriminator = null;
        _currentRecipe = recipe;

        // Pass 1: pre-resolve tag inputs from current storage and capture the cycle's material discriminator.
        var db = ResourceDatabase.Instance;
        foreach (var input in recipe.InputResources)
        {
            if (!RecipeDefinition.IsTagInput(input.Key))
                continue;
            string tagName = RecipeDefinition.GetTagName(input.Key);
            string? picked = PickTaggedResourceFromStorage(_owner, tagName, _maxResourceTier);
            if (picked == null)
                continue;
            ResolvedTagInputs[input.Key] = picked;
            if (CycleMaterialDiscriminator == null)
                CycleMaterialDiscriminator = FindDiscriminatorTag(picked);
        }

        // Pass 2: populate ExpectedOutputs for both literal and resolvable tag outputs.
        foreach (var output in recipe.OutputResources)
            QueueRecipeOutput(output.Key, output.Value, db);

        // Pass 2b: evaluate conditional outputs against the building's recipe
        // context and add any that fire, additively, to ExpectedOutputs.
        if (recipe.ConditionalOutputs.Count > 0)
        {
            var ctx = _owner.BuildRecipeContext();
            foreach (var co in recipe.ConditionalOutputs)
            {
                if (!co.EvaluateBool(ctx))
                    continue;
                QueueRecipeOutput(co.Resource, co.Amount, db);
            }
        }

        // Pass 3: withdraw inputs, recording pending (under original key) for anything short.
        // Recipe input amounts are floored to whole units — resources can only be consumed
        // as integers.
        var missingInputs = new Dictionary<string, int>();
        foreach (var input in recipe.InputResources)
        {
            if (input.Key == "power")
                continue;

            int amountNeeded = Mathf.FloorToInt(input.Value);
            if (amountNeeded <= 0)
                continue;

            string? concreteId;
            if (RecipeDefinition.IsTagInput(input.Key))
            {
                if (!ResolvedTagInputs.TryGetValue(input.Key, out concreteId))
                {
                    missingInputs[input.Key] = amountNeeded;
                    continue;
                }
            }
            else
            {
                concreteId = input.Key;
                // Block literal inputs exceeding the building's max resource tier
                if (db != null && db.IsLoaded && db.TryGetResource(concreteId, out var inDef) && inDef != null)
                {
                    if (inDef.ResourceTier > _maxResourceTier)
                    {
                        GameLogger.Warning(
                            $"ManufacturingBehavior: blocking literal input '{concreteId}' (tier {inDef.ResourceTier}) — exceeds building max tier {_maxResourceTier}");
                        missingInputs[input.Key] = amountNeeded;
                        continue;
                    }
                }
            }

            int available = _owner.InputStorage.GetQuantity(concreteId);
            int toWithdraw = System.Math.Min(available, amountNeeded);
            if (toWithdraw > 0)
            {
                _owner.InputStorage.Withdraw(concreteId, toWithdraw);
                InputsHeld[concreteId] = InputsHeld.GetValueOrDefault(concreteId) + toWithdraw;
                amountNeeded -= toWithdraw;
            }

            if (amountNeeded > 0)
                missingInputs[RecipeDefinition.IsTagInput(input.Key) ? input.Key : concreteId] = amountNeeded;
        }

        if (missingInputs.Count > 0)
        {
            State = ManufacturingState.WaitingForInputs;
            _pendingInputs = new Dictionary<string, int>(missingInputs);
        }
        else
        {
            State = ManufacturingState.Manufacturing;
        }
    }

    /// <summary>
    /// Selects the highest-quantity resource in <paramref name="owner"/>'s InputStorage whose
    /// ResourceDefinition carries <paramref name="tag"/> and has a tier at or below
    /// <paramref name="maxTier"/>. Ties are broken alphabetically by resource id.
    /// Returns null when no matching resource is currently in storage or the database is unavailable.
    /// </summary>
    private static string? PickTaggedResourceFromStorage(Building owner, string tag, int maxTier)
    {
        if (owner == null || string.IsNullOrEmpty(tag))
            return null;
        var db = ResourceDatabase.Instance;
        if (db == null || !db.IsLoaded)
            return null;

        var quantities = owner.InputStorage.GetAllQuantities();
        if (quantities.Count == 0)
            return null;

        var matching = db.GetResourcesByTagAndMaxTier(tag, maxTier);
        if (matching.Count == 0)
            return null;

        string? best = null;
        float bestQty = 0f;
        foreach (var def in matching)
        {
            if (def == null || string.IsNullOrEmpty(def.IdName))
                continue;
            if (!quantities.TryGetValue(def.IdName, out var qty) || qty <= 0)
                continue;
            if (qty > bestQty
                || (qty == bestQty && best != null && string.CompareOrdinal(def.IdName, best) < 0))
            {
                best = def.IdName;
                bestQty = qty;
            }
        }
        return best;
    }

    private static string? FindDiscriminatorTag(string resourceId)
    {
        var db = ResourceDatabase.Instance;
        if (db == null || !db.IsLoaded)
            return null;
        if (!db.TryGetResource(resourceId, out var def) || def?.Tags == null)
            return null;
        foreach (var tag in def.Tags)
        {
            if (tag != null && tag.StartsWith(ResourceDatabase.DiscriminatorTagPrefix))
                return tag;
        }
        return null;
    }

    /// <summary>
    /// Resolves a tag-output key and appends it to ExpectedOutputs and OutputStorage slots.
    /// Resolution uses the cycle's captured <c>material:*</c> discriminator tag (from a
    /// tag input) to select the unique resource carrying both the output tag and the
    /// discriminator (smelting-style).
    /// No-op when the discriminator is not yet available — the caller can retry once a
    /// tag input arrives.
    /// </summary>
    /// <summary>
    /// Routes a single recipe output (literal or tag-prefixed) through the same
    /// tag-resolution + tier-guard logic that Pass 2 applies. Conditional outputs
    /// add additively when the key is already present in ExpectedOutputs.
    /// </summary>
    private void QueueRecipeOutput(string key, float amount, ResourceDatabase? db)
    {
        if (key == "power")
            return;
        if (RecipeDefinition.IsTagInput(key))
        {
            TryResolveAndQueueTagOutput(key, amount);
            return;
        }
        if (db != null && db.IsLoaded && db.TryGetResource(key, out var outDef) && outDef != null)
        {
            if (outDef.ResourceTier > _maxResourceTier)
            {
                GameLogger.Warning(
                    $"ManufacturingBehavior: skipping output '{key}' (tier {outDef.ResourceTier}) — exceeds building max tier {_maxResourceTier}");
                return;
            }
        }
        float scaled = amount * EnvScaleFactor;
        if (ExpectedOutputs.TryGetValue(key, out var existing))
            ExpectedOutputs[key] = existing + scaled;
        else
            ExpectedOutputs[key] = scaled;
    }

    private void TryResolveAndQueueTagOutput(string tagOutputKey, float amount)
    {
        if (_owner == null)
            return;
        if (ResolvedTagOutputs.ContainsKey(tagOutputKey))
            return;

        string tagName = RecipeDefinition.GetTagName(tagOutputKey);

        if (CycleMaterialDiscriminator == null)
            return;
        var db = ResourceDatabase.Instance;
        if (db == null || !db.IsLoaded)
            return;
        if (!db.TryResolveTaggedOutput(tagName, CycleMaterialDiscriminator, _maxResourceTier, out var def) || def?.IdName == null)
        {
            GameLogger.Warning(
                $"ManufacturingBehavior: could not resolve output '{tagOutputKey}' for discriminator '{CycleMaterialDiscriminator}' on recipe '{_currentRecipe?.RecipeId}'");
            return;
        }

        ResolvedTagOutputs[tagOutputKey] = def.IdName;
        ExpectedOutputs[def.IdName] = amount * EnvScaleFactor;
        EnsureOutputSlotForResource(def.IdName);
    }

    private void EnsureOutputSlotForResource(string resourceId)
    {
        if (_owner == null) return;
        foreach (var slot in _owner.OutputStorage.Slots)
        {
            if (slot.Filter.Kind == SlotFilterKind.Resource
                && slot.Filter.ResourceId == resourceId)
                return;
        }
        _owner.OutputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(resourceId)));
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
                int held = kvp.Value;
                if (held <= 0)
                    continue;
                int deposited = _owner.InputStorage.Deposit(kvp.Key, held);
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
        ResolvedTagInputs.Clear();
        ResolvedTagOutputs.Clear();
        CycleMaterialDiscriminator = null;
        _currentRecipe = null;
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

        var db = ResourceDatabase.Instance;

        var existingIn = new HashSet<string>();
        foreach (var slot in _owner.InputStorage.Slots)
        {
            if (slot.Filter.Kind == SlotFilterKind.Resource && slot.Filter.ResourceId != null)
                existingIn.Add(slot.Filter.ResourceId);
        }
        foreach (var input in recipe.InputResources.Keys)
        {
            if (input == "power") continue;
            if (RecipeDefinition.IsTagInput(input))
            {
                if (db == null || !db.IsLoaded)
                    continue;
                var matching = db.GetResourcesByTagAndMaxTier(RecipeDefinition.GetTagName(input), _maxResourceTier);
                foreach (var def in matching)
                {
                    if (def?.IdName == null || existingIn.Contains(def.IdName))
                        continue;
                    _owner.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(def.IdName)));
                    existingIn.Add(def.IdName);
                }
                continue;
            }
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
            if (RecipeDefinition.IsTagInput(output))
            {
                // Tag-output slots are created lazily once the cycle's material discriminator
                // resolves the concrete output id (see TryResolveAndQueueTagOutput).
                continue;
            }
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

        var stillMissing = new Dictionary<string, int>();
        bool discriminatorChanged = false;
        foreach (var kvp in _pendingInputs)
        {
            string key = kvp.Key;
            int needed = kvp.Value;

            string? concreteId;
            if (RecipeDefinition.IsTagInput(key))
            {
                if (!ResolvedTagInputs.TryGetValue(key, out concreteId))
                {
                    concreteId = PickTaggedResourceFromStorage(owner, RecipeDefinition.GetTagName(key), _maxResourceTier);
                    if (concreteId == null)
                    {
                        stillMissing[key] = needed;
                        continue;
                    }
                    ResolvedTagInputs[key] = concreteId;
                    if (CycleMaterialDiscriminator == null)
                    {
                        CycleMaterialDiscriminator = FindDiscriminatorTag(concreteId);
                        if (CycleMaterialDiscriminator != null)
                            discriminatorChanged = true;
                    }
                }
            }
            else
            {
                concreteId = key;
            }

            int inStorage = owner.InputStorage.GetQuantity(concreteId);
            int toWithdraw = System.Math.Min(inStorage, needed);
            if (toWithdraw > 0)
            {
                owner.InputStorage.Withdraw(concreteId, toWithdraw);
                InputsHeld[concreteId] = InputsHeld.GetValueOrDefault(concreteId) + toWithdraw;
                needed -= toWithdraw;
            }

            if (needed > 0)
                stillMissing[key] = needed;
        }

        _pendingInputs = stillMissing;

        // If we just captured the cycle's discriminator, resolve any tag outputs that were deferred.
        if (discriminatorChanged && _currentRecipe != null)
        {
            foreach (var output in _currentRecipe.OutputResources)
            {
                if (output.Key == "power" || !RecipeDefinition.IsTagInput(output.Key))
                    continue;
                TryResolveAndQueueTagOutput(output.Key, output.Value);
            }
        }

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
        ResolvedTagInputs.Clear();
        ResolvedTagOutputs.Clear();
        CycleMaterialDiscriminator = null;
        _currentRecipe = null;
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
            int amountAvailable = kvp.Value;
            if (amountAvailable <= 0)
                continue;

            foreach (var node in exportNodes)
            {
                if (amountAvailable <= 0)
                    break;
                if (node.Link == null)
                    continue;

                int enqueued = node.Link.TryEnqueueAmount(resourceId, amountAvailable);
                if (enqueued > 0)
                {
                    owner.OutputStorage.Withdraw(resourceId, enqueued);
                    amountAvailable -= enqueued;
                }
            }
        }
    }
}
