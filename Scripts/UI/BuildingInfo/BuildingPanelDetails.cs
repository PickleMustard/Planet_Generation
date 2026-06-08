using Constructables;
using Constructables.Buildings.Behaviors;
using Constructables.Power;
using Godot;
using Structures.Enums;
using Structures.Resources;
using UI.GridWindow;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Bespoke panel for manufacturing / extraction / agriculture buildings.
/// Composition: assembly-line top strip (3D render thumb + console-style stat header
/// + input slots → progress arrow → output slots) above a recipe bus and a
/// resource-nodes list.
/// </summary>
public partial class BuildingPanelDetails : BaseBuildingDetails
{
    /// <summary>Raised when the player clicks an extraction slot row; carries the slot index.
    /// <see cref="BuildingInfoWindow"/> opens the resource picker in response.</summary>
    [Signal]
    public delegate void ExtractionSlotClickedEventHandler(int slotIndex);

    private TextureRect? _renderIcon;

    private Label? _stateValueLabel;
    private ColorRect? _stateDot;
    private Label? _workValueLabel;
    private Label? _cycleValueLabel;
    private Label? _powerValueLabel;
    private Label? _efficiencyValueLabel;

    private Label? _gridValueLabel;
    private Label? _gridStatusLabel;
    private Button? _openGridBtn;

    private GridContainer? _inputSlotsGrid;
    private GridContainer? _outputSlotsGrid;
    private ProgressBar? _workProgressBar;
    private Label? _workNumLabel;
    private Label? _workDenomLabel;

    private RecipeDisplay? _recipeDisplay;
    private VBoxContainer? _busInputsList;
    private VBoxContainer? _busOutputsList;

    private VBoxContainer? _nodesList;

    private VBoxContainer? _extractionSlotsPane;
    private GridContainer? _extractionSlotsGrid;

    private PackedScene? _resourceSlotItemScene;
    private PackedScene? _resourceRateItemScene;

    private static readonly Color StateDotIdle = new(0.55f, 0.55f, 0.6f);
    private static readonly Color StateDotRun = new(0.29f, 0.65f, 0.32f);
    private static readonly Color StateDotBlock = new(0.78f, 0.32f, 0.21f);
    private static readonly Color StateDotFull = new(0.85f, 0.62f, 0.25f);
    private static readonly Color StatusOk = new(0.29f, 0.65f, 0.32f);
    private static readonly Color StatusDeficit = new(0.85f, 0.55f, 0.25f);
    private static readonly Color StatusBrownout = new(0.85f, 0.20f, 0.20f);
    private static readonly Color StatusMuted = new(0.55f, 0.55f, 0.6f);

    private int _refreshCounter;
    public override void _PhysicsProcess(double delta)
    {
        _refreshCounter++;
        if (_refreshCounter >= 10) { _refreshCounter = 0; UpdateDisplay(); }
    }

    public override void _Ready()
    {
        _renderIcon = GetNodeOrNull<TextureRect>("Layout/TopStrip/RenderTile/RenderIcon");

        _stateValueLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/StateTile/Row/StateLabel");
        _stateDot = GetNodeOrNull<ColorRect>("Layout/TopStrip/RightVBox/Header/StateTile/Row/StateDot");
        _workValueLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/StateTile/Row/WorkLabel");
        _cycleValueLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/CycleTile/VBox/CycleValue");
        _powerValueLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/PowerTile/VBox/PowerValue");
        _efficiencyValueLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/EfficiencyTile/VBox/EfficiencyValue");

        _gridValueLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/GridTile/VBox/GridValue");
        _gridStatusLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/GridTile/VBox/GridStatus");
        _openGridBtn = GetNodeOrNull<Button>("Layout/TopStrip/RightVBox/Header/GridTile/VBox/OpenBtn");
        if (_openGridBtn != null)
            _openGridBtn.Pressed += OnOpenGridPressed;

        _inputSlotsGrid = GetNodeOrNull<GridContainer>("Layout/TopStrip/RightVBox/AssemblyLine/InputSlotsGrid");
        _outputSlotsGrid = GetNodeOrNull<GridContainer>("Layout/TopStrip/RightVBox/AssemblyLine/OutputSlotsGrid");
        _workProgressBar = GetNodeOrNull<ProgressBar>("Layout/TopStrip/RightVBox/AssemblyLine/ProgressBox/WorkProgressBar");
        _workNumLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/AssemblyLine/ProgressBox/WorkLabels/WorkNum");
        _workDenomLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/AssemblyLine/ProgressBox/WorkLabels/WorkDenom");

        _recipeDisplay = GetNodeOrNull<RecipeDisplay>("Layout/Bottom/RecipePane/RecipeDisplay");
        _busInputsList = GetNodeOrNull<VBoxContainer>("Layout/Bottom/RecipePane/Bus/InputsScroll/InputsList");
        _busOutputsList = GetNodeOrNull<VBoxContainer>("Layout/Bottom/RecipePane/Bus/OutputsScroll/OutputsList");

        _nodesList = GetNodeOrNull<VBoxContainer>("Layout/Bottom/NodesPane/Scroll/NodesList");

        _extractionSlotsPane = GetNodeOrNull<VBoxContainer>("Layout/ExtractionSlotsPane");
        _extractionSlotsGrid = GetNodeOrNull<GridContainer>("Layout/ExtractionSlotsPane/ExtractionSlotsGrid");

        _resourceSlotItemScene = ResourceLoader.Load<PackedScene>("res://UI/BuildingInfo/ResourceSlotItem.tscn");
        _resourceRateItemScene = ResourceLoader.Load<PackedScene>("res://UI/BuildingInfo/ResourceRateItem.tscn");
    }

