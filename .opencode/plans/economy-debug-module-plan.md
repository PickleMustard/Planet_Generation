# Economy Debug Module - Implementation Plan

## Overview
Add a dedicated economy monitoring panel to the debug console. Shows real-time stats for continent and station economies with live updates.

---

## Ticket 1: Infrastructure - Signals and Registry

**Files to Modify:**
- `Scripts/UtilityLibrary/SignalBus.cs`
- `UI/Debug/Console/InstanceRegistry.cs`

**Implementation Steps:**

### 1. Add SignalBus signals (SignalBus.cs)
```csharp
// Add after existing economy signals around line 173
[Signal]
public delegate void EconomyRegisteredEventHandler(string economyNamespace, string economyType, string parentName);

[Signal]
public delegate void EconomyUnregisteredEventHandler(string economyNamespace);

public void EmitEconomyRegistered(string economyNamespace, string economyType, string parentName)
{
    EmitSignal(SignalName.EconomyRegistered, economyNamespace, economyType, parentName);
}

public void EmitEconomyUnregistered(string economyNamespace)
{
    EmitSignal(SignalName.EconomyUnregistered, economyNamespace);
}
```

### 2. Add InstanceRegistry methods (InstanceRegistry.cs)
```csharp
public static string RegisterContinentEconomy(ContinentEconomy economy, int continentIndex)
{
    var ns = $"ContinentEconomy.{continentIndex}";
    return Register(economy, ns);
}

public static string RegisterStationEconomy(StationEconomy economy, string stationId)
{
    var ns = $"StationEconomy.{stationId}";
    return Register(economy, ns);
}

public static IEnumerable<string> GetAllEconomyNamespaces()
{
    lock (_lock)
    {
        return _instances.Keys
            .Where(ns => ns.StartsWith("ContinentEconomy.", StringComparison.OrdinalIgnoreCase) 
                      || ns.StartsWith("StationEconomy.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(ns => ns)
            .ToList();
    }
}
```

**Testing:**
- Verify signals appear in SignalBus autocomplete
- Test that InstanceRegistry.GetAllEconomyNamespaces() returns empty list initially

---

## Ticket 2: Economy Class Registration

**Files to Modify:**
- `Scripts/Structures/GameState/ContinentEconomy.cs`
- `Scripts/Structures/GameState/StationEconomy.cs`

**Implementation Steps:**

### 1. Update ContinentEconomy.cs
Add field:
```csharp
#if DEBUG
private string? _debugNamespace;
#endif
```

In constructor (after InitializeDefaultCapacities()):
```csharp
#if DEBUG
    try
    {
        _debugNamespace = InstanceRegistry.RegisterContinentEconomy(this, continent.StartingIndex);
        GameLogger.Debug($"[ContinentEconomy] Registered with debug console as '{_debugNamespace}'");
        SignalBus.Instance?.EmitEconomyRegistered(_debugNamespace, "ContinentEconomy", continent.StartingIndex.ToString());
    }
    catch (Exception e)
    {
        GameLogger.Warning($"[ContinentEconomy] Failed to register with debug: {e.Message}");
    }
#endif
```

### 2. Update StationEconomy.cs
Add field:
```csharp
#if DEBUG
private string? _debugNamespace;
#endif
```

In constructor (after InitializeDefaultCapacities()):
```csharp
#if DEBUG
    try
    {
        _debugNamespace = InstanceRegistry.RegisterStationEconomy(this, stationId);
        GameLogger.Debug($"[StationEconomy] Registered with debug console as '{_debugNamespace}'");
        SignalBus.Instance?.EmitEconomyRegistered(_debugNamespace, "StationEconomy", stationId);
    }
    catch (Exception e)
    {
        GameLogger.Warning($"[StationEconomy] Failed to register with debug: {e.Message}");
    }
#endif
```

**Testing:**
- Build a building on a continent
- Check debug console - should see registration log
- Verify InstanceRegistry contains "ContinentEconomy.0"

---

## Ticket 3: EconomyDebugModule UI

**Files to Create:**
- `UI/Debug/Economy/EconomyDebugModule.cs`

