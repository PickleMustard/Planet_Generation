using System.Collections.Generic;
using Constructables;
using Constructables.Buildings.Behaviors;
using Constructables.Power;
using Godot;
using Structures.Resources;
using UI.GridWindow;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Bespoke panel for power-generation buildings. Stat strip across the top
/// (State / Output / Fuel / Grid). Three-column body: render + connections,
/// recipe + fuel bus + cycle indicator, grid header + producers/consumers/batteries list.
/// </summary>
public partial class PowerPanelDetails : BaseBuildingDetails
{
    private TextureRect? _renderIcon;

    // Stat strip
    private PanelContainer? _statFuelTile;
    private Label? _stateValueLabel;
    private ColorRect? _stateDot;
    private Label? _outputValueLabel;
    private Label? _fuelValueLabel;
    private Label? _gridValueLabel;

    // Cycle strip (center column)
    private Label? _outputBigLabel;
    private ProgressBar? _cycleBar;
    private ProgressBar? _fuelBar;
    private Label? _cycleFooterStateLabel;
    private Label? _cycleValueLabel;

    // Recipe / fuel bus
    private Label? _recipeHeader;
    private RecipeDisplay? _recipeDisplay;
    private ScrollContainer? _fuelScroll;
    private VBoxContainer? _fuelList;

    // Grid header (right column)
    private Label? _gridTitleLabel;
    private Button? _gridDetailsBtn;
    private Label? _genValueLabel;
    private Label? _drawValueLabel;
    private Label? _batteryValueLabel;
    private ProgressBar? _balanceBar;
    private Label? _balanceValueLabel;

    // Grid sections
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

    private static readonly Color BalanceColorOk = new(0.29f, 0.65f, 0.32f);
    private static readonly Color BalanceColorDeficit = new(0.78f, 0.32f, 0.21f);
    private static readonly Color BalanceColorNeutral = new(0.353f, 0.314f, 0.282f);

    private int _refreshCounter;
    public override void _PhysicsProcess(double delta)
    {
        _refreshCounter++;
        if (_refreshCounter >= 10) { _refreshCounter = 0; UpdateDisplay(); }
    }

