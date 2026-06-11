using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary;

namespace Constructables.Buildings.Behaviors;

/// <summary>Whether an extraction slot produces at full rate (primary) or the reduced
/// <see cref="ExtractionBehavior.SecondaryRateMultiplier"/> rate (secondary).</summary>
public enum ExtractionSlotKind { Primary, Secondary }

/// <summary>
/// One player-assignable extraction slot. <see cref="ResourceId"/> is null when the slot
/// is empty (produces nothing). The pickable pool is the behavior's
/// <see cref="ExtractionBehavior.AvailableDeposits"/> (recipe-tag + tier gated).
/// </summary>
public sealed class ExtractionSlot
{
    public ExtractionSlotKind Kind { get; set; }
    public string? ResourceId { get; set; }
}

/// <summary>
/// Self-contained extraction cycle driver. Resolves concrete outputs from cell deposits
/// by tag (derived from the configured recipe's output keys), scales by deposit abundance,
/// and runs a discrete manufacturing cycle
/// (Idle → Manufacturing → Outputting → Idle) mirroring <see cref="ManufacturingBehavior"/>'s
/// pattern. Extraction buildings attach this behavior (optionally with
/// <see cref="PowerConsumerBehavior"/>) instead of <see cref="ManufacturingBehavior"/>.
/// <see cref="ManufacturingBehavior"/> can still consult <see cref="AvailableDeposits"/>
/// and <see cref="TryResolveOutput"/> for hybrid setups.
/// </summary>
public partial class ExtractionBehavior : RefCounted, IBuildingBehavior, IBehaviorConfigurable
{
    private Building? _owner;

    /// <summary>Cached max resource tier from the owner's BuildingDefinition. Updated in OnAttach.</summary>
    private int _maxResourceTier;

    public Building? Owner => _owner;

    // ── Deposit tracking ────────────────────────────────────────────────────

    /// <summary>
    /// Resource id → summed abundance across this building's occupied cells. Populated at
    /// <see cref="OnRegister"/> and <see cref="OnRecipeChanged"/>; only resources whose
    /// <see cref="ResourceDefinition.Tags"/> contain a tag from the active recipe's output
    /// keys and whose <see cref="ResourceDefinition.ResourceTier"/> does not exceed
    /// <see cref="_maxResourceTier"/> are included.
    /// </summary>
    public Dictionary<string, float> AvailableDeposits { get; private set; } = new();

    // ── Cycle state ─────────────────────────────────────────────────────────

    private ManufacturingState _state = ManufacturingState.Idle;
    public ManufacturingState State
    {
        get => _state;
        private set
        {
            _state = value;
            _owner?.SetProductionState(value);
        }
    }
    public float WorkProgress { get; private set; }
    public float WorkRequired { get; private set; }
    public Dictionary<string, float> ExpectedOutputs { get; private set; } = new();

    /// <summary>
    /// Tag output key (e.g., "tag:ore") → concrete resource id resolved at
    /// <see cref="OnRegister"/> or <see cref="OnRecipeChanged"/> time.
    /// Persists across cycles until the recipe changes.
    /// </summary>
    public Dictionary<string, string> ResolvedTagOutputs { get; private set; } = new();

    // ── Recipe config ───────────────────────────────────────────────────────

    /// <summary>Default recipe read from YAML behavior config.</summary>
    public string? DefaultRecipe { get; private set; }

    private List<string> _alternativeRecipes = new();
    public IReadOnlyList<string> AlternativeRecipes => _alternativeRecipes;

    /// <summary>Production speed multiplier read from YAML behavior config.</summary>
    public float ProductionSpeed { get; private set; } = 1.0f;

    // ── Extraction slots ────────────────────────────────────────────────────

    /// <summary>
    /// Guards <see cref="_slots"/> against concurrent access: slot mutators
    /// (<see cref="TryAssignSlot"/>/<see cref="ClearSlot"/>) run on the UI thread while
    /// <see cref="StartCycle"/> reads slots on the ManufactureTickEngine thread.
    /// </summary>
    private readonly object _slotLock = new();

    /// <summary>Primary slot count from YAML; -1 means "auto" (distinct output-tag count of the default recipe).</summary>
    private int _primarySlotCount = -1;
    private int _secondarySlotCount;

    /// <summary>Per-cycle output multiplier applied to secondary slots. From YAML, default 0.5.</summary>
    public float SecondaryRateMultiplier { get; private set; } = 0.5f;

    private readonly List<ExtractionSlot> _slots = new();

