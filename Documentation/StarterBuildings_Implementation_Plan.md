# Implementation Plan: Starter Buildings for New Game Flow

## Overview

This feature introduces two **starter constructs** that players place at the beginning of a new game to bootstrap their economy:

1. **Company Headquarters** - Surface building (limit: 1/game)
   - All-in-one: Storage, Power Generation, Extraction, Manufacturing
   - Starts with full resource piles configured via YAML
   - Produces enough power for early buildings
   - Extracts basic materials for construction
   - Manufactures tier 0 construction materials
   - Triggers game start naming sequence
   - Can only use headquarters-specific recipes

2. **Ramshackle Builder** - Orbital station (limit: 1/game)
   - Functions as `OrbitalArchitectStation` but slower with more waste
   - Can be upgraded to normal Orbital Architect
   - Allows constructing other buildings

## Architecture Overview

### Key Systems

| System | Purpose | Key Files |
|--------|---------|-----------|
| Building System | Surface building placement/construction | `BuildingConstruction.cs`, `BuildingDefinition.cs`, `BuildingDatabase.cs` |
| Station System | Orbital stations/ships | `StationSatellite.cs`, `OrbitalArchitectStation.cs`, `StationDefinition` |
| Economy System | Resource production/consumption | `ContinentEconomy.cs`, `RecipeDefinition.cs`, `RecipeDatabase.cs` |
| Construction System | Build queue/work budget | `ConstructionManager.cs`, `BuildingWorkBudget.cs` |
| Recipe System | Production recipes | `RecipeDefinition.cs`, `RecipeDatabase.cs`, YAML files |

### Data Flow

```
Player Places Building
        |
        v
BuildingPlacementMode (validation)
        |
        v
ConstructionManager.CreateHeadquarters()
        |
        v
HeadquartersBuilding.InitializeHeadquarters()
        |
        +--> ContinentEconomy (adds stockpiles/capacity)
        +--> SignalBus.EmitGameStartHeadquartersPlaced()
                    |
                    v
        GameStartController (shows naming UI)
                    |
                    v
        SystemData.StartGame() (stores names)
```

## Implementation Tickets

---

### Ticket 1: Create SystemData Script for system_container

**New File:** `Scripts/GameState/SystemData.cs`

```csharp
using Godot;
using UtilityLibrary;

namespace Structures.GameState;

/// <summary>
/// Attached to the system_container Node to store system-level information.
/// This is the authoritative source for system-wide data.
/// </summary>
public partial class SystemData : Node
{
    [Export]
    public string SystemName { get; set; } = "Unnamed System";
    
    [Export]
    public bool IsGameStarted { get; set; } = false;
    
    [Export]
    public string HeadquartersBodyName { get; set; } = "";
    
    /// <summary>
    /// Called when the game officially starts (headquarters placed and named).
    /// </summary>
    public void StartGame(string systemName, string headquartersBodyName)
    {
        SystemName = systemName;
        HeadquartersBodyName = headquartersBodyName;
        IsGameStarted = true;
        
        GameLogger.Info($"[SystemData] Game started: System='{systemName}', HQ on '{headquartersBodyName}'");
    }
}
```

**Acceptance Criteria:**
- [ ] Script attaches to `system_container` node
- [ ] Stores system name, game started flag, and headquarters body name
- [ ] `StartGame()` method properly initializes all fields

---

### Ticket 2: Update BuildingDefinition for Recipe Restrictions and Starting Resources

**File:** `Scripts/Structures/Resources/BuildingDefinition.cs`

**Additions:**

```csharp
public class BuildingDefinition
{
    // ... existing properties ...
    
    /// <summary>
    /// If set, building can only use recipes in this category.
    /// Null or empty means no restriction.
    /// </summary>
    public string? AllowedRecipeCategory { get; set; }
    
    /// <summary>
    /// Resource stockpiles to initialize when building is placed (for starter buildings).
    /// </summary>
    public Dictionary<string, int> StartingStockpiles { get; set; } = new();
    
    /// <summary>
    /// Storage capacity bonuses to add per category.
    /// </summary>
    public Dictionary<string, float> StartingStorageCapacity { get; set; } = new();
}
```

