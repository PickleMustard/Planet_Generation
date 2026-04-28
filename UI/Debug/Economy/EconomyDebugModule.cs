#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Constructables;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using Structures.Resources;
using UI.Debug.Console;
using UtilityLibrary;

namespace UI.Debug.Economy
{
    public partial class EconomyDebugModule : BaseDebugModule
    {
        public override string ModuleName => "Economy";

        // Split container with configurable offset
        [Export] public int SplitOffset { get; set; } = 250;

        // Node references (fetched in _Ready via FetchNodes)
        private HSplitContainer? _splitContainer;
        private ItemList? _economyList;
        private Label? _headerTypeLabel;
        private Label? _headerIdLabel;
        private Label? _powerGenLabel;
        private Label? _powerConLabel;
        private Label? _powerStoredLabel;
        private Label? _powerDeficitLabel;
        private Label? _buildingsActiveLabel;
        private Label? _buildingsPausedLabel;
        private VBoxContainer? _stockpilesContainer;
        private VBoxContainer? _shortagesContainer;

        private double _updateTimer;
        private const double UPDATE_INTERVAL = 0.1; // 10fps
        private string? _selectedNamespace;

        public override void _Ready()
        {
            base._Ready();

            // Fetch nodes from scene tree
            FetchNodes();

            // Apply split offset
            if (_splitContainer != null)
            {
                _splitContainer.SplitOffsets = new[] { SplitOffset };
            }

            // Connect to economy list selection
            if (_economyList != null)
            {
                _economyList.ItemSelected += OnEconomySelected;
            }

            ConnectSignals();
            RefreshEconomyList();
        }

        private void FetchNodes()
        {
            _splitContainer = GetNode<HSplitContainer>("SplitContainer");
            _economyList = GetNode<ItemList>("SplitContainer/LeftPanel/EconomyList");

            // Header section values
            _headerTypeLabel = GetNode<Label>("SplitContainer/RightPanel/DetailScroll/DetailContainer/HeaderSection/HeaderContent/TypeRow/HeaderTypeValue");
            _headerIdLabel = GetNode<Label>("SplitContainer/RightPanel/DetailScroll/DetailContainer/HeaderSection/HeaderContent/IDRow/HeaderIdValue");

            // Power section values
            _powerGenLabel = GetNode<Label>("SplitContainer/RightPanel/DetailScroll/DetailContainer/PowerSection/PowerContent/GenerationRow/PowerGenValue");
            _powerConLabel = GetNode<Label>("SplitContainer/RightPanel/DetailScroll/DetailContainer/PowerSection/PowerContent/ConsumptionRow/PowerConValue");
            _powerStoredLabel = GetNode<Label>("SplitContainer/RightPanel/DetailScroll/DetailContainer/PowerSection/PowerContent/StoredRow/PowerStoredValue");
            _powerDeficitLabel = GetNode<Label>("SplitContainer/RightPanel/DetailScroll/DetailContainer/PowerSection/PowerContent/DeficitRow/PowerDeficitValue");

            // Buildings section values
            _buildingsActiveLabel = GetNode<Label>("SplitContainer/RightPanel/DetailScroll/DetailContainer/BuildingsSection/BuildingsContent/ActiveRow/BuildingsActiveValue");
            _buildingsPausedLabel = GetNode<Label>("SplitContainer/RightPanel/DetailScroll/DetailContainer/BuildingsSection/BuildingsContent/PausedRow/BuildingsPausedValue");

            // Dynamic content containers
            _stockpilesContainer = GetNode<VBoxContainer>("SplitContainer/RightPanel/DetailScroll/DetailContainer/StockpilesSection/StockpilesContainer");
            _shortagesContainer = GetNode<VBoxContainer>("SplitContainer/RightPanel/DetailScroll/DetailContainer/ShortagesSection/ShortagesContainer");
        }

        private void ConnectSignals()
        {
            if (SignalBus.Instance != null)
            {
                SignalBus.Instance.EconomyRegistered += OnEconomyRegistered;
                SignalBus.Instance.EconomyUnregistered += OnEconomyUnregistered;
                SignalBus.Instance.ContinentEconomyTicked += OnEconomyTicked;
            }
        }