**Full Implementation:**
```csharp
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

        private HSplitContainer? _splitContainer;
        private ItemList? _economyList;
        private ScrollContainer? _detailScroll;
        private VBoxContainer? _detailContainer;

        // Detail labels
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
            BuildUI();
            ConnectSignals();
            RefreshEconomyList();
        }

        private void BuildUI()
        {
            AnchorRight = 1f;
            AnchorBottom = 1f;
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;

            _splitContainer = new HSplitContainer
            {
                AnchorRight = 1f,
                AnchorBottom = 1f,
                OffsetRight = 0,
                OffsetBottom = 0,
                SplitOffset = 250,
            };
            AddChild(_splitContainer);

            // Left: Economy list
            var leftPanel = new PanelContainer();
            _splitContainer.AddChild(leftPanel);

            _economyList = new ItemList
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            _economyList.ItemSelected += OnEconomySelected;
            leftPanel.AddChild(_economyList);

            // Right: Detail panel
            var rightPanel = new PanelContainer();
            _splitContainer.AddChild(rightPanel);

            _detailScroll = new ScrollContainer
            {
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            rightPanel.AddChild(_detailScroll);

            _detailContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _detailScroll.AddChild(_detailContainer);

            BuildDetailSections();
        }

        private void BuildDetailSections()
        {
            // Header
            var headerSection = CreateSection("Economy Info");
            _headerTypeLabel = AddInfoRow(headerSection, "Type", "-");
            _headerIdLabel = AddInfoRow(headerSection, "ID", "-");
            _detailContainer!.AddChild(headerSection);

            // Power
            var powerSection = CreateSection("Power");
            _powerGenLabel = AddInfoRow(powerSection, "Generation", "-");
            _powerConLabel = AddInfoRow(powerSection, "Consumption", "-");
            _powerStoredLabel = AddInfoRow(powerSection, "Stored", "-");
            _powerDeficitLabel = AddInfoRow(powerSection, "Deficit", "-");
            _detailContainer.AddChild(powerSection);

            // Buildings
            var buildingsSection = CreateSection("Buildings");
            _buildingsActiveLabel = AddInfoRow(buildingsSection, "Active", "-");
            _buildingsPausedLabel = AddInfoRow(buildingsSection, "Paused", "-");
            _detailContainer.AddChild(buildingsSection);

            // Stockpiles
            var stockpilesSection = CreateSection("Stockpiles by Category");
            _stockpilesContainer = new VBoxContainer();
            stockpilesSection.AddChild(_stockpilesContainer);
            _detailContainer.AddChild(stockpilesSection);

            // Shortages
            var shortagesSection = CreateSection("Active Shortages");
            _shortagesContainer = new VBoxContainer();
            shortagesSection.AddChild(_shortagesContainer);
            _detailContainer.AddChild(shortagesSection);
        }

        private VBoxContainer CreateSection(string title)
        {
            var section = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            var panel = new PanelContainer();
            var style = new StyleBoxFlat 
            { 
                BgColor = new Color(0.12f, 0.12f, 0.14f),
                ContentMarginLeft = 8,
                ContentMarginTop = 4,
                ContentMarginRight = 8,
                ContentMarginBottom = 4,
            };
            panel.AddThemeStyleboxOverride("panel", style);
            section.AddChild(panel);

            var headerLabel = new Label { Text = title };
            headerLabel.AddThemeFontSizeOverride("font_size", 14);
            headerLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.4f));
            panel.AddChild(headerLabel);

            var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            content.AddThemeConstantOverride("separation", 2);
            section.AddChild(content);

            return content;
        }

        private Label AddInfoRow(VBoxContainer container, string label, string value)
        {
            var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            container.AddChild(hbox);

            var labelText = new Label
            {
                Text = $"{label}:",
                CustomMinimumSize = new Vector2(120, 0),
            };
            labelText.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            hbox.AddChild(labelText);

            var valueLabel = new Label { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            valueLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            hbox.AddChild(valueLabel);

            return valueLabel;
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
            if (!IsVisible) return;

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
            var bodies = InstanceRegistry.GetAllInstances()
                .OfType<CelestialBody>();

            foreach (var body in bodies)
            {
                if (body.EconomyMgr == null) continue;

                foreach (var eco in body.EconomyMgr.GetActiveEconomies())
                {
                    if (eco.ActiveBuildingCount > 0)
                    {
                        var idx = eco.Continent.StartingIndex;
                        var display = $"Continent {idx} ({eco.ActiveBuildingCount} buildings)";
                        _economyList?.AddItem(display);
                        
                        // Color code based on status
                        if (eco.IsPowerDeficit)
                            _economyList?.SetItemCustomFgColor(_economyList.ItemCount - 1, new Color(1, 0.5f, 0.5f));
                    }
                }

                foreach (var eco in body.EconomyMgr.GetActiveStationEconomies())
                {
                    if (eco.ActiveBuildingCount > 0)
                    {
                        var display = $"Station {eco.StationId} ({eco.ActiveBuildingCount} buildings)";
                        _economyList?.AddItem(display);
                        
                        if (eco.IsPowerDeficit)
                            _economyList?.SetItemCustomFgColor(_economyList.ItemCount - 1, new Color(1, 0.5f, 0.5f));
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
            _headerTypeLabel!.Text = "Continent";
            _headerIdLabel!.Text = eco.Continent.StartingIndex.ToString();
            
            _powerGenLabel!.Text = $"{eco.PowerGeneration:F1}/s";
            _powerConLabel!.Text = $"{eco.PowerConsumption:F1}/s";
            _powerStoredLabel!.Text = $"{eco.PowerStored:F1} / {eco.PowerStorageCapacity:F1}";
            _powerDeficitLabel!.Text = eco.IsPowerDeficit ? "YES" : "No";
            _powerDeficitLabel.AddThemeColorOverride("font_color", eco.IsPowerDeficit ? new Color(1, 0.3f, 0.3f) : new Color(0.3f, 1, 0.3f));

            _buildingsActiveLabel!.Text = eco.ActiveBuildingCount.ToString();
            
            int pausedCount = eco.ActiveBuildings.Count(b => b.IsPaused);
            _buildingsPausedLabel!.Text = pausedCount.ToString();

            UpdateStockpiles(eco.GetAllStockpiles(), eco.GetAllNetRates());
            UpdateShortages(eco);
        }

        private void UpdateStationEconomyDetails(StationEconomy eco)
        {
            _headerTypeLabel!.Text = "Station";
            _headerIdLabel!.Text = eco.StationId;
            
            _powerGenLabel!.Text = $"{eco.PowerGeneration:F1}/s";
            _powerConLabel!.Text = $"{eco.PowerConsumption:F1}/s";
            _powerStoredLabel!.Text = $"{eco.PowerStored:F1} / {eco.PowerStorageCapacity:F1}";
            _powerDeficitLabel!.Text = eco.IsPowerDeficit ? "YES" : "No";
            _powerDeficitLabel.AddThemeColorOverride("font_color", eco.IsPowerDeficit ? new Color(1, 0.3f, 0.3f) : new Color(0.3f, 1, 0.3f));

            _buildingsActiveLabel!.Text = eco.ActiveBuildingCount.ToString();
            
            int pausedCount = eco.ActiveBuildings.Count(b => b.IsPaused);
            _buildingsPausedLabel!.Text = pausedCount.ToString();

            UpdateStockpiles(eco.GetAllStockpiles(), eco.GetAllNetRates());
            UpdateShortages(eco);
        }

        private void UpdateStockpiles(IReadOnlyDictionary<string, float> stockpiles, IReadOnlyDictionary<string, float> netRates)
        {
            foreach (var child in _stockpilesContainer!.GetChildren())
                child.QueueFree();

            // Group by category
            var categories = new Dictionary<string, List<(string resource, float amount, float netRate)>>();
            
            foreach (var kvp in stockpiles)
            {
                if (kvp.Value <= 0.01f) continue; // Skip near-zero
                
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
                    rateLabel.AddThemeColorOverride("font_color", item.netRate >= 0 ? new Color(0.3f, 1, 0.3f) : new Color(1, 0.3f, 0.3f));
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
            foreach (var child in _shortagesContainer!.GetChildren())
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
            foreach (var child in _shortagesContainer!.GetChildren())
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
            _headerTypeLabel!.Text = "-";
            _headerIdLabel!.Text = "-";
            _powerGenLabel!.Text = "-";
            _powerConLabel!.Text = "-";
            _powerStoredLabel!.Text = "-";
            _powerDeficitLabel!.Text = "-";
            _buildingsActiveLabel!.Text = "-";
            _buildingsPausedLabel!.Text = "-";
            
            foreach (var child in _stockpilesContainer!.GetChildren())
                child.QueueFree();
            foreach (var child in _shortagesContainer!.GetChildren())
                child.QueueFree();
        }

        private static string GetResourceCategory(string resourceId)
        {
            var db = ResourceDatabase.Instance;
            if (db?.IsLoaded == true && db.TryGetResource(resourceId, out var def) && def?.ResourceType != null)
                return def.ResourceType;
            
            if (resourceId.EndsWith("_ore")) return "ore";
            if (resourceId == "power") return "power";
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
```