**Acceptance Criteria:**
- [ ] `AllowedRecipeCategory` property added
- [ ] `StartingStockpiles` dictionary added
- [ ] `StartingStorageCapacity` dictionary added

---

### Ticket 3: Create Headquarters YAML with Full Configuration

**New File:** `Configuration/Buildings/Administration/CompanyHeadquarters.yaml`

```yaml
buildings:
  - id_name: company_headquarters
    display_name: Company Headquarters
    description: The nerve center of your interstellar enterprise. Provides power, storage, extraction, and basic manufacturing capabilities.
    category: administration
    building_limit: 1
    building_time: 0
    work_required: 0

    # Only headquarters-specific recipes allowed
    allowed_recipe_category: headquarters

    placement_requirements:
      biomes: []
      min_elevation: 0.0
      max_elevation: 1.0
      max_slope: 90.0
      cell_count: 4
      requires_adjacent: true

    required_resources: {}

    production:
      default_recipe: "hq_all_in_one_operation"
      alternative_recipes: ["hq_power_focus", "hq_extraction_focus", "hq_fabrication_focus"]
      input_storage_amount: 500
      output_storage_amount: 500
      production_speed: 1.0

    # Starting resources - data driven
    starting_stockpiles:
      power: 250
      concrete: 100
      iron: 50
      copper: 30
      water: 200
      grain: 50

    # Storage capacity bonuses
    starting_storage_capacity:
      ore: 500
      raw_material: 500
      fuel: 500
      food: 500
      construction: 500
      industrial: 500

    visual:
      model_path: ""  # Placeholder - uses fallback box mesh
      scale: 2.0
      rotation_offset: [0, 0, 0]
```

**Acceptance Criteria:**
- [ ] YAML file validates without errors
- [ ] `building_limit: 1` is properly set
- [ ] `allowed_recipe_category: headquarters` restricts recipe selection
- [ ] Starting stockpiles and storage capacity configured

---

### Ticket 4: Create Headquarters Recipes

**New File:** `Configuration/Recipes/Headquarters/headquarters_recipes.yaml`

```yaml
recipes:
  # All-in-one operation for early game
  - recipe_id: "hq_all_in_one_operation"
    display_name: "Emergency Operations"
    description: "Combines power generation, material gathering, and basic fabrication using scavenged equipment."
    category: headquarters
    work_required: 10.0
    input_resources: []  # No inputs needed
    output_resources:
      - power: 20
      - concrete: 2
      - iron: 1
      - copper: 1

  # Power focus mode
  - recipe_id: "hq_power_focus"
    display_name: "Emergency Power Focus"
    description: "Diverts all resources to power generation."
    category: headquarters
    work_required: 10.0
    input_resources: []
    output_resources:
      - power: 40

  # Extraction focus mode
  - recipe_id: "hq_extraction_focus"
    display_name: "Material Salvage"
    description: "Focuses on gathering loose materials from the surrounding area."
    category: headquarters
    work_required: 12.0
    input_resources:
      - power: 5
    output_resources:
      - clay: 8
      - iron_ore: 5
      - copper_ore: 3

  # Manufacturing focus mode
  - recipe_id: "hq_fabrication_focus"
    display_name: "Basic Fabrication"
    description: "Processes raw materials into construction supplies."
    category: headquarters
    work_required: 15.0
    input_resources:
      - power: 8
      - clay: 10
      - iron_ore: 5
    output_resources:
      - concrete: 5
      - iron: 3
```

**Acceptance Criteria:**
- [ ] All recipes load via `RecipeConfigLoader`
- [ ] All recipes have `category: headquarters`
- [ ] Recipes provide appropriate early-game outputs

---

### Ticket 5: Update ContinentEconomy for Recipe Restrictions

**File:** `Scripts/Structures/GameState/ContinentEconomy.cs`

**Modify `ChangeRecipe()` method:**

