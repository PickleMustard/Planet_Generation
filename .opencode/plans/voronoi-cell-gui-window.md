# Voronoi Cell Selection GUI Window - Implementation Plan

## Overview
Replace the debug console CellInfo tab with a proper in-game GUI window for Voronoi cell information. Change selection input from "R" key to left mouse click.

## Key Behaviors (Per User Requirements)
1. **Window blocks camera/movement** - User cannot control camera or move while window is open
2. **No multi-selection** - User cannot click different cells while window is open
3. **No Building display** - BuildingInfoPanel shows "No Building" when cell has no building
4. **Close icon** - Use `UI/xmark.svg` for close button
5. **Clear on close** - Selection highlight and data cleared when window closes
6. **UI priority** - Left-click only triggers selection when clicking game world (UI elements take priority)

---

## Ticket 1: Change Input from "R" Key to Left Mouse Click

### Files to Modify
- `Scripts/PlayerInteraction/InputHandler.cs`
- `Scripts/PlayerInteraction/PlayerController.cs`

### Changes in InputHandler.cs

**Remove** the R key handling block (~lines 125-132):
```csharp
// REMOVE THIS BLOCK:
if (keyEvent.Keycode == Key.R)
{
    var mousePos = GetViewport().GetMousePosition();
    var camera = GetNode<Camera3D>("../Camera3D");
    Vector3 origin = camera.ProjectRayOrigin(mousePos);
    var direction = origin + camera.ProjectRayNormal(mousePos) * 1000f;
    EmitSignal(SignalName.CastRay, origin, direction);
}
```

**Add** left mouse button handling in the `InputEventMouseButton` section (around line 45):
```csharp
if (@event is InputEventMouseButton mouseEvent)
{
    // Existing right/middle mouse handling...
    
    // ADD: Left mouse click for cell selection
    if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
    {
        // Only handle if no UI consumed the event
        if (!@event.IsHandled())
        {
            var mousePos = GetViewport().GetMousePosition();
            var camera = GetNode<Camera3D>("../Camera3D");
            Vector3 origin = camera.ProjectRayOrigin(mousePos);
            var direction = origin + camera.ProjectRayNormal(mousePos) * 1000f;
            EmitSignal(SignalName.CastRay, origin, direction);
            GetViewport().SetInputAsHandled();
        }
    }
    // ... rest of existing code
}
```

**Add** window visibility check (new section after existing early returns, around line 35):
```csharp
// Skip input if VoronoiCellInfoWindow is open
if (VoronoiCellInfoWindow.Instance?.IsVisible == true)
    return;
```

### Changes in PlayerController.cs
No changes needed - the `OnCastRay` method signature remains compatible.

---

## Ticket 2: Create VoronoiCellInfoWindow Scene

### New Files
- `UI/CellInfo/VoronoiCellInfoWindow.tscn`
- `Scripts/UI/CellInfo/VoronoiCellInfoWindow.cs`

### Scene Structure

```
VoronoiCellInfoWindow (CanvasLayer)
├── BlockInput (ColorRect) - Full screen, catches all input when visible
│   ├── Color: Color(0, 0, 0, 0.6) - Semi-transparent black
│   └── Mouse filter: Stop (blocks all mouse input)
└── PanelContainer (Centered, 70% window size)
    ├── Layout: Anchors centered, custom_minimum_size based on viewport
    ├── Theme: Use UI/Theme/theme.res
    └── MarginContainer
        ├── Theme overrides: margin_left/right/top/bottom = 16
        └── MainVBox (VBoxContainer)
            ├── Header (HBoxContainer)
            │   ├── CloseButton (TextureButton)
            │   │   ├── Texture: res://UI/xmark.svg
            │   │   ├── Custom minimum size: 32x32
            │   │   └── Stretch mode: Keep Aspect Centered
            │   ├── Spacer (Control)
            │   │   └── Size flags: ExpandHorizontal
            │   └── ContinentViewButton (Button)
            │       ├── Text: "Continent View"
            │       └── Disabled: true (future feature)
            └── Content (HBoxContainer)
                ├── LeftColumn (VBoxContainer)
                │   ├── Size flags: ExpandVertical + ExpandHorizontal
                │   ├── Custom minimum width: 40% of parent
                │   ├── CellViewContainer (PanelContainer)
                │   │   ├── Size flags: ExpandVertical (50%)
                │   │   └── CellViewPanel (instance)
                │   │       └── Scene: res://UI/CellView/CellViewPanel.tscn
                │   └── CellGeneralInfoContainer (PanelContainer)
                │       ├── Size flags: ExpandVertical (50%)
                │       └── CellGeneralInfoPanel (instance)
                │           └── Scene: res://UI/CellInfo/CellGeneralInfoPanel.tscn
                └── RightColumn (VBoxContainer)
                    ├── Size flags: ExpandVertical + ExpandHorizontal
                    ├── Custom minimum width: 60% of parent
                    ├── CellResourceContainer (PanelContainer)
                    │   ├── Size flags: ExpandVertical (40%)
                    │   └── CellResourcePanel (instance)
                    │       └── Scene: res://UI/CellInfo/CellResourcePanel.tscn
                    └── BuildingInfoContainer (PanelContainer)
                        ├── Size flags: ExpandVertical (60%)
                        └── BuildingInfoPanel (instance)
                            └── Scene: res://UI/BuildingInfo/BuildingInfoPanel.tscn
```

