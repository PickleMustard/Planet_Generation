# Implementation Plan: Voronoi Cell General Info Panel

## Overview
A reusable UI sub-panel that displays core generated attributes of a selected Voronoi cell: **Biome**, **Elevation** (Height), and **Slope**. This panel is designed as a sub-component of a larger GUI element and does not manage its own visibility or lifecycle.

## Architecture Decisions
- **Sub-panel Design**: No header, close button, or collapsible features - pure data display
- **Slope Calculation**: Computed from vertex geometry using surface normal deviation from radial vector
- **Data Binding**: Connects to `CellSelectionManager.CellSelected` signal for updates
- **Passive Display**: Parent container manages visibility; panel only updates content when visible

---

## Files to Create

### 1. UI/CellInfo/CellGeneralInfoPanel.tscn
**Purpose**: Scene structure for the general info sub-panel

**Node Hierarchy**:
```
CellGeneralInfoPanel (Control)
└── MarginContainer (padding: 8px all sides)
    └── VBoxContainer (vertical layout, spacing: 4px)
        ├── BiomeRow (HBoxContainer)
        │   ├── BiomeLabel (Label): "Biome" (min-width: 80px, bold)
        │   └── BiomeValue (Label): "-" (expand, right-aligned)
        ├── ElevationRow (HBoxContainer)
        │   ├── ElevationLabel (Label): "Elevation"
        │   └── ElevationValue (Label): "-"
        └── SlopeRow (HBoxContainer)
            ├── SlopeLabel (Label): "Slope"
            └── SlopeValue (Label): "-"
```

**Control Settings**:
- `CellGeneralInfoPanel`: Custom minimum size 200x80, layout_mode=1 (anchors)
- `MarginContainer`: theme_override_constants/margin_* = 8
- Labels: Use theme default font, BiomeLabel/ElevationLabel/SlopeLabel have custom_minimum_size.x = 80
- Value labels: HorizontalAlignment = Right

---

### 2. Scripts/UI/CellInfo/CellGeneralInfoPanel.cs
**Purpose**: Behavior script for displaying Voronoi cell general information

**Namespace**: `UI.CellInfo`

**Public API**:
```csharp
public partial class CellGeneralInfoPanel : Control
{
    /// <summary>
    /// Updates the panel display with data from the selected Voronoi cell.
    /// Called automatically when CellSelectionManager emits CellSelected signal.
    /// </summary>
    /// <param name="cell">The VoronoiCell to display information for</param>
    public void UpdateFromCell(VoronoiCell cell);
    
    /// <summary>
    /// Clears all displayed values, resetting to default "-" state.
    /// Called automatically when selection is cleared.
    /// </summary>
    public void ClearDisplay();
}
```

**Private Members**:
```csharp
private Label? _biomeValueLabel;
private Label? _elevationValueLabel;
private Label? _slopeValueLabel;
```

**Implementation Details**:

**_Ready()**:
1. Cache node references using GetNodeOrNull (with [Export] fallback)
2. Connect to `CellSelectionManager.Instance.CellSelected` signal → `OnCellSelected()`
3. Connect to `CellSelectionManager.Instance.SelectionCleared` signal → `OnSelectionCleared()`

**OnCellSelected(VoronoiCell cell, Node3D body, Continent continent)**:
```csharp
_biomeValueLabel!.Text = cell.Biome.ToString();
_elevationValueLabel!.Text = $"{cell.Height:P1}"; // Percentage format (e.g., "45.2%")
_slopeValueLabel!.Text = $"{cell.GetSlope():F1}°"; // Degrees format (e.g., "12.3°")
```

**OnSelectionCleared()**:
```csharp
ClearDisplay();
```

**ClearDisplay()**:
```csharp
_biomeValueLabel!.Text = "-";
_elevationValueLabel!.Text = "-";
_slopeValueLabel!.Text = "-";
```

---

## Files to Modify

### 3. Scripts/Structures/GameState/VoronoiCell.cs
**Purpose**: Add slope calculation method to VoronoiCell

