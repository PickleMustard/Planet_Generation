using Constructables.Buildings.Behaviors;
using Godot;
using Structures.Enums;
using Structures.Resources;
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
    private TextureRect? _renderIcon;

    private Label? _stateValueLabel;
    private ColorRect? _stateDot;
    private Label? _workValueLabel;
    private Label? _cycleValueLabel;
    private Label? _powerValueLabel;
    private Label? _efficiencyValueLabel;

    private GridContainer? _inputSlotsGrid;
    private GridContainer? _outputSlotsGrid;
    private ProgressBar? _workProgressBar;
    private Label? _workNumLabel;
    private Label? _workDenomLabel;

    private RecipeDisplay? _recipeDisplay;
    private VBoxContainer? _busInputsList;
    private VBoxContainer? _busOutputsList;

    private VBoxContainer? _nodesList;

    private PackedScene? _resourceSlotItemScene;
    private PackedScene? _resourceRateItemScene;

    private static readonly Color StateDotIdle = new(0.55f, 0.55f, 0.6f);
    private static readonly Color StateDotRun = new(0.29f, 0.65f, 0.32f);
    private static readonly Color StateDotBlock = new(0.78f, 0.32f, 0.21f);
    private static readonly Color StateDotFull = new(0.85f, 0.62f, 0.25f);

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

        _inputSlotsGrid = GetNodeOrNull<GridContainer>("Layout/TopStrip/RightVBox/AssemblyLine/InputSlotsGrid");
        _outputSlotsGrid = GetNodeOrNull<GridContainer>("Layout/TopStrip/RightVBox/AssemblyLine/OutputSlotsGrid");
        _workProgressBar = GetNodeOrNull<ProgressBar>("Layout/TopStrip/RightVBox/AssemblyLine/ProgressBox/WorkProgressBar");
        _workNumLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/AssemblyLine/ProgressBox/WorkLabels/WorkNum");
        _workDenomLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/AssemblyLine/ProgressBox/WorkLabels/WorkDenom");

        _recipeDisplay = GetNodeOrNull<RecipeDisplay>("Layout/Bottom/RecipePane/RecipeDisplay");
        _busInputsList = GetNodeOrNull<VBoxContainer>("Layout/Bottom/RecipePane/Bus/InputsScroll/InputsList");
        _busOutputsList = GetNodeOrNull<VBoxContainer>("Layout/Bottom/RecipePane/Bus/OutputsScroll/OutputsList");

        _nodesList = GetNodeOrNull<VBoxContainer>("Layout/Bottom/NodesPane/Scroll/NodesList");

        _resourceSlotItemScene = ResourceLoader.Load<PackedScene>("res://UI/BuildingInfo/ResourceSlotItem.tscn");
        _resourceRateItemScene = ResourceLoader.Load<PackedScene>("res://UI/BuildingInfo/ResourceRateItem.tscn");
    }

    protected override void UpdateDisplay()
    {
        if (_building == null) { Clear(); return; }

        UpdateRender();

        var recipe = GetActiveRecipe();
        var mfg = _building.GetBehavior<ManufacturingBehavior>();
        UpdateHeader(mfg, recipe);
        UpdateAssemblyLine(mfg);
        UpdateRecipePane(recipe);
        UpdateNodesList();
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
        ClearChildren(_inputSlotsGrid);
        ClearChildren(_outputSlotsGrid);
        ClearChildren(_busInputsList);
        ClearChildren(_busOutputsList);
        ClearChildren(_nodesList);
        _recipeDisplay?.Clear();
    }

    private void UpdateRender()
    {
        if (_renderIcon == null || _building?.Definition == null) return;
        var iconDef = _building.Definition.Icon;
        Texture2D? tex = iconDef?.IsValid == true
            ? (iconDef.MediumTexture ?? iconDef.SmallTexture)
            : null;
        if (tex == null && !string.IsNullOrEmpty(iconDef?.BasePath))
        {
            try { tex = ResourceLoader.Load<Texture2D>(iconDef.BasePath + "_medium.png"); }
            catch { tex = null; }
        }
        _renderIcon.Texture = tex;
    }

    private void UpdateHeader(ManufacturingBehavior? mfg, RecipeDefinition? recipe)
    {
        var state = mfg?.State ?? ManufacturingState.Idle;
        (string label, Color dot) = state switch
        {
            ManufacturingState.Manufacturing => ("Manufacturing", StateDotRun),
            ManufacturingState.WaitingForInputs => ("Input Starved", StateDotBlock),
            ManufacturingState.Outputting => ("Output Full", StateDotFull),
            _ => ("Idle", StateDotIdle),
        };

        if (_stateValueLabel != null) _stateValueLabel.Text = label;
        if (_stateDot != null) _stateDot.Color = dot;

        float pct = 0f;
        if (mfg != null && mfg.WorkRequired > 0f)
            pct = Mathf.Clamp(mfg.WorkProgress / mfg.WorkRequired, 0f, 1f);
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
            float speed = _building?.Definition?.Production?.ProductionSpeed ?? 1f;
            _efficiencyValueLabel.Text = $"{speed:P0}";
        }
    }

    private void UpdateAssemblyLine(ManufacturingBehavior? mfg)
    {
        PopulateSlotGrid(_inputSlotsGrid, _building?.InputStorage, _resourceSlotItemScene);
        PopulateSlotGrid(_outputSlotsGrid, _building?.OutputStorage, _resourceSlotItemScene);

        if (_workProgressBar != null)
        {
            _workProgressBar.MinValue = 0;
            _workProgressBar.MaxValue = 1;
            float frac = (mfg != null && mfg.WorkRequired > 0f)
                ? Mathf.Clamp(mfg.WorkProgress / mfg.WorkRequired, 0f, 1f) : 0f;
            _workProgressBar.Value = frac;
        }

        if (_workNumLabel != null)
            _workNumLabel.Text = $"work · {(mfg?.WorkProgress ?? 0f):F0}";
        if (_workDenomLabel != null)
            _workDenomLabel.Text = $"req · {(mfg?.WorkRequired ?? 0f):F0}";
    }

    private void UpdateRecipePane(RecipeDefinition? recipe)
    {
        _recipeDisplay?.SetRecipe(recipe);
        bool isExtraction = _building?.GetBehavior<ExtractionBehavior>() != null;
        _recipeDisplay?.SetEnabled(!isExtraction);

        ClearChildren(_busInputsList);
        ClearChildren(_busOutputsList);
        if (recipe == null || _resourceRateItemScene == null) return;

        var (inputRates, outputRates) = ComputeRecipeRates(recipe, _building?.Definition);

        if (_busInputsList != null)
        {
            foreach (var kvp in inputRates)
            {
                var item = _resourceRateItemScene.Instantiate<ResourceRateItem>();
                if (item == null) continue;
                _busInputsList.AddChild(item);
                item.SetResourceRate(kvp.Key, -kvp.Value);
            }
        }
        if (_busOutputsList != null)
        {
            foreach (var kvp in outputRates)
            {
                var item = _resourceRateItemScene.Instantiate<ResourceRateItem>();
                if (item == null) continue;
                _busOutputsList.AddChild(item);
                item.SetResourceRate(kvp.Key, kvp.Value);
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

            var head = new Label { Text = $"{node.Kind} · {node.Side}" };
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
