using System.Collections.Generic;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Godot;
using UtilityLibrary;

namespace UI.BuildingInfo.Administration;

/// <summary>
/// Inheritable tabbed panel for administrative buildings. Tab 0 is the bespoke admin
/// tab (subclass-supplied via <see cref="AdminTabScene"/>); subsequent tabs are derived
/// from the building's <see cref="Building.Behaviors"/> list and reuse existing detail
/// scenes (BuildingPanelDetails, PowerPanelDetails, HubPanelDetails).
/// </summary>
public partial class AdministrationTabbedPanel : BaseBuildingDetails
{
    [Export]
    public PackedScene? ManufacturingTabScene;

    [Export]
    public PackedScene? PowerTabScene;

    [Export]
    public PackedScene? StorageTabScene;

    [Export]
    public PackedScene? ResearchTabScene;

    [Export]
    public PackedScene? AdminTabScene;

    [Export]
    public Label? BuildingNameLabel;

    [Export]
    public Label? BuildingCodeLabel;

    [Export]
    public Label? BuildingTagLabel;

    [Export]
    public HBoxContainer? StatStrip;

    [Export]
    public Label? RenderPlaceholderLabel;

    [Export]
    public VBoxContainer? BehaviorChipsContainer;

    [Export]
    public HBoxContainer? TabBar;

    [Export]
    public VBoxContainer? TabBodyContainer;

    protected virtual string AdminTabLabel => "Admin";

    protected virtual string BuildingTag => "ADMIN";

    protected virtual string RenderText => "3D · Building";

    protected enum TabKind
    {
        Admin,
        Manufacturing,
        Power,
        Storage,
        Research,
    }

    protected sealed class TabSpec
    {
        public TabKind Kind;
        public string Label = "";
        public PackedScene? Scene;
        public BaseBuildingDetails? CachedInstance;
    }

    private readonly List<TabSpec> _tabs = new();
    private int _activeIndex = -1;
    private readonly List<Button> _tabButtons = new();
    private StyleBox? _activeStyle;
    private StyleBox? _inactiveStyle;

    public override void _Ready()
    {
        if (TabBar != null && TabBar.GetChildCount() > 0)
        {
            var probe = TabBar.GetChild(0) as Button;
            if (probe != null)
            {
                _activeStyle = probe.GetThemeStylebox("normal");
                probe.QueueFree();
            }
        }
    }

    public override void SetBuilding(Building building)
    {
        _building = building;
        UpdateDisplay();
    }

    protected override void UpdateDisplay()
    {
        BuildTabsFromBehaviors();
        RebuildTabBar();
        RebuildBehaviorChips();
        UpdateHeaderLabels();
        UpdateStatStrip();
        if (_tabs.Count > 0)
        {
            _activeIndex = -1;
            SwitchTab(0);
        }
    }

    public override void Clear()
    {
        foreach (var spec in _tabs)
        {
            if (spec.CachedInstance != null && GodotObject.IsInstanceValid(spec.CachedInstance))
            {
                if (spec.CachedInstance.GetParent() != null)
                    spec.CachedInstance.GetParent().RemoveChild(spec.CachedInstance);
                spec.CachedInstance.QueueFree();
            }
            spec.CachedInstance = null;
        }
        _tabs.Clear();
        _activeIndex = -1;

        foreach (var btn in _tabButtons)
        {
            if (GodotObject.IsInstanceValid(btn))
                btn.QueueFree();
        }
        _tabButtons.Clear();

        if (TabBodyContainer != null)
        {
            foreach (var child in TabBodyContainer.GetChildren())
                child.QueueFree();
        }

        base.Clear();
    }

