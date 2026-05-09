using System.Collections.Generic;
using Constructables;
using Constructables.Buildings.Behaviors;
using Constructables.Power;
using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Bespoke panel for power-generation buildings. Top strip shows the building thumbnail,
/// a power-cycle stat header, and a fuel/cycle bar pair. Bottom split shows the fuel bus
/// (recipe inputs only) and the grid list (producers / consumers / batteries).
/// </summary>
public partial class PowerPanelDetails : BaseBuildingDetails
{
    private TextureRect? _renderIcon;

    private Label? _stateValueLabel;
    private ColorRect? _stateDot;
    private Label? _cycleValueLabel;
    private Label? _outputValueLabel;
    private Label? _gridValueLabel;
    private Label? _fuelValueLabel;

    private ProgressBar? _fuelBar;
    private ProgressBar? _cycleBar;
    private Label? _outputDetailLabel;
    private Label? _batteryDetailLabel;

    private RecipeDisplay? _recipeDisplay;
    private VBoxContainer? _fuelList;

    private Label? _producersHeader;
    private Label? _consumersHeader;
    private Label? _batteriesHeader;
    private VBoxContainer? _producersList;
    private VBoxContainer? _consumersList;
    private VBoxContainer? _batteriesList;

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
        _cycleValueLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/StateTile/Row/CycleLabel");
        _outputValueLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/OutputTile/VBox/OutputValue");
        _gridValueLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/GridTile/VBox/GridValue");
        _fuelValueLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/Header/FuelTile/VBox/FuelValue");

        _cycleBar = GetNodeOrNull<ProgressBar>("Layout/TopStrip/RightVBox/CycleRow/CycleBox/CycleBar");
        _fuelBar = GetNodeOrNull<ProgressBar>("Layout/TopStrip/RightVBox/CycleRow/FuelBox/FuelBar");
        _outputDetailLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/CycleRow/Detail/OutputDetail");
        _batteryDetailLabel = GetNodeOrNull<Label>("Layout/TopStrip/RightVBox/CycleRow/Detail/BatteryDetail");

        _recipeDisplay = GetNodeOrNull<RecipeDisplay>("Layout/Bottom/FuelPane/RecipeDisplay");
        _fuelList = GetNodeOrNull<VBoxContainer>("Layout/Bottom/FuelPane/Scroll/FuelList");

        _producersHeader = GetNodeOrNull<Label>("Layout/Bottom/GridPane/Scroll/Sections/Producers/Header");
        _consumersHeader = GetNodeOrNull<Label>("Layout/Bottom/GridPane/Scroll/Sections/Consumers/Header");
        _batteriesHeader = GetNodeOrNull<Label>("Layout/Bottom/GridPane/Scroll/Sections/Batteries/Header");
        _producersList = GetNodeOrNull<VBoxContainer>("Layout/Bottom/GridPane/Scroll/Sections/Producers/List");
        _consumersList = GetNodeOrNull<VBoxContainer>("Layout/Bottom/GridPane/Scroll/Sections/Consumers/List");
        _batteriesList = GetNodeOrNull<VBoxContainer>("Layout/Bottom/GridPane/Scroll/Sections/Batteries/List");

