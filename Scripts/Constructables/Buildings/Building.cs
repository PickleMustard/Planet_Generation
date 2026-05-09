using System.Collections.Generic;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Constructables.Tick;
using Godot;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary;
using GodotDict = Godot.Collections.Dictionary;
using GodotTypedDict = Godot.Collections.Dictionary<string, int>;

namespace Constructables;

/// <summary>
/// Runtime building instance. Lives as a Godot Resource to avoid the Node3D
/// _Process / _PhysicsProcess pipeline; ticking is driven externally.
/// A BuildingNode visual proxy in the scene tree owns the model.
/// </summary>
public partial class Building : Resource, IConstructable, IManufactureTickable
{
    [Signal]
    public delegate void OnCompletionEventHandler();

    [Signal]
    public delegate void OnConstructionBlockedEventHandler();

    [Signal]
    public delegate void OnConstructionResumedEventHandler();

    [Signal]
    public delegate void OnRecipeChangedEventHandler(string newRecipeId);

    private ConstructionState? _constructionState;
    private BuildingDefinition? _definition;
    private bool _isUnderConstruction;

    public string Id { get; set; } = "";

    public string Name { get; set; } = "Building";

    public BuildingDefinition? Definition => _definition;

    public Godot.Collections.Array<ResourceNode> Nodes { get; } = new();

    public Storage InputStorage { get; } = new();
    public Storage OutputStorage { get; } = new();
    public Storage BulkStorage { get; } = new();

    public List<IBuildingBehavior> Behaviors { get; } = new();

    public bool PoweredOn { get; set; } = true;

    // TODO: persist when save system lands
    public string? ActiveRecipeId { get; set; }

    public StationSatellite? ConstructingStation { get; set; }

    public VoronoiCell? PrimaryCell { get; private set; }
    public List<VoronoiCell> OccupiedCells { get; private set; } = new();

    public BuildingNode? VisualNode { get; internal set; }

    public float SpeedModifier { get; set; } = 1f;

    /// <summary>
    /// Returns the first attached behavior of type T, or null. Replaces the legacy
    /// hard-coded `Manufacturing` accessor — use GetBehavior&lt;ManufacturingBehavior&gt;().
    /// </summary>
    public T? GetBehavior<T>()
        where T : class, IBuildingBehavior
    {
        foreach (var b in Behaviors)
            if (b is T match)
                return match;
        return null;
    }

    public bool IsUnderConstruction => _isUnderConstruction;

    #region IConstructable

    public float workRequired
    {
        get => _constructionState?.WorkRequired ?? 0f;
        set
        {
            if (_constructionState != null)
                _constructionState.WorkRequired = value;
        }
    }

    public float workDone
    {
        get => _constructionState?.WorkDone ?? 0f;
        set
        {
            if (_constructionState != null)
                _constructionState.WorkDone = value;
        }
    }

    public GodotTypedDict requiredResources
    {
        get
        {
            var dict = new GodotTypedDict();
            if (_constructionState != null)
                foreach (var kvp in _constructionState.RequiredResources)
                    dict[kvp.Key] = kvp.Value;
            return dict;
        }
        set
        {
            if (_constructionState != null)
            {
                _constructionState.RequiredResources.Clear();
                foreach (var kvp in value)
                    _constructionState.RequiredResources[kvp.Key] = kvp.Value;
            }
        }
    }

    public GodotTypedDict availableResources
    {
        get
        {
            var dict = new GodotTypedDict();
            if (_constructionState != null)
                foreach (var kvp in _constructionState.AvailableResources)
                    dict[kvp.Key] = kvp.Value;
            return dict;
        }
        set
        {
            if (_constructionState != null)
            {
                _constructionState.AvailableResources.Clear();
                foreach (var kvp in value)
                    _constructionState.AvailableResources[kvp.Key] = kvp.Value;
            }
        }
    }

    public bool CanConstruct(GodotDict locationDetails) => true;