    private void BuildTabsFromBehaviors()
    {
        // Tear down any prior tab cache before rebuilding so the dispatcher can
        // call SetBuilding multiple times without leaks.
        foreach (var spec in _tabs)
        {
            if (spec.CachedInstance != null && GodotObject.IsInstanceValid(spec.CachedInstance))
            {
                if (spec.CachedInstance.GetParent() != null)
                    spec.CachedInstance.GetParent().RemoveChild(spec.CachedInstance);
                spec.CachedInstance.QueueFree();
            }
        }
        _tabs.Clear();

        _tabs.Add(new TabSpec { Kind = TabKind.Admin, Label = AdminTabLabel, Scene = AdminTabScene });

        if (_building == null)
            return;

        var seen = new HashSet<TabKind>();
        foreach (var behavior in _building.Behaviors)
        {
            TabKind? kind = MapBehaviorToTabKind(behavior);
            if (kind == null || !seen.Add(kind.Value))
                continue;

            var spec = BuildSpecForKind(kind.Value);
            if (spec != null)
                _tabs.Add(spec);
        }
    }

    private static TabKind? MapBehaviorToTabKind(IBuildingBehavior behavior) =>
        behavior switch
        {
            ManufacturingBehavior => TabKind.Manufacturing,
            ExtractionBehavior => TabKind.Manufacturing,
            PowerProducerBehavior => TabKind.Power,
            PowerConsumerBehavior => TabKind.Power,
            BatteryBehavior => TabKind.Power,
            StorageHubBehavior => TabKind.Storage,
            TransferStationBehavior => TabKind.Storage,
            ResearchBehavior => TabKind.Research,
            _ => null,
        };

    private TabSpec? BuildSpecForKind(TabKind kind) =>
        kind switch
        {
            TabKind.Manufacturing => new TabSpec { Kind = kind, Label = "Manufacturing", Scene = ManufacturingTabScene },
            TabKind.Power => new TabSpec { Kind = kind, Label = "Power", Scene = PowerTabScene },
            TabKind.Storage => new TabSpec { Kind = kind, Label = "Storage", Scene = StorageTabScene },
            TabKind.Research => new TabSpec { Kind = kind, Label = "Research", Scene = ResearchTabScene },
            _ => null,
        };