    protected override void UpdateDisplay()
    {
        if (_building == null) { Clear(); return; }

        UpdateRender();

        var recipe = GetActiveRecipe();
        var mfg = _building.GetBehavior<ManufacturingBehavior>();
        var ext = _building.GetBehavior<ExtractionBehavior>();
        UpdateHeader(mfg, ext, recipe);
        UpdateGridTile();
        UpdateAssemblyLine(mfg, ext);
        UpdateRecipePane(recipe, mfg, ext);
        UpdateExtractionSlots(ext);
        UpdateNodesList();
    }

    /// <summary>
    /// Renders one clickable button per extraction slot (primary/secondary badge, assigned
    /// resource icon+name, and per-cycle rate) for extraction buildings; hidden otherwise.
    /// Clicking a row raises <see cref="ExtractionSlotClicked"/> so the window opens the picker.
    /// </summary>
    private void UpdateExtractionSlots(ExtractionBehavior? ext)
    {
        if (_extractionSlotsPane == null || _extractionSlotsGrid == null) return;

        if (ext == null)
        {
            _extractionSlotsPane.Visible = false;
            ClearChildren(_extractionSlotsGrid);
            return;
        }

        _extractionSlotsPane.Visible = true;
        ClearChildren(_extractionSlotsGrid);

        var slots = ext.Slots;
        var db = ResourceDatabase.Instance;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var btn = new Button
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
                Alignment = HorizontalAlignment.Left,
            };
            string badge = slot.Kind == ExtractionSlotKind.Primary ? "[P]" : "[S]";

            if (slot.ResourceId == null)
            {
                btn.Text = $"{badge}  — empty —";
                btn.Modulate = new Color(0.7f, 0.7f, 0.7f);
            }
            else
            {
                ResourceDefinition? rdef = null;
                db?.TryGetResource(slot.ResourceId, out rdef);
                if (rdef?.Icon?.Texture != null)
                    btn.Icon = rdef.Icon.Texture;
                string name = PrettifyResourceId(rdef?.IdName ?? slot.ResourceId);
                float rate = ext.GetSlotRate(i);
                btn.Text = $"{badge} {name}  ·  {rate:0.##}/cyc";
            }