    public IConstructable StartConstruction(GodotDict locationDetails)
    {
        if (_constructionState == null)
            return this;

        _isUnderConstruction = true;
        _constructionState.TryStart();

        GameLogger.Info(
            $"Building {Name}: Construction started ({_constructionState.WorkRequired}s)"
        );
        return this;
    }

    public bool DefineTemplate(GodotDict templateData) => true;

    public bool UpdateConfiguration(GodotDict updateData) => true;

    public bool CancelConstruction()
    {
        if (_constructionState == null)
            return false;

        _constructionState.Cancel();
        _isUnderConstruction = false;

        GameLogger.Info($"Building {Name}: Construction cancelled");
        return true;
    }

    public string GetStatus() => _constructionState?.Status.ToString() ?? "None";

    public float GetProgress() => _constructionState?.GetProgress() ?? 0f;

    public void UpdateProgress(float delta)
    {
        if (_constructionState == null || !_isUnderConstruction)
            return;

        var previousStatus = _constructionState.Status;
        _constructionState.UpdateProgress(delta);

        if (_constructionState.StatusChanged)
        {
            if (
                _constructionState.Status == ConstructionStatus.Blocked
                && previousStatus == ConstructionStatus.InProgress
            )
            {
                EmitSignal(SignalName.OnConstructionBlocked);
            }
            else if (
                _constructionState.Status == ConstructionStatus.InProgress
                && previousStatus == ConstructionStatus.Blocked
            )
            {
                EmitSignal(SignalName.OnConstructionResumed);
            }
            else if (_constructionState.Status == ConstructionStatus.Complete)
            {
                _isUnderConstruction = false;
                EmitSignal(SignalName.OnCompletion);
                GameLogger.Info($"Building {Name}: Construction complete");
                if (PoweredOn)
                    ManufactureTickEngine.Instance?.Register(this);
            }
        }
    }

    public bool CheckRequiredResourcesAvailable() =>
        _constructionState?.CheckRequiredResourcesAvailable() ?? true;

    public bool CanDemolish() => !_isUnderConstruction && _definition?.Demolishable != false;

    public bool DemolishConstructable() => false;

    public bool CanDestroy() => true;

    public bool DestroyConstructable()
    {
        Unregister();
        Shutdown();

        foreach (var node in Nodes)
        {
            var link = node.Link;
            if (link != null)
            {
                link.Disconnect();
            }
        }

        if (VisualNode != null && GodotObject.IsInstanceValid(VisualNode))
            VisualNode.QueueFree();
        VisualNode = null;
        return true;
    }

    #endregion

    /// <summary>
    /// Configures the building from its definition. Called by BuildingDefinition.Instantiate.
    /// </summary>
    internal void ApplyDefinition(BuildingDefinition definition)
    {
        _definition = definition;
        _constructionState = new ConstructionState(
            definition.WorkRequired,
            definition.RequiredResources
        );
        Name = definition.DisplayName ?? definition.IdName ?? "Building";

        // InputStorage / OutputStorage start empty. Recipe-aware behaviors
        // (ManufacturingBehavior, ExtractionBehavior) populate them with slots
        // sized to the active recipe. StorageHubBehavior populates BulkStorage
        // from the definition's storage_capacity + slot_filters block.
        InputStorage.StorageUpdated += OnStorageUpdated;
        OutputStorage.StorageUpdated += OnStorageUpdated;
        BulkStorage.StorageUpdated += OnStorageUpdated;

        // Auto-attach bulk routing whenever the definition declares bulk slots, so links
        // route through BulkStorage rather than InputStorage/OutputStorage. Behaviors
        // already loaded by Definition.Instantiate above are not visible here — that loop
        // adds them after this method returns — so the duplicate-check is defensive only.
        if (definition.StorageCapacity > 0 && GetBehavior<BulkStorageRoutingBehavior>() == null)
        {
            var routing = new BulkStorageRoutingBehavior();
            Behaviors.Add(routing);
            routing.OnAttach(this);
        }
    }