    private void RebuildTabBar()
    {
        if (TabBar == null)
            return;

        foreach (var btn in _tabButtons)
        {
            if (GodotObject.IsInstanceValid(btn))
                btn.QueueFree();
        }
        _tabButtons.Clear();

        for (int i = 0; i < _tabs.Count; i++)
        {
            int idx = i;
            var spec = _tabs[i];
            var btn = new Button
            {
                Text = i == 0 ? "★ " + spec.Label : spec.Label,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            btn.AddThemeFontSizeOverride("font_size", 13);
            btn.Pressed += () => SwitchTab(idx);
            TabBar.AddChild(btn);
            _tabButtons.Add(btn);
        }
    }

    private void RebuildBehaviorChips()
    {
        if (BehaviorChipsContainer == null)
            return;
        foreach (var child in BehaviorChipsContainer.GetChildren())
            child.QueueFree();

        for (int i = 0; i < _tabs.Count; i++)
        {
            var spec = _tabs[i];
            var chip = new Label
            {
                Text = (i == 0 ? "★ " : "· ") + spec.Label,
            };
            chip.AddThemeFontSizeOverride("font_size", 12);
            BehaviorChipsContainer.AddChild(chip);
        }
    }

    private void UpdateHeaderLabels()
    {
        if (BuildingNameLabel != null)
            BuildingNameLabel.Text = _building?.Name ?? "—";
        if (BuildingCodeLabel != null)
            BuildingCodeLabel.Text = _building?.Definition?.IdName ?? "";
        if (BuildingTagLabel != null)
            BuildingTagLabel.Text = BuildingTag;
        if (RenderPlaceholderLabel != null)
            RenderPlaceholderLabel.Text = RenderText;
    }

    private void UpdateStatStrip()
    {
        if (StatStrip == null)
            return;

        foreach (var child in StatStrip.GetChildren())
            child.QueueFree();

        int behaviorTabCount = _tabs.Count - 1;
        int powerCount = 0;
        bool hasManufacturing = false;
        bool hasStorage = false;
        if (_building != null)
        {
            foreach (var b in _building.Behaviors)
            {
                if (b is PowerProducerBehavior or PowerConsumerBehavior or BatteryBehavior)
                    powerCount++;
                if (b is ManufacturingBehavior or ExtractionBehavior)
                    hasManufacturing = true;
                if (b is StorageHubBehavior)
                    hasStorage = true;
            }
        }

        string recipeText = "—";
        if (hasManufacturing)
        {
            var mfg = _building?.GetBehavior<Constructables.Buildings.Behaviors.ManufacturingBehavior>();
            string? recipeId = _building?.ActiveRecipeId ?? mfg?.DefaultRecipe;
            recipeText = string.IsNullOrEmpty(recipeId) ? "—" : recipeId;
        }

        string storageText = "—";
        if (hasStorage && _building != null)
        {
            float used = GetStorageTotalUsed(_building.BulkStorage);
            float cap = GetStorageTotalCapacity(_building.BulkStorage);
            storageText = $"{Mathf.RoundToInt(used)}/{Mathf.RoundToInt(cap)}";
        }

        AddStatCell("BUILDING", _building?.Name ?? "—");
        AddStatCell("CATEGORY", _building?.Definition?.Category ?? "—");
        AddStatCell("BEHAVIORS", $"★ + {behaviorTabCount}");
        AddStatCell("POWER", powerCount > 0 ? powerCount.ToString() : "—");
        AddStatCell("RECIPE", recipeText);
        AddStatCell("STORAGE", storageText);
    }

    private void AddStatCell(string key, string value)
    {
        if (StatStrip == null)
            return;
        var cell = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var k = new Label { Text = key };
        k.AddThemeFontSizeOverride("font_size", 9);
        k.Modulate = new Color(0.7f, 0.7f, 0.75f);
        var v = new Label { Text = value };
        v.AddThemeFontSizeOverride("font_size", 13);
        cell.AddChild(k);
        cell.AddChild(v);
        StatStrip.AddChild(cell);
    }

    protected void SwitchTab(int idx)
    {
        if (idx == _activeIndex || idx < 0 || idx >= _tabs.Count)
            return;
        if (TabBodyContainer == null)
            return;

        // Detach the previously active instance without freeing it so the
        // cache survives tab switches.
        if (_activeIndex >= 0 && _activeIndex < _tabs.Count)
        {
            var prev = _tabs[_activeIndex].CachedInstance;
            if (prev != null && GodotObject.IsInstanceValid(prev) && prev.GetParent() != null)
                prev.GetParent().RemoveChild(prev);
        }

        _activeIndex = idx;
        UpdateTabButtonStyles();

        var spec = _tabs[idx];
        if (spec.CachedInstance == null)
        {
            if (spec.Scene == null)
            {
                GameLogger.Warning(
                    $"AdministrationTabbedPanel: tab '{spec.Label}' has no scene configured."
                );
                return;
            }
            spec.CachedInstance = spec.Scene.Instantiate<BaseBuildingDetails>();
            if (spec.CachedInstance == null)
                return;

            if (spec.CachedInstance is HubPanelDetails hub)
                hub.ManageRoutesRequested += OnNestedManageRoutesRequested;
        }

        spec.CachedInstance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        spec.CachedInstance.SizeFlagsVertical = SizeFlags.ExpandFill;
        TabBodyContainer.AddChild(spec.CachedInstance);
        if (_building != null)
            spec.CachedInstance.SetBuilding(_building);
    }

    private static void OnNestedManageRoutesRequested()
    {
        BuildingInfoWindow.Instance?.EmitSignal(BuildingInfoWindow.SignalName.ManageRoutesRequested);
    }

    private void UpdateTabButtonStyles()
    {
        if (_activeStyle == null)
            return;
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            if (i == _activeIndex)
                _tabButtons[i].AddThemeStyleboxOverride("normal", _activeStyle);
            else
                _tabButtons[i].RemoveThemeStyleboxOverride("normal");
        }
    }
}