```csharp
/// <summary>
/// Changes the active recipe for a registered building.
/// Validates against building's allowed recipe category if restricted.
/// </summary>
public bool ChangeRecipe(BuildingConstruction building, string newRecipeId)
{
    // Check if building has recipe category restriction
    if (!string.IsNullOrEmpty(building.Definition?.AllowedRecipeCategory))
    {
        if (!RecipeDatabase.Instance.TryGetRecipe(newRecipeId, out var recipe) || 
            recipe == null)
        {
            GameLogger.Warning($"[ContinentEconomy] Recipe '{newRecipeId}' not found");
            return false;
        }
        
        if (recipe.Category != building.Definition.AllowedRecipeCategory)
        {
            GameLogger.Warning(
                $"[ContinentEconomy] Building '{building.Name}' cannot use recipe '{newRecipeId}' - " +
                $"restricted to category '{building.Definition.AllowedRecipeCategory}'"
            );
            return false;
        }
    }
    
    UnregisterBuilding(building);
    RegisterBuilding(building, newRecipeId);
    return true;
}
```

**Acceptance Criteria:**
- [ ] Headquarters can only use recipes with `category: headquarters`
- [ ] Non-restricted buildings can use any recipe
- [ ] Returns `false` and logs warning when restriction violated

---

### Ticket 6: Create Ramshackle Builder with Upgrade Path

**New File:** `Scripts/Constructables/ArtificialSatellites/RamshackleBuilderStation.cs`

```csharp
using Godot;
using UtilityLibrary;

namespace Constructables.ArtificialSatellites;

/// <summary>
/// Starter orbital architect station. Can be upgraded to normal OrbitalArchitectStation.
/// </summary>
public partial class RamshackleBuilderStation : OrbitalArchitectStation
{
    [Export]
    public bool IsUpgraded { get; private set; } = false;
    
    private float _wasteFactor = 1.5f;
    private float _slowFactor = 0.3f; // 30% speed of normal
    
    public override void SetStationDefinition(StationDefinition definition)
    {
        // Apply waste and speed penalties
        var modifiedDef = new StationDefinition
        {
            Name = definition.Name,
            StationType = definition.StationType,
            ConstructionTime = definition.ConstructionTime,
            CanBuildShips = definition.CanBuildShips,
            CanBuildBuildings = definition.CanBuildBuildings,
            BuildingWorkBudgetPerTick = definition.BuildingWorkBudgetPerTick * _slowFactor,
            BuildingScalingPenalty = definition.BuildingScalingPenalty * 2, // Worse penalty
            RequiredResources = definition.RequiredResources
        };
        
        base.SetStationDefinition(modifiedDef);
        
        GameLogger.Info($"[RamshackleBuilderStation] Initialized at {_slowFactor:P0} speed with {_wasteFactor}x waste");
    }
    
    public override void RegisterBuildingConstruction(BuildingConstruction building)
    {
        if (building?.Definition != null && !IsUpgraded)
        {
            // Apply waste to work required
            building.workRequired *= _wasteFactor;
        }
        
        base.RegisterBuildingConstruction(building);
    }
    
    /// <summary>
    /// Upgrades this station to normal OrbitalArchitectStation performance.
    /// </summary>
    public void Upgrade()
    {
        if (IsUpgraded)
        {
            GameLogger.Warning("[RamshackleBuilderStation] Already upgraded");
            return;
        }
        
        IsUpgraded = true;
        _wasteFactor = 1.0f;
        _slowFactor = 1.0f;
        
        // Recreate work budget with normal stats
        if (_stationDefinition != null)
        {
            SetStationDefinition(_stationDefinition);
        }
        
        GameLogger.Info("[RamshackleBuilderStation] Upgraded to full functionality!");
        
        // Emit signal for UI notification
        SignalBus.Instance?.EmitSignal(
            nameof(SignalBus.StationUpgraded),
            this
        );
    }
    
    public override void _EnterTree()
    {
        base._EnterTree();
        StationDatabase.Instance?.MarkGloballyPlaced("Ramshackle_Builder");
    }
}
```

**Update SignalBus (`Scripts/UtilityLibrary/SignalBus.cs`):**

```csharp
[Signal]
public delegate void StationUpgradedEventHandler(StationSatellite station);
```

**Acceptance Criteria:**
- [ ] Extends `OrbitalArchitectStation`
- [ ] Construction speed is 30% of normal
- [ ] Waste factor increases building work required by 50%
- [ ] `Upgrade()` method restores normal performance
- [ ] Marked as globally placed on creation

---

### Ticket 7: Create Ramshackle Builder Station YAML

**New File:** `Configuration/stations/RamshackleBuilder.yaml`