        _resourceRateItemScene = ResourceLoader.Load<PackedScene>("res://UI/BuildingInfo/ResourceRateItem.tscn");
    }

    protected override void UpdateDisplay()
    {
        if (_building == null) { Clear(); return; }

        UpdateRender();
        var grid = FindGridForBuilding(_building);
        var producer = _building.GetBehavior<PowerProducerBehavior>();
        var battery = _building.GetBehavior<BatteryBehavior>();
        var consumer = _building.GetBehavior<PowerConsumerBehavior>();
        var recipe = GetActiveRecipe();

        UpdateHeader(producer, battery, consumer, grid, recipe);
        UpdateCycleRow(producer, battery, recipe);
        UpdateFuelPane(recipe);
        UpdateGridList(grid);
    }

    public override void Clear()
    {
        base.Clear();
        if (_renderIcon != null) _renderIcon.Texture = null;
        if (_stateValueLabel != null) _stateValueLabel.Text = "—";
        if (_cycleValueLabel != null) _cycleValueLabel.Text = "—";
        if (_outputValueLabel != null) _outputValueLabel.Text = "—";
        if (_gridValueLabel != null) _gridValueLabel.Text = "—";
        if (_fuelValueLabel != null) _fuelValueLabel.Text = "—";
        if (_cycleBar != null) _cycleBar.Value = 0;
        if (_fuelBar != null) _fuelBar.Value = 0;
        if (_outputDetailLabel != null) _outputDetailLabel.Text = "";
        if (_batteryDetailLabel != null) _batteryDetailLabel.Text = "";
        ClearChildren(_fuelList);
        ClearChildren(_producersList);
        ClearChildren(_consumersList);
        ClearChildren(_batteriesList);
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

    private void UpdateHeader(PowerProducerBehavior? producer, BatteryBehavior? battery,
        PowerConsumerBehavior? consumer, PowerGrid? grid, RecipeDefinition? recipe)
    {
        bool producing = producer?.IsProducing ?? false;
        bool brownedOut = grid?.IsBrownedOut ?? false;
        (string label, Color dot) = brownedOut ? ("Brownout", StateDotBlock)
            : producing ? ("Generating", StateDotRun)
            : (battery != null && battery.Stored > 0f) ? ("Discharging", StateDotFull)
            : ("Idle", StateDotIdle);
        if (_stateValueLabel != null) _stateValueLabel.Text = label;
        if (_stateDot != null) _stateDot.Color = dot;

        var mfg = _building?.GetBehavior<ManufacturingBehavior>();
        float cyclePct = (mfg != null && mfg.WorkRequired > 0f)
            ? Mathf.Clamp(mfg.WorkProgress / mfg.WorkRequired, 0f, 1f) : 0f;
        if (_cycleValueLabel != null) _cycleValueLabel.Text = $"cycle · {Mathf.RoundToInt(cyclePct * 100)}%";

        if (_outputValueLabel != null)
        {
            float output = producer?.Output ?? 0f;
            _outputValueLabel.Text = output > 0f ? $"{output:F0} kW" : "—";
        }

        if (_gridValueLabel != null)
        {
            if (grid == null) _gridValueLabel.Text = "no grid";
            else _gridValueLabel.Text = brownedOut ? "DEFICIT" : "OK";
        }

        if (_fuelValueLabel != null)
        {
            float fuel = ComputeFuelLevel(recipe);
            _fuelValueLabel.Text = recipe == null ? "—" : $"{Mathf.RoundToInt(fuel * 100)}%";
        }
    }

    private void UpdateCycleRow(PowerProducerBehavior? producer, BatteryBehavior? battery, RecipeDefinition? recipe)
    {
        var mfg = _building?.GetBehavior<ManufacturingBehavior>();
        if (_cycleBar != null)
        {
            _cycleBar.MinValue = 0; _cycleBar.MaxValue = 1;
            _cycleBar.Value = (mfg != null && mfg.WorkRequired > 0f)
                ? Mathf.Clamp(mfg.WorkProgress / mfg.WorkRequired, 0f, 1f) : 0f;
        }

        if (_fuelBar != null)
        {
            _fuelBar.MinValue = 0; _fuelBar.MaxValue = 1;
            _fuelBar.Value = ComputeFuelLevel(recipe);
        }

        if (_outputDetailLabel != null)
        {
            float output = producer?.Output ?? 0f;
            _outputDetailLabel.Text = output > 0f ? $"+{output:F0} kW · to grid" : "—";
        }

        if (_batteryDetailLabel != null)
        {
            if (battery != null && battery.Capacity > 0f)
                _batteryDetailLabel.Text = $"battery · {battery.Stored:F0}/{battery.Capacity:F0} kWh";
            else
                _batteryDetailLabel.Text = "";
        }
    }

    private float ComputeFuelLevel(RecipeDefinition? recipe)
    {
        if (recipe == null || _building == null) return 0f;
        float total = 0f, held = 0f;
        foreach (var kvp in recipe.InputResources)
        {
            if (kvp.Key == "power" || kvp.Value <= 0f) continue;
            total += kvp.Value;
            held += Mathf.Min(_building.InputStorage.GetQuantity(kvp.Key), kvp.Value);
        }
        return total > 0f ? Mathf.Clamp(held / total, 0f, 1f) : (recipe.InputResources.Count == 0 ? 1f : 0f);
    }

    private void UpdateFuelPane(RecipeDefinition? recipe)
    {
        _recipeDisplay?.SetRecipe(recipe);
        ClearChildren(_fuelList);
        if (recipe == null || _resourceRateItemScene == null) return;

        var (inputRates, _) = ComputeRecipeRates(recipe, _building?.Definition);
        if (_fuelList == null) return;
        foreach (var kvp in inputRates)
        {
            var item = _resourceRateItemScene.Instantiate<ResourceRateItem>();
            if (item == null) continue;
            _fuelList.AddChild(item);
            item.SetResourceRate(kvp.Key, -kvp.Value);
        }
    }

    private void UpdateGridList(PowerGrid? grid)
    {
        ClearChildren(_producersList);
        ClearChildren(_consumersList);
        ClearChildren(_batteriesList);

        if (_producersHeader != null)
            _producersHeader.Text = grid != null
                ? $"PRODUCERS · {grid.LastGeneration:F0} kW"
                : "PRODUCERS";
        if (_consumersHeader != null)
            _consumersHeader.Text = grid != null
                ? $"CONSUMERS · {grid.LastDraw:F0} kW"
                : "CONSUMERS";
        if (_batteriesHeader != null)
            _batteriesHeader.Text = grid != null
                ? $"BATTERIES · {grid.BatteryStored:F0} / {grid.BatteryCapacity:F0} kWh"
                : "BATTERIES";

        if (grid == null) return;

        foreach (var b in grid.Contributors)
        {
            var prod = b.GetBehavior<PowerProducerBehavior>();
            var bat = b.GetBehavior<BatteryBehavior>();
            if (prod != null && prod.Output > 0f)
            {
                AddGridRow(_producersList, b, $"{prod.Output:F0} kW", prod.IsProducing);
            }
            if (bat != null && bat.Capacity > 0f)
            {
                AddGridRow(_batteriesList, b, $"{bat.Stored:F0}/{bat.Capacity:F0} kWh", bat.Stored > 0f);
            }
        }
        foreach (var b in grid.Consumers)
        {
            var cons = b.GetBehavior<PowerConsumerBehavior>();
            float draw = cons?.GetCurrentDraw() ?? 0f;
            AddGridRow(_consumersList, b, $"{draw:F0} kW", draw > 0f);
        }
    }

    private void AddGridRow(VBoxContainer? list, Building b, string value, bool active)
    {
        if (list == null) return;
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);

        var dot = new ColorRect
        {
            CustomMinimumSize = new Vector2(8, 8),
            Color = active ? StateDotRun : StateDotIdle,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        row.AddChild(dot);

        var name = new Label { Text = b.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (ReferenceEquals(b, _building))
        {
            name.Modulate = new Color(1f, 0.85f, 0.4f);
            name.Text = $"› {b.Name}";
        }
        row.AddChild(name);

        var val = new Label { Text = value };
        row.AddChild(val);

        list.AddChild(row);
    }

    private PowerGrid? FindGridForBuilding(Building building)
    {
        // Consumers carry a direct grid reference.
        var consumer = building.GetBehavior<PowerConsumerBehavior>();
        if (consumer?.Grid != null) return consumer.Grid;

        // Producers and batteries: scan registered BodyPowerGridManager instances.
        var tree = GetTree();
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

    private static void ClearChildren(Node? container)
    {
        if (container == null) return;
        foreach (var child in container.GetChildren()) child.QueueFree();
    }
}
