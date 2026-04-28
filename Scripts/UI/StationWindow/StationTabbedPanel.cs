using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.GameState;

namespace UI.StationWindow;

/// <summary>
/// Right-side tabbed panel with conditionally-visible tabs based on station type.
/// Switching tabs populates the content area; clicking items emits ItemSelected.
/// </summary>
public partial class StationTabbedPanel : PanelContainer
{
    [Signal]
    public delegate void ItemSelectedEventHandler(string itemType, int itemIndex);

    private enum StationTab
    {
        Overview = 0,
        Inventory = 1,
        Buildings = 2,
        Manufacturing = 3,
        ConstructionQueue = 4,
    }

    private struct TabEntry
    {
        public StationTab Tab;
        public Button Button;
    }

    private StationSatellite? _station;
    private int _activeTabIndex = -1;
    private readonly List<TabEntry> _visibleTabs = new();
    private HBoxContainer? _tabBar;
    private VBoxContainer? _contentContainer;

    private StyleBox? _activeTabStyle;
    private StyleBox? _inactiveTabStyle;

    public override void _Ready()
    {
        _tabBar = GetNode<HBoxContainer>("VBoxContainer/TabBar");
        _contentContainer = GetNode<VBoxContainer>(
            "VBoxContainer/TabContent/ContentContainer"
        );
    }

    public void Initialize(StationSatellite station)
    {
        _station = station;
        RebuildTabs();
        if (_visibleTabs.Count > 0)
            SwitchTab(0);
    }

    public void Clear()
    {
        _station = null;
        ClearContent();
        ClearTabs();
        _activeTabIndex = -1;
    }

    // ───────── Tab Management ─────────

