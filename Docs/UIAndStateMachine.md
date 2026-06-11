# UI and State Machine

## Overview

The UI system is built around a Hierarchical State Machine (HSM) using LimboHSM. The root `GUIControllerHSM` manages top-level states such as HUD, Planet Selection, Voronoi Cell Inspection, Continent View, Orbital Body Inspection, Construction Menu, Station Management, Logistics Unit View, and Transfer Planning. Each state owns its own panel windows and transitions based on player input and game events. The UI also includes a Toast system, far-field orbital indicators, and various information panels.

## Key Classes

### GUIControllerHSM
- **Location**: `Scripts/UI/StateMachine/GUIControllerHSM.cs`
- **Purpose**: Root HSM attached to the GUI controller node. Manages transitions between major UI modes.
- **Key States**:
  - `HUD`: Default in-game overlay.
  - `VoronoiCell`: Cell inspection and building placement.
  - `Continent`: Continent-wide economy and overview.
  - `OrbitalBody`: Body information and orbit camera.
  - `ConstructionMenu`: Building and station construction.
  - `Station`: Station management and ship queues.
  - `LogisticsUnit`: Ship details and trajectory info.

### GUIManager
- **Location**: `Scripts/UI/StateMachine/GUIManager.cs`
- **Purpose**: High-level manager that wires the HSM to the scene tree and handles global UI events.

### GuiBlackboard
- **Location**: `Scripts/UI/StateMachine/GuiBlackboard.cs`
- **Purpose**: Shared data store used by HSM states to communicate without tight coupling.

### Individual States
- **Location**: `Scripts/UI/StateMachine/States/`
- **Purpose**: Each state class (e.g., `PlanetSelectionState`, `BuildingPlacementState`, `TransferPlanningState`) encapsulates the behavior for a single UI mode.

### MainGameUI / HUD
- **Location**: `Scripts/UI/MainGameUI.cs`, `Scripts/UI/HeadsUpDisplay/HUD.cs`
- **Purpose**: Root UI scene and the persistent HUD overlay showing system time, resources, and alerts.

### ToastSystem
- **Location**: `Scripts/UI/ToastSystem.cs`
- **Purpose**: Displays transient notification messages to the player.

### FarFieldIndicatorManager / FarFieldIcon
- **Location**: `Scripts/UI/FarFieldIndicators/FarFieldIndicatorManager.cs`, `FarFieldIcon.cs`
- **Purpose**: Shows simplified icons for distant orbital bodies when they are too far to render in full detail.

### VoronoiCellInfoWindow / CellGeneralInfoPanel / CellResourcePanel
- **Location**: `Scripts/UI/CellInfo/`
- **Purpose**: Windows and panels that display information about a selected Voronoi cell, including resources, biome, and existing buildings.

### CellViewPanel
- **Location**: `Scripts/UI/CellView/CellViewPanel.cs`
- **Purpose**: Renders a visual representation of the selected cell's contents.

### ContinentInfoWindow / ContinentDetailsPanel / ContinentHeaderPanel
- **Location**: `Scripts/UI/ContinentInfo/`
- **Purpose**: Displays economy summaries, building lists, and power statistics for an entire continent.

### OrbitalBodyWindow / OrbitalBodyDetailsPanel / OrbitalBodyHeaderPanel / OrbitalBodyOrbitCamera
- **Location**: `Scripts/UI/OrbitalBodyWindow/`
- **Purpose**: Detailed information window for a celestial body, including statistics and an orbit inspection camera.

### StationWindow / StationDetailsPanel / StationHeaderPanel / StationBespokeBar
- **Location**: `Scripts/UI/StationWindow/`
- **Purpose**: Management UI for orbital stations, including active construction and ship queues.

### ConstructionYardWindow / ActiveBuildRow / QueuedShipRow / ShipPickerPanel
- **Location**: `Scripts/UI/ConstructionYard/`
- **Purpose**: UI for managing ship construction queues at construction yard stations.

### TransferPlanningWindow / CargoManifestPanel / EndpointSelectionPanel / TransferRoutePanel
- **Location**: `Scripts/UI/TransferPlanning/`
- **Purpose**: UI for planning resource transfers between bodies and stations.

### LogisticsUnitWindow / LogisticsUnitDetailsPanel / LogisticsUnitHeaderPanel
- **Location**: `Scripts/UI/LogisticsUnitWindow/`
- **Purpose**: Window showing logistics unit status, cargo, and trajectory.

### BuildingInfoPanel / BaseBuildingDetails / ExtractionDetails / ManufacturingDetails / PowerDetails / StorageDetails
- **Location**: `Scripts/UI/BuildingInfo/`
- **Purpose**: Contextual panels that show details for a selected building based on its behavior type.