    public override void _Ready()
    {
        _renderIcon = GetNodeOrNull<TextureRect>("Layout/Body/LeftColumn/RenderTile/RenderIcon");

        _stateValueLabel = GetNodeOrNull<Label>("Layout/StatStrip/StateTile/VBox/Row/StateLabel");
        _stateDot = GetNodeOrNull<ColorRect>("Layout/StatStrip/StateTile/VBox/Row/StateDot");
        _outputValueLabel = GetNodeOrNull<Label>("Layout/StatStrip/OutputTile/VBox/OutputValue");
        _statFuelTile = GetNodeOrNull<PanelContainer>("Layout/StatStrip/FuelTile");
        _fuelValueLabel = GetNodeOrNull<Label>("Layout/StatStrip/FuelTile/VBox/FuelValue");
        _gridValueLabel = GetNodeOrNull<Label>("Layout/StatStrip/GridTile/VBox/GridValue");

        _outputBigLabel = GetNodeOrNull<Label>("Layout/Body/CenterColumn/CycleStrip/CycleHBox/CycleDetail/OutputBig");
        _cycleBar = GetNodeOrNull<ProgressBar>("Layout/Body/CenterColumn/CycleStrip/CycleHBox/CycleDetail/CycleBar");
        _fuelBar = GetNodeOrNull<ProgressBar>("Layout/Body/CenterColumn/CycleStrip/CycleHBox/CycleDetail/FuelBar");
        _cycleFooterStateLabel = GetNodeOrNull<Label>("Layout/Body/CenterColumn/CycleStrip/CycleHBox/CycleDetail/CycleFooter/CycleFooterState");
        _cycleValueLabel = GetNodeOrNull<Label>("Layout/Body/CenterColumn/CycleStrip/CycleHBox/CycleDetail/CycleFooter/CycleLabel");

        _recipeHeader = GetNodeOrNull<Label>("Layout/Body/CenterColumn/RecipeHeader");
        _recipeDisplay = GetNodeOrNull<RecipeDisplay>("Layout/Body/CenterColumn/RecipeDisplay");
        _fuelScroll = GetNodeOrNull<ScrollContainer>("Layout/Body/CenterColumn/FuelScroll");
        _fuelList = GetNodeOrNull<VBoxContainer>("Layout/Body/CenterColumn/FuelScroll/FuelList");

        _gridTitleLabel = GetNodeOrNull<Label>("Layout/Body/RightColumn/GridHeaderBox/GridTitleRow/GridTitle");
        _gridDetailsBtn = GetNodeOrNull<Button>("Layout/Body/RightColumn/GridHeaderBox/GridTitleRow/GridDetailsBtn");
        if (_gridDetailsBtn != null)
            _gridDetailsBtn.Pressed += OnGridDetailsPressed;
        _genValueLabel = GetNodeOrNull<Label>("Layout/Body/RightColumn/GridHeaderBox/GridStatsRow/GenStat/GenValue");
        _drawValueLabel = GetNodeOrNull<Label>("Layout/Body/RightColumn/GridHeaderBox/GridStatsRow/DrawStat/DrawValue");
        _batteryValueLabel = GetNodeOrNull<Label>("Layout/Body/RightColumn/GridHeaderBox/GridStatsRow/BatteryStat/BatteryValue");
        _balanceBar = GetNodeOrNull<ProgressBar>("Layout/Body/RightColumn/GridHeaderBox/BalanceRow/BalanceBar");
        _balanceValueLabel = GetNodeOrNull<Label>("Layout/Body/RightColumn/GridHeaderBox/BalanceRow/BalanceValue");

        _producersHeader = GetNodeOrNull<Label>("Layout/Body/RightColumn/SectionsScroll/Sections/Producers/Header");
        _consumersHeader = GetNodeOrNull<Label>("Layout/Body/RightColumn/SectionsScroll/Sections/Consumers/Header");
        _batteriesHeader = GetNodeOrNull<Label>("Layout/Body/RightColumn/SectionsScroll/Sections/Batteries/Header");
        _producersList = GetNodeOrNull<VBoxContainer>("Layout/Body/RightColumn/SectionsScroll/Sections/Producers/List");
        _consumersList = GetNodeOrNull<VBoxContainer>("Layout/Body/RightColumn/SectionsScroll/Sections/Consumers/List");
        _batteriesList = GetNodeOrNull<VBoxContainer>("Layout/Body/RightColumn/SectionsScroll/Sections/Batteries/List");

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

        bool hasFuelCycle = recipe != null && HasFuelInputs(recipe);
        ApplyFuelCycleVisibility(hasFuelCycle);

        UpdateStatStrip(producer, battery, consumer, grid, recipe, hasFuelCycle);
        UpdateCycleStrip(producer, battery, grid, recipe, hasFuelCycle);
        UpdateFuelPane(recipe);
        UpdateGridHeader(grid);
        UpdateGridList(grid);
    }

    private bool HasFuelInputs(RecipeDefinition recipe)
    {
        foreach (var kvp in recipe.InputResources)
            if (kvp.Key != "power" && kvp.Value > 0f) return true;
        return false;
    }

    private void ApplyFuelCycleVisibility(bool show)
    {
        if (_statFuelTile != null) _statFuelTile.Visible = show;
        if (_recipeHeader != null) _recipeHeader.Visible = show;
        if (_recipeDisplay != null) _recipeDisplay.Visible = show;
        if (_fuelScroll != null) _fuelScroll.Visible = show;
        if (_cycleBar != null) _cycleBar.Visible = show;
        if (_fuelBar != null) _fuelBar.Visible = show;
        if (_cycleValueLabel != null) _cycleValueLabel.Visible = show;
    }