    /// <summary>
    /// Storage change wake-up. Any quantity change is a signal that a sleeping behavior may now
    /// be able to make progress; register the building so it gets at least one tick to re-evaluate.
    /// End-of-tick <see cref="EvaluateTickRegistration"/> will put it back to sleep if no behavior
    /// wants to keep ticking.
    /// </summary>
    private void OnStorageUpdated(string resourceId, float delta)
    {
        if (_isUnderConstruction || !PoweredOn)
            return;
        ManufactureTickEngine.Instance?.Register(this);
    }

    /// <summary>
    /// Called after the building's tick has run. Unregisters from the tick engine if no
    /// behavior currently wants ticks. Construction-in-progress and powered-off buildings
    /// short-circuit so existing register/unregister wiring stays authoritative.
    /// </summary>
    private void EvaluateTickRegistration()
    {
        if (_isUnderConstruction || !PoweredOn)
            return;

        foreach (var b in Behaviors)
        {
            if (b.WantsTick)
                return;
        }

        ManufactureTickEngine.Instance?.Unregister(this);
    }

    /// <summary>
    /// Records the cells this building occupies. Visual placement is handled by BuildingNode.
    /// </summary>
    public void SetPlacement(VoronoiCell primaryCell, List<VoronoiCell>? additionalCells)
    {
        PrimaryCell = primaryCell;
        OccupiedCells.Clear();
        OccupiedCells.Add(primaryCell);
        if (additionalCells != null)
            OccupiedCells.AddRange(additionalCells);
    }

    /// <summary>
    /// Fans <see cref="IBuildingBehavior.OnRegister"/> across all attached behaviors.
    /// Call after <see cref="SetPlacement"/> so behaviors can query <see cref="OccupiedCells"/>.
    /// Sorts <see cref="Behaviors"/> by <see cref="IBuildingBehavior.Priority"/> first so per-tick
    /// dispatch order is deterministic (e.g. BulkStorageRoutingBehavior runs before
    /// ManufacturingBehavior on hybrid bulk + manufacturing buildings).
    /// </summary>
    public void Register()
    {
        Behaviors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        foreach (var b in Behaviors)
            b.OnRegister();
    }

    /// <summary>
    /// Fans <see cref="IBuildingBehavior.OnUnregister"/> across all attached behaviors in
    /// reverse priority order. Called by <see cref="DestroyConstructable"/> so behaviors
    /// can release any external registrations (e.g. <c>TransferStationBehavior</c>'s
    /// endpoint registration with <see cref="BodyTransferManager"/>).
    /// </summary>
    public void Unregister()
    {
        for (int i = Behaviors.Count - 1; i >= 0; i--)
            Behaviors[i].OnUnregister();
    }

    public void DeliverResources(string resourceId, int amount)
    {
        _constructionState?.DeliverResources(resourceId, amount);
    }

    public void TogglePower()
    {
        PoweredOn = !PoweredOn;
        if (PoweredOn)
            ManufactureTickEngine.Instance?.Register(this);
        else
            ManufactureTickEngine.Instance?.Unregister(this);
    }

    /// <summary>
    /// Switches the active recipe. Validates the target id is one of this building's defined
    /// recipes (default + alternatives), cancels any in-flight manufacturing cycle (returning
    /// held inputs to InputStorage), updates ActiveRecipeId, emits OnRecipeChanged, and
    /// re-registers with the tick engine so the next tick starts a new cycle.
    /// Returns false if the swap is rejected (same recipe, unknown id, extraction building, etc.).
    /// </summary>
    public bool SwapRecipe(string recipeId)
    {
        if (string.IsNullOrEmpty(recipeId) || recipeId == ActiveRecipeId)
            return false;

        if (Definition?.Production == null)
            return false;

        if (GetBehavior<ExtractionBehavior>() != null)
            return false;

        var production = Definition.Production;
        bool isDefault = !string.IsNullOrEmpty(production.DefaultRecipe)
            && production.DefaultRecipe == recipeId;
        bool isAlternative = production.AlternativeRecipes.Contains(recipeId);
        if (!isDefault && !isAlternative)
        {
            GameLogger.Warning(
                $"Building {Name}: SwapRecipe rejected, '{recipeId}' not in default or alternative recipes."
            );
            return false;
        }

        if (RecipeDatabase.Instance == null
            || !RecipeDatabase.Instance.TryGetRecipe(recipeId, out _))
        {
            GameLogger.Warning(
                $"Building {Name}: SwapRecipe rejected, recipe '{recipeId}' not in RecipeDatabase."
            );
            return false;
        }

        var mfg = GetBehavior<ManufacturingBehavior>();
        mfg?.CancelCycle(returnHeldInputs: true);

        ActiveRecipeId = recipeId;
        EmitSignal(SignalName.OnRecipeChanged, recipeId);

        if (PoweredOn && !_isUnderConstruction)
            ManufactureTickEngine.Instance?.Register(this);

        GameLogger.Info($"Building {Name}: recipe swapped to '{recipeId}'");
        return true;
    }