```yaml
stations:
  - name: Ramshackle_Builder
    station_type: Ramshackle_Builder
    construction_time: 0
    can_build_ships: false
    can_build_buildings: true
    building_work_budget_per_tick: 1.0
    building_scaling_penalty: 0.1
    required_resources: {}
```

**Acceptance Criteria:**
- [ ] YAML file validates without errors
- [ ] `station_type: Ramshackle_Builder` maps to correct C# class
- [ ] No resources required for construction

---

### Ticket 8: Create Game Start Controller

**New File:** `Scripts/UI/GameStart/GameStartController.cs`

```csharp
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary;

namespace UI.GameStart;

/// <summary>
/// Manages the game start sequence triggered by headquarters placement.
/// Prompts player to name the settled body and the system.
/// </summary>
public partial class GameStartController : Control
{
    [Signal]
    public delegate void GameStartCompletedEventHandler(string bodyName, string systemName);
    
    private CelestialBody? _settledBody;
    private SystemData? _systemData;
    private LineEdit? _bodyNameInput;
    private LineEdit? _systemNameInput;
    private Button? _confirmButton;
    
    public override void _Ready()
    {
        // Get system data
        var systemContainer = GetNodeOrNull<Node>("/root/GameScene/system_container");
        if (systemContainer != null)
        {
            _systemData = systemContainer.GetNodeOrNull<SystemData>("SystemData");
            if (_systemData == null)
            {
                // Add SystemData if not present
                _systemData = new SystemData();
                systemContainer.AddChild(_systemData);
            }
        }
        
        // Connect UI elements
        _bodyNameInput = GetNodeOrNull<LineEdit>("%BodyNameInput");
        _systemNameInput = GetNodeOrNull<LineEdit>("%SystemNameInput");
        _confirmButton = GetNodeOrNull<Button>("%ConfirmButton");
        
        if (_confirmButton != null)
            _confirmButton.Pressed += OnConfirmPressed;
        
        // Connect to signal
        SignalBus.Instance?.Connect(
            nameof(SignalBus.GameStartHeadquartersPlaced),
            new Callable(this, nameof(OnHeadquartersPlaced))
        );
        
        Visible = false;
    }
    
    private void OnHeadquartersPlaced(CelestialBody body)
    {
        if (_systemData?.IsGameStarted == true)
            return; // Game already started
            
        _settledBody = body;
        
        // Generate default names
        string defaultBodyName = UtilityLibrary.NameGeneration.NameGenerator.GeneratePlanetName();
        string defaultSystemName = UtilityLibrary.NameGeneration.NameGenerator.GenerateSystemName();
        
        // Show dialog
        ShowNamingDialog(defaultBodyName, defaultSystemName);
    }
    
    private void ShowNamingDialog(string defaultBodyName, string defaultSystemName)
    {
        if (_bodyNameInput != null)
            _bodyNameInput.Text = defaultBodyName;
        if (_systemNameInput != null)
            _systemNameInput.Text = defaultSystemName;
            
        Visible = true;
        
        // Pause game
        GetTree().Paused = true;
    }
    
    private void OnConfirmPressed()
    {
        string bodyName = _bodyNameInput?.Text?.Trim() ?? "";
        string systemName = _systemNameInput?.Text?.Trim() ?? "";
        
        if (string.IsNullOrEmpty(bodyName) || string.IsNullOrEmpty(systemName))
        {
            // Show validation error
            return;
        }
        
        // Apply names
        if (_settledBody != null)
        {
            _settledBody.Name = bodyName;
            _settledBody.BodyName = bodyName;
        }
        
        // Start game
        _systemData?.StartGame(systemName, bodyName);
        
        // Hide and unpause
        Visible = false;
        GetTree().Paused = false;
        
        EmitSignal(SignalName.GameStartCompleted, bodyName, systemName);
        
        ToastSystem.Instance?.Show($"Welcome to the {systemName} system!");
    }
}
```

**Acceptance Criteria:**
- [ ] Connects to `GameStartHeadquartersPlaced` signal
- [ ] Shows naming dialog with default names
- [ ] Pauses game while dialog is open
- [ ] Updates `SystemData` on confirm
- [ ] Shows toast notification on completion

---

### Ticket 9: Create GameStart UI Scene