    /// <summary>
    /// Read-only snapshot (deep copy) of the current slot assignments for UI display.
    /// Mutating the returned objects has no effect on behavior state.
    /// </summary>
    public IReadOnlyList<ExtractionSlot> Slots
    {
        get
        {
            lock (_slotLock)
            {
                var copy = new List<ExtractionSlot>(_slots.Count);
                foreach (var s in _slots)
                    copy.Add(new ExtractionSlot { Kind = s.Kind, ResourceId = s.ResourceId });
                return copy;
            }
        }
    }

    // ── Current cycle recipe ────────────────────────────────────────────────

    private RecipeDefinition? _currentRecipe;
    public RecipeDefinition? CurrentRecipe => _currentRecipe;

    // ── Environmental modifier ──────────────────────────────────────────────

    private EnvironmentalModifier? _environmentalModifier;

    /// <summary>
    /// Cached factor from the environmental modifier; 1.0 when none configured.
    /// Multiplies every non-power output at deposit time. A value of 0 blocks
    /// new cycles from starting.
    /// </summary>
    public float EnvScaleFactor { get; private set; } = 1f;

    // ── Tick control ────────────────────────────────────────────────────────

    public bool WantsTick => State != ManufacturingState.WaitingForInputs
                          && State != ManufacturingState.Idle;

    public int Priority { get; set; } = 5;

    // ── Pending inputs (for WaitingForInputs → Manufacturing transition) ─────

    private Dictionary<string, int> _pendingInputs = new();

    // ── IBuildingBehavior lifecycle ─────────────────────────────────────────

    public void OnAttach(Building owner)
    {
        _owner = owner;
        _maxResourceTier = owner.Definition?.MaxResourceTier ?? 0;
        _owner.SetProductionState(_state);
    }

    public void Configure(Dictionary<string, object> config)
    {
        DefaultRecipe = BehaviorConfigHelper.ReadString(config, "default_recipe", null);
        _alternativeRecipes = BehaviorConfigHelper.ReadStringList(config, "alternative_recipes");
        ProductionSpeed = BehaviorConfigHelper.ReadFloat(config, "production_speed", 1.0f);
        _primarySlotCount = BehaviorConfigHelper.ReadInt(config, "primary_slots", -1);
        _secondarySlotCount = BehaviorConfigHelper.ReadInt(config, "secondary_slots", 0);
        SecondaryRateMultiplier = BehaviorConfigHelper.ReadFloat(config, "secondary_rate_multiplier", 0.5f);
        _environmentalModifier = EnvironmentalModifier.FromConfig(config);
    }

    public void OnRegister()
    {
        RebuildAvailableDeposits();
        lock (_slotLock)
        {
            EnsureSlotsAllocatedLocked();
            ReconcileSlotsLocked();
            AutoFillPrimarySlotsLocked();
        }
        ResolveTagOutputs();

        if (_environmentalModifier == null)
        {
            EnvScaleFactor = 1f;
            return;
        }
        EnvScaleFactor = _environmentalModifier.ComputeFactor(_owner);
        GameLogger.Debug(
            $"ExtractionBehavior: env modifier {_environmentalModifier.Type} -> scale {EnvScaleFactor:F3}");
    }

    public void OnUnregister() { }
    public void OnDetach() => _owner = null;

    // ── Recipe change hook ───────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="Building.SwapRecipe"/> after the active recipe changes.
    /// Re-resolves tag outputs from cell deposits based on the new recipe's output keys
    /// and rebuilds <see cref="AvailableDeposits"/> with the new tag set and tier filter.
    /// </summary>
    public void OnRecipeChanged(string recipeId)
    {
        RebuildAvailableDeposits();
        lock (_slotLock)
        {
            // Slot count is config-driven and fixed; only resource assignments reconcile.
            ReconcileSlotsLocked();
            AutoFillPrimarySlotsLocked();
        }
        ResolveTagOutputs();
    }

    // ── Extraction slot management ───────────────────────────────────────────

    /// <summary>
    /// Allocates <see cref="_slots"/> on first registration. Primary count comes from YAML
    /// (<c>primary_slots</c>); when absent (-1) it defaults to the distinct output-tag count of
    /// the active/default recipe (min 1) so multi-tag extraction recipes keep producing every
    /// tag. Secondary count comes from <c>secondary_slots</c> (default 0). Idempotent: a
    /// previously allocated slot list (e.g. on recipe change) is left untouched to preserve
    /// assignments. Caller must hold <see cref="_slotLock"/>.
    /// </summary>
    private void EnsureSlotsAllocatedLocked()
    {
        if (_slots.Count > 0)
            return;

        int primary = _primarySlotCount;
        if (primary < 0)
        {
            primary = 1;
            var recipe = LookupRecipe(_owner?.ActiveRecipeId ?? DefaultRecipe);
            if (recipe != null)
                primary = Mathf.Max(1, ExtractOutputTags(recipe).Count);
        }
        int secondary = Mathf.Max(0, _secondarySlotCount);

        for (int i = 0; i < primary; i++)
            _slots.Add(new ExtractionSlot { Kind = ExtractionSlotKind.Primary });
        for (int i = 0; i < secondary; i++)
            _slots.Add(new ExtractionSlot { Kind = ExtractionSlotKind.Secondary });
    }