    public void Shutdown()
    {
        PoweredOn = false;
        ManufactureTickEngine.Instance?.Unregister(this);
    }

    /// <summary>
    /// Per-tick hook driven by ManufactureTickEngine on its dedicated thread.
    /// Drives the manufacturing state machine (Idle→StartCycle, Manufacturing→OnTick) and
    /// dispatches non-manufacturing behaviors. The engine wouldn't tick this building if
    /// it had been deregistered for power deficit, so no power-deficit check is needed here.
    /// </summary>
    public void OnManufactureTick(float delta)
    {
        if (!PoweredOn)
            return;

        TryStartManufacturingCycleFromRegistration();

        foreach (var behavior in Behaviors)
        {
            try
            {
                behavior.OnManufactureTick(delta, this);
            }
            catch (System.Exception ex)
            {
                GameLogger.Error(
                    $"Building {Name}: behavior {behavior.GetType().Name} threw on tick: {ex.GetType().Name}: {ex.Message}"
                );
            }
        }

        EvaluateTickRegistration();
    }

    private void TryStartManufacturingCycleFromRegistration()
    {
        var mfg = GetBehavior<ManufacturingBehavior>();
        if (mfg == null || mfg.State != ManufacturingState.Idle)
            return;

        // ExtractionBehavior takes precedence: its synthetic recipe is built from the cell
        // resource mix and supersedes the YAML default for extraction buildings.
        var extraction = GetBehavior<ExtractionBehavior>();
        if (extraction?.SyntheticRecipe != null)
        {
            mfg.StartCycle(extraction.SyntheticRecipe, SpeedModifier);
            return;
        }

        if (Definition?.Production == null)
            return;

        string? recipeId = ActiveRecipeId ?? Definition.Production.DefaultRecipe;

        if (
            string.IsNullOrEmpty(recipeId)
            || RecipeDatabase.Instance == null
            || !RecipeDatabase.Instance.TryGetRecipe(recipeId, out var recipe)
            || recipe == null
        )
            return;

        mfg.StartCycle(recipe, SpeedModifier);
    }

    /// <summary>
    /// Called when a <see cref="ResourcePackage"/> arrives at this building via a link.
    /// When a <see cref="BulkStorageRoutingBehavior"/> is attached and bulk slots exist, the
    /// routing behavior decides where the package lands (BulkStorage, or InputStorage as a
    /// recipe-locked fallback). Otherwise the legacy path deposits straight to InputStorage.
    /// Returns false to signal the link to buffer the package for retry.
    /// </summary>
    /// <param name="package">The delivered package.</param>
    /// <returns>True if the deposit succeeded; otherwise false.</returns>
    public virtual bool OnDelivery(ResourcePackage package)
    {
        if (package == null)
        {
            return false;
        }

        var routing = GetBehavior<BulkStorageRoutingBehavior>();
        if (routing != null && BulkStorage.Slots.Count > 0)
        {
            return routing.TryRouteIncoming(package);
        }

        string resourceId = package.ResourceId;
        float amount = package.Quantity;

        if (!InputStorage.HasSpace(resourceId, amount))
        {
            return false;
        }

        InputStorage.Deposit(resourceId, amount);
        return true;
    }
}