**Additions**:
```csharp
/// <summary>
/// Cached slope value in degrees. Calculated on first access.
/// </summary>
private float? _cachedSlope;

/// <summary>
/// Calculates the average slope of this cell in degrees.
/// Slope is determined by the angle between each triangle's surface normal
/// and the vector from the cell center to the planet center (assumed at origin).
/// </summary>
/// <returns>Slope angle in degrees (0-90)</returns>
public float GetSlope()
{
    if (_cachedSlope.HasValue)
        return _cachedSlope.Value;

    if (Triangles == null || Triangles.Length == 0 || Points == null || Points.Length < 3)
    {
        _cachedSlope = 0f;
        return 0f;
    }

    float totalSlope = 0f;
    int validTriangles = 0;

    foreach (var triangle in Triangles)
    {
        // Get the three vertices of this triangle
        if (triangle.Vertices == null || triangle.Vertices.Length < 3)
            continue;

        Vector3 p0 = triangle.Vertices[0].Position;
        Vector3 p1 = triangle.Vertices[1].Position;
        Vector3 p2 = triangle.Vertices[2].Position;

        // Calculate triangle surface normal
        Vector3 edge1 = p1 - p0;
        Vector3 edge2 = p2 - p0;
        Vector3 normal = edge1.Cross(edge2).Normalized();

        // Calculate radial vector (from assumed planet center at origin to triangle centroid)
        Vector3 centroid = (p0 + p1 + p2) / 3f;
        Vector3 radial = centroid.Normalized();

        // Calculate angle between normal and radial vector
        // Flat terrain = normal parallel to radial (0° slope)
        // Vertical cliff = normal perpendicular to radial (90° slope)
        float dot = Mathf.Abs(normal.Dot(radial));
        float angleRad = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
        float angleDeg = Mathf.RadToDeg(angleRad);

        totalSlope += angleDeg;
        validTriangles++;
    }

    _cachedSlope = validTriangles > 0 ? totalSlope / validTriangles : 0f;
    return _cachedSlope.Value;
}

/// <summary>
/// Invalidates the cached slope value. Call this if the cell's geometry changes.
/// </summary>
public void InvalidateSlopeCache()
{
    _cachedSlope = null;
}
```

**Imports to Add** (if not already present):
```csharp
using Godot;
```

---

## Implementation Order

1. Modify `Scripts/Structures/GameState/VoronoiCell.cs` - Add `GetSlope()` method and cache
2. Create directory `UI/CellInfo/` if it doesn't exist
3. Create directory `Scripts/UI/CellInfo/` if it doesn't exist
4. Create `Scripts/UI/CellInfo/CellGeneralInfoPanel.cs` script
5. Create `UI/CellInfo/CellGeneralInfoPanel.tscn` scene
6. Verify script compiles without errors (`dotnet build`)

---

## Usage Example

```csharp
// In parent panel/container that hosts this sub-panel
public partial class ParentCellPanel : Control
{
    [Export]
    public CellGeneralInfoPanel? GeneralInfoPanel { get; set; }
    
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
└── ... other sub-panels ...
```

---

## Slope Calculation Algorithm

The slope is calculated as the average angle between each triangle's surface normal and the radial vector from the planet center:

1. **For each triangle in the cell:**
   - Get three vertex positions
   - Calculate edges: `edge1 = p1 - p0`, `edge2 = p2 - p0`
   - Cross product gives surface normal: `normal = edge1.Cross(edge2).Normalized()`

2. **Calculate radial vector:**
   - Triangle centroid: `centroid = (p0 + p1 + p2) / 3`
   - Radial direction: `radial = centroid.Normalized()` (assumes planet center at origin)

3. **Calculate slope angle:**
   - Dot product: `dot = normal.Dot(radial)`
   - Angle in radians: `angleRad = Acos(Abs(dot))`
   - Convert to degrees: `angleDeg = RadToDeg(angleRad)`

4. **Average all triangles:**
   - Return mean of all triangle slope angles

**Interpretation**:
- 0° = Flat terrain (normal points directly away from planet center)
- 45° = Moderate slope
- 90° = Vertical cliff (normal perpendicular to radial)

---

## Technical Notes

- **Slope Caching**: Calculated once per cell and cached; cells are immutable after generation
- **Planet Center Assumption**: Slope calculation assumes planet center at world origin (0,0,0)
- **Performance**: O(n) where n = number of triangles in cell; cached after first call
- **Null Safety**: All label references use null-conditional operators with fallback defaults
- **Formatting**: 
  - Biome: enum name as-is (e.g., "Mountain", "Grassland")
  - Elevation: Percentage format P1 (e.g., "45.2%")
  - Slope: Fixed-point F1 with degree symbol (e.g., "12.3°")

---

## Dependencies

- Existing: `CellSelectionManager` (autoload singleton with CellSelected/SelectionCleared signals)
- Existing: `VoronoiCell` data structure (add GetSlope method)
- Existing: `Biome.BiomeType` enum
- Godot 4.x Control/Label APIs

---

## Testing Notes

To verify the implementation:
1. Select a cell in-game (press R to raycast)
2. Verify panel updates with:
   - Biome: matches cell's assigned biome type
   - Elevation: matches cell.Height formatted as percentage
   - Slope: calculated value between 0-90 degrees
3. Verify panel clears when selection is cleared (press Escape or click elsewhere)
4. Verify ocean cells show low slope (~0°), mountain cells show higher slope (>30°)