    /// <summary>
    /// Clears any slot whose assigned resource is no longer eligible (not in
    /// <see cref="AvailableDeposits"/> after a deposit/recipe change). Caller holds the lock.
    /// </summary>
    private void ReconcileSlotsLocked()
    {
        foreach (var slot in _slots)
            if (slot.ResourceId != null && !AvailableDeposits.ContainsKey(slot.ResourceId))
                slot.ResourceId = null;
    }

    /// <summary>
    /// Fills empty PRIMARY slots with the highest-abundance eligible resources, preferring
    /// distinct resources across slots and allowing repeats once the eligible pool is
    /// exhausted. Secondary slots and existing (user) assignments are left untouched.
    /// Caller holds the lock.
    /// </summary>
    private void AutoFillPrimarySlotsLocked()
    {
        var ordered = OrderedEligibleResources();
        if (ordered.Count == 0)
            return;

        var used = new HashSet<string>();
        foreach (var s in _slots)
            if (s.ResourceId != null)
                used.Add(s.ResourceId);

        int cursor = 0;
        foreach (var slot in _slots)
        {
            if (slot.Kind != ExtractionSlotKind.Primary || slot.ResourceId != null)
                continue;

            string? pick = null;
            for (int k = 0; k < ordered.Count; k++)
            {
                var cand = ordered[(cursor + k) % ordered.Count];
                if (!used.Contains(cand))
                {
                    pick = cand;
                    cursor = (cursor + k + 1) % ordered.Count;
                    break;
                }
            }
            if (pick == null)
            {
                pick = ordered[cursor % ordered.Count];
                cursor++;
            }

            slot.ResourceId = pick;
            used.Add(pick);
            EnsureOutputSlotForResource(pick);
        }
    }

    /// <summary>
    /// Eligible resources (keys of <see cref="AvailableDeposits"/>) sorted by abundance
    /// descending, ties broken alphabetically — the same ordering as <see cref="TryResolveOutput"/>.
    /// </summary>
    private List<string> OrderedEligibleResources()
    {
        var list = new List<string>(AvailableDeposits.Keys);
        list.Sort((a, b) =>
        {
            int cmp = AvailableDeposits[b].CompareTo(AvailableDeposits[a]);
            return cmp != 0 ? cmp : string.CompareOrdinal(a, b);
        });
        return list;
    }