    private void RebuildTabs()
    {
        ClearTabs();

        if (_station == null || _tabBar == null)
            return;

        // Determine which tabs to show
        var allTabs = new[]
        {
            StationTab.Overview,
            StationTab.Inventory,
            StationTab.Buildings,
            StationTab.Manufacturing,
            StationTab.ConstructionQueue,
        };

        bool isFirst = true;
        foreach (var tab in allTabs)
        {
            if (!ShouldShowTab(tab, _station))
                continue;

            var button = new Button
            {
                Text = GetTabName(tab),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            button.AddThemeFontSizeOverride("font_size", 13);

            // Cache styles from first two buttons
            if (isFirst)
            {
                var activeStyle = new StyleBoxFlat
                {
                    ContentMarginLeft = 8,
                    ContentMarginTop = 4,
                    ContentMarginRight = 8,
                    ContentMarginBottom = 4,
                    BgColor = new Color(0.25f, 0.25f, 0.3f, 1f),
                    CornerRadiusTopLeft = 4,
                    CornerRadiusTopRight = 4,
                };
                var inactiveStyle = new StyleBoxFlat
                {
                    ContentMarginLeft = 8,
                    ContentMarginTop = 4,
                    ContentMarginRight = 8,
                    ContentMarginBottom = 4,
                    BgColor = new Color(0.12f, 0.12f, 0.14f, 0.8f),
                    CornerRadiusTopLeft = 4,
                    CornerRadiusTopRight = 4,
                };
                _activeTabStyle = activeStyle;
                _inactiveTabStyle = inactiveStyle;
                isFirst = false;
            }

            int visibleIndex = _visibleTabs.Count;
            button.Pressed += () => SwitchTab(visibleIndex);

            _tabBar.AddChild(button);
            _visibleTabs.Add(new TabEntry { Tab = tab, Button = button });
        }
    }

    private void ClearTabs()
    {
        if (_tabBar != null)
        {
            foreach (var child in _tabBar.GetChildren())
                child.QueueFree();
        }
        _visibleTabs.Clear();
    }

    private static bool ShouldShowTab(StationTab tab, StationSatellite station)
    {
        return tab switch
        {
            StationTab.Overview => true,
            StationTab.Inventory => station.Economy != null,
            StationTab.Buildings => station is OrbitalArchitectStation,
            StationTab.Manufacturing => station.StationType == "Refinery",
            StationTab.ConstructionQueue => station is ConstructionYardStation,
            _ => false,
        };
    }

    private static string GetTabName(StationTab tab)
    {
        return tab switch
        {
            StationTab.Overview => "Overview",
            StationTab.Inventory => "Inventory",
            StationTab.Buildings => "Buildings",
            StationTab.Manufacturing => "Manufacturing",
            StationTab.ConstructionQueue => "Build Queue",
            _ => tab.ToString(),
        };
    }

    public void SwitchTab(int visibleTabIndex)
    {
        if (visibleTabIndex < 0 || visibleTabIndex >= _visibleTabs.Count)
            return;

        if (visibleTabIndex == _activeTabIndex)
            return;

        _activeTabIndex = visibleTabIndex;
        UpdateTabButtonStyles();
        ClearContent();

        if (_station == null || _contentContainer == null)
            return;

        var tab = _visibleTabs[visibleTabIndex].Tab;
        switch (tab)
        {
            case StationTab.Overview:
                PopulateOverview();
                break;
            case StationTab.Inventory:
                PopulateInventory();
                break;
            case StationTab.Buildings:
                PopulateBuildings();
                break;
            case StationTab.Manufacturing:
                PopulateManufacturing();
                break;
            case StationTab.ConstructionQueue:
                PopulateConstructionQueue();
                break;
        }
    }

    private void UpdateTabButtonStyles()
    {
        if (_activeTabStyle == null || _inactiveTabStyle == null)
            return;

        for (int i = 0; i < _visibleTabs.Count; i++)
        {
            _visibleTabs[i].Button.AddThemeStyleboxOverride(
                "normal",
                i == _activeTabIndex ? _activeTabStyle : _inactiveTabStyle
            );
        }
    }

    private void ClearContent()
    {
        if (_contentContainer == null)
            return;
        foreach (var child in _contentContainer.GetChildren())
            child.QueueFree();
    }

    // ───────── Overview Tab ─────────

    private void PopulateOverview()
    {
        if (_station == null || _contentContainer == null)
            return;

        AddSectionHeader("Station Info");
        AddInfoRow("Name", _station.Name);
        AddInfoRow("Type", _station.StationType);
        AddInfoRow("Band", _station.BandIndex.ToString());
        AddInfoRow("Active", _station.IsActive ? "Yes" : "No");

        if (_station.IsUnderConstruction)
        {
            AddSectionHeader("Construction");
            AddInfoRow("Status", _station.GetStatus());
            AddInfoRow("Progress", $"{_station.GetProgress() * 100:F0}%");
            AddInfoRow("Work Done", $"{_station.workDone:F1} / {_station.workRequired:F1}");

            if (_station.requiredResources.Count > 0)
            {
                AddSectionHeader("Required Resources");
                foreach (var kvp in _station.requiredResources)
                {
                    int delivered = _station.availableResources.ContainsKey(kvp.Key)
                        ? _station.availableResources[kvp.Key]
                        : 0;
                    AddInfoRow(kvp.Key, $"{delivered} / {kvp.Value}");
                }
            }
        }

        if (_station.CanBuildShips)
            AddInfoRow("Can Build Ships", "Yes");

        // Orbital info
        AddSectionHeader("Orbital Parameters");
        AddInfoRow("Radius", $"{_station.OrbitalRadius:F2}");
        AddInfoRow("Speed", $"{_station.OrbitalSpeed:F6} rad/s");

        // Economy summary
        if (_station.Economy != null)
        {
            var eco = _station.Economy;
            AddSectionHeader("Economy Summary");
            AddInfoRow("Power", $"{eco.PowerGeneration:F1} gen / {eco.PowerConsumption:F1} use");
            AddInfoRow("Power Stored", $"{eco.PowerStored:F1} / {eco.PowerStorageCapacity:F1}");
            AddInfoRow("Buildings", eco.ActiveBuildingCount.ToString());

            if (eco.IsPowerDeficit)
                AddAlertRow("POWER DEFICIT");
        }
    }

    // ───────── Inventory Tab ─────────

    private void PopulateInventory()
    {
        if (_station?.Economy == null || _contentContainer == null)
            return;

        var eco = _station.Economy;
        var stockpiles = eco.GetAllStockpiles();
        var netRates = eco.GetAllNetRates();

        if (stockpiles.Count == 0)
        {
            AddEmptyLabel("No resources in inventory");
            return;
        }

        AddSectionHeader("Stockpiles");
        int index = 0;
        foreach (var kvp in stockpiles)
        {
            if (kvp.Value <= 0 && (!netRates.TryGetValue(kvp.Key, out float rate) || rate == 0))
                continue;

            var row = CreateClickableRow(index, "resource");

            float netRate = netRates.TryGetValue(kvp.Key, out float nr) ? nr : 0f;
            string rateStr = netRate >= 0 ? $"+{netRate:F2}" : $"{netRate:F2}";

            var label = new Label
            {
                Text = $"{kvp.Key}:  {kvp.Value:F1}  ({rateStr}/s)",
            };
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
            label.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(label);

            _contentContainer.AddChild(row);
            index++;
        }

        // Power section
        AddSectionHeader("Power");
        AddInfoRow("Generation", $"{eco.PowerGeneration:F1}/s");
        AddInfoRow("Consumption", $"{eco.PowerConsumption:F1}/s");
        AddInfoRow("Stored", $"{eco.PowerStored:F1} / {eco.PowerStorageCapacity:F1}");

        if (eco.IsPowerDeficit)
            AddAlertRow("POWER DEFICIT");
    }

    // ───────── Buildings Tab (Orbital Architect) ─────────

    private void PopulateBuildings()
    {
        if (_station == null || _contentContainer == null)
            return;

        var parentBody = _station.ParentBody;
        var constructionMgr = parentBody?.BuildingConstructionMgr;

        if (constructionMgr == null)
        {
            AddEmptyLabel("No building construction manager available");
            return;
        }

        var activeBuildings = constructionMgr.GetActiveBuildings();
        int pendingCount = constructionMgr.PendingBuildingCount;

        if (activeBuildings.Count == 0 && pendingCount == 0)
        {
            AddEmptyLabel("No buildings under construction");
            return;
        }

        AddInfoRow("Architects", constructionMgr.ArchitectCount.ToString());

        if (activeBuildings.Count > 0)
        {
            AddSectionHeader($"Active Construction ({activeBuildings.Count})");
            for (int i = 0; i < activeBuildings.Count; i++)
            {
                var building = activeBuildings[i];
                var row = CreateClickableRow(i, "active_building");

                float progress = building.GetProgress();
                var label = new Label
                {
                    Text = $"{building.Name}  |  {progress * 100:F0}%",
                };
                label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
                label.AddThemeFontSizeOverride("font_size", 13);
                row.AddChild(label);

                _contentContainer.AddChild(row);
            }
        }

        if (pendingCount > 0)
        {
            AddSectionHeader($"Pending ({pendingCount})");
            AddEmptyLabel($"{pendingCount} building(s) waiting for architect assignment");
        }
    }

    // ───────── Manufacturing Tab (Refinery) ─────────

    private void PopulateManufacturing()
    {
        if (_station == null || _contentContainer == null)
            return;

        AddSectionHeader("Manufacturing Facilities");
        AddEmptyLabel("Manufacturing configuration coming soon");
    }

    // ───────── Construction Queue Tab (Shipyard) ─────────

    private void PopulateConstructionQueue()
    {
        if (_station is not ConstructionYardStation shipyard || _contentContainer == null)
            return;

        var activeBuilds = shipyard.GetActiveBuilds();
        var queuedShips = shipyard.GetShipBuildQueue();

        if (activeBuilds.Count == 0 && queuedShips.Count == 0)
        {
            AddEmptyLabel("No ships in build queue");
            return;
        }

        if (activeBuilds.Count > 0)
        {
            AddSectionHeader($"Building ({activeBuilds.Count})");
            for (int i = 0; i < activeBuilds.Count; i++)
            {
                var ship = activeBuilds[i];
                var row = CreateClickableRow(i, "active_ship");

                float progress = ship.GetProgress();
                string status = ship.GetStatus();
                var label = new Label
                {
                    Text = $"{ship.Name}  |  {progress * 100:F0}%  |  {status}",
                };
                label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
                label.AddThemeFontSizeOverride("font_size", 13);
                row.AddChild(label);

                _contentContainer.AddChild(row);
            }
        }

        if (queuedShips.Count > 0)
        {
            AddSectionHeader($"Queued ({queuedShips.Count})");
            for (int i = 0; i < queuedShips.Count; i++)
            {
                var ship = queuedShips[i];
                var row = CreateClickableRow(i, "queued_ship");

                var label = new Label
                {
                    Text = $"{ship.Name}  |  Waiting...",
                };
                label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
                label.AddThemeFontSizeOverride("font_size", 13);
                row.AddChild(label);

                _contentContainer.AddChild(row);
            }
        }
    }

    // ───────── Helpers ─────────

    private HBoxContainer CreateClickableRow(int index, string itemType)
    {
        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Stop;
        row.AddThemeConstantOverride("separation", 4);

        row.GuiInput += (InputEvent @event) =>
        {
            if (
                @event is InputEventMouseButton btn
                && btn.ButtonIndex == MouseButton.Left
                && btn.Pressed
            )
            {
                EmitSignal(SignalName.ItemSelected, itemType, index);
                row.GetViewport().SetInputAsHandled();
            }
        };

        return row;
    }

    private void AddSectionHeader(string text)
    {
        if (_contentContainer == null)
            return;

        var header = new Label { Text = text };
        header.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.95f));
        header.AddThemeFontSizeOverride("font_size", 15);
        _contentContainer.AddChild(header);

        _contentContainer.AddChild(new HSeparator());
    }

    private void AddInfoRow(string labelText, string value)
    {
        if (_contentContainer == null)
            return;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var keyLabel = new Label { Text = labelText + ":" };
        keyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        keyLabel.AddThemeFontSizeOverride("font_size", 13);
        keyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(keyLabel);

        var valLabel = new Label { Text = value };
        valLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(valLabel);

        _contentContainer.AddChild(row);
    }

    private void AddEmptyLabel(string text)
    {
        if (_contentContainer == null)
            return;

        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        label.AddThemeFontSizeOverride("font_size", 13);
        _contentContainer.AddChild(label);
    }

    private void AddAlertRow(string message)
    {
        if (_contentContainer == null)
            return;

        var label = new Label { Text = $"[!] {message}" };
        label.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.3f));
        label.AddThemeFontSizeOverride("font_size", 12);
        _contentContainer.AddChild(label);
    }
}