### Panel Container Setup
Each panel container should have:
- StyleBoxFlat background with subtle color (Color(0.15, 0.15, 0.18, 1.0))
- Content margins: 8px all sides
- Separation from sibling containers

---

## Ticket 3: Create VoronoiCellInfoWindow Controller Script

### File: `Scripts/UI/CellInfo/VoronoiCellInfoWindow.cs`

```csharp
using Godot;
using PlayerInteraction.CellSelection;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UI.CellInfo;
using UI.CellView;
using UI.BuildingInfo;
using UtilityLibrary;

namespace UI.CellInfo;

/// <summary>
/// Main window for displaying detailed Voronoi cell information.
/// Blocks game input while open. Auto-populates when a cell is selected.
/// </summary>
public partial class VoronoiCellInfoWindow : CanvasLayer
{
    public static VoronoiCellInfoWindow? Instance { get; private set; }

    [Export] private Control? _blockInput;
    [Export] private PanelContainer? _panelContainer;
    [Export] private TextureButton? _closeButton;
    [Export] private Button? _continentViewButton;
    
    [Export] private CellViewPanel? _cellViewPanel;
    [Export] private CellGeneralInfoPanel? _cellGeneralInfoPanel;
    [Export] private CellResourcePanel? _cellResourcePanel;
    [Export] private BuildingInfoPanel? _buildingInfoPanel;
    
    [Export] private Label? _noBuildingLabel; // Shown when cell has no building

    private VoronoiCell? _currentCell;
    private Node3D? _currentBody;
    private Continent? _currentContinent;

    public bool IsVisible => Visible;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        Layer = 20; // Above ConstructionHUD (10) but below DebugMenu (100)
        
        // Initial state: hidden
        Hide();
        
        // Connect signals
        if (_closeButton != null)
            _closeButton.Pressed += OnClosePressed;
        
        if (_continentViewButton != null)
            _continentViewButton.Pressed += OnContinentViewPressed;
        
        // Connect to selection manager
        if (CellSelectionManager.Instance != null)
        {
            CellSelectionManager.Instance.CellSelected += OnCellSelected;
            CellSelectionManager.Instance.SelectionCleared += OnSelectionCleared;
        }
        
        // Block input backdrop
        if (_blockInput != null)
        {
            _blockInput.GuiInput += OnBackdropInput;
        }
        
        GameLogger.Info("VoronoiCellInfoWindow initialized");
    }

    /// <summary>
    /// Shows the window and populates it with cell data.
    /// </summary>
    public void ShowWindow(VoronoiCell cell, Node3D body, Continent? continent)
    {
        _currentCell = cell;
        _currentBody = body;
        _currentContinent = continent;
        
        // Show the window
        Show();
        
        // Capture mouse for UI interaction
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        
        // Populate panels
        PopulatePanels();
        
        GameLogger.Info($"VoronoiCellInfoWindow shown for cell {cell.Index}");
    }

    /// <summary>
    /// Hides the window and clears selection.
    /// </summary>
    public void HideWindow()
    {
        Hide();
        
        // Restore captured mouse for game
        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        
        // Clear selection (this will also clear panels via signals)
        CellSelectionManager.Instance?.ClearSelection();
        
        GameLogger.Info("VoronoiCellInfoWindow hidden");
    }

    /// <summary>
    /// Clears all panel data.
    /// </summary>
    public void Clear()
    {
        _currentCell = null;
        _currentBody = null;
        _currentContinent = null;
        
        _cellGeneralInfoPanel?.ClearDisplay();
        _cellResourcePanel?.ClearDisplay();
        _cellViewPanel?.Close();
        
        // Show "No Building" label, hide BuildingInfoPanel
        if (_noBuildingLabel != null)
            _noBuildingLabel.Show();
        _buildingInfoPanel?.Clear();
    }

    private void PopulatePanels()
    {
        if (_currentCell == null || _currentBody == null)
            return;
        
        // Cell View Panel
        if (_cellViewPanel != null && _currentBody is ISelectableBody selectableBody)
        {
            _cellViewPanel.Initialize(selectableBody, _currentCell.Index);
        }
        
        // General Info Panel
        _cellGeneralInfoPanel?.UpdateFromCell(_currentCell);
        
        // Resource Panel
        _cellResourcePanel?.UpdateFromCell(_currentCell);
        
        // Building Info Panel
        PopulateBuildingInfo();
    }

    private void PopulateBuildingInfo()
    {
        // Check if cell has a building
        bool hasBuilding = false;
        
        // TODO: Implement building lookup logic
        // This depends on how buildings are associated with cells
        // Example: hasBuilding = _currentContinent?.GetBuildingAtCell(_currentCell.Index) != null;
        
        if (hasBuilding && _currentContinent != null)
        {
            // Hide "No Building" label, show building info
            _noBuildingLabel?.Hide();
            
            // TODO: Get building data and economy reference
            // _buildingInfoPanel?.SetBuilding(building, economy);
        }
        else
        {
            // Show "No Building" label
            _noBuildingLabel?.Show();
            _buildingInfoPanel?.Clear();
        }
    }

    private void OnCellSelected(VoronoiCell cell, Node3D body, Continent continent)
    {
        ShowWindow(cell, body, continent);
    }

    private void OnSelectionCleared()
    {
        Hide();
        Clear();
    }

    private void OnClosePressed()
    {
        HideWindow();
    }

    private void OnContinentViewPressed()
    {
        // Future feature: Navigate to continent view
        GameLogger.Info("Continent View button pressed (feature not yet implemented)");
    }

    private void OnBackdropInput(InputEvent @event)
    {
        // Clicking the backdrop closes the window
        if (@event is InputEventMouseButton mouseEvent && 
            mouseEvent.ButtonIndex == MouseButton.Left && 
            mouseEvent.Pressed)
        {
            HideWindow();
        }
    }
}
```