            int idx = i; // capture per-iteration index
            btn.Pressed += () => EmitSignal(SignalName.ExtractionSlotClicked, idx);
            _extractionSlotsGrid.AddChild(btn);
        }
    }

    private static string PrettifyResourceId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "Unknown";
        var words = id.Split('_');
        for (int i = 0; i < words.Length; i++)
            if (words[i].Length > 0)
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
        return string.Join(" ", words);
    }

    private void UpdateGridTile()
    {
        if (_building == null) return;
        var grid = FindGridForBuilding(_building);
        var consumer = _building.GetBehavior<PowerConsumerBehavior>();

        if (grid == null)
        {
            if (_gridValueLabel != null) _gridValueLabel.Text = "—";
            if (_gridStatusLabel != null) { _gridStatusLabel.Text = "no grid"; _gridStatusLabel.Modulate = StatusMuted; }
            if (_openGridBtn != null) _openGridBtn.Disabled = true;
            return;
        }

        float myDraw = consumer?.GetCurrentDraw() ?? 0f;
        float gridDraw = grid.LastDraw;
        float gridCap = grid.LastGeneration;
        if (_gridValueLabel != null)
            _gridValueLabel.Text = $"{myDraw:F0} / {gridDraw:F0} kW";
        if (_gridStatusLabel != null)
        {
            float net = grid.LastGeneration - grid.LastDraw;
            if (grid.IsBrownedOut) { _gridStatusLabel.Text = $"cap {gridCap:F0} · BROWNOUT"; _gridStatusLabel.Modulate = StatusBrownout; }
            else if (net < 0f) { _gridStatusLabel.Text = $"cap {gridCap:F0} · DEFICIT"; _gridStatusLabel.Modulate = StatusDeficit; }
            else { _gridStatusLabel.Text = $"cap {gridCap:F0} · OK"; _gridStatusLabel.Modulate = StatusOk; }
        }
        if (_openGridBtn != null) _openGridBtn.Disabled = false;
    }

    private void OnOpenGridPressed()
    {
        if (_building == null) return;
        var grid = FindGridForBuilding(_building);
        if (grid != null)
            GridDetailWindow.Instance?.ShowGrid(grid);
    }

    private static PowerGrid? FindGridForBuilding(Building building)
    {
        var consumer = building.GetBehavior<PowerConsumerBehavior>();
        if (consumer?.Grid != null) return consumer.Grid;

        var tree = building.VisualNode?.GetTree();
        if (tree == null) return null;
        foreach (var node in tree.Root.GetChildren())
            if (TryFindGrid(node, building, out var g)) return g;
        return null;
    }

    private static bool TryFindGrid(Node node, Building building, out PowerGrid? grid)
    {
        if (node is BodyPowerGridManager mgr)
        {
            grid = mgr.GetGridForBuilding(building);
            if (grid != null) return true;
        }
        foreach (var child in node.GetChildren())
            if (TryFindGrid(child, building, out grid)) return true;
        grid = null;
        return false;
    }

    public override void Clear()
    {
        base.Clear();
        if (_renderIcon != null) _renderIcon.Texture = null;
        if (_stateValueLabel != null) _stateValueLabel.Text = "—";
        if (_workValueLabel != null) _workValueLabel.Text = "0%";
        if (_workProgressBar != null) _workProgressBar.Value = 0;
        if (_workNumLabel != null) _workNumLabel.Text = "work · 0";
        if (_workDenomLabel != null) _workDenomLabel.Text = "req · 0";
        if (_cycleValueLabel != null) _cycleValueLabel.Text = "—";
        if (_powerValueLabel != null) _powerValueLabel.Text = "—";
        if (_efficiencyValueLabel != null) _efficiencyValueLabel.Text = "—";
        if (_gridValueLabel != null) _gridValueLabel.Text = "—";
        if (_gridStatusLabel != null) { _gridStatusLabel.Text = "no grid"; _gridStatusLabel.Modulate = StatusMuted; }
        if (_openGridBtn != null) _openGridBtn.Disabled = true;
        ClearChildren(_inputSlotsGrid);
        ClearChildren(_outputSlotsGrid);
        ClearChildren(_busInputsList);
        ClearChildren(_busOutputsList);
        ClearChildren(_nodesList);
        ClearChildren(_extractionSlotsGrid);
        if (_extractionSlotsPane != null) _extractionSlotsPane.Visible = false;
        _recipeDisplay?.Clear();
    }

    private void UpdateRender()
    {
        if (_renderIcon == null || _building?.Definition == null) return;
        var iconDef = _building.Definition.Icon;
        Texture2D? tex = iconDef?.Texture;
        if (tex == null && !string.IsNullOrEmpty(iconDef?.ResourcePath))
        {
            tex = UtilityLibrary.DataLoading.IconDataLoader.LoadIconTexture(iconDef.ResourcePath, "building-panel");
        }
        _renderIcon.Texture = tex;
    }

    private void UpdateHeader(ManufacturingBehavior? mfg, ExtractionBehavior? ext, RecipeDefinition? recipe)
    {
        var state = mfg?.State ?? ext?.State ?? ManufacturingState.Idle;
        (string label, Color dot) = state switch
        {
            ManufacturingState.Manufacturing => ("Manufacturing", StateDotRun),
            ManufacturingState.WaitingForInputs => ("Input Starved", StateDotBlock),
            ManufacturingState.Outputting => ("Output Full", StateDotFull),
            _ => ("Idle", StateDotIdle),
        };

        if (_stateValueLabel != null) _stateValueLabel.Text = label;
        if (_stateDot != null) _stateDot.Color = dot;

        float workProgress = mfg?.WorkProgress ?? ext?.WorkProgress ?? 0f;
        float workRequired = mfg?.WorkRequired ?? ext?.WorkRequired ?? 0f;
        float pct = 0f;
        if (workRequired > 0f)
            pct = Mathf.Clamp(workProgress / workRequired, 0f, 1f);
        if (_workValueLabel != null) _workValueLabel.Text = $"{Mathf.RoundToInt(pct * 100f)}%";

        if (_cycleValueLabel != null)
            _cycleValueLabel.Text = recipe?.WorkRequired > 0f ? $"{recipe.WorkRequired:F0}u" : "—";

        if (_powerValueLabel != null)
        {
            float power = 0f;
            var consumer = _building?.GetBehavior<PowerConsumerBehavior>();
            if (consumer != null) power = consumer.BaseDraw;
            if (recipe?.InputResources != null && recipe.InputResources.TryGetValue("power", out float recipePower))
                power = recipePower;
            _powerValueLabel.Text = power > 0f ? $"{power:F0} kW" : "—";
        }

        if (_efficiencyValueLabel != null)
        {
            float speed = mfg?.ProductionSpeed ?? ext?.ProductionSpeed ?? 1f;
            _efficiencyValueLabel.Text = $"{speed:P0}";
        }
    }

    private void UpdateAssemblyLine(ManufacturingBehavior? mfg, ExtractionBehavior? ext)
    {
        PopulateSlotGrid(_inputSlotsGrid, _building?.InputStorage, _resourceSlotItemScene);
        PopulateSlotGrid(_outputSlotsGrid, _building?.OutputStorage, _resourceSlotItemScene);

        float workProgress = mfg?.WorkProgress ?? ext?.WorkProgress ?? 0f;
        float workRequired = mfg?.WorkRequired ?? ext?.WorkRequired ?? 0f;

        if (_workProgressBar != null)
        {
            _workProgressBar.MinValue = 0;
            _workProgressBar.MaxValue = 1;
            float frac = workRequired > 0f
                ? Mathf.Clamp(workProgress / workRequired, 0f, 1f) : 0f;
            _workProgressBar.Value = frac;
        }

        if (_workNumLabel != null)
            _workNumLabel.Text = $"work · {workProgress:F0}";
        if (_workDenomLabel != null)
            _workDenomLabel.Text = $"req · {workRequired:F0}";
    }

    private void UpdateRecipePane(RecipeDefinition? recipe, ManufacturingBehavior? mfg, ExtractionBehavior? ext)
    {
        _recipeDisplay?.SetRecipe(recipe);
        // Enable recipe swap when either behavior has alternative recipes
        bool hasAlternatives = (mfg != null && mfg.AlternativeRecipes.Count > 0)
                            || (ext != null && ext.AlternativeRecipes.Count > 0);
        _recipeDisplay?.SetEnabled(hasAlternatives);

        ClearChildren(_busInputsList);
        ClearChildren(_busOutputsList);
        if (recipe == null || _resourceRateItemScene == null) return;

        var (inputRates, outputRates, tagContext) = mfg != null
            ? ComputeRecipeRatesWithTagContext(recipe, mfg)
            : ComputeRecipeRatesWithTagContext(recipe, ext);

        if (_busInputsList != null)
        {
            foreach (var kvp in inputRates)
            {
                var item = _resourceRateItemScene.Instantiate<ResourceRateItem>();
                if (item == null) continue;
                _busInputsList.AddChild(item);

                bool isTag = tagContext.TryGetValue(kvp.Key, out var ctx) && ctx.IsTag;
                string? tagName = isTag ? ctx.TagName : null;
                string? resolvedId = isTag ? ctx.ResolvedId : null;
                item.SetResourceRate(kvp.Key, -kvp.Value, isTag, tagName, resolvedId);
            }
        }
        if (_busOutputsList != null)
        {
            foreach (var kvp in outputRates)
            {
                var item = _resourceRateItemScene.Instantiate<ResourceRateItem>();
                if (item == null) continue;
                _busOutputsList.AddChild(item);

                bool isTag = tagContext.TryGetValue(kvp.Key, out var ctx) && ctx.IsTag;
                string? tagName = isTag ? ctx.TagName : null;
                string? resolvedId = isTag ? ctx.ResolvedId : null;
                item.SetResourceRate(kvp.Key, kvp.Value, isTag, tagName, resolvedId);
            }
        }
    }

    private void UpdateNodesList()
    {
        ClearChildren(_nodesList);
        if (_nodesList == null || _building == null) return;

        foreach (var node in _building.Nodes)
        {
            var row = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 2);

            var head = new Label { Text = $"{node.Kind} · side {node.SideIndex}.{node.SlotIndex}" };
            row.AddChild(head);

            var link = node.Link;
            if (link == null)
            {
                var lbl = new Label
                {
                    Text = "  — empty port —",
                    Modulate = new Color(0.7f, 0.7f, 0.7f),
                };
                row.AddChild(lbl);
            }
            else
            {
                var other = ReferenceEquals(link.Source, node) ? link.Target : link.Source;
                string otherName = other?.Owner?.Name ?? "(unknown)";
                int inFlight = link.InFlight.Count;
                int slotCap = link.Profile?.SlotCapacity ?? 0;
                var lbl = new Label
                {
                    Text = $"  → {otherName}  ·  in-flight {inFlight}/{slotCap}",
                };
                row.AddChild(lbl);
            }
            _nodesList.AddChild(row);
        }
    }

    private static void ClearChildren(Node? container)
    {
        if (container == null) return;
        foreach (var child in container.GetChildren()) child.QueueFree();
    }
}