**New File:** `UI/GameStart/GameStartDialog.tscn`

Scene hierarchy:
```
Control (GameStartController)
├── PanelContainer (centered, full screen overlay)
    ├── ColorRect (semi-transparent background)
    ├── CenterContainer
        ├── PanelContainer (dialog box)
            ├── MarginContainer
                ├── VBoxContainer
                    ├── Label: "Establish Your Company"
                    ├── Label: "Body Name"
                    ├── LineEdit: %BodyNameInput
                    ├── Label: "System Name"
                    ├── LineEdit: %SystemNameInput
                    ├── Button: %ConfirmButton ("Begin Operations")
```

**Acceptance Criteria:**
- [ ] Dialog appears centered on screen
- [ ] Background is semi-transparent/dimmed
- [ ] Input fields have unique name references
- [ ] Confirm button triggers confirmation

---

### Ticket 10: Update SignalBus for New Signals

**File:** `Scripts/UtilityLibrary/SignalBus.cs`

**Additions:**

```csharp
/// <summary>
/// Fired when company headquarters is placed, triggering game start sequence.
/// Parameters: CelestialBody (the body headquarters was placed on)
/// </summary>
[Signal]
public delegate void GameStartHeadquartersPlacedEventHandler(CelestialBody body);

public void EmitGameStartHeadquartersPlaced(CelestialBody body)
{
    EmitSignal(SignalName.GameStartHeadquartersPlaced, body);
}

/// <summary>
/// Fired when a station is upgraded (e.g., Ramshackle Builder).
/// Parameters: StationSatellite (the upgraded station)
/// </summary>
[Signal]
public delegate void StationUpgradedEventHandler(StationSatellite station);

public void EmitStationUpgraded(StationSatellite station)
{
    EmitSignal(SignalName.StationUpgraded, station);
}
```

**Acceptance Criteria:**
- [ ] `GameStartHeadquartersPlaced` signal added
- [ ] `StationUpgraded` signal added
- [ ] Both signals have proper emit methods

---

### Ticket 11: Update HeadquartersBuilding with YAML-Driven Initialization

**New File:** `Scripts/Constructables/HeadquartersBuilding.cs`

```csharp
using System.Collections.Generic;
using Godot;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Special headquarters building for game start. Provides all essential services.
/// </summary>
public partial class HeadquartersBuilding : BuildingConstruction
{
    [Export]
    public bool IsGameStarted { get; private set; } = false;
    
    public override void _Ready()
    {
        base._Ready();
        
        BuildingDatabase.Instance?.MarkGloballyPlaced("company_headquarters");
    }
    
    /// <summary>
    /// Initializes headquarters using data from BuildingDefinition YAML.
    /// </summary>
    public void InitializeHeadquarters(CelestialBody parentBody, int continentIndex)
    {
        if (parentBody?.Mesh?.Continents == null)
            return;
            
        if (!parentBody.Mesh.Continents.TryGetValue(continentIndex, out var continent))
            return;
            
        continent.InitializeEconomy();
        
        if (continent.Economy == null)
            return;
        
        // Initialize stockpiles from YAML configuration
        foreach (var kvp in Definition?.StartingStockpiles ?? new())
        {
            continent.Economy.DepositResource(kvp.Key, kvp.Value);
        }
        
        // Add storage capacity bonuses from YAML
        foreach (var kvp in Definition?.StartingStorageCapacity ?? new())
        {
            continent.Economy.AddStorageCapacity(kvp.Key, kvp.Value);
        }
        
        GameLogger.Info($"[HeadquartersBuilding] Initialized on {parentBody.Name}");
        
        // Trigger game start
        if (!IsGameStarted)
        {
            TriggerGameStart(parentBody);
        }
    }
    
    private void TriggerGameStart(CelestialBody parentBody)
    {
        IsGameStarted = true;
        SignalBus.Instance?.EmitGameStartHeadquartersPlaced(parentBody);
    }
    
    /// <summary>
    /// Headquarters cannot be demolished.
    /// </summary>
    public override bool CanDemolish() => false;
}
```

**Acceptance Criteria:**
- [ ] Extends `BuildingConstruction`
- [ ] Initializes stockpiles from YAML `starting_stockpiles`
- [ ] Adds storage capacity from YAML `starting_storage_capacity`
- [ ] Triggers game start signal
- [ ] Cannot be demolished