        private void OnEconomyRegistered(string ns, string type, string parent)
        {
            RefreshEconomyList();
        }

        private void OnEconomyUnregistered(string ns)
        {
            if (_selectedNamespace == ns)
            {
                _selectedNamespace = null;
            }
            RefreshEconomyList();
        }

        private void OnEconomyTicked(int continentIndex)
        {
            // Don't refresh list on every tick, just the selected economy details
        }

        private void OnEconomySelected(long index)
        {
            var text = _economyList?.GetItemText((int)index);
            if (text != null)
            {
                // Parse namespace from display text
                var parts = text.Split(' ');
                if (parts.Length >= 2)
                {
                    var type = parts[0] == "Continent" ? "ContinentEconomy" : "StationEconomy";
                    var id = parts[1];
                    _selectedNamespace = $"{type}.{id}";
                }
            }
        }

        public override void _Process(double delta)
        {
            if (!IsVisible)
                return;

            _updateTimer += delta;
            if (_updateTimer >= UPDATE_INTERVAL)
            {
                _updateTimer = 0;
                RefreshEconomyList();
                RefreshSelectedEconomy();
            }
        }

        private void RefreshEconomyList()
        {
            _economyList?.Clear();

            // Scan all celestial bodies for economies
            var bodies = InstanceRegistry.GetAllInstances().OfType<CelestialBody>();

            foreach (var body in bodies)
            {
                if (body.EconomyMgr == null)
                    continue;

                foreach (var eco in body.EconomyMgr.GetActiveEconomies())
                {
                    if (eco.ActiveBuildingCount > 0)
                    {
                        var idx = eco.Continent.StartingIndex;
                        var display = $"Continent {idx} ({eco.ActiveBuildingCount} buildings)";
                        _economyList?.AddItem(display);

                        // Color code based on status
                        if (eco.IsPowerDeficit)
                            _economyList?.SetItemCustomFgColor(
                                _economyList.ItemCount - 1,
                                new Color(1, 0.5f, 0.5f)
                            );
                    }
                }

                foreach (var eco in body.EconomyMgr.GetActiveStationEconomies())
                {
                    if (eco.ActiveBuildingCount > 0)
                    {
                        var display =
                            $"Station {eco.StationId} ({eco.ActiveBuildingCount} buildings)";
                        _economyList?.AddItem(display);

                        if (eco.IsPowerDeficit)
                            _economyList?.SetItemCustomFgColor(
                                _economyList.ItemCount - 1,
                                new Color(1, 0.5f, 0.5f)
                            );
                    }
                }
            }
        }

        private void RefreshSelectedEconomy()
        {
            if (string.IsNullOrEmpty(_selectedNamespace))
            {
                ClearDetails();
                return;
            }

            if (!InstanceRegistry.TryGetInstance(_selectedNamespace, out var instance))
            {
                ClearDetails();
                return;
            }

            switch (instance)
            {
                case ContinentEconomy continentEco:
                    UpdateContinentEconomyDetails(continentEco);
                    break;
                case StationEconomy stationEco:
                    UpdateStationEconomyDetails(stationEco);
                    break;
            }
        }

        private void UpdateContinentEconomyDetails(ContinentEconomy eco)
        {
            if (_headerTypeLabel != null)
                _headerTypeLabel.Text = "Continent";
            if (_headerIdLabel != null)
                _headerIdLabel.Text = eco.Continent.StartingIndex.ToString();

            if (_powerGenLabel != null)
                _powerGenLabel.Text = $"{eco.PowerGeneration:F1}/s";
            if (_powerConLabel != null)
                _powerConLabel.Text = $"{eco.PowerConsumption:F1}/s";
            if (_powerStoredLabel != null)
                _powerStoredLabel.Text = $"{eco.PowerStored:F1} / {eco.PowerStorageCapacity:F1}";
            if (_powerDeficitLabel != null)
            {
                _powerDeficitLabel.Text = eco.IsPowerDeficit ? "YES" : "No";
                _powerDeficitLabel.AddThemeColorOverride(
                    "font_color",
                    eco.IsPowerDeficit ? new Color(1, 0.3f, 0.3f) : new Color(0.3f, 1, 0.3f)
                );
            }

            if (_buildingsActiveLabel != null)
                _buildingsActiveLabel.Text = eco.ActiveBuildingCount.ToString();

            int pausedCount = eco.ActiveBuildings.Count(b => b.IsPaused);
            if (_buildingsPausedLabel != null)
                _buildingsPausedLabel.Text = pausedCount.ToString();

            UpdateStockpiles(eco.GetAllStockpiles(), eco.GetAllNetRates());
            UpdateShortages(eco);
        }

