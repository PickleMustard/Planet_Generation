# Implementation Plan: VoronoiCell Viewing Panel

## Overview
A reusable UI panel that displays a focused view of a specific VoronoiCell using a dedicated inspection camera attached to the orbital body. The panel shows a SubViewport with a pulsing highlight effect on the selected cell.

## Architecture Decisions
- **Inspection Camera**: Each orbital body (CelestialBody/SatelliteBody) has its own Camera3D child for cell inspection
- **Shader Effect**: Variant of existing cell selection shader with time-based pulsing
- **UI Structure**: Godot Control-based panel with embedded SubViewport for 3D rendering
- **Static View**: Camera is positioned once and remains fixed on the cell (no orbit/rotation)

---

## Files to Create

### 1. Shaders/cell_pulse_highlight.gdshader
**Purpose**: Pulsing variant of cell selection highlight shader

**Key Parameters**:
- `selected_cell_id` (float): Cell to highlight (-1 for none)
- `pulse_enabled` (bool): Toggle pulse effect
- `pulse_speed` (float, default 2.0): Speed of pulse cycle
- `pulse_min_alpha` (float, default 0.15): Minimum alpha during pulse
- `pulse_max_alpha` (float, default 0.4): Maximum alpha during pulse
- `fill_color` (vec4): Base fill color
- `outline_color` (vec4): Outline color

**Behavior**: When `pulse_enabled` is true, fill alpha oscillates using `sin(TIME * pulse_speed)` mapped to min/max range.

---

### 2. Scripts/UI/CellView/CellViewPanel.cs
**Purpose**: Behavior script for the cell viewing panel

**Namespace**: `UI.CellView`

**Public API**:
```csharp
public partial class CellViewPanel : Control
{
    /// <summary>
    /// Initializes the panel to display a specific VoronoiCell on an orbital body.
    /// Positions the inspection camera, activates pulse shader, and renders to SubViewport.
    /// </summary>
    /// <param name="body">The orbital body containing the cell</param>
    /// <param name="voronoiCellId">The Index of the VoronoiCell to display</param>
    public void Initialize(ISelectableBody body, int voronoiCellId);
    
    /// <summary>
    /// Closes the panel and cleans up resources.
    /// </summary>
    public void Close();
}
```

**Implementation Details**:
- Locate cell via `body.GetFaceFromIndex(voronoiCellId)`
- Get/create inspection camera via `body.GetOrCreateInspectionCamera()`
- Position camera: cell center + normalized normal * body.Radius * 1.5
- Camera looks at cell center
- Apply pulse shader material to body mesh
- Set SubViewport camera to inspection camera
- On close: reset shader, clear selection, hide panel

---

### 3. UI/CellView/CellViewPanel.tscn
**Purpose**: Scene structure for the panel

**Node Hierarchy**:
```
CellViewPanel (Control)
└── PanelContainer
    └── MarginContainer
        └── VBoxContainer
            ├── Header (HBoxContainer)
            │   ├── TitleLabel (Label): "Cell View"
            │   └── CloseButton (Button): "X"
            └── SubViewportContainer
                └── SubViewport (400x300, expandable)
```

**SubViewport Settings**:
- Render target update mode: Always
- Size: 400x300 minimum (expandable with container)

---

## Files to Modify

### 4. Scripts/ProceduralGeneration/ISelectableBody.cs
**Additions to interface**:
```csharp
/// <summary>
/// The inspection camera attached to this body for cell viewing.
/// Created on-demand via GetOrCreateInspectionCamera().
/// </summary>
Camera3D? InspectionCamera { get; }

/// <summary>
/// Gets or creates the inspection camera for this body.
/// Camera is created as a child of the body node.
/// </summary>
Camera3D GetOrCreateInspectionCamera();

/// <summary>
/// Positions the inspection camera to focus on a specific cell.
/// Camera is placed along the cell normal at 1.5x body radius distance.
/// </summary>
/// <param name="cell">The VoronoiCell to focus on</param>
void FocusInspectionCameraOnCell(VoronoiCell cell);
```

---

### 5. Scripts/ProceduralGeneration/CelestialBody.cs
**Implement interface additions**:

```csharp
public Camera3D? InspectionCamera { get; private set; }

public Camera3D GetOrCreateInspectionCamera()
{
    if (InspectionCamera == null)
    {
        InspectionCamera = new Camera3D();
        InspectionCamera.Name = "InspectionCamera";
        AddChild(InspectionCamera);
    }
    return InspectionCamera;
}

public void FocusInspectionCameraOnCell(VoronoiCell cell)
{
    if (InspectionCamera == null) return;
    
    Vector3 cellCenter = cell.Center;
    Vector3 normal = cellCenter.Normalized();
    float offset = Radius * 1.5f;
    Vector3 cameraPos = cellCenter + normal * offset;
    
    InspectionCamera.GlobalPosition = GlobalPosition + cameraPos;
    InspectionCamera.LookAt(GlobalPosition + cellCenter);
}
```

---

### 6. Scripts/ProceduralGeneration/SatelliteBody.cs
**Implement same interface additions as CelestialBody** (similar code pattern)

---

## Implementation Order

1. Create `Shaders/cell_pulse_highlight.gdshader`
2. Update `Scripts/ProceduralGeneration/ISelectableBody.cs` (add interface methods)
3. Implement in `Scripts/ProceduralGeneration/CelestialBody.cs`
4. Implement in `Scripts/ProceduralGeneration/SatelliteBody.cs`
5. Create `Scripts/UI/CellView/` directory
6. Create `Scripts/UI/CellView/CellViewPanel.cs`
7. Create `UI/CellView/` directory
8. Create `UI/CellView/CellViewPanel.tscn`
9. Test by instantiating panel and calling Initialize()

---

## Usage Example

```csharp
// Open cell view panel
var panel = GetNode<CellViewPanel>("CellViewPanel");
if (body is ISelectableBody selectableBody)
{
    panel.Initialize(selectableBody, cellIndex);
    panel.Show();
}

// Close panel
panel.Close(); // or user clicks X button
```

---

## Technical Notes

- **Camera Persistence**: Inspection camera is created once per body and reused
- **Shader Management**: Pulse shader is applied as material override, restored on close
- **SubViewport World**: Uses camera's World3D for correct scene rendering
- **Cell Lookup**: Uses `GetFaceFromIndex()` for O(1) cell retrieval by ID
- **Cleanup**: On panel close, shader is reset and cell highlight is cleared

---

## Dependencies

- Existing: `cell_selection_highlight.gdshader` (base for variant)
- Existing: `ISelectableBody` interface
- Existing: `VoronoiCell` data structure
- Godot 4.x SubViewport and Camera3D APIs