---

### Ticket 12: Update ConstructionManager for Headquarters Creation

**File:** `Scripts/Constructables/ConstructionManager.cs`

**Additions:**

```csharp
/// <summary>
/// Creates the company headquarters building - can only be called once per game.
/// </summary>
public HeadquartersBuilding? CreateHeadquarters(
    VoronoiCell primaryCell,
    Node3D parentBody,
    List<VoronoiCell>? additionalCells = null)
{
    if (BuildingDatabase.Instance?.IsGloballyPlaced("company_headquarters") == true)
    {
        GameLogger.Warning("[ConstructionManager] Headquarters already exists");
        return null;
    }
    
    if (!BuildingDatabase.Instance.TryGetBuilding("company_headquarters", out var definition))
    {
        GameLogger.Error("[ConstructionManager] Headquarters definition not found");
        return null;
    }
    
    var building = new HeadquartersBuilding();
    parentBody.AddChild(building);
    
    building.SetBuildingDefinition(definition);
    building.SetPlacement(primaryCell, additionalCells, parentBody);
    building.Visible = true;
    
    // Register with economy
    RegisterHeadquartersWithEconomy(building);
    
    EmitBuildingConstruct(building, new Dictionary
    {
        { "building", building },
        { "name", building.Name.ToString() },
        { "is_headquarters", true }
    });
    
    return building;
}

private void RegisterHeadquartersWithEconomy(HeadquartersBuilding building)
{
    var parentBody = building.GetParent() as CelestialBody;
    if (parentBody?.Mesh?.Continents == null || building.PrimaryCell == null)
        return;
        
    int continentIdx = building.PrimaryCell.ContinentIndex;
    if (continentIdx < 0 || !parentBody.Mesh.Continents.TryGetValue(continentIdx, out var continent))
        return;
        
    continent.InitializeEconomy();
    TransferManager.Instance?.RegisterContinentEndpoint(continentIdx, continent.Economy!);
    
    string recipeId = building.Definition?.Production?.DefaultRecipe ?? "";
    if (!string.IsNullOrEmpty(recipeId))
    {
        continent.Economy!.RegisterBuilding(building, recipeId);
        building.ActiveRecipeId = recipeId;
    }
    
    building.InitializeHeadquarters(parentBody, continentIdx);
}
```

**Update `CreateStationInstance()`:**

```csharp
private static StationSatellite CreateStationInstance(string name, StationDefinition? definition)
{
    if (definition?.CanBuildShips == true)
        return new ConstructionYardStation { Name = name };

    if (definition?.StationType == "Ramshackle_Builder")
        return new RamshackleBuilderStation { Name = name };

    if (definition?.CanBuildBuildings == true)
        return new OrbitalArchitectStation { Name = name };

    return new StationSatellite { Name = name };
}
```

**Acceptance Criteria:**
- [ ] `CreateHeadquarters()` method added
- [ ] Returns null if headquarters already exists
- [ ] Properly registers with continent economy
- [ ] Initializes headquarters with YAML data
- [ ] `CreateStationInstance()` handles `Ramshackle_Builder` type

---

### Ticket 13: Update BuildingDatabase for Global Placement Tracking

**File:** `Scripts/Structures/Resources/BuildingDatabase.cs`

**Additions:**

```csharp
public partial class BuildingDatabase : ILoadableDatabase
{
    // ... existing code ...
    
    // Add tracking for globally-limited buildings
    private readonly HashSet<string> _globallyPlacedBuildings = new();
    
    /// <summary>
    /// Checks if a building with a global limit has already been placed.
    /// </summary>
    public bool IsGloballyPlaced(string buildingId) => 
        _globallyPlacedBuildings.Contains(buildingId);
    
    /// <summary>
    /// Marks a building as globally placed (for buildings with BuildingLimit = 1).
    /// </summary>
    public void MarkGloballyPlaced(string buildingId)
    {
        if (!string.IsNullOrEmpty(buildingId))
            _globallyPlacedBuildings.Add(buildingId);
    }
    
    /// <summary>
    /// Resets global placement tracking (for new game).
    /// </summary>
    public void ResetGlobalPlacements() => _globallyPlacedBuildings.Clear();
    
    /// <summary>
    /// Validates if placement is allowed considering global limits.
    /// </summary>
    public bool ValidateGlobalPlacement(string buildingId)
    {
        if (!TryGetBuilding(buildingId, out var def) || def == null)
            return false;
            
        if (def.BuildingLimit == 1 && IsGloballyPlaced(buildingId))
            return false;
            
        return true;
    }
}
```

