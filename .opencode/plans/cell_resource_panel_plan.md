# Implementation Plan: Cell Resource Panel

## Overview
A reusable UI sub-panel that displays the natural resources available in a selected Voronoi cell. Shows a list of resources with their names, visual color indicators, and abundance levels. This panel is designed as a sub-component of a larger GUI element and does not manage its own visibility or lifecycle.

## Architecture Decisions
- **Sub-panel Design**: No header, close button, or collapsible features - pure data display
- **Data Binding**: Connects to `CellSelectionManager.CellSelected` signal for updates
- **Passive Display**: Parent container manages visibility; panel only updates content when visible
- **Resource Definition Lookup**: Uses `ResourceDatabase` to get display names and colors
- **No Resources State**: Shows a friendly message when the cell has no natural resources
- **Sorted Display**: Resources sorted by abundance (highest first)

---

## Files to Create

### 1. UI/CellInfo/ResourceListItem.tscn
**Purpose**: Scene for a single resource row item (reusable list element)

**Node Hierarchy**:
```
ResourceListItem (HBoxContainer)
├── ColorRect (color swatch, 16x16, margin_top/bottom=2)
├── ResourceNameLabel (Label): "Resource Name" (expand, left-aligned)
└── AbundanceLabel (Label): "85%" (min-width: 50px, right-aligned)
```

**Control Settings**:
- `ResourceListItem`: Custom minimum size 200x24, separation=8
- `ColorRect`: Custom minimum size 16x16, layout_vertical_center
- `ResourceNameLabel`: Size flags horizontal expand + fill
- `AbundanceLabel`: Custom minimum size x=50, horizontal alignment right

---

### 2. Scripts/UI/CellInfo/ResourceListItem.cs
**Purpose**: Behavior script for a single resource list item

**Namespace**: `UI.CellInfo`

**Public API**:
```csharp
public partial class ResourceListItem : HBoxContainer
{
    /// <summary>
    /// Sets up the resource item with the given resource data.
    /// </summary>
    /// <param name="resourceId">The resource identifier</param>
    /// <param name="abundance">Abundance value 0-1</param>
    /// <param name="definition">Optional resource definition for display info</param>
    public void SetResource(string resourceId, float abundance, ResourceDefinition? definition);
    
    /// <summary>
    /// Clears the item back to default state.
    /// </summary>
    public void Clear();
}
```

**Private Members**:
```csharp
private ColorRect? _colorRect;
private Label? _nameLabel;
private Label? _abundanceLabel;
```

**Implementation Details**:

**_Ready()**:
1. Cache node references

**SetResource(string resourceId, float abundance, ResourceDefinition? definition)**:
```csharp
// Get display name from definition or format resource ID
string displayName = definition?.IdName ?? FormatResourceId(resourceId);
_nameLabel!.Text = displayName;

// Set color from definition or default
_colorRect!.Color = definition?.DisplayColor ?? Colors.Gray;

// Format abundance as percentage
_abundanceLabel!.Text = $"{abundance:P0}";
```

**FormatResourceId(string resourceId)**:
```csharp
// Convert "iron_ore" to "Iron Ore"
return string.Join(" ", resourceId.Split('_').Select(w => char.ToUpper(w[0]) + w.Substring(1)));
```

---

### 3. UI/CellInfo/CellResourcePanel.tscn
**Purpose**: Scene structure for the resource list sub-panel

**Node Hierarchy**:
```
CellResourcePanel (Control)
└── MarginContainer (padding: 8px all sides)
    └── VBoxContainer (vertical layout, spacing: 4px)
        ├── ResourcesLabel (Label): "Resources" (bold, underline)
        ├── ResourcesList (VBoxContainer) - dynamic items added here
        └── NoResourcesLabel (Label): "No natural resources" (centered, gray, initially hidden)
```

**Control Settings**:
- `CellResourcePanel`: Custom minimum size 200x100, layout_mode=1 (anchors)
- `MarginContainer`: theme_override_constants/margin_* = 8
- `ResourcesLabel`: Theme override font bold
- `ResourcesList`: Separation = 2, size flags vertical expand + fill
- `NoResourcesLabel`: Horizontal alignment center, font color gray, visible=false

---

### 4. Scripts/UI/CellInfo/CellResourcePanel.cs
**Purpose**: Behavior script for displaying Voronoi cell resources

**Namespace**: `UI.CellInfo`

**Public API**:
```csharp
public partial class CellResourcePanel : Control
{
    [Export]
    public PackedScene? ResourceListItemScene { get; set; }
    
    /// <summary>
    /// Updates the panel display with resources from the selected Voronoi cell.
    /// Called automatically when CellSelectionManager emits CellSelected signal.
    /// </summary>
    /// <param name="cell">The VoronoiCell to display resources for</param>
    public void UpdateFromCell(VoronoiCell cell);
    
    /// <summary>
    /// Clears all displayed resources.
    /// Called automatically when selection is cleared.
    /// </summary>
    public void ClearDisplay();
}
```