        private void UpdateStationEconomyDetails(StationEconomy eco)
        {
            if (_headerTypeLabel != null)
                _headerTypeLabel.Text = "Station";
            if (_headerIdLabel != null)
                _headerIdLabel.Text = eco.StationId;

            if (_powerGenLabel != null)
                _powerGenLabel.Text = $"{eco.PowerGeneration:F1}/s";
            if (_powerConLabel != null)
                _powerConLabel.Text = $"{eco.PowerConsumption:F1}/s";
            if (_powerStoredLabel != null)
                _powerStoredLabel.Text = $"{eco.PowerStored:F1} / {eco.PowerStorageCapacity:F1}";
            if (_powerDeficitLabel != null)
            {
                _powerDeficitLabel.Text = eco.IsPowerDeficit ? "YES" : "No";
                _powerDeficitLabel.AddThemeColorOverride(
                    "font_color",
                    eco.IsPowerDeficit ? new Color(1, 0.3f, 0.3f) : new Color(0.3f, 1, 0.3f)
                );
            }

            if (_buildingsActiveLabel != null)
                _buildingsActiveLabel.Text = eco.ActiveBuildingCount.ToString();

            int pausedCount = eco.ActiveBuildings.Count(b => b.IsPaused);
            if (_buildingsPausedLabel != null)
                _buildingsPausedLabel.Text = pausedCount.ToString();

            UpdateStockpiles(eco.GetAllStockpiles(), eco.GetAllNetRates());
            UpdateShortages(eco);
        }

        private void UpdateStockpiles(
            IReadOnlyDictionary<string, float> stockpiles,
            IReadOnlyDictionary<string, float> netRates
        )
        {
            if (_stockpilesContainer == null)
                return;

            foreach (var child in _stockpilesContainer.GetChildren())
                child.QueueFree();

            // Group by category
            var categories =
                new Dictionary<string, List<(string resource, float amount, float netRate)>>();

            foreach (var kvp in stockpiles)
            {
                if (kvp.Value <= 0.01f)
                    continue; // Skip near-zero

                string category = GetResourceCategory(kvp.Key);
                if (!categories.ContainsKey(category))
                    categories[category] = new List<(string, float, float)>();

                float netRate = netRates.TryGetValue(kvp.Key, out var nr) ? nr : 0f;
                categories[category].Add((kvp.Key, kvp.Value, netRate));
            }

            foreach (var cat in categories.OrderBy(c => c.Key))
            {
                var catLabel = new Label { Text = $"[ {cat.Key.ToUpper()} ]" };
                catLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.4f));
                _stockpilesContainer.AddChild(catLabel);

                foreach (var item in cat.Value.OrderBy(i => i.resource))
                {
                    var hbox = new HBoxContainer();

                    var nameLabel = new Label
                    {
                        Text = $"  {item.resource}",
                        CustomMinimumSize = new Vector2(150, 0),
                    };
                    hbox.AddChild(nameLabel);

                    var amountLabel = new Label { Text = $"{item.amount:F1}" };
                    hbox.AddChild(amountLabel);

                    var rateLabel = new Label
                    {
                        Text = $"({(item.netRate >= 0 ? "+" : "")}{item.netRate:F2}/s)",
                    };
                    rateLabel.AddThemeColorOverride(
                        "font_color",
                        item.netRate >= 0 ? new Color(0.3f, 1, 0.3f) : new Color(1, 0.3f, 0.3f)
                    );
                    hbox.AddChild(rateLabel);

                    _stockpilesContainer.AddChild(hbox);
                }
            }

