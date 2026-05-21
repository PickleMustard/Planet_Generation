using System.Collections.Generic;
using Constructables;
using Constructables.Stations;
using Godot;
using UtilityLibrary;

namespace UI.StationWindow;

/// <summary>
/// Behavior-driven tabbed panel. Builds one tab per attached <see cref="IStationBehavior"/>,
/// sorted by <see cref="IStationBehavior.Priority"/> descending. The first tab — the
/// signature behavior — additionally exposes the station's bulk storage; the remaining
/// tabs focus solely on their behavior. Mirrors the dynamic tab pattern in
/// <see cref="UI.BuildingInfo.Administration.AdministrationTabbedPanel"/>.
/// </summary>
public partial class StationTabbedPanel : PanelContainer
{
    [Signal]
    public delegate void ItemSelectedEventHandler(string itemType, int itemIndex);

    [Export]
    private PackedScene? _behaviorTabScene;

    private sealed class TabSpec
    {
        public string Label = "";
        public IStationBehavior? Behavior;
        public bool ShowStorage;
        public StationBehaviorTab? Cached;
    }

    private StationSatellite? _station;
    private HBoxContainer? _tabBar;
    private VBoxContainer? _tabBodyContainer;
    private int _activeIndex = -1;
    private readonly List<TabSpec> _tabs = new();
    private readonly List<Button> _tabButtons = new();

    private StyleBox? _activeTabStyle;
    private StyleBox? _inactiveTabStyle;

    public override void _Ready()
    {
        _tabBar = GetNode<HBoxContainer>("VBoxContainer/TabBar");
        _tabBodyContainer = GetNode<VBoxContainer>("VBoxContainer/TabBodyContainer");
        BuildTabStyles();
    }

    public void Initialize(StationSatellite station)
    {
        Clear();
        _station = station;

        BuildTabSpecsFromBehaviors();
        RebuildTabBar();

        if (_tabs.Count > 0)
        {
            _activeIndex = -1;
            SwitchTab(0);
        }
    }

    public void Clear()
    {
        DropCachedTabs();
        ClearTabButtons();
        _activeIndex = -1;
        _station = null;

        if (_tabBodyContainer != null)
        {
            foreach (var child in _tabBodyContainer.GetChildren())
                if (child is Node node && node.GetParent() == _tabBodyContainer)
                    _tabBodyContainer.RemoveChild(node);
        }
    }

    private void BuildTabSpecsFromBehaviors()
    {
        _tabs.Clear();
        if (_station == null)
            return;

        var ordered = new List<IStationBehavior>(_station.Behaviors);
        ordered.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        if (ordered.Count == 0)
        {
            _tabs.Add(new TabSpec
            {
                Label = "Overview",
                Behavior = null,
                ShowStorage = true,
            });
            return;
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            _tabs.Add(new TabSpec
            {
                Label = LabelForBehavior(ordered[i]),
                Behavior = ordered[i],
                ShowStorage = i == 0,
            });
        }
    }

    private static string LabelForBehavior(IStationBehavior behavior) => behavior switch
    {
        Constructables.Stations.Behaviors.ShipyardBehavior => "Shipyard",
        Constructables.Stations.Behaviors.OrbitalConstructorBehavior => "Architect",
        Constructables.Stations.Behaviors.TransferHubBehavior => "Transfers",
        Constructables.Stations.Behaviors.StorageHubBehavior => "Storage",
        _ => behavior.GetType().Name.Replace("Behavior", ""),
    };

    private void RebuildTabBar()
    {
        ClearTabButtons();
        if (_tabBar == null)
            return;

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
            _tabBar.AddChild(btn);
            _tabButtons.Add(btn);
        }
    }

    public void SwitchTab(int idx)
    {
        if (idx < 0 || idx >= _tabs.Count || _station == null || _tabBodyContainer == null)
            return;
        if (idx == _activeIndex)
            return;

        // Detach previously active instance from the body container without freeing.
        if (_activeIndex >= 0 && _activeIndex < _tabs.Count)
        {
            var prev = _tabs[_activeIndex].Cached;
            if (prev != null && IsInstanceValid(prev) && prev.GetParent() != null)
                prev.GetParent().RemoveChild(prev);
        }

        _activeIndex = idx;
        UpdateTabButtonStyles();

        var spec = _tabs[idx];
        if (spec.Cached == null)
        {
            if (_behaviorTabScene == null)
            {
                GameLogger.Warning("StationTabbedPanel: no BehaviorTab scene configured");
                return;
            }
            spec.Cached = _behaviorTabScene.Instantiate<StationBehaviorTab>();
            spec.Cached.ItemSelected += OnNestedItemSelected;
        }

        spec.Cached.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        spec.Cached.SizeFlagsVertical = SizeFlags.ExpandFill;
        _tabBodyContainer.AddChild(spec.Cached);
        spec.Cached.Initialize(_station, spec.Behavior, spec.ShowStorage);
    }

    private void OnNestedItemSelected(string itemType, int itemIndex)
        => EmitSignal(SignalName.ItemSelected, itemType, itemIndex);

    private void DropCachedTabs()
    {
        foreach (var spec in _tabs)
        {
            if (spec.Cached != null && IsInstanceValid(spec.Cached))
            {
                spec.Cached.ItemSelected -= OnNestedItemSelected;
                if (spec.Cached.GetParent() != null)
                    spec.Cached.GetParent().RemoveChild(spec.Cached);
                spec.Cached.QueueFree();
            }
            spec.Cached = null;
        }
        _tabs.Clear();
    }

    private void ClearTabButtons()
    {
        if (_tabBar != null)
        {
            foreach (var btn in _tabButtons)
                if (IsInstanceValid(btn))
                    btn.QueueFree();
        }
        _tabButtons.Clear();
    }

    private void BuildTabStyles()
    {
        _activeTabStyle = new StyleBoxFlat
        {
            ContentMarginLeft = 8,
            ContentMarginTop = 4,
            ContentMarginRight = 8,
            ContentMarginBottom = 4,
            BgColor = new Color(0.25f, 0.25f, 0.3f, 1f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
        };
        _inactiveTabStyle = new StyleBoxFlat
        {
            ContentMarginLeft = 8,
            ContentMarginTop = 4,
            ContentMarginRight = 8,
            ContentMarginBottom = 4,
            BgColor = new Color(0.12f, 0.12f, 0.14f, 0.8f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
        };
    }

    private void UpdateTabButtonStyles()
    {
        if (_activeTabStyle == null || _inactiveTabStyle == null)
            return;
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            _tabButtons[i].AddThemeStyleboxOverride(
                "normal",
                i == _activeIndex ? _activeTabStyle : _inactiveTabStyle
            );
        }
    }
}