**Private Members**:
```csharp
private VBoxContainer? _resourcesList;
private Label? _noResourcesLabel;
private List<ResourceListItem> _resourceItems = new();
```

**Implementation Details**:

**_Ready()**:
1. Cache node references using GetNodeOrNull
2. Connect to `CellSelectionManager.Instance.CellSelected` signal → `OnCellSelected()`
3. Connect to `CellSelectionManager.Instance.SelectionCleared` signal → `OnSelectionCleared()`
4. Initialize with cleared display

**UpdateFromCell(VoronoiCell cell)**:
```csharp
ClearDisplay();

if (cell.Resources == null || cell.Resources.Count == 0)
{
    _noResourcesLabel!.Visible = true;
    return;
}

_noResourcesLabel!.Visible = false;

// Sort resources by abundance (descending)
var sortedResources = cell.Resources
    .OrderByDescending(kvp => kvp.Value)
    .ToList();

foreach (var kvp in sortedResources)
{
    if (ResourceListItemScene == null) continue;
    
    var item = ResourceListItemScene.Instantiate<ResourceListItem>();
    
    // Look up resource definition for display info
    ResourceDefinition? definition = null;
    if (ResourceDatabase.Instance != null && ResourceDatabase.Instance.IsLoaded)
    {
        ResourceDatabase.Instance.TryGetResource(kvp.Key, out definition);
    }
    
    item.SetResource(kvp.Key, kvp.Value, definition);
    _resourcesList!.AddChild(item);
    _resourceItems.Add(item);
}
```

**ClearDisplay()**:
```csharp
// Remove all existing resource items
foreach (var item in _resourceItems)
{
    item.QueueFree();
}
_resourceItems.Clear();

if (_noResourcesLabel != null)
{
    _noResourcesLabel.Visible = false;
}
```

**OnCellSelected(VoronoiCell cell, Node3D body, Continent continent)**:
```csharp
UpdateFromCell(cell);
```

**OnSelectionCleared()**:
```csharp
ClearDisplay();
```

**_ExitTree()**:
```csharp
// Disconnect from signals
if (CellSelectionManager.Instance != null)
{
    CellSelectionManager.Instance.CellSelected -= OnCellSelected;
    CellSelectionManager.Instance.SelectionCleared -= OnSelectionCleared;
}
```

---

## Implementation Order

1. Create directory `Scripts/UI/CellInfo/` if it doesn't exist (already exists)
2. Create directory `UI/CellInfo/` if it doesn't exist (already exists)
3. Create `Scripts/UI/CellInfo/ResourceListItem.cs` script
4. Create `UI/CellInfo/ResourceListItem.tscn` scene
5. Create `Scripts/UI/CellInfo/CellResourcePanel.cs` script
6. Create `UI/CellInfo/CellResourcePanel.tscn` scene
7. Verify script compiles without errors (`dotnet build`)

---

## Usage Example

```csharp
// In parent panel/container that hosts this sub-panel
public partial class ParentCellPanel : Control
{
    [Export]
    public CellResourcePanel? ResourcePanel { get; set; }
    
    public override void _Ready()
    {
        // Panel auto-connects to CellSelectionManager signals
        // No manual initialization needed
    }
}
```

Scene tree in parent:
```
ParentCellPanel (Control)
├── ... other sub-panels ...
├── CellGeneralInfoPanel (instance of CellGeneralInfoPanel.tscn)
├── CellResourcePanel (instance of CellResourcePanel.tscn)
└── ... other sub-panels ...
```

---

## Technical Notes

- **Resource Lookup**: Uses `ResourceDatabase.Instance.TryGetResource()` for O(1) lookup
- **List Item Reuse**: ResourceListItem is a separate scene for reusability and cleaner code
- **Performance**: List is cleared and rebuilt on each cell selection; cells typically have 0-3 resources
- **Null Safety**: All node references use null-conditional operators
- **Formatting**:
  - Resource Name: Title case from definition ID (e.g., "iron_ore" → "Iron Ore")
  - Abundance: Percentage format P0 (e.g., "85%")
  - Color: From ResourceDefinition.DisplayColor or Colors.Gray fallback

---

## Dependencies

- Existing: `CellSelectionManager` (autoload singleton with CellSelected/SelectionCleared signals)
- Existing: `VoronoiCell.Resources` dictionary (key: resource ID, value: abundance 0-1)
- Existing: `ResourceDatabase` singleton with TryGetResource method
- Existing: `ResourceDefinition` with IdName and DisplayColor
- Godot 4.x Control/Label/ColorRect APIs

---

## Testing Notes

To verify the implementation:
1. Select a cell with resources in-game (press R to raycast)
2. Verify panel shows:
   - Resource names (formatted from IDs)
   - Color swatches matching resource definitions
   - Abundance percentages
3. Verify resources are sorted by abundance (highest first)
4. Verify panel shows "No natural resources" for cells without resources
5. Verify panel clears when selection is cleared (press Escape or click elsewhere)