### ConstructionMenu / BuildingPlacementMode / StationSelectionPopup
- **Location**: `Scripts/UI/Construction/`
- **Purpose**: UI flow for initiating new construction, selecting building types, and placing them on the surface.

### BillboardLabelManager
- **Location**: `Scripts/UI/OrbitalBodyWindow/BillboardLabelManager.cs`
- **Purpose**: Manages 3D text labels that hover over orbital bodies in the scene.

### LoadingScreen / LoadingBodyItem
- **Location**: `Scripts/UI/Loading/`
- **Purpose**: Loading screen shown during procedural system generation. Displays per-body progress.

### DetailPanel / InfoRow / RangeControl / SectionHeader / ToggleSection
- **Location**: `Scripts/UI/` and `Scripts/UI/Components/`
- **Purpose**: Reusable UI components used across multiple panels.

## GUI Authoring Convention (layout-in-`.tscn` / logic-in-`.cs`)

UI panels separate **layout → `.tscn`**, **logic → `.cs`**, and **styling → shared theme**.
Building Control trees in C# (`new VBoxContainer()` + `AddChild`) for static layout is an
**anti-pattern** — it cannot be edited in the Godot editor. All new panels follow the recipe
below; existing code-built panels are being migrated to it.

### Reference patterns
- **Window / static panel**: `UI/BuildingInfo/BuildingInfoWindow.tscn` +
  `Scripts/UI/BuildingInfo/BuildingInfoWindow.cs`. The `.tscn` root declares
  `node_paths=PackedStringArray("_field",...)` and assigns each `[Export]` field a
  `NodePath(...)`; the script only wires signals/data in `_Ready()`.
- **Per-item card factory**: `DeveloperTools/ShipEditor/ShipCard.cs` + `ShipCard.tscn`. A
  `static Create(...)` loads the PackedScene, `Instantiate<T>()`, then `Initialize()`.
- **Item rows**: `UI/SatelliteItem.tscn`, `UI/Components/DetailRow.tscn`,
  `UI/Components/ResourceCostRow.tscn`, `UI/Components/LabeledFieldRow.tscn`.

### Extraction recipe (per panel)
0. **Classify**: (a) static window → whole tree to one `.tscn`; (b) per-item card built in a
   loop → item `.tscn` + `Create()` factory; (c) hybrid skeleton + runtime-filled region →
   skeleton to `.tscn`, export the container nodes, keep `BuildDynamicContent()`.
1. **Author the `.tscn`** under `UI/<area>/` (in-game) or colocated in `DeveloperTools/<area>/`,
   mirroring the exact node hierarchy + node names the build method currently creates. Set
   `theme = wireframe_paper.tres` on top-level window roots; child cards inherit it.
2. **Add exports to the root**: `node_paths=PackedStringArray(...)` + `_field = NodePath("Path/To/Node")`.
3. **Convert the script**: each `new`/`AddChild` field becomes `[Export] private SomeControl? _x;`.
   Delete the construction from `_Ready()`; keep signal wiring + `Bind()`/data binding. Add the
   `Create()` factory for shape (b)/(c).
4. **Move styling to the theme**: delete `AddThemeColorOverride`/`AddThemeStyleboxOverride`/
   `AddThemeFontSizeOverride` for static styling; set `theme_type_variation` in the `.tscn`
   instead. Layout constants (`separation`, `margin`) may stay as `.tscn`
   `theme_override_constants/*` (they are layout, not palette).
5. **Extract list rows**: replace per-item loops with `Instantiate` of a shared/item `.tscn` + `Bind(data)`.
6. **Fix call sites**: replace every `new <Card>()` with `<Card>.Create(...)`. Anti-pattern
   reminder: a `new`'d layout-owning Control has null `[Export]` fields and renders blank/crashes.
7. **Verify**: `dotnet build`, launch the panel, confirm layout + styling unchanged.

### Theme strategy
Palette, fonts, and styleboxes live in `UI/Theme/wireframe_paper/wireframe_paper.tres`; scripts
set only `theme_type_variation`. Semantic Label variations: `LabelHand`, `LabelMono`, `LabelSub`,
`LabelFaint`, `LabelKey`, `LabelMonoTiny`, `LabelOk`/`LabelWarn`/`LabelAlert`, and the
high-contrast `LabelHighContrast*` set for the debug editors. Pills: `Pill`, `PillOrange`.
`Scripts/UI/Wireframe/WireColors.cs` is reserved for runtime-computed visuals (`_Draw()`,
`Modulate`, `ColorRect.Color`) — never for theme overrides.

## Related Documentation
- [Player Interaction](PlayerInteraction.md)
- [Debug System](DebugSystem.md)