**Acceptance Criteria:**
- [ ] Global placement tracking added
- [ ] `ValidateGlobalPlacement()` checks building limit
- [ ] `ResetGlobalPlacements()` clears tracking for new game

---

### Ticket 14: Update StationDatabase for Global Placement Tracking

**File:** `Scripts/Logistics/Resources/StationDatabase.cs`

**Additions:**

```csharp
public partial class StationDatabase : ILoadableDatabase
{
    // ... existing code ...
    
    // Add tracking for globally-limited stations
    private readonly HashSet<string> _globallyPlacedStations = new();
    
    public bool IsGloballyPlaced(string stationName) => 
        _globallyPlacedStations.Contains(stationName);
    
    public void MarkGloballyPlaced(string stationName)
    {
        if (!string.IsNullOrEmpty(stationName))
            _globallyPlacedStations.Add(stationName);
    }
    
    public void ResetGlobalPlacements() => _globallyPlacedStations.Clear();
    
    /// <summary>
    /// Checks if a station can be built considering global limits.
    /// </summary>
    public bool ValidateGlobalPlacement(string stationName)
    {
        // Ramshackle_Builder can only be built once
        if (stationName == "Ramshackle_Builder" && IsGloballyPlaced(stationName))
            return false;
            
        return true;
    }
}
```

**Acceptance Criteria:**
- [ ] Global placement tracking for stations added
- [ ] `Ramshackle_Builder` limited to one per game

---

### Ticket 15: Update BuildingPlacementMode for Validation

**File:** `Scripts/UI/Construction/BuildingPlacementMode.cs`

**Update `OnPlacementClick()`:**

```csharp
private void OnPlacementClick()
{
    GD.Print("OnPlacementClick");
    if (_hoveredCell == null || _hoveredBodyNode == null)
        return;

    // Check global placement limits
    string buildingId = _definition.IdName!;
    if (!BuildingDatabase.Instance.ValidateGlobalPlacement(buildingId))
    {
        ToastSystem.Instance?.Show($"{_definition.DisplayName} has already been built");
        return;
    }

    if (_allCellsValid)
    {
        var additionalCells = new Godot.Collections.Array<VoronoiCell>();
        for (int i = 1; i < _selectedCells.Count; i++)
            additionalCells.Add(_selectedCells[i]);

        GD.Print("Construction started");
        ToastSystem.Instance?.Show("Construction started");
        EmitSignal(
            SignalName.PlacementConfirmed,
            _hoveredCell,
            _hoveredBodyNode,
            additionalCells
        );
    }
    else
    {
        ToastSystem.Instance?.Show("Construction blocked: placement requirements not met");
        GD.Print("Construction blocked: placement requirements not met");
    }
}
```

**Acceptance Criteria:**
- [ ] Shows toast if building limit reached
- [ ] Prevents placement of duplicate limited buildings

---

### Ticket 16: Update GameScene to Add GameStartController

**File:** `Scripts/GameScene.cs`

**Update `_Ready()`:**

