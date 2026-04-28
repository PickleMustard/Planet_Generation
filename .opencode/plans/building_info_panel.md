# Building Information Panel - Implementation Plan

## User Clarifications Applied

| Question | Answer | Implementation Impact |
|----------|--------|----------------------|
| Storage buildings | Dedicated buildings with category + max capacity | `StorageDetails` will read from `BuildingDefinition.StorageCategory` and `BuildingDefinition.StorageCapacity` |
| Icons | All items have `IconPath` property | Header and displays will use `TextureLoader` with the IconPath |
| Donut chart style | RAG transition, configurable direction, donut style | `DonutChart` will have `ColorMode` enum (GreenToRed/RedToGreen) and `InnerRadiusRatio` |
| Recipe icons | Same as icons answer | `RecipeDisplay` uses `RecipeDefinition.IconPath` |
| Upgrade button | Create SignalBus signal | Add `EmitBuildingUpgradeRequested(int buildingInstanceId)` to SignalBus |
| Panel size | Flexible/responsive | Use `SizeFlags` (Expand/Fill), min size constraints, avoid fixed sizes |
| Integration | Purely data-driven | Public `SetBuilding(BuildingConstruction, ContinentEconomy)` method only |
| Testing | Not required | No test files needed |

---

## Architecture

### Component Hierarchy
```
BuildingInfoPanel (PanelContainer)
├── MarginContainer
│   └── VBoxContainer (main layout)
│       ├── BuildingInfoHeader (HBoxContainer)
│       │   ├── BuildingIcon (TextureRect)
│       │   ├── BuildingNameLabel (Label)
│       │   ├── UpgradeButton (Button)
│       │   └── DemolishButton (Button)
│       │
│       └── DetailsContainer (VBoxContainer)
│           └── DynamicDetailView (instance swapped by category)
│               ├── ExtractionDetails
│               ├── ManufacturingDetails
│               ├── PowerDetails
│               └── StorageDetails
```

### Data Flow
```
Parent Panel
    ↓ calls SetBuilding(building, economy)
BuildingInfoPanel
    ↓ reads building.Definition.Category
    ↓ instantiates appropriate details scene
    ↓ passes building + economy to details view
DynamicDetailView (category-specific)
    ↓ queries BuildingRegistration from economy
    ↓ displays data + subscribes to updates
```

---

## Files to Create

### 1. Core Panel Components

| File | Type | Description |
|------|------|-------------|
| `UI/BuildingInfo/BuildingInfoPanel.tscn` | Scene | Main panel container with margin and layout |
| `Scripts/UI/BuildingInfo/BuildingInfoPanel.cs` | Script | Main controller, category routing, public API |
| `UI/BuildingInfo/BuildingInfoHeader.tscn` | Scene | Header with icon, name, upgrade/demolish buttons |
| `Scripts/UI/BuildingInfo/BuildingInfoHeader.cs` | Script | Header controller, button signal emission |

### 2. Donut Chart Component

| File | Type | Description |
|------|------|-------------|
| `UI/Components/DonutChart.tscn` | Scene | Donut chart scene (minimal, mainly script) |
| `Scripts/UI/Components/DonutChart.cs` | Script | Custom Control with `_Draw()` override for RAG donut |

### 3. Detail Section Variants

| File | Type | Description |
|------|------|-------------|
| `UI/BuildingInfo/ExtractionDetails.tscn` | Scene | Recipe left, output resource + rate + donut right |
| `Scripts/UI/BuildingInfo/ExtractionDetails.cs` | Script | Extraction-specific display logic |
| `UI/BuildingInfo/ManufacturingDetails.tscn` | Scene | Recipe left, inputs/outputs + donuts right |
| `Scripts/UI/BuildingInfo/ManufacturingDetails.cs` | Script | Manufacturing display with multiple I/O rows |
| `UI/BuildingInfo/PowerDetails.tscn` | Scene | Recipe + power generation + input resources |
| `Scripts/UI/BuildingInfo/PowerDetails.cs` | Script | Power-specific display, handles fuel consumption |
| `UI/BuildingInfo/StorageDetails.tscn` | Scene | Category, capacity donut, scrollable resource list |
| `Scripts/UI/BuildingInfo/StorageDetails.cs` | Script | Storage building display, reads from stockpile |

### 4. Shared Sub-Components