**Testing:**
- Open debug menu (backtick key)
- Click "Economy" tab
- Build a building on a continent
- Verify economy appears in list
- Click economy to see details
- Verify power/building/stockpile data updates live

---

## Ticket 4: Debug Commands

**Files to Create:**
- `UI/Debug/Console/Commands/EconomyCommands.cs`

**Full Implementation:**
```csharp
#if DEBUG
using System;
using System.Linq;
using Structures.GameState;
using UI.Debug.Console;
using UtilityLibrary;

namespace UI.Debug.Console.Commands
{
    public static class EconomyCommands
    {
        [DebugCommand(
            "economy_list",
            "List all registered economies",
            "economy_list",
            Category = "Economy"
        )]
        public static int EconomyList(CommandContext ctx, string[] args)
        {
            var namespaces = InstanceRegistry.GetAllEconomyNamespaces().ToList();
            
            if (namespaces.Count == 0)
            {
                ctx.WriteLine("[color=yellow]No economies registered.[/color]");
                return 0;
            }

            ctx.WriteLine($"[color=cyan]=== Registered Economies ({namespaces.Count}) ===[/color]");
            
            foreach (var ns in namespaces)
            {
                if (!InstanceRegistry.TryGetInstance(ns, out var instance))
                    continue;

                string status = instance switch
                {
                    ContinentEconomy ce => $"Buildings: {ce.ActiveBuildingCount}, Power: {ce.PowerGeneration:F1}/{ce.PowerConsumption:F1}",
                    StationEconomy se => $"Buildings: {se.ActiveBuildingCount}, Power: {se.PowerGeneration:F1}/{se.PowerConsumption:F1}",
                    _ => "Unknown type"
                };

                string deficitWarning = instance switch
                {
                    ContinentEconomy ce => ce.IsPowerDeficit ? " [color=red][DEFICIT][/color]" : "",
                    StationEconomy se => se.IsPowerDeficit ? " [color=red][DEFICIT][/color]" : "",
                    _ => ""
                };

                ctx.WriteLine($"  {ns}: {status}{deficitWarning}");
            }

            return 0;
        }

        [DebugCommand(
            "economy_info",
            "Show detailed info for an economy",
            "economy_info <namespace>",
            Category = "Economy"
        )]
        public static int EconomyInfo(CommandContext ctx, string[] args)
        {
            if (args.Length < 1)
            {
                ctx.WriteError("Usage: economy_info <namespace>");
                ctx.WriteLine("Example: economy_info ContinentEconomy.0");
                return 1;
            }

            string ns = args[0];
            if (!InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteError($"Economy not found: {ns}");
                return 1;
            }

            ctx.WriteLine($"[color=cyan]=== {ns} ===[/color]");

            switch (instance)
            {
                case ContinentEconomy ce:
                    ShowContinentEconomyInfo(ctx, ce);
                    break;
                case StationEconomy se:
                    ShowStationEconomyInfo(ctx, se);
                    break;
                default:
                    ctx.WriteError("Unknown economy type");
                    return 1;
            }

            return 0;
        }

        private static void ShowContinentEconomyInfo(CommandContext ctx, ContinentEconomy eco)
        {
            ctx.WriteLine($"Type: Continent");
            ctx.WriteLine($"Continent Index: {eco.Continent.StartingIndex}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Power:[/color]");
            ctx.WriteLine($"  Generation: {eco.PowerGeneration:F1}/s");
            ctx.WriteLine($"  Consumption: {eco.PowerConsumption:F1}/s");
            ctx.WriteLine($"  Stored: {eco.PowerStored:F1} / {eco.PowerStorageCapacity:F1}");
            ctx.WriteLine($"  Deficit: {(eco.IsPowerDeficit ? "[color=red]YES[/color]" : "No")}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Buildings:[/color]");
            ctx.WriteLine($"  Active: {eco.ActiveBuildingCount}");
            ctx.WriteLine($"  Paused: {eco.ActiveBuildings.Count(b => b.IsPaused)}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Stockpiles:[/color]");
            foreach (var kvp in eco.GetAllStockpiles().Where(s => s.Value > 0.01f).OrderBy(s => s.Key))
            {
                float netRate = eco.GetNetRate(kvp.Key);
                string rateStr = $"({(netRate >= 0 ? "+" : "")}{netRate:F2}/s)";
                ctx.WriteLine($"  {kvp.Key}: {kvp.Value:F1} {rateStr}");
            }
        }

        private static void ShowStationEconomyInfo(CommandContext ctx, StationEconomy eco)
        {
            ctx.WriteLine($"Type: Station");
            ctx.WriteLine($"Station ID: {eco.StationId}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Power:[/color]");
            ctx.WriteLine($"  Generation: {eco.PowerGeneration:F1}/s");
            ctx.WriteLine($"  Consumption: {eco.PowerConsumption:F1}/s");
            ctx.WriteLine($"  Stored: {eco.PowerStored:F1} / {eco.PowerStorageCapacity:F1}");
            ctx.WriteLine($"  Deficit: {(eco.IsPowerDeficit ? "[color=red]YES[/color]" : "No")}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Buildings:[/color]");
            ctx.WriteLine($"  Active: {eco.ActiveBuildingCount}");
            ctx.WriteLine($"  Paused: {eco.ActiveBuildings.Count(b => b.IsPaused)}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Stockpiles:[/color]");
            foreach (var kvp in eco.GetAllStockpiles().Where(s => s.Value > 0.01f).OrderBy(s => s.Key))
            {
                float netRate = eco.GetNetRate(kvp.Key);
                string rateStr = $"({(netRate >= 0 ? "+" : "")}{netRate:F2}/s)";
                ctx.WriteLine($"  {kvp.Key}: {kvp.Value:F1} {rateStr}");
            }
        }

        [DebugCommand(
            "economy_add_resource",
            "Add resource to economy stockpile",
            "economy_add_resource <namespace> <resource> <amount>",
            Category = "Economy"
        )]
        public static int EconomyAddResource(CommandContext ctx, string[] args)
        {
            if (args.Length < 3)
            {
                ctx.WriteError("Usage: economy_add_resource <namespace> <resource> <amount>");
                return 1;
            }

            string ns = args[0];
            string resourceId = args[1];
            if (!float.TryParse(args[2], out float amount))
            {
                ctx.WriteError($"Invalid amount: {args[2]}");
                return 1;
            }

            if (!InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteError($"Economy not found: {ns}");
                return 1;
            }

            float deposited = instance switch
            {
                ContinentEconomy ce => ce.DepositResource(resourceId, amount),
                StationEconomy se => se.DepositResource(resourceId, amount),
                _ => 0f
            };

            ctx.WriteLine($"[color=green]Added {deposited:F1} {resourceId}[/color]");
            return 0;
        }

        [DebugCommand(
            "economy_remove_resource",
            "Remove resource from economy stockpile",
            "economy_remove_resource <namespace> <resource> <amount>",
            Category = "Economy"
        )]
        public static int EconomyRemoveResource(CommandContext ctx, string[] args)
        {
            if (args.Length < 3)
            {
                ctx.WriteError("Usage: economy_remove_resource <namespace> <resource> <amount>");
                return 1;
            }

            string ns = args[0];
            string resourceId = args[1];
            if (!float.TryParse(args[2], out float amount))
            {
                ctx.WriteError($"Invalid amount: {args[2]}");
                return 1;
            }

            if (!InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteError($"Economy not found: {ns}");
                return 1;
            }

            float withdrawn = instance switch
            {
                ContinentEconomy ce => ce.WithdrawResource(resourceId, amount),
                StationEconomy se => se.WithdrawResource(resourceId, amount),
                _ => 0f
            };

            ctx.WriteLine($"[color=green]Removed {withdrawn:F1} {resourceId}[/color]");
            return 0;
        }

        [DebugCommand(
            "economy_set_power",
            "Set power stored in economy",
            "economy_set_power <namespace> <amount>",
            Category = "Economy"
        )]
        public static int EconomySetPower(CommandContext ctx, string[] args)
        {
            ctx.WriteLine("[color=yellow]Note: Direct power manipulation not implemented.[/color]");
            ctx.WriteLine("Power is calculated from building recipes.");
            ctx.WriteLine("Use building construction/demolition to change power.");
            return 0;
        }

        [DebugCommand(
            "economy_pause_buildings",
            "Pause all non-power-generating buildings",
            "economy_pause_buildings <namespace>",
            Category = "Economy"
        )]
        public static int EconomyPauseBuildings(CommandContext ctx, string[] args)
        {
            if (args.Length < 1)
            {
                ctx.WriteError("Usage: economy_pause_buildings <namespace>");
                return 1;
            }

            string ns = args[0];
            if (!InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteError($"Economy not found: {ns}");
                return 1;
            }

            int pausedCount = 0;
            switch (instance)
            {
                case ContinentEconomy ce:
                    foreach (var building in ce.ActiveBuildings.Where(b => b.PowerGeneration <= 0 && !b.IsPaused))
                    {
                        building.IsPaused = true;
                        building.PauseReasons.Add("debug");
                        pausedCount++;
                    }
                    break;
                case StationEconomy se:
                    foreach (var building in se.ActiveBuildings.Where(b => b.PowerGeneration <= 0 && !b.IsPaused))
                    {
                        building.IsPaused = true;
                        building.PauseReasons.Add("debug");
                        pausedCount++;
                    }
                    break;
            }

            ctx.WriteLine($"[color=green]Paused {pausedCount} buildings[/color]");
            return 0;
        }

        [DebugCommand(
            "economy_unpause_buildings",
            "Unpause all buildings",
            "economy_unpause_buildings <namespace>",
            Category = "Economy"
        )]
        public static int EconomyUnpauseBuildings(CommandContext ctx, string[] args)
        {
            if (args.Length < 1)
            {
                ctx.WriteError("Usage: economy_unpause_buildings <namespace>");
                return 1;
            }

            string ns = args[0];
            if (!InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteError($"Economy not found: {ns}");
                return 1;
            }

            int unpausedCount = 0;
            switch (instance)
            {
                case ContinentEconomy ce:
                    foreach (var building in ce.ActiveBuildings.Where(b => b.IsPaused))
                    {
                        building.PauseReasons.Remove("debug");
                        if (building.PauseReasons.Count == 0)
                            building.IsPaused = false;
                        unpausedCount++;
                    }
                    break;
                case StationEconomy se:
                    foreach (var building in se.ActiveBuildings.Where(b => b.IsPaused))
                    {
                        building.PauseReasons.Remove("debug");
                        if (building.PauseReasons.Count == 0)
                            building.IsPaused = false;
                        unpausedCount++;
                    }
                    break;
            }

            ctx.WriteLine($"[color=green]Unpaused {unpausedCount} buildings[/color]");
            return 0;
        }
    }
}
#endif
```