            if (categories.Count == 0)
            {
                var emptyLabel = new Label { Text = "No stockpiled resources" };
                emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                _stockpilesContainer.AddChild(emptyLabel);
            }
        }

        private void UpdateShortages(ContinentEconomy eco)
        {
            if (_shortagesContainer == null)
                return;

            foreach (var child in _shortagesContainer.GetChildren())
                child.QueueFree();

            var shortages = new List<string>();
            var stockpiles = eco.GetAllStockpiles();
            var netRates = eco.GetAllNetRates();

            foreach (var kvp in netRates)
            {
                float stock = stockpiles.TryGetValue(kvp.Key, out var s) ? s : 0f;
                if (stock <= 0 && kvp.Value < 0)
                    shortages.Add(kvp.Key);
            }

            if (shortages.Count == 0)
            {
                var okLabel = new Label { Text = "No active shortages" };
                okLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1, 0.3f));
                _shortagesContainer.AddChild(okLabel);
            }
            else
            {
                foreach (var shortage in shortages)
                {
                    var label = new Label { Text = $"⚠ {shortage}" };
                    label.AddThemeColorOverride("font_color", new Color(1, 0.5f, 0.3f));
                    _shortagesContainer.AddChild(label);
                }
            }
        }

        private void UpdateShortages(StationEconomy eco)
        {
            if (_shortagesContainer == null)
                return;

            foreach (var child in _shortagesContainer.GetChildren())
                child.QueueFree();

            var shortages = new List<string>();
            var stockpiles = eco.GetAllStockpiles();
            var netRates = eco.GetAllNetRates();

            foreach (var kvp in netRates)
            {
                float stock = stockpiles.TryGetValue(kvp.Key, out var s) ? s : 0f;
                if (stock <= 0 && kvp.Value < 0)
                    shortages.Add(kvp.Key);
            }

            if (shortages.Count == 0)
            {
                var okLabel = new Label { Text = "No active shortages" };
                okLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1, 0.3f));
                _shortagesContainer.AddChild(okLabel);
            }
            else
            {
                foreach (var shortage in shortages)
                {
                    var label = new Label { Text = $"⚠ {shortage}" };
                    label.AddThemeColorOverride("font_color", new Color(1, 0.5f, 0.3f));
                    _shortagesContainer.AddChild(label);
                }
            }
        }

        private void ClearDetails()
        {
            if (_headerTypeLabel != null)
                _headerTypeLabel.Text = "-";
            if (_headerIdLabel != null)
                _headerIdLabel.Text = "-";
            if (_powerGenLabel != null)
                _powerGenLabel.Text = "-";
            if (_powerConLabel != null)
                _powerConLabel.Text = "-";
            if (_powerStoredLabel != null)
                _powerStoredLabel.Text = "-";
            if (_powerDeficitLabel != null)
                _powerDeficitLabel.Text = "-";
            if (_buildingsActiveLabel != null)
                _buildingsActiveLabel.Text = "-";
            if (_buildingsPausedLabel != null)
                _buildingsPausedLabel.Text = "-";

            if (_stockpilesContainer != null)
                foreach (var child in _stockpilesContainer.GetChildren())
                    child.QueueFree();

            if (_shortagesContainer != null)
                foreach (var child in _shortagesContainer.GetChildren())
                    child.QueueFree();
        }

        private static string GetResourceCategory(string resourceId)
        {
            var db = ResourceDatabase.Instance;
            if (
                db?.IsLoaded == true
                && db.TryGetResource(resourceId, out var def)
                && def?.ResourceType != null
            )
                return def.ResourceType;

            if (resourceId.EndsWith("_ore"))
                return "ore";
            if (resourceId == "power")
                return "power";
            return "raw_material";
        }

        public override void _ExitTree()
        {
            if (SignalBus.Instance != null)
            {
                SignalBus.Instance.EconomyRegistered -= OnEconomyRegistered;
                SignalBus.Instance.EconomyUnregistered -= OnEconomyUnregistered;
                SignalBus.Instance.ContinentEconomyTicked -= OnEconomyTicked;
            }
            base._ExitTree();
        }
    }
}
#endif