    /// <summary>
    /// Assigns <paramref name="resourceId"/> to slot <paramref name="slotIndex"/>. The resource
    /// must be eligible (present in <see cref="AvailableDeposits"/>) and within the building's
    /// max resource tier. The same resource may occupy multiple slots. Thread-safe.
    /// Returns false when the index is out of range or the resource is ineligible.
    /// </summary>
    public bool TryAssignSlot(int slotIndex, string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return false;

        lock (_slotLock)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
                return false;
            if (!AvailableDeposits.ContainsKey(resourceId))
                return false;

            var db = ResourceDatabase.Instance;
            if (db != null && db.IsLoaded && db.TryGetResource(resourceId, out var def)
                && def != null && def.ResourceTier > _maxResourceTier)
                return false;

            _slots[slotIndex].ResourceId = resourceId;
            EnsureOutputSlotForResource(resourceId);
            return true;
        }
    }

    /// <summary>Empties slot <paramref name="slotIndex"/> (produces nothing). Thread-safe.</summary>
    public void ClearSlot(int slotIndex)
    {
        lock (_slotLock)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
                return;
            _slots[slotIndex].ResourceId = null;
        }
    }

    /// <summary>
    /// Per-cycle output contribution of slot <paramref name="slotIndex"/> for UI display:
    /// <c>baseValue × fractionWeight × (primary ? 1 : SecondaryRateMultiplier)</c>, where
    /// fractionWeight is the summed richness-tier fraction across occupied cells (stacks/cycle).
    /// Returns 0 for empty/out-of-range slots. Thread-safe.
    /// </summary>
    public float GetSlotRate(int slotIndex)
    {
        lock (_slotLock)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
                return 0f;
            var slot = _slots[slotIndex];
            if (slot.ResourceId == null)
                return 0f;
            var recipe = LookupRecipe(_owner?.ActiveRecipeId ?? DefaultRecipe);
            float baseVal = ResolveSlotBaseValue(slot.ResourceId, recipe);
            if (baseVal < 0f)
                return 0f;
            float weight = AvailableDeposits.GetValueOrDefault(slot.ResourceId);
            float rate = slot.Kind == ExtractionSlotKind.Primary ? 1f : SecondaryRateMultiplier;
            return baseVal * weight * rate;
        }
    }

    /// <summary>
    /// Restores slot assignments from a save. Slot kinds/counts are re-applied from YAML at
    /// <see cref="Configure"/>/<see cref="OnRegister"/> time; this maps saved resource ids onto
    /// the existing slots by index where the kind matches, dropping resources no longer eligible.
    /// A saved null clears an auto-filled slot (preserves an explicit player "clear"). Thread-safe.
    /// </summary>
    public void RestoreSlots(IReadOnlyList<(ExtractionSlotKind Kind, string? ResourceId)> saved)
    {
        if (saved == null)
            return;
        lock (_slotLock)
        {
            int n = Mathf.Min(saved.Count, _slots.Count);
            for (int i = 0; i < n; i++)
            {
                if (_slots[i].Kind != saved[i].Kind)
                    continue; // slot layout changed since save; leave auto-filled default
                var res = saved[i].ResourceId;
                if (res != null && AvailableDeposits.ContainsKey(res))
                {
                    _slots[i].ResourceId = res;
                    EnsureOutputSlotForResource(res);
                }
                else
                {
                    _slots[i].ResourceId = null;
                }
            }
        }
    }

    /// <summary>
    /// Base per-cycle amount for a resource: the value of the first <paramref name="recipe"/>
    /// output tag the resource carries (fallback 1.0). Returns -1 when the resource/recipe/db is
    /// unavailable.
    /// </summary>
    private float ResolveSlotBaseValue(string resourceId, RecipeDefinition? recipe)
    {
        if (recipe == null)
            return -1f;
        var db = ResourceDatabase.Instance;
        if (db == null || !db.IsLoaded
            || !db.TryGetResource(resourceId, out var rdef) || rdef?.Tags == null)
            return -1f;
        foreach (var output in recipe.OutputResources)
        {
            if (!RecipeDefinition.IsTagInput(output.Key))
                continue;
            if (rdef.Tags.Contains(RecipeDefinition.GetTagName(output.Key)))
                return output.Value;
        }
        return 1f;
    }

    // ── Tag output resolution ────────────────────────────────────────────────

    /// <summary>
    /// Resolves tag output keys from the active recipe into concrete resource ids via
    /// <see cref="TryResolveOutput"/>. Populates <see cref="ResolvedTagOutputs"/> and
    /// creates output storage slots for each resolved resource.
    /// Called at <see cref="OnRegister"/> and <see cref="OnRecipeChanged"/>.
    /// </summary>
    public void ResolveTagOutputs()
    {
        ResolvedTagOutputs.Clear();

        if (_owner == null)
            return;

        var recipe = LookupRecipe(_owner.ActiveRecipeId ?? DefaultRecipe);
        if (recipe == null)
            return;

        foreach (var output in recipe.OutputResources)
        {
            if (output.Key == "power")
                continue;

            if (!RecipeDefinition.IsTagInput(output.Key))
                continue;

            string tagName = RecipeDefinition.GetTagName(output.Key);
            if (TryResolveOutput(tagName, out var resourceId, out _)
                && resourceId != null)
            {
                ResolvedTagOutputs[output.Key] = resourceId;
                EnsureOutputSlotForResource(resourceId);
            }
            else
            {
                GameLogger.Warning(
                    $"ExtractionBehavior: could not resolve tag output '{output.Key}' from AvailableDeposits on recipe '{recipe.RecipeId}'");
            }
        }
    }

    // ── Recipe lookup helper ─────────────────────────────────────────────────

    /// <summary>
    /// Looks up a <see cref="RecipeDefinition"/> by id from the <see cref="RecipeDatabase"/>.
    /// Returns null when the database is unavailable or the id is not found.
    /// </summary>
    private static RecipeDefinition? LookupRecipe(string? recipeId)
    {
        if (string.IsNullOrEmpty(recipeId))
            return null;
        if (RecipeDatabase.Instance == null || !RecipeDatabase.Instance.TryGetRecipe(recipeId, out var recipe))
            return null;
        return recipe;
    }

    // ── Deposit tag extraction ───────────────────────────────────────────────

    /// <summary>
    /// Extracts the set of bare tag names (e.g., "ore" from "tag:ore") from a recipe's
    /// output resource keys. Returns an empty set when the recipe has no tag outputs.
    /// </summary>
    private static HashSet<string> ExtractOutputTags(RecipeDefinition recipe)
    {
        var tags = new HashSet<string>();
        foreach (var key in recipe.OutputResources.Keys)
        {
            if (RecipeDefinition.IsTagInput(key))
                tags.Add(RecipeDefinition.GetTagName(key));
        }
        return tags;
    }

    // ── Cycle entry point ───────────────────────────────────────────────────

    /// <summary>
    /// Starts a new extraction cycle using the given recipe. Uses pre-resolved tag outputs
    /// from <see cref="ResolvedTagOutputs"/> (populated at OnRegister/OnRecipeChanged time)
    /// with abundance scaling. Only callable when the state is
    /// <see cref="ManufacturingState.Idle"/> or <see cref="ManufacturingState.Outputting"/>.
    /// </summary>
    public void StartCycle(RecipeDefinition recipe, float productionSpeed)
    {
        if (_owner == null)
            return;

        if (State != ManufacturingState.Idle && State != ManufacturingState.Outputting)
            return;

        if (EnvScaleFactor <= 0f)
        {
            GameLogger.Debug(
                $"ExtractionBehavior: refusing cycle on recipe '{recipe.RecipeId}' — env scale factor is 0");
            return;
        }

        WorkRequired = recipe.WorkRequired / productionSpeed;
        WorkProgress = 0f;
        ExpectedOutputs.Clear();
        _pendingInputs.Clear();
        _currentRecipe = recipe;

        var db = ResourceDatabase.Instance;

        // Tag-driven outputs come from the player-assigned extraction slots: each filled slot
        // produces its chosen resource at baseValue × fractionWeight × (primary ? 1 :
        // secondaryRate), where fractionWeight is the summed richness-tier fraction across the
        // building's cells. Slots sharing a resource sum into one output entry.
        List<(string Resource, bool Primary)> filledSlots = new();
        lock (_slotLock)
        {
            foreach (var slot in _slots)
                if (slot.ResourceId != null)
                    filledSlots.Add((slot.ResourceId, slot.Kind == ExtractionSlotKind.Primary));
        }

        foreach (var (resourceId, primary) in filledSlots)
        {
            float baseVal = ResolveSlotBaseValue(resourceId, recipe);
            if (baseVal < 0f)
                continue;
            float weight = AvailableDeposits.GetValueOrDefault(resourceId);
            float rate = primary ? 1f : SecondaryRateMultiplier;
            float contribution = baseVal * weight * rate;
            ExpectedOutputs[resourceId] = ExpectedOutputs.GetValueOrDefault(resourceId) + contribution;
        }

        // Literal (non-tag) outputs are not slot-driven: produced as-is, tier-gated.
        foreach (var output in recipe.OutputResources)
        {
            if (output.Key == "power" || RecipeDefinition.IsTagInput(output.Key))
                continue;

            if (db != null && db.IsLoaded && db.TryGetResource(output.Key, out var outDef) && outDef != null
                && outDef.ResourceTier > _maxResourceTier)
            {
                GameLogger.Warning(
                    $"ExtractionBehavior: skipping literal output '{output.Key}' (tier {outDef.ResourceTier}) — exceeds building max tier {_maxResourceTier}");
                continue;
            }
            ExpectedOutputs[output.Key] = ExpectedOutputs.GetValueOrDefault(output.Key) + output.Value * EnvScaleFactor;
        }

        // Conditional outputs: evaluate against the building's recipe context
        // (temperature/moisture/elevation/atmosphere/specifier) and add any that
        // fire additively. Tag-prefixed conditional outputs use the cycle's
        // pre-resolved tag mappings, matching the base-output logic above.
        if (recipe.ConditionalOutputs.Count > 0)
        {
            var ctx = _owner.BuildRecipeContext();
            foreach (var co in recipe.ConditionalOutputs)
            {
                if (!co.EvaluateBool(ctx))
                    continue;
                AddConditionalExtractionOutput(co.Resource, co.Amount, db);
            }
        }

        // No outputs (all slots empty, no literal/conditional outputs fired) — nothing to do.
        // Return to Idle instead of spinning a no-op Manufacturing cycle.
        if (ExpectedOutputs.Count == 0)
        {
            ClearCycleState();
            State = ManufacturingState.Idle;
            return;
        }

        // Withdraw inputs from InputStorage, tracking pending for shortages.
        // Recipe input amounts are floored to whole units — resources consume as integers.
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
                concreteId = PickTaggedResourceFromStorage(_owner, RecipeDefinition.GetTagName(input.Key), _maxResourceTier);
                if (concreteId == null)
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
                            $"ExtractionBehavior: blocking literal input '{concreteId}' (tier {inDef.ResourceTier}) — exceeds building max tier {_maxResourceTier}");
                        missingInputs[input.Key] = amountNeeded;
                        continue;
                    }
                }
            }

            int available = _owner.InputStorage.GetQuantity(concreteId);
            int toWithdraw = System.Math.Min(available, amountNeeded);
            if (toWithdraw > 0)
                _owner.InputStorage.Withdraw(concreteId, toWithdraw);

            if (toWithdraw < amountNeeded)
                missingInputs[RecipeDefinition.IsTagInput(input.Key) ? input.Key : concreteId] =
                    amountNeeded - toWithdraw;
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

    // ── Tick driver ─────────────────────────────────────────────────────────

    public void OnManufactureTick(float delta, Building owner)
    {
        if (!owner.PoweredOn)
            return;

        if (State == ManufacturingState.WaitingForInputs)
            TryFulfillInputsFromStorage(owner);

        if (State == ManufacturingState.Manufacturing)
        {
            WorkProgress += delta;
            if (WorkProgress >= WorkRequired)
                FinishCycle(owner);
        }

        PushOutputs(owner);
    }

    // ── Cycle completion ────────────────────────────────────────────────────

    /// <summary>
    /// Deposits <see cref="ExpectedOutputs"/> into the owner's OutputStorage
    /// and returns to Idle.
    /// </summary>
    public void FinishCycle(Building owner)
    {
        State = ManufacturingState.Outputting;

        foreach (var output in ExpectedOutputs)
            owner.OutputStorage.Deposit(output.Key, output.Value);

        ClearCycleState();
        State = ManufacturingState.Idle;
    }

    // ── Cycle cancellation ─────────────────────────────────────────────────

    /// <summary>
    /// Aborts any in-flight extraction cycle and returns to Idle.
    /// No held inputs to return (extraction doesn't hold inputs mid-cycle;
    /// they are consumed immediately on withdrawal).
    /// </summary>
    public void CancelCycle(bool returnHeldInputs)
    {
        // ExtractionBehavior does not track InputsHeld separately — inputs
        // are withdrawn from InputStorage at StartCycle time. If the caller
        // requests returning held inputs, there is nothing to return because
        // the extraction model does not buffer inputs mid-cycle.
        ClearCycleState();
        State = ManufacturingState.Idle;
    }

    // ── Output pushing ──────────────────────────────────────────────────────

    /// <summary>
    /// Drains OutputStorage to export links. Identical logic to
    /// <see cref="ManufacturingBehavior.PushOutputs"/>: skips when
    /// <see cref="BulkStorageRoutingBehavior"/> is active.
    /// </summary>
    public void PushOutputs(Building owner)
    {
        if (owner.GetBehavior<BulkStorageRoutingBehavior>() != null
            && owner.BulkStorage.Slots.Count > 0)
            return;

        var quantities = owner.OutputStorage.GetAllQuantities();
        if (quantities.Count == 0)
            return;

        var exportNodes = new List<ResourceNode>();
        foreach (var node in owner.Nodes)
        {
            if ((node.Kind == ResourceNodeKind.Export || node.Kind == ResourceNodeKind.Flex)
                && node.Link != null)
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

    // ── Storage helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Adds a resource-locked slot to OutputStorage if one doesn't already exist
    /// for the given resource.
    /// </summary>
    public void EnsureOutputSlotForResource(string resourceId)
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

    // ── Input fulfillment ──────────────────────────────────────────────────

    /// <summary>
    /// Attempts to withdraw pending inputs from InputStorage and transitions
    /// to <see cref="ManufacturingState.Manufacturing"/> when all inputs are fulfilled.
    /// Same pattern as <see cref="ManufacturingBehavior.TryFulfillInputsFromStorage"/>.
    /// </summary>
    private void TryFulfillInputsFromStorage(Building owner)
    {
        if (_pendingInputs.Count == 0)
            return;

        var stillMissing = new Dictionary<string, int>();
        foreach (var kvp in _pendingInputs)
        {
            string key = kvp.Key;
            int needed = kvp.Value;

            string? concreteId;
            if (RecipeDefinition.IsTagInput(key))
            {
                concreteId = PickTaggedResourceFromStorage(owner, RecipeDefinition.GetTagName(key), _maxResourceTier);
                if (concreteId == null)
                {
                    stillMissing[key] = needed;
                    continue;
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
                needed -= toWithdraw;
            }

            if (needed > 0)
                stillMissing[key] = needed;
        }

        _pendingInputs = stillMissing;

        if (_pendingInputs.Count == 0)
            State = ManufacturingState.Manufacturing;
    }

    // ── Tag input resolution ───────────────────────────────────────────────

    /// <summary>
    /// Selects the highest-quantity resource in <paramref name="owner"/>'s InputStorage whose
    /// ResourceDefinition carries <paramref name="tag"/> and whose tier does not exceed
    /// <paramref name="maxTier"/>. Ties broken alphabetically.
    /// Returns null when no matching resource is in storage.
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

        string? best = null;
        int bestQty = 0;
        foreach (var kvp in quantities)
        {
            if (!db.TryGetResource(kvp.Key, out var def) || def?.Tags == null)
                continue;
            if (!def.Tags.Contains(tag))
                continue;
            if (def.ResourceTier > maxTier)
                continue;
            int qty = kvp.Value;
            if (qty > bestQty
                || (qty == bestQty && best != null && string.CompareOrdinal(kvp.Key, best) < 0))
            {
                best = kvp.Key;
                bestQty = qty;
            }
        }
        return best;
    }

    // ── Deposit tracking ───────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds <see cref="AvailableDeposits"/> from the owner's current cell mix.
    /// Only resources whose <see cref="ResourceDefinition.Tags"/> contain a tag from the
    /// active recipe's output keys and whose <see cref="ResourceDefinition.ResourceTier"/>
    /// does not exceed <see cref="_maxResourceTier"/> are included.
    /// Public so callers (tests, placement-change logic) can re-trigger after the cell set changes.
    /// </summary>
    public void RebuildAvailableDeposits()
    {
        AvailableDeposits.Clear();
        if (_owner == null)
            return;

        var db = ResourceDatabase.Instance;
        if (db == null || !db.IsLoaded)
            return;

        // Derive tags from the active recipe's output keys
        var recipe = LookupRecipe(_owner.ActiveRecipeId ?? DefaultRecipe);
        if (recipe == null)
            return;

        var outputTags = ExtractOutputTags(recipe);
        if (outputTags.Count == 0)
            return;

        foreach (var cell in _owner.OccupiedCells)
            AccumulateDeposits(cell, outputTags, _maxResourceTier, db, AvailableDeposits);
    }

    /// <summary>
    /// Adds the extractable deposits from <paramref name="cell"/> into <paramref name="into"/>.
    /// A resource is extractable when it has positive abundance, its tier does not exceed
    /// <paramref name="maxResourceTier"/>, and its tags contain at least one of
    /// <paramref name="outputTags"/>. Shared by the live behavior and the placement preview so
    /// the two cannot drift.
    /// </summary>
    private static void AccumulateDeposits(
        VoronoiCell? cell,
        HashSet<string> outputTags,
        int maxResourceTier,
        ResourceDatabase db,
        Dictionary<string, float> into)
    {
        if (cell?.Resources == null) return;
        foreach (var kvp in cell.Resources)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0f) continue;
            if (!db.TryGetResource(kvp.Key, out var def) || def?.Tags == null) continue;
            if (def.ResourceTier > maxResourceTier) continue;

            // Include if the resource carries any of the recipe's output tags
            bool matches = false;
            foreach (var tag in outputTags)
            {
                if (def.Tags.Contains(tag))
                {
                    matches = true;
                    break;
                }
            }
            if (!matches) continue;

            // Bucket each cell's raw abundance % into its richness-tier fraction-of-a-stack,
            // then sum across occupied cells. The stored deposit "weight" is therefore the
            // summed per-cycle fraction, not a raw abundance — extraction reads it directly.
            into[kvp.Key] = into.GetValueOrDefault(kvp.Key) + AbundanceCategoryTable.FractionFor(kvp.Value);
        }
    }

    /// <summary>
    /// Computes the resources an extraction building defined by <paramref name="def"/> could
    /// extract from <paramref name="cell"/>, abundance-descending (alpha tiebreak). Uses the
    /// same recipe-tag + tier filtering as the live <see cref="RebuildAvailableDeposits"/>, so a
    /// placement preview matches what the built building will actually be able to extract.
    /// Returns an empty list when the building has no ExtractionBehavior, the database is
    /// unavailable, the default recipe is missing, or nothing on the cell qualifies.
    /// </summary>
    public static List<KeyValuePair<string, float>> GetExtractableDeposits(
        BuildingDefinition def, VoronoiCell cell)
    {
        var result = new List<KeyValuePair<string, float>>();
        if (def == null || cell == null)
            return result;

        var db = ResourceDatabase.Instance;
        if (db == null || !db.IsLoaded)
            return result;

        var entry = def.BehaviorEntries.Find(e => e.BehaviorId == "ExtractionBehavior");
        if (entry == null)
            return result;

        var recipeId = BehaviorConfigHelper.ReadString(entry.Config, "default_recipe", null);
        var recipe = LookupRecipe(recipeId);
        if (recipe == null)
            return result;

        var outputTags = ExtractOutputTags(recipe);
        if (outputTags.Count == 0)
            return result;

        var deposits = new Dictionary<string, float>();
        AccumulateDeposits(cell, outputTags, def.MaxResourceTier, db, deposits);

        result.AddRange(deposits);
        result.Sort((a, b) =>
        {
            int cmp = b.Value.CompareTo(a.Value);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.Key, b.Key);
        });
        return result;
    }

    /// <summary>
    /// Picks the highest-weight deposit whose ResourceDefinition carries <paramref name="outputTag"/>
    /// and whose tier does not exceed <see cref="_maxResourceTier"/>.
    /// Ties are broken alphabetically by resource id. Returns false when no deposit matches.
    /// </summary>
    public bool TryResolveOutput(string outputTag, out string? resourceId, out float weight)
    {
        resourceId = null;
        weight = 0f;
        if (string.IsNullOrEmpty(outputTag) || AvailableDeposits.Count == 0)
            return false;

        var db = ResourceDatabase.Instance;
        if (db == null || !db.IsLoaded)
            return false;

        string? best = null;
        float bestWeight = 0f;
        foreach (var kvp in AvailableDeposits)
        {
            if (!db.TryGetResource(kvp.Key, out var def) || def?.Tags == null) continue;
            if (!def.Tags.Contains(outputTag)) continue;
            if (def.ResourceTier > _maxResourceTier) continue;
            float w = kvp.Value;
            if (w > bestWeight
                || (w == bestWeight && best != null && string.CompareOrdinal(kvp.Key, best) < 0))
            {
                best = kvp.Key;
                bestWeight = w;
            }
        }

        if (best == null)
            return false;
        resourceId = best;
        weight = bestWeight;
        return true;
    }

    // ── Internal helpers ────────────────────────────────────────────────────

    private void ClearCycleState()
    {
        ExpectedOutputs.Clear();
        _pendingInputs.Clear();
        // ResolvedTagOutputs persists across cycles — only cleared on recipe change
        _currentRecipe = null;
        WorkProgress = 0f;
        WorkRequired = 0f;
    }

    /// <summary>
    /// Mirrors the base-output add-with-tier-guard for conditional outputs.
    /// Tag-prefixed keys reuse the cycle's pre-resolved tag map; literal keys
    /// stack additively on whatever the base recipe already enqueued.
    /// </summary>
    private void AddConditionalExtractionOutput(string key, float amount, ResourceDatabase? db)
    {
        if (string.IsNullOrEmpty(key) || key == "power")
            return;

        if (RecipeDefinition.IsTagInput(key))
        {
            // Conditional tag outputs are not slot-split: resolve to the single highest-abundance
            // eligible resource for the tag (deterministic), matching legacy conditional behavior.
            if (!TryResolveOutput(RecipeDefinition.GetTagName(key), out var resourceId, out var weight)
                || resourceId == null)
            {
                GameLogger.Warning(
                    $"ExtractionBehavior: conditional tag output '{key}' could not be resolved on recipe '{_currentRecipe?.RecipeId}'");
                return;
            }
            // weight is the summed richness-tier fraction (stacks/cycle), env no longer applied.
            float scaled = amount * weight;
            ExpectedOutputs[resourceId] = ExpectedOutputs.GetValueOrDefault(resourceId) + scaled;
            return;
        }

        if (db != null && db.IsLoaded && db.TryGetResource(key, out var def) && def != null
            && def.ResourceTier > _maxResourceTier)
        {
            GameLogger.Warning(
                $"ExtractionBehavior: skipping conditional output '{key}' (tier {def.ResourceTier}) — exceeds building max tier {_maxResourceTier}");
            return;
        }

        float scaledLit = amount * EnvScaleFactor;
        ExpectedOutputs[key] = ExpectedOutputs.GetValueOrDefault(key) + scaledLit;
    }
}
