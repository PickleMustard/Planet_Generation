using System.Collections.Generic;
using Godot;
using Logistics.Resources;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace UI.Construction;

/// <summary>
/// The docked composite construction menu. Replaces the old center-screen popup.
/// The static shell — panel, title, and TabContainer — lives in
/// <c>ConstructionMenu.tscn</c>; this script only populates it: one tab per
/// constructable type, a left rail of developer-defined category buttons per
/// tab, and a scrollable list of <see cref="ConstructionCard"/>s on the right
/// (all data-driven, so they stay in code). Toggling visibility is pure (no HSM
/// transition); clicking a card emits <see cref="CardActivated"/> and closes the
/// menu, leaving any other open window untouched.
/// </summary>
public partial class ConstructionMenuController : PanelContainer
{
    [Signal]
    public delegate void CardActivatedEventHandler(string itemType, string definitionName);

    private sealed class TabView
    {
        public string Kind = "";            // "Buildings" or "Stations"
        public VBoxContainer CardList = null!;
        public string CurrentCategoryKey = "";
    }

    [Export]
    private TabContainer _tabContainer = null!;

    private static readonly PackedScene TabScene =
        GD.Load<PackedScene>("res://UI/Construction/ConstructionTab.tscn");

    private readonly List<TabView> _tabViews = new();

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        _tabContainer.TabChanged += OnTabChanged;
        BuildTabs();
        Visible = false;

        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.BuildingConstructed += OnPlacementCountChanged;
            SignalBus.Instance.BuildingRemoved += OnPlacementCountChanged;
            SignalBus.Instance.DatabaseReloaded += OnDatabaseReloaded;
        }
    }

    public override void _ExitTree()
    {
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.BuildingConstructed -= OnPlacementCountChanged;
            SignalBus.Instance.BuildingRemoved -= OnPlacementCountChanged;
            SignalBus.Instance.DatabaseReloaded -= OnDatabaseReloaded;
        }
        base._ExitTree();
    }

    // ───────── Open / Close ─────────

    public void Open()
    {
        Visible = true;
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        ResetToDefault();
    }

    public void Close()
    {
        Visible = false;
        // Leave the cursor visible if another window is still managing it;
        // otherwise hand control back to the captured HUD camera.
        bool windowOpen = OrbitalBodyWindow.Instance?.IsOpen == true;
        Input.SetMouseMode(windowOpen
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured);
    }

    public void Toggle()
    {
        if (Visible)
            Close();
        else
            Open();
    }

    /// <summary>Selects the first tab and its first category.</summary>
    private void ResetToDefault()
    {
        if (_tabViews.Count == 0)
            return;
        _tabContainer.CurrentTab = 0;
        SelectFirstCategory(0);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible)
            return;

        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // ───────── Tab construction ─────────

    private void BuildTabs()
    {
        // Clear any previous tabs (DatabaseReloaded rebuild).
        foreach (var child in _tabContainer.GetChildren())
            child.QueueFree();
        _tabViews.Clear();

        var tabs = ConstructionCategoryLoader.Load();
        if (tabs.Count == 0)
        {
            GameLogger.Warning("ConstructionMenuController: no category tabs configured");
            return;
        }

        foreach (var tab in tabs)
            _tabViews.Add(BuildTab(tab));
    }

    private TabView BuildTab(ConstructionCategoryLoader.CategoryTab tab)
    {
        // Instance the static tab skeleton; its node name becomes the tab title.
        var content = TabScene.Instantiate<HBoxContainer>();
        content.Name = tab.Name;
        _tabContainer.AddChild(content);

        var rail = content.GetNode<VBoxContainer>("Rail");
        var view = new TabView
        {
            Kind = tab.Name,
            CardList = content.GetNode<VBoxContainer>("Scroll/CardList"),
        };

        // Category buttons are developer-defined and data-driven, so built here.
        foreach (var category in tab.Categories)
        {
            var entry = category; // capture
            var button = new Button
            {
                Text = category.Label,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ToggleMode = true,
            };
            button.Pressed += () => OnCategorySelected(view, entry.Key, button, rail);
            rail.AddChild(button);
        }

        return view;
    }

    // ───────── Population ─────────

    private void OnCategorySelected(TabView view, string categoryKey, Button source, VBoxContainer rail)
    {
        view.CurrentCategoryKey = categoryKey;

        // Keep only the active category button pressed.
        foreach (var child in rail.GetChildren())
            if (child is Button b)
                b.ButtonPressed = b == source;

        PopulateCards(view);
    }

    private void PopulateCards(TabView view)
    {
        foreach (var child in view.CardList.GetChildren())
            child.QueueFree();

        if (string.IsNullOrEmpty(view.CurrentCategoryKey))
            return;

        if (view.Kind == "Stations")
        {
            foreach (var def in StationDatabase.Instance.GetStationsByCategory(view.CurrentCategoryKey))
            {
                var card = ConstructionCard.CreateForStation(def);
                card.Clicked += OnCardClicked;
                view.CardList.AddChild(card);
            }
        }
        else // Buildings (default)
        {
            foreach (var def in BuildingDatabase.Instance.GetBuildingsByCategory(view.CurrentCategoryKey))
            {
                var card = ConstructionCard.CreateForBuilding(def);
                card.Clicked += OnCardClicked;
                view.CardList.AddChild(card);
            }
        }
    }

    private void SelectFirstCategory(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabViews.Count)
            return;

        var content = _tabContainer.GetTabControl(tabIndex);
        if (content == null)
            return;

        // Rail is the first child (VBoxContainer of category buttons).
        if (content.GetChildCount() > 0 && content.GetChild(0) is VBoxContainer rail)
        {
            foreach (var child in rail.GetChildren())
            {
                if (child is Button b)
                {
                    OnCategorySelected(_tabViews[tabIndex], FindCategoryKey(tabIndex, b), b, rail);
                    return;
                }
            }
        }
    }

    private static string FindCategoryKey(int tabIndex, Button button)
    {
        var tabs = ConstructionCategoryLoader.Load();
        if (tabIndex < 0 || tabIndex >= tabs.Count)
            return "";
        foreach (var cat in tabs[tabIndex].Categories)
            if (cat.Label == button.Text)
                return cat.Key;
        return "";
    }

    private void OnCardClicked(string itemType, string definitionName)
    {
        EmitSignal(SignalName.CardActivated, itemType, definitionName);
        Close();
    }

    // ───────── Signal handlers ─────────

    private void OnTabChanged(long tab) => SelectFirstCategory((int)tab);

    private void OnPlacementCountChanged(int _)
    {
        // Refresh the visible building tab so cards at their limit gray out live.
        if (!Visible)
            return;
        int idx = _tabContainer.CurrentTab;
        if (idx >= 0 && idx < _tabViews.Count && _tabViews[idx].Kind != "Stations")
            PopulateCards(_tabViews[idx]);
    }

    private void OnDatabaseReloaded(string databaseName)
    {
        if (databaseName is "BuildingDatabase" or "StationDatabase")
        {
            BuildTabs();
            if (Visible)
                ResetToDefault();
        }
    }
}