    public override void Clear()
    {
        base.Clear();
        if (_renderIcon != null) _renderIcon.Texture = null;
        if (_stateValueLabel != null) _stateValueLabel.Text = "—";
        if (_stateDot != null) _stateDot.Color = StateDotIdle;
        if (_outputValueLabel != null) _outputValueLabel.Text = "—";
        if (_fuelValueLabel != null) _fuelValueLabel.Text = "—";
        if (_gridValueLabel != null) _gridValueLabel.Text = "—";

        if (_outputBigLabel != null) _outputBigLabel.Text = "—";
        if (_cycleBar != null) _cycleBar.Value = 0;
        if (_fuelBar != null) _fuelBar.Value = 0;
        if (_cycleFooterStateLabel != null) _cycleFooterStateLabel.Text = "—";
        if (_cycleValueLabel != null) _cycleValueLabel.Text = "cycle · 0%";

        if (_gridTitleLabel != null) _gridTitleLabel.Text = "GRID";
        if (_genValueLabel != null) _genValueLabel.Text = "—";
        if (_drawValueLabel != null) _drawValueLabel.Text = "—";
        if (_batteryValueLabel != null) _batteryValueLabel.Text = "—";
        if (_balanceBar != null) _balanceBar.Value = 0;
        if (_balanceValueLabel != null) { _balanceValueLabel.Text = "—"; _balanceValueLabel.Modulate = Colors.White; }

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
        Texture2D? tex = iconDef?.Texture;
        if (tex == null && !string.IsNullOrEmpty(iconDef?.BasePath))
        {
            try { tex = ResourceLoader.Load<Texture2D>(iconDef.BasePath + ".png"); }
            catch { tex = null; }
        }
        _renderIcon.Texture = tex;
    }

    private void UpdateStatStrip(PowerProducerBehavior? producer, BatteryBehavior? battery,
        PowerConsumerBehavior? consumer, PowerGrid? grid, RecipeDefinition? recipe, bool hasFuelCycle)
    {
        bool producing = producer?.IsProducing ?? false;
        bool brownedOut = grid?.IsBrownedOut ?? false;
        (string label, Color dot) = brownedOut ? ("Brownout", StateDotBlock)
            : producing ? ("Generating", StateDotRun)
            : (battery != null && battery.Stored > 0f) ? ("Discharging", StateDotFull)
            : ("Idle", StateDotIdle);
        if (_stateValueLabel != null) _stateValueLabel.Text = label;
        if (_stateDot != null) _stateDot.Color = dot;

        if (_outputValueLabel != null)
        {
            float output = producer?.Output ?? 0f;
            _outputValueLabel.Text = output > 0f ? $"{output:F0} kW" : "—";
        }

        if (hasFuelCycle && _fuelValueLabel != null)
        {
            float fuel = ComputeFuelLevel(recipe);
            _fuelValueLabel.Text = $"{Mathf.RoundToInt(fuel * 100)}%";
        }

        if (_gridValueLabel != null)
        {
            if (grid == null) _gridValueLabel.Text = "no grid";
            else _gridValueLabel.Text = brownedOut ? "DEFICIT" : "OK";
        }
    }