---

## Ticket 4: Modify CellViewPanel for Embedded Mode

### File: `Scripts/UI/CellView/CellViewPanel.cs`

**Add** exported property to control header visibility:
```csharp
[Export] private bool _showHeader = true;
[Export] private Control? _header;
```

**Modify** `_Ready()` to conditionally show/hide header:
```csharp
public override void _Ready()
{
    // Get node references
    _subViewport = GetNode<SubViewport>("PanelContainer/MarginContainer/VBoxContainer/SubViewportContainer/SubViewport");
    _closeButton = GetNode<Button>("PanelContainer/MarginContainer/VBoxContainer/Header/CloseButton");
    _titleLabel = GetNode<Label>("PanelContainer/MarginContainer/VBoxContainer/Header/TitleLabel");
    _header = GetNode<Control>("PanelContainer/MarginContainer/VBoxContainer/Header");
    
    // Show/hide header based on mode
    if (_header != null)
        _header.Visible = _showHeader;

    // Connect close button signal (only if header is visible)
    if (_closeButton != null && _showHeader)
    {
        _closeButton.Pressed += Close;
    }

    // Initially hidden
    Hide();
}
```

---

## Ticket 5: Add Window to MainGameUI

### File: `UI/MainGameUI.tscn`

**Add** after ConstructionHUD:
```
[node name="VoronoiCellInfoWindow" parent="." instance=ExtResource("4_cellwindow")]
visible = false
```

**Add** to ext_resources:
```
[ext_resource type="PackedScene" uid="uid://..." path="res://UI/CellInfo/VoronoiCellInfoWindow.tscn" id="4_cellwindow"]
```

---

## Ticket 6: Update BuildingInfoPanel for "No Building" State

### File: `Scripts/UI/BuildingInfo/BuildingInfoPanel.cs`

**Add** method to show "No Building" state:
```csharp
/// <summary>
/// Shows the "No Building" state when cell has no building.
/// </summary>
public void ShowNoBuilding()
{
    ClearCurrentDetails();
    
    if (Header != null)
    {
        Header.ShowNoBuilding();
    }
    
    // Add a "No Building" label to details container
    var label = new Label
    {
        Text = "No Building",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical = SizeFlags.ExpandFill
    };
    label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
    DetailsContainer?.AddChild(label);
}
```

---

## Implementation Checklist

### Pre-Implementation
- [ ] Review existing panel scenes to understand their export properties
- [ ] Verify xmark.svg exists at `res://UI/xmark.svg`
- [ ] Check theme.res for available styles

### Implementation Order
1. **Ticket 1** - Input changes (smallest, enables testing)
2. **Ticket 2** - Create window scene
3. **Ticket 3** - Create window controller
4. **Ticket 4** - Modify CellViewPanel
5. **Ticket 5** - Add to MainGameUI
6. **Ticket 6** - BuildingInfoPanel "No Building" state

### Testing Scenarios
- [ ] Left click on Voronoi cell opens window
- [ ] Left click on UI element doesn't trigger selection
- [ ] Window blocks camera movement
- [ ] Close button clears selection and closes window
- [ ] Clicking backdrop closes window
- [ ] All panels populate with correct data
- [ ] "No Building" shows when cell has no building
- [ ] Window is centered and 70% of screen
- [ ] Panels are correctly proportioned (40/60 split, then vertical splits)

---

## Dependencies
- Existing panels: CellGeneralInfoPanel, CellResourcePanel, CellViewPanel, BuildingInfoPanel
- CellSelectionManager singleton
- InputHandler and PlayerController
- Theme resources

## Notes
- Window layer is set to 20 (above ConstructionHUD at 10, below DebugMenu at 100)
- Input.SetMouseMode switches between Captured (game) and Visible (UI)
- The backdrop ColorRect prevents clicks from passing through to the game world
- Continent View button is disabled for future implementation