| File | Type | Description |
|------|------|-------------|
| `UI/BuildingInfo/RecipeDisplay.tscn` | Scene | Horizontal row with recipe icon + name |
| `Scripts/UI/BuildingInfo/RecipeDisplay.cs` | Script | Recipe display controller |
| `UI/BuildingInfo/ResourceRateItem.tscn` | Scene | Resource icon + name + rate label |
| `Scripts/UI/BuildingInfo/ResourceRateItem.cs` | Script | Rate display row (consumption/production) |
| `UI/BuildingInfo/ResourceStorageItem.tscn` | Scene | Resource icon + name + amount + mini-donut |
| `Scripts/UI/BuildingInfo/ResourceStorageItem.cs` | Script | Storage list item for scrollable view |

### 5. SignalBus Updates

| File | Change | Description |
|------|--------|-------------|
| `Scripts/UtilityLibrary/SignalBus.cs` | Add signals | `BuildingUpgradeRequested`, `BuildingDemolishRequested` |

---

## Class Specifications

### BuildingInfoPanel.cs
```csharp
public partial class BuildingInfoPanel : PanelContainer
{
    [Export] public BuildingInfoHeader? Header;
    [Export] public VBoxContainer? DetailsContainer;
    [Export] public PackedScene? ExtractionDetailsScene;
    [Export] public PackedScene? ManufacturingDetailsScene;
    [Export] public PackedScene? PowerDetailsScene;
    [Export] public PackedScene? StorageDetailsScene;
    
    private BuildingConstruction? _currentBuilding;
    private ContinentEconomy? _economy;
    private BaseBuildingDetails? _currentDetails;
    
    // Public API - called by parent panel
    public void SetBuilding(BuildingConstruction building, ContinentEconomy economy)
    
    // Private
    private void ShowDetailsForCategory(string? category)
    private void ClearCurrentDetails()
    private void OnUpgradeRequested()
    private void OnDemolishRequested()
}
```

### DonutChart.cs
```csharp
public partial class DonutChart : Control
{
    public enum ColorMode { GreenToRed, RedToGreen }
    
    [Export] public Color BackgroundColor { get; set; } = new Color(0.15f, 0.15f, 0.15f);
    [Export] public ColorMode Mode { get; set; } = ColorMode.GreenToRed;
    [Export] public float InnerRadiusRatio { get; set; } = 0.65f;
    [Export] public float Value { get; set; } = 0.0f; // 0.0 to 1.0
    
    public override void _Draw()
    {
        // Draw background ring
        // Calculate fill color based on Value and Mode
        // Draw filled arc from -90 degrees clockwise
    }
}
```

### BuildingRegistration Access
All detail views receive `BuildingRegistration` via:
```csharp
var registration = economy.GetActiveBuildings()
    .FirstOrDefault(b => b.BuildingNode == building);
```

---

## Layout Specifications

### Header Section
- **Container**: `HBoxContainer` with `theme_override_constants/separation = 12`
- **Building Icon**: 48x48px `TextureRect`, `expand_mode = FitWidthProportional`
- **Name Label**: `Label` with `size_flags_horizontal = Expand | Fill`
- **Buttons**: 36x36px `Button` with icon only, `flat = true`

### ExtractionDetails Layout
```
HBoxContainer (split proportionally)
├── Left (40%): VBoxContainer
│   ├── RecipeDisplay (icon 32x32 + name)
│   └── DescriptionLabel (small, wrapped)
└── Right (60%): VBoxContainer
    ├── HBoxContainer: OutputResourceIcon + Name
    ├── RateLabel: "12.5 units/sec"
    └── DonutChart (storage fill)
```

### ManufacturingDetails Layout
```
HBoxContainer (split 40/60)
├── Left: RecipeDisplay + Description
└── Right: VBoxContainer
    ├── InputsSection (VBoxContainer)
    │   ├── SectionLabel "Inputs"
    │   └── ResourceRateItem[] (icon + name + "-X/sec")
    ├── OutputsSection (VBoxContainer)
    │   ├── SectionLabel "Outputs"
    │   └── ResourceRateItem[] (icon + name + "+X/sec")
    └── DonutChartsHBox (one per output resource)
```