    private void UpdateCycleStrip(PowerProducerBehavior? producer, BatteryBehavior? battery,
        PowerGrid? grid, RecipeDefinition? recipe, bool hasFuelCycle)
    {
        if (hasFuelCycle && producer != null)
        {
            float remainingFrac = producer.WorkRequired > 0f
                ? Mathf.Clamp(producer.WorkRemaining / producer.WorkRequired, 0f, 1f) : 1f;
            float progressPct = 1f - remainingFrac;

            if (_cycleBar != null) { _cycleBar.MinValue = 0; _cycleBar.MaxValue = 1; _cycleBar.Value = remainingFrac; }
            if (_fuelBar != null) { _fuelBar.MinValue = 0; _fuelBar.MaxValue = 1; _fuelBar.Value = ComputeFuelLevel(recipe); }
            if (_cycleValueLabel != null) _cycleValueLabel.Text = $"cycle · {Mathf.RoundToInt(progressPct * 100)}%";
        }

        if (_outputBigLabel != null)
        {
            float output = producer?.Output ?? 0f;
            _outputBigLabel.Text = output > 0f ? $"+{output:F0} kW" : "—";
        }

        if (_cycleFooterStateLabel != null)
        {
            bool producing = producer?.IsProducing ?? false;
            bool brownedOut = grid?.IsBrownedOut ?? false;
            _cycleFooterStateLabel.Text = brownedOut ? "stalled · brownout"
                : producing ? (hasFuelCycle ? "generating" : "generating · continuous")
                : (battery != null && battery.Stored > 0f) ? "discharging"
                : "idle";
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

        var (inputRates, _) = ComputeRecipeRates(recipe, _building?.GetBehavior<PowerProducerBehavior>());
        if (_fuelList == null) return;
        foreach (var kvp in inputRates)
        {
            var item = _resourceRateItemScene.Instantiate<ResourceRateItem>();
            if (item == null) continue;
            _fuelList.AddChild(item);
            item.SetResourceRate(kvp.Key, -kvp.Value);
        }
    }

    private void UpdateGridHeader(PowerGrid? grid)
    {
        if (grid == null)
        {
            if (_gridTitleLabel != null) _gridTitleLabel.Text = "GRID · (none)";
            if (_genValueLabel != null) _genValueLabel.Text = "—";
            if (_drawValueLabel != null) _drawValueLabel.Text = "—";
            if (_batteryValueLabel != null) _batteryValueLabel.Text = "—";
            if (_balanceBar != null) _balanceBar.Value = 0;
            if (_balanceValueLabel != null) { _balanceValueLabel.Text = "—"; _balanceValueLabel.Modulate = Colors.White; }
            return;
        }

        if (_gridTitleLabel != null) _gridTitleLabel.Text = $"GRID · {grid.Id}";
        if (_genValueLabel != null) _genValueLabel.Text = $"{grid.LastGeneration:F0} kW";
        if (_drawValueLabel != null) _drawValueLabel.Text = $"{grid.LastDraw:F0} kW";
        if (_batteryValueLabel != null) _batteryValueLabel.Text = grid.BatteryCapacity > 0f
            ? $"{grid.BatteryStored:F0} / {grid.BatteryCapacity:F0}"
            : "—";

        if (_balanceBar != null)
        {
            _balanceBar.MinValue = 0; _balanceBar.MaxValue = 1;
            _balanceBar.Value = grid.BatteryCapacity > 0f
                ? Mathf.Clamp(grid.BatteryStored / grid.BatteryCapacity, 0f, 1f)
                : 0f;
        }

        if (_balanceValueLabel != null)
        {
            float balance = grid.LastGeneration - grid.LastDraw;
            string sign = balance >= 0f ? "+" : "";
            _balanceValueLabel.Text = $"{sign}{balance:F0} kW";
            _balanceValueLabel.Modulate = grid.IsBrownedOut ? BalanceColorDeficit
                : (balance >= 0f ? BalanceColorOk : BalanceColorDeficit);
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
            if (prod != null)
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

    private void OnGridDetailsPressed()
    {
        if (_building == null) return;
        var grid = FindGridForBuilding(_building);
        if (grid != null)
            GridDetailWindow.Instance?.ShowGrid(grid);
    }

    private PowerGrid? FindGridForBuilding(Building building)
    {
        // Consumers carry a direct grid reference.
        var consumer = building.GetBehavior<PowerConsumerBehavior>();
        if (consumer?.Grid != null) return consumer.Grid;

        // Producers: resolve via BodyPowerGridManager.GetGridForBuilding.
        var producer = building.GetBehavior<PowerProducerBehavior>();
        if (producer != null)
        {
            var tree = GetTree();
            if (tree != null)
            {
                foreach (var node in tree.Root.GetChildren())
                    if (TryFindGrid(node, building, out var g)) return g;
            }
            return null;
        }

        // Batteries and others: scan registered BodyPowerGridManager instances.
        var tree2 = GetTree();
        if (tree2 == null) return null;
        foreach (var node in tree2.Root.GetChildren())
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