```csharp
public override void _Ready()
{
    GameLogger.EnterFunction(nameof(_Ready));

    // Instantiate Construction HUD
    var constructionHudScene = GD.Load<PackedScene>(
        "res://UI/Construction/ConstructionHUD.tscn"
    );
    if (constructionHudScene != null)
    {
        AddChild(constructionHudScene.Instantiate());
        GameLogger.Debug("ConstructionHUD added to GameScene");
    }

    // Instantiate Main Game UI (includes ToastSystem)
    var mainGameUIScene = GD.Load<PackedScene>("res://UI/MainGameUI.tscn");
    if (mainGameUIScene != null)
    {
        AddChild(mainGameUIScene.Instantiate());
        GameLogger.Debug("MainGameUI added to GameScene");
    }

    // Instantiate Game Start Controller (for naming flow)
    var gameStartScene = GD.Load<PackedScene>("res://UI/GameStart/GameStartDialog.tscn");
    if (gameStartScene != null)
    {
        AddChild(gameStartScene.Instantiate());
        GameLogger.Debug("GameStartController added to GameScene");
    }

    // Ensure system_container has SystemData
    var systemContainer = GetNodeOrNull<Node>("system_container");
    if (systemContainer != null)
    {
        // Add SystemData if not present
        if (systemContainer.GetNodeOrNull<SystemData>("SystemData") == null)
        {
            var systemData = new SystemData { Name = "SystemData" };
            systemContainer.AddChild(systemData);
            GameLogger.Debug("SystemData added to system_container");
        }
        
        int bodyCount = systemContainer.GetChildCount();
        if (bodyCount > 0)
        {
            GameLogger.Info($"GameScene loaded with {bodyCount} bodies in system_container");
        }
        else
        {
            GameLogger.Warning("GameScene system_container is empty - no bodies were generated");
        }
    }
    else
    {
        GameLogger.Error("GameScene system_container not found");
    }

    // Clear any template selection (handled by LoadingScreen)
    if (SignalBus.Instance != null)
    {
        SignalBus.Instance.SelectedTemplate = null;
    }

    GameLogger.Info("Game scene loaded (bodies generated by LoadingScreen)");
    GameLogger.ExitFunction(nameof(_Ready));
}
```

**Acceptance Criteria:**
- [ ] GameStartController instantiated from scene
- [ ] SystemData added to system_container if missing

---

## Implementation Order

| Order | Ticket | Description | Dependencies |
|-------|--------|-------------|--------------|
| 1 | 1 | SystemData script | None |
| 2 | 2 | BuildingDefinition updates | None |
| 3 | 3, 4, 7 | YAML configs (HQ, recipes, Ramshackle) | Ticket 2 |
| 4 | 5 | ContinentEconomy recipe restriction | Ticket 2 |
| 5 | 10 | SignalBus new signals | None |
| 6 | 11 | HeadquartersBuilding class | Tickets 3, 10 |
| 7 | 6 | RamshackleBuilderStation | Ticket 10 |
| 8 | 13, 14 | Database global tracking | None |
| 9 | 12 | ConstructionManager updates | Tickets 6, 11, 13, 14 |
| 10 | 9 | GameStart UI Scene | None |
| 11 | 8 | GameStartController | Tickets 1, 9, 10 |
| 12 | 15 | Placement validation | Ticket 13 |
| 13 | 16 | GameScene updates | Tickets 1, 8 |

---

## Testing Checklist

### Functional Tests
- [ ] Company Headquarters can be placed once and only once
- [ ] Ramshackle Builder can be placed once and only once
- [ ] Headquarters starts with configured resources from YAML
- [ ] Headquarters provides storage capacity bonuses
- [ ] Headquarters can only use `headquarters` category recipes
- [ ] Recipe change validation works correctly
- [ ] Game start dialog appears on headquarters placement
- [ ] System and body names are stored in SystemData
- [ ] Ramshackle Builder constructs buildings slower than normal
- [ ] Ramshackle Builder Upgrade() restores normal speed

### Integration Tests
- [ ] Building placement flow works end-to-end
- [ ] Economy integration (stockpiles update correctly)
- [ ] Transfer system integration (headquarters as endpoint)
- [ ] UI flow (placement → naming → game start)

### Edge Cases
- [ ] Attempting to place second headquarters shows error
- [ ] Closing game during naming flow (handled on restart)
- [ ] Loading saved game (SystemData persists)

---

## Notes

### Visual Placeholders
Both the Company Headquarters and Ramshackle Builder use placeholder visuals:
- **Headquarters**: Falls back to box mesh (0.5, 0.5, 0.5) with construction material
- **Ramshackle Builder**: Uses standard station cylinder mesh

To add proper visuals later:
1. Create models and place in `res://Models/Buildings/`
2. Update YAML `visual.model_path` fields

### Future Enhancements
1. **Upgrade UI**: Add interface for upgrading Ramshackle Builder
2. **Headquarters Expansion**: Allow upgrading headquarters capabilities
3. **Starter Pack Variations**: Different starting resource configurations
4. **Tutorial Integration**: Link game start flow with tutorial system