### PowerDetails Layout
```
VBoxContainer
├── RecipeDisplay
├── PowerOutputLabel (large): "⚡ Generates: 450 kW"
├── Separator
└── InputsSection (visible only if inputs exist)
    └── ResourceRateItem[] (fuel/resources)
```

### StorageDetails Layout
```
VBoxContainer
├── CategoryLabel: "Storage: Raw Materials"
├── CapacityRow: DonutChart (used/total) + Label "450/1000"
├── Separator
└── ScrollContainer (size_flags_vertical = Expand | Fill)
    └── VBoxContainer
        └── ResourceStorageItem[] (icon + name + amount + mini-donut)
```

---

## SignalBus Additions

Add to `SignalBus.cs`:
```csharp
[Signal]
public delegate void BuildingUpgradeRequestedEventHandler(int buildingInstanceId);

[Signal]
public delegate void BuildingDemolishRequestedEventHandler(int buildingInstanceId);

public void EmitBuildingUpgradeRequested(int buildingInstanceId)
    => EmitSignal(SignalName.BuildingUpgradeRequested, buildingInstanceId);

public void EmitBuildingDemolishRequested(int buildingInstanceId)
    => EmitSignal(SignalName.BuildingDemolishRequested, buildingInstanceId);
```

---

## Building Category to Detail View Mapping

| Category String | Detail Scene | Data Display |
|-----------------|--------------|--------------|
| `extraction` | ExtractionDetails | Single output resource, extraction rate, deposit yield multiplier if applicable |
| `agriculture` | ExtractionDetails | Same as extraction (agriculture produces food resources) |
| `manufacturing` | ManufacturingDetails | Multiple inputs/outputs, production rates, storage per output |
| `power` | PowerDetails | Power generation capacity, fuel consumption (if not renewable) |
| `storage` | StorageDetails | Category filter, capacity used/total, scrollable inventory |
| `logistics` | (future) | Not in scope for this ticket |
| `administration` | (bespoke) | Not in scope per requirements |

---

## Usage Example

```csharp
// In parent panel (e.g., BuildingManagementWindow)
[Export] public BuildingInfoPanel? BuildingInfoPanel;

public void OnBuildingSelected(BuildingConstruction building)
{
    var continent = building.GetParent<Continent>();
    var economy = continent?.GetEconomy();
    
    if (BuildingInfoPanel != null && economy != null)
    {
        BuildingInfoPanel.SetBuilding(building, economy);
    }
}

// Connect to signals for upgrade/demolish
public override void _Ready()
{
    SignalBus.Instance.BuildingUpgradeRequested += OnUpgradeRequested;
    SignalBus.Instance.BuildingDemolishRequested += OnDemolishRequested;
}
```

---

## Dependencies

- `BuildingConstruction` (existing)
- `BuildingDefinition` (existing, needs `IconPath` property)
- `RecipeDefinition` (existing, needs `IconPath` property)
- `ContinentEconomy` (existing)
- `BuildingRegistration` (existing inner class)
- `ResourceDatabase` (existing)
- `RecipeDatabase` (existing)
- `SignalBus` (existing, needs new signals)
- `GameLogger` (existing, for debug logging)

---

## Responsive Behavior

- Panel uses `SizeFlags` for expansion
- Min size: 350x400px (prevents crushing)
- Max width: 600px (prevents excessive stretching)
- ScrollContainer for storage resource list (max height before scrolling: 300px)
- Detail sections use `separation` constants for consistent spacing

---

## Implementation Notes

1. **BaseBuildingDetails Abstract Class**: Create an abstract base class that all detail views inherit from, providing common functionality like getting BuildingRegistration from economy.

2. **Icon Loading**: Use Godot's `ResourceLoader.Load<Texture2D>(iconPath)` with null checks. Cache textures where appropriate.

3. **Rate Formatting**: Display rates as "X.Y units/sec" or use unit-appropriate formatting (e.g., "kW" for power).

4. **Donut Chart Performance**: Use `_Draw()` efficiently - only call `QueueRedraw()` when Value actually changes.

5. **Null Safety**: All detail views must gracefully handle null data (building not yet registered in economy, missing icons, etc.).

6. **Category Detection**: Case-insensitive category string matching (extraction, Extraction, EXTRACTION all map to same view).

7. **Storage Buildings**: These buildings have special handling - they don't produce/consume via recipes but instead provide capacity bonuses and store resources directly.