**Testing:**
- Open debug console
- Type `economy_list` - should show empty or list economies
- Type `economy_info ContinentEconomy.0` - show details
- Type `economy_add_resource ContinentEconomy.0 iron_ore 1000` - add resources

---

## Ticket 5: DebugMenu Integration

**Files to Modify:**
- `UI/Debug/DebugMenu.cs`

**Implementation Steps:**

### 1. Add using statement
```csharp
#if DEBUG
using System.Collections.Generic;
using Godot;
using UI.Debug.Console;
using UI.Debug.Economy;  // ADD THIS
using CellInfoModule = UI.Debug.CellInfo.CellInfo;
using DatabaseViewerModule = UI.Debug.DatabaseViewer.DatabaseViewer;
```

### 2. Update InitializeDefaultModules()
```csharp
private void InitializeDefaultModules()
{
    var console = new DebugConsole();
    RegisterModule(console);

    var databaseViewer = new DatabaseViewerModule();
    RegisterModule(databaseViewer);

    var cellInfo = new CellInfoModule();
    RegisterModule(cellInfo);

    // ADD THIS:
    var economyModule = new EconomyDebugModule();
    RegisterModule(economyModule);
}
```

**Testing:**
- Launch game
- Press backtick (`) to open debug menu
- Verify "Economy" tab appears
- Click tab - should show empty list initially
- Build a building and verify economy appears

---

## Summary of All Changes

| File | Action | Lines |
|------|--------|-------|
| `Scripts/UtilityLibrary/SignalBus.cs` | Add 2 signals + emit methods | +20 |
| `UI/Debug/Console/InstanceRegistry.cs` | Add 3 economy registration methods | +30 |
| `Scripts/Structures/GameState/ContinentEconomy.cs` | Add debug registration | +15 |
| `Scripts/Structures/GameState/StationEconomy.cs` | Add debug registration | +15 |
| `UI/Debug/Economy/EconomyDebugModule.cs` | **NEW** - Main UI module | ~450 |
| `UI/Debug/Console/Commands/EconomyCommands.cs` | **NEW** - Debug commands | ~280 |
| `UI/Debug/DebugMenu.cs` | Add EconomyDebugModule registration | +4 |

**Total:** ~800 lines of new code, ~70 lines modified
