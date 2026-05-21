# PlanetBoard GUI Remake — Implementation Plan

## 1. Executive Summary

The PlanetBoard is currently a monolithic `Control` that renders all buildings as 2D polygons via Godot's immediate-mode `_Draw()` API. Buildings are plain C# objects (`BuildingNodeView`), not scene tree nodes — they cannot be individually dragged, hovered, or repositioned. The `FitToContent()` call on every refresh zooms to fit all content, causing buildings to shrink as more are added. Collision-relaxation passes push buildings away from their true 3D-projected positions.

This plan replaces the architecture with a **SubViewport + Camera2D + Node2D world** where each building is a real `Node2D` that can be freely dragged, pinned back to its projected position, and individually interacted with.

---

## 2. Current Architecture

```
PlanetBoardWindow (PanelContainer)
  └─ MainVBox (VBoxContainer)
       ├─ TitleBar (HBoxContainer)
       │    ├─ TitleLabel
       │    ├─ ModeSwitcher (OptionButton)
       │    └─ CloseButton
       └─ BoardView (Control) [PlanetBoardView.cs]        ← single canvas, all drawing here
```

### Key Files (Current)

| File | Role |
|------|------|
| `Scripts/UI/PlanetBoard/PlanetBoardView.cs` | Monolithic Control — `_Draw()` renders buildings, ports, links, grid, drag preview |
| `Scripts/UI/PlanetBoard/BoardCamera.cs` | Pure-C# zoom/pan transform (not a Godot camera) |
| `Scripts/UI/PlanetBoard/BoardLayoutEngine.cs` | Equirectangular projection + 20 relaxation passes |
| `Scripts/UI/PlanetBoard/BuildingShapeGeometry.cs` | Pure geometry — polygon vertices, port anchors/normals |
| `Scripts/UI/PlanetBoard/Modes/IPlanetBoardMode.cs` | Strategy interface for mode-specific input/overlay |
| `Scripts/UI/PlanetBoard/Modes/ResourceLinkPlanningMode.cs` | Drag-from-port to create `ResourceLink` |
| `Scripts/UI/PlanetBoard/Modes/TransferRoutePlanningMode.cs` | Click transfer station to pick destination |
| `Scripts/UI/PlanetBoard/Modes/OverviewMode.cs` | Read-only overview stub |
| `Scripts/UI/PlanetBoard/Testing/MockBuildingFactory.cs` | Test helper for creating mock buildings |
| `Scripts/UI/PlanetBoard/Testing/MockBuildingConstructionManager.cs` | Test helper for mock building list |
| `Scripts/UI/PlanetBoard/Testing/MockOrbitalBody.cs` | Test helper for mock orbital body |
| `UI/PlanetBoard/PlanetBoard.tscn` | Scene file for the PlanetBoard window |

### Current Data Flow

1. `PlanetBoardView.SetBody(IOrbitalBody)` stores the body reference.
2. `RefreshFromBody()` triggered by `SignalBus.BuildingConstructed` / `BuildingRemoved`.
3. `BoardLayoutEngine.Compute(matched)` projects 3D cell centers via equirectangular projection, then runs 20 collision-relaxation passes.
4. Each building becomes a `BuildingNodeView` plain C# object with `BoardPos`, `Shape`, `Radius`, `FillColor`, `Icon`, `DisplayName`, and `List<PortView>`.
5. `Camera.SetContentBBox(layout.BoundingBox)` and `Camera.FitToContent()` — this is what causes the shrinking.
6. `RebuildLinkViews()` pairs linked ports.
7. `QueueRedraw()` triggers `_Draw()` which paints everything.

### Problems

| Problem | Root Cause |
|---------|------------|
| Buildings shrink as more are added | `FitToContent()` zooms to fit the bounding box of all buildings on every refresh |
| Position does not reflect 3D position | Relaxation passes push buildings apart from their projected positions |
| Buildings cannot be moved | `BuildingNodeView` is a plain object, not a `Node2D`; position is set only by `BoardLayoutEngine` |
| No per-building interaction | All hit-testing is done manually in `PlanetBoardView`; buildings have no input callbacks |
| All drawing is monolithic | Single `_Draw()` renders everything; any change requires full redraw |

---

## 3. Target Architecture

```
PlanetBoardWindow (PanelContainer)
  └─ MainVBox (VBoxContainer)
       ├─ TitleBar (HBoxContainer)              ← unchanged
       │    ├─ TitleLabel
       │    ├─ ModeSwitcher (OptionButton)
       │    └─ CloseButton
       └─ PlanetBoardView (Control)             ← thin wrapper
            └─ SubViewportContainer (stretch=true)
                 └─ SubViewport (update=always)
                      ├─ Camera2D [BoardCameraController.cs]
                      └─ BoardWorld (Node2D) [BoardWorld.cs]
                           ├─ BoardBackground (Node2D)       ← grid/background drawing
                           ├─ BoardLinkRenderer (Node2D) [BoardLinkRenderer.cs]
                           ├─ BuildingNode2D [BuildingNode2D.cs]   ← per building
                           ├─ BuildingNode2D ...
                           └─ ...
```

### New Files

| File | Role |
|------|------|
| `Scripts/UI/PlanetBoard/BuildingNode2D.cs` | `Node2D` subclass — draws one building's polygon, icon, label, port dots; handles drag and port interaction |
| `Scripts/UI/PlanetBoard/BoardWorld.cs` | Root `Node2D` — manages `BuildingNode2D` children, `BoardLinkRenderer`, signal-driven refresh, input dispatch, mode forwarding |
| `Scripts/UI/PlanetBoard/BoardCameraController.cs` | Script on `Camera2D` — middle-mouse pan, scroll zoom (cursor-anchored), fit-to-content |
| `Scripts/UI/PlanetBoard/BoardLinkRenderer.cs` | `Node2D` — draws all `ResourceLink` lines and drag preview in its `_Draw()` |

### Deleted Files

| File | Reason |
|------|--------|
| `Scripts/UI/PlanetBoard/BoardCamera.cs` | Fully replaced by `Camera2D` + `BoardCameraController` |
| `Tests/UI/PlanetBoard/BoardCameraTest.cs` | Tests the deleted `BoardCamera` class |

### Significantly Rewritten Files

| File | Nature of Rewrite |
|------|-------------------|
| `Scripts/UI/PlanetBoard/PlanetBoardView.cs` | From monolithic `_Draw()` Control → thin SubViewport wrapper; all inner classes and drawing code removed |
| `UI/PlanetBoard/PlanetBoard.tscn` | `BoardView` node script path unchanged; scene otherwise identical |

### Modified Files

| File | Changes |
|------|---------|
| `Scripts/UI/PlanetBoard/BoardLayoutEngine.cs` | Remove relaxation passes; simplify `Compute()` to pure projection |
| `Scripts/UI/PlanetBoard/Modes/IPlanetBoardMode.cs` | `OnEnter(BoardWorld, ...)` instead of `OnEnter(PlanetBoardView, ...)`; `DrawOverlay(CanvasItem, BoardCameraController)` |
| `Scripts/UI/PlanetBoard/Modes/ResourceLinkPlanningMode.cs` | Adapt to `BoardWorld` API; read drag state from `BoardWorld` |
| `Scripts/UI/PlanetBoard/Modes/TransferRoutePlanningMode.cs` | Adapt overlay to iterate `BuildingNode2D` nodes; use `BoardCameraController` for coordinate transforms |
| `Scripts/UI/PlanetBoard/Modes/OverviewMode.cs` | Update `OnEnter` signature |
| `Scripts/UI/PlanetBoard/PlanetBoardWindow.cs` | Verify API compatibility; minor adjustments |
| `Scripts/UI/TestScenes/TestPlanetBoardScene.cs` | Update camera reset call; add pin/unpin test buttons |
| `Scripts/UI/TransferPlanning/PickDestinationView.cs` | Verify programmatic `PlanetBoardView` creation still works |

### Unchanged Files

| File | Reason |
|------|--------|
| `Scripts/UI/PlanetBoard/BuildingShapeGeometry.cs` | Pure geometry helpers; still used by `BuildingNode2D` |
| `Scripts/UI/PlanetBoard/Testing/MockBuildingFactory.cs` | Test helper; API unchanged |
| `Scripts/UI/PlanetBoard/Testing/MockBuildingConstructionManager.cs` | Test helper; API unchanged |
| `Scripts/UI/PlanetBoard/Testing/MockOrbitalBody.cs` | Test helper; API unchanged |
| `Tests/UI/PlanetBoard/BuildingShapeGeometryTest.cs` | Tests unchanged geometry class |
| `Scripts/Constructables/Building.cs` | Data model unchanged |
| `Scripts/Constructables/Buildings/BuildingNode.cs` | 3D visual proxy; unrelated to 2D board |
| `Scripts/Structures/Resources/VisualDefinition.cs` | Board shape properties unchanged |
| `Scripts/Structures/Resources/BuildingDefinition.cs` | Data model unchanged |
| `Scripts/ProceduralGeneration/IOrbitalBody.cs` | Interface unchanged |
| `Scripts/UtilityLibrary/SignalBus.cs` | Signal signatures unchanged |

---

## 4. Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| 2D plane container | SubViewportContainer + SubViewport | Clean isolation of the 2D world from UI chrome; native Camera2D features (smoothing, limits, drag margins); true 2D coordinate system |
| Projection | Equirectangular (kept from current) | User preference; maps longitude→X, latitude→Y; simple and shows the full sphere |
| Relaxation | Removed entirely | Buildings should sit at their projected 3D positions; overlapping buildings can be resolved by the user via drag; eliminates the "position doesn't reflect 3D" problem |
| Building movement | Free drag + manual pin | Buildings can be freely dragged to any position; pinned buildings snap back to their 3D-projected position when `PinToProjection()` is called; defaults to `IsPinned = true` on creation |
| Camera control | Real Camera2D with BoardCameraController | Middle-mouse drag for pan; scroll wheel for cursor-anchored zoom; `FitToContent()` for framing all buildings; native Godot camera smoothing available if desired |
| Port/link rendering | Port dots on polygon edges + link lines between ports | Preserves current interaction model; ports drawn by each `BuildingNode2D` in local space; links drawn by a single `BoardLinkRenderer` in world space |
| Mode system | `IPlanetBoardMode` interface adapted to `BoardWorld` | Same strategy pattern; interface methods take `BoardWorld` instead of `PlanetBoardView` so modes can access `BuildingNode2D` nodes directly |
| Background/grid | Drawn by a dedicated background Node2D child of BoardWorld | Grid gives a sense of motion when panning; purely cosmetic |

---

## 5. Coordinate Systems

### Board Space (World Space)
- Units are pixels at zoom = 1.0
- Origin is at the center of the equirectangular projection (longitude = 0, latitude = 0)
- `BuildingNode2D.Position` is in board space
- `BoardWorld.Position = Vector2.Zero` (the world origin)

### Screen Space
- Pixels within the SubViewport
- `SubViewportContainer` stretches the SubViewport to fill the Control
- `Camera2D` handles the board→screen transform:
  - `screen = (board - Camera.Position) * Camera.Zoom + ViewportSize / 2`

### Conversion (provided by BoardCameraController)
```
BoardToScreen(boardPos):
  return (boardPos - Position) * Zoom.X + ViewportSize / 2

ScreenToBoard(screenPos):
  return (screenPos - ViewportSize / 2) / Zoom.X + Position
```

### Projection (BoardLayoutEngine.Project)
```
Given Vector3 center (unit sphere):
  u = Atan2(center.Z, center.X)   // longitude ∈ [-π, π]
  v = Asin(center.Y)              // latitude  ∈ [-π/2, π/2]
  boardPos = new Vector2(u, v) * ProjectionScale   // ProjectionScale = 600f
```

---

## 6. Implementation Tickets

### Ticket 1: Simplify BoardLayoutEngine

**Title:** Remove relaxation passes from BoardLayoutEngine, keep only equirectangular projection

**Description:** The current `BoardLayoutEngine.Compute()` runs 20 collision-relaxation passes after projecting building positions. The new design keeps equirectangular projection only — buildings sit at their projected 3D positions and can be freely dragged later. This ticket strips the relaxation logic and simplifies `Compute` to a pure projection pass, while preserving the `Layout` result type and `Project()` method.

**Detailed Steps:**

1. Remove the `RelaxOnce` method and the `RelaxationPasses` constant (line 18, value 20).
2. Remove the `Padding` constant (line 17, value 16f) — it was only used by relaxation for minimum separation distance. The fallback row spacing can use `maxRadius * 2f + 16f` inline (or a new `FallbackRowSpacing = 16f` constant).
3. Remove the relaxation loop in `Compute()` (current lines 56–61: `for (int pass = 0; pass < RelaxationPasses; pass++) { if (!RelaxOnce(positions, maxRadius)) break; }`).
4. Simplify `Compute()`: After projecting all placed buildings via `Project()`, skip relaxation entirely. Unplaced buildings (those without a `PrimaryCell`) still get a fallback row below the main bbox.
5. Keep `ProjectionScale = 600f` unchanged.
6. Keep `Layout` class unchanged (Positions dictionary, BoundingBox, ShapeRadius).
7. Keep `Project(Vector3 center)` unchanged.
8. Keep `ResolveRadius` unchanged.
9. Keep `ComputeBBox`, `GetMin`, `GetMax` unchanged.
10. Update the XML doc comment on the class to remove mention of "collision-relaxation passes".

**Files to Modify:**
- `Scripts/UI/PlanetBoard/BoardLayoutEngine.cs`

**Files to Create:** None

**Files to Delete:** None

**Acceptance Criteria:**
- `BoardLayoutEngine.Compute()` returns the same `Layout` type with positions projected via equirectangular projection only.
- No relaxation passes run; the method is a single-pass projection + fallback row.
- `Project(Vector3)` is unchanged and still returns `new Vector2(u, v) * ProjectionScale`.
- `dotnet build` succeeds.
- The class doc comment accurately describes the simplified behavior.

**Dependencies:** None (foundational; all others depend on this)

---

### Ticket 2: Create BuildingNode2D

**Title:** Create BuildingNode2D — a Node2D subclass that draws a single building's polygon, icon, label, and port dots, and supports drag and pin

**Description:** Currently, `PlanetBoardView.BuildingNodeView` is a plain C# object and all drawing happens in `PlanetBoardView._Draw()`. The remake requires each building to be a real `Node2D` in the scene tree so it can be independently positioned, dragged, and interacted with. This ticket creates the `BuildingNode2D` class that owns the visual rendering of a single building and exposes port hit-testing and drag state.

**Detailed Steps:**

1. Create `Scripts/UI/PlanetBoard/BuildingNode2D.cs` as `public partial class BuildingNode2D : Node2D`.

2. **Nested `PortData` class:**
   ```csharp
   public sealed class PortData
   {
       public ResourceNode Node = null!;
       public Vector2 LocalAnchor;    // midpoint of polygon edge (local coords)
       public Vector2 OutwardNormal;   // unit normal pointing away from center
   }
   ```

3. **Public setup method:**
   ```csharp
   public void Setup(Building building, Vector2 projectedPosition)
   ```
   - Store `Building` reference.
   - Store `ProjectedPosition = projectedPosition`.
   - Set `Position = projectedPosition` (Node2D position in board space).
   - Read `Building.Definition.Visual` to resolve:
     - `Shape` = `BuildingShapeGeometry.NormalizeShape(visual?.Shape)`
     - `Radius` = `visual?.ShapeSize > 0 ? visual.ShapeSize : 64f`
     - `FillColor` = `visual?.ShapeColor ?? new Color(0.30f, 0.45f, 0.60f)`
     - `Icon` = `def?.Icon?.GetTexture(IconSize.Medium)`
     - `DisplayName` = `building.Name`
   - Build `List<PortData>` from `Building.Nodes`:
     - `LocalAnchor` = `BuildingShapeGeometry.GetPortAnchor(shape, node.Side, Vector2.Zero, radius)`
     - `OutwardNormal` = `BuildingShapeGeometry.GetPortNormal(shape, node.Side, Vector2.Zero, radius)`
   - Set `IsPinned = true` (buildings default to pinned at their projected position).

4. **Public properties:**
   - `Building Building` — the data model reference.
   - `Vector2 ProjectedPosition` — the original equirectangular-projected position.
   - `string Shape` — cached shape name.
   - `float Radius` — cached circumradius.
   - `Color FillColor` — cached fill color.
   - `Texture2D? Icon` — cached icon texture.
   - `string DisplayName` — cached display name.
   - `IReadOnlyList<PortData> Ports` — the port list.
   - `bool IsPinned` — whether the building is pinned to its projected position (default `true`).
   - `bool IsDragged` — whether the building is currently being dragged.
   - `PortData? HoveredPort` — the port currently being hovered (for highlight rendering).
   - `PortData? DragSourcePort` — the port being used as a drag source (for link creation highlight).

5. **Drag support methods:**
   - `public void StartDrag()` — sets `IsDragged = true`, calls `QueueRedraw()`.
   - `public void DragTo(Vector2 boardPosition)` — sets `Position = boardPosition`, `IsPinned = false`, calls `QueueRedraw()`.
   - `public void EndDrag()` — sets `IsDragged = false`, calls `QueueRedraw()`.
   - `public void PinToProjection()` — sets `Position = ProjectedPosition`, `IsPinned = true`, calls `QueueRedraw()`.
   - `public void Unpin()` — sets `IsPinned = false` (building stays at current position but is no longer snapped).

6. **`_Draw()` override** — all coordinates in local space (center = `Vector2.Zero`):
   - **Polygon:** `DrawColoredPolygon` using `BuildingShapeGeometry.GetVertices(shape, Vector2.Zero, radius)`.
   - **Outline:** `DrawPolyline` on the closed loop, black `(0, 0, 0, 0.85)`, width `Mathf.Max(1.5f, 1.0f)`.
   - **Icon:** If `Icon != null`, draw via `DrawTextureRect` centered above the label area, icon size = `Mathf.Min(radius * 0.9f, 64f)`.
   - **Label:** `DrawString` with `ThemeDB.FallbackFont`, font size clamped 8–20, white `(1, 1, 1, 0.9f)`, positioned below center.
   - **Port dots:** For each `PortData p`:
     - Draw position: `p.LocalAnchor + p.OutwardNormal * PortStubBoard` (where `PortStubBoard = 8f`).
     - Filled circle radius: `PortVisualRadius = 6f`.
     - Color by `p.Node.Kind`: Import = blue `(0.30, 0.55, 0.95)`, Export = orange `(0.95, 0.55, 0.20)`, Flex = gray `(0.75, 0.75, 0.75)`.
     - Black outline arc.
     - If `p.Node.Link != null`, draw a green ring `(0.30, 0.85, 0.40)` at `PortVisualRadius + 3f`.
     - If `p == HoveredPort` or `p == DragSourcePort`, draw a white highlight ring `(1, 1, 1, 0.9)` at `PortVisualRadius + 6f`.

7. **Hit testing methods:**
   - `public PortData? HitTestPort(Vector2 localMousePos, float hitRadius = 12f)` — for each port, compute drawn position (`LocalAnchor + OutwardNormal * PortStubBoard`), check distance to `localMousePos`, return closest within `hitRadius`.
   - `public bool HitTestShape(Vector2 localMousePos)` — point-in-polygon test against shape vertices. Use the ray-casting algorithm: cast a horizontal ray from `localMousePos` and count edge crossings.

8. **Setter methods that trigger redraw:**
   - When `HoveredPort` or `DragSourcePort` is set, call `QueueRedraw()` so the highlight ring updates.

9. Use `GameLogger` for any debug/warning logging, never `GD.Print`.

**Files to Create:**
- `Scripts/UI/PlanetBoard/BuildingNode2D.cs`

**Files to Modify:** None

**Files to Delete:** None

**Acceptance Criteria:**
- `BuildingNode2D` compiles and builds successfully.
- `Setup()` correctly reads all visual data from `Building.Definition.Visual`.
- `_Draw()` renders polygon, outline, icon, label, and port dots in local coordinates.
- `HitTestPort()` returns the closest port within hit radius, null otherwise.
- `HitTestShape()` returns true for points inside the polygon, false otherwise.
- Drag methods (`StartDrag`, `DragTo`, `EndDrag`) update position and redraw state.
- `PinToProjection()` snaps position back to `ProjectedPosition` and sets `IsPinned = true`.
- `IsPinned` defaults to `true`.
- No references to `BoardCamera`, `PlanetBoardView`, or `BoardWorld` — this class is self-contained.
- File-scoped namespace `UI.PlanetBoard`.
- `_camelCase` for private fields, PascalCase for public API.

**Dependencies:** Ticket 1 (BoardLayoutEngine simplification — needed so `Project()` is the only positioning method)

---

### Ticket 3: Create BoardCameraController

**Title:** Create BoardCameraController — a Camera2D script for middle-mouse pan and scroll-wheel cursor-anchored zoom

**Description:** The current `BoardCamera` is a pure-C# class that manually transforms between board-space and screen-space. The remake replaces it with a real Godot `Camera2D` node, which natively handles the coordinate transform. This ticket creates `BoardCameraController`, a script attached to a `Camera2D` that handles middle-mouse drag for panning and scroll-wheel for cursor-anchored zoom.

**Detailed Steps:**

1. Create `Scripts/UI/PlanetBoard/BoardCameraController.cs` as `public partial class BoardCameraController : Camera2D`.

2. **Export properties:**
   ```csharp
   [Export] public float MaxZoom = 4.0f;
   [Export] public float MinZoom = 0.1f;
   [Export] public float ZoomStep = 1.1f;
   [Export] public float FitMargin = 0.95f;
   ```

3. **Private state:**
   ```csharp
   private bool _panning;
   private Vector2 _panStartScreen;
   private Vector2 _panStartOffset;
   private Vector2 _viewportSize = new(800, 600);
   private Rect2 _contentBBox = new(Vector2.Zero, Vector2.One);
   ```

4. **`_Ready()`:**
   - Set `AnchorMode = Camera2D.AnchorModeEnum.DragCenter` so `Position` directly maps to the board-space center of view.
   - Set `ProcessCallback = Camera2D.CameraProcessCallback.Idle`.
   - Set initial `Zoom = Vector2.One`.

5. **`_UnhandledInput(InputEvent @event)`:**
   - **Scroll up** (`InputEventMouseButton`, `ButtonIndex == WheelUp`):
     - Call `ZoomAtScreen(mousePosition, ZoomStep)`.
     - Accept event.
   - **Scroll down** (`InputEventMouseButton`, `ButtonIndex == WheelDown`):
     - Call `ZoomAtScreen(mousePosition, 1f / ZoomStep)`.
     - Accept event.
   - **Middle mouse pressed**: Start panning — record `_panStartScreen = mb.Position`, `_panStartOffset = Position`.
   - **Middle mouse released**: Stop panning — `_panning = false`.
   - **Mouse motion when panning**:
     - `Position = _panStartOffset - (mm.Position - _panStartScreen) / Zoom.X`.
     - Clamp position so the content bbox center is always reachable.

6. **`public void ZoomAtScreen(Vector2 screenPos, float factor)`:**
   - Convert screen pos to board-space **before** zoom:
     `Vector2 boardBefore = ScreenToBoard(screenPos)`.
   - Apply zoom:
     `float newZoom = Mathf.Clamp(Zoom.X * factor, MinZoom, MaxZoom)`.
     `Zoom = new Vector2(newZoom, newZoom)`.
   - Convert same screen pos to board-space **after** zoom:
     `Vector2 boardAfter = ScreenToBoard(screenPos)`.
   - Adjust position so the board point under cursor stays fixed:
     `Position += boardBefore - boardAfter`.

7. **`public void FitToContent(Rect2 contentBBox)`:**
   - Store `_contentBBox = contentBBox`.
   - Compute `MinZoom` from content bbox and viewport:
     ```csharp
     float fx = _viewportSize.X / Mathf.Max(1f, contentBBox.Size.X);
     float fy = _viewportSize.Y / Mathf.Max(1f, contentBBox.Size.Y);
     MinZoom = Mathf.Min(fx, fy) * FitMargin;
     if (MinZoom > MaxZoom) MinZoom = MaxZoom;
     ```
   - Set `Zoom = new Vector2(MinZoom, MinZoom)`.
   - Set `Position = contentBBox.GetCenter()`.

8. **`public void UpdateViewportSize(Vector2 size)`:**
   - Store `_viewportSize = size`.
   - Recompute `MinZoom` using current `_contentBBox`.

9. **Public coordinate helpers:**
   ```csharp
   public Vector2 BoardToScreen(Vector2 boardPos) =>
       (boardPos - Position) * Zoom.X + _viewportSize * 0.5f;

   public Vector2 ScreenToBoard(Vector2 screenPos) =>
       (screenPos - _viewportSize * 0.5f) / Zoom.X + Position;
   ```

10. **Position clamping** (private method called after pan):
    - When at min zoom, lock `Position = _contentBBox.GetCenter()`.
    - When above min zoom, clamp so the bbox center is within half a viewport of the camera position.

11. Use `GameLogger` for any logging.

**Files to Create:**
- `Scripts/UI/PlanetBoard/BoardCameraController.cs`

**Files to Modify:** None

**Files to Delete:** None

**Acceptance Criteria:**
- `BoardCameraController` compiles and builds.
- Middle-mouse drag pans the camera (board-space movement inversely proportional to zoom).
- Scroll-wheel zooms with cursor-anchored invariant (the board point under the cursor stays fixed).
- `FitToContent()` sets zoom and position to frame the given bbox.
- `MinZoom` is computed from content bbox and viewport size.
- `BoardToScreen` / `ScreenToBoard` produce correct coordinate transforms.
- No dependency on the old `BoardCamera` class.
- File-scoped namespace `UI.PlanetBoard`.

**Dependencies:** None (can be developed in parallel with Ticket 2)

---

### Ticket 4: Create BoardLinkRenderer

**Title:** Create BoardLinkRenderer — a Node2D that draws all ResourceLink lines and drag previews

**Description:** In the current system, link lines are drawn in `PlanetBoardView._Draw()`. In the remake, a dedicated `BoardLinkRenderer` Node2D child of `BoardWorld` draws all link lines in its own `_Draw()`. It reads the current set of `ResourceLink`s from the buildings' ports and draws lines between the corresponding port positions on the `BuildingNode2D` nodes.

**Detailed Steps:**

1. Create `Scripts/UI/PlanetBoard/BoardLinkRenderer.cs` as `public partial class BoardLinkRenderer : Node2D`.

2. **Private state:**
   ```csharp
   private List<(ResourceLink Link, BuildingNode2D.PortData Source, BuildingNode2D.PortData Target)> _links = new();
   private ResourceLink? _hoveredLink;
   private Vector2? _dragPreviewFrom;
   private Vector2? _dragPreviewTo;
   private bool _dragPreviewValid;
   ```

3. **`public void RebuildLinks(IReadOnlyList<BuildingNode2D> buildings)`:**
   - Clear `_links`.
   - Use a `HashSet<ResourceLink>` to avoid duplicate pairs.
   - Iterate all buildings' ports; for each port with a non-null `Node.Link`, find the matching port on another building by iterating all buildings' ports again.
   - Store the `(Link, SourcePort, TargetPort)` tuples.
   - Call `QueueRedraw()`.

4. **`public void SetHoveredLink(ResourceLink? link)`:**
   - Set `_hoveredLink = link`.
   - Call `QueueRedraw()`.

5. **`public void SetDragPreview(Vector2? fromWorld, Vector2? toWorld, bool valid)`:**
   - Set `_dragPreviewFrom = fromWorld`, `_dragPreviewTo = toWorld`, `_dragPreviewValid = valid`.
   - Call `QueueRedraw()`.

6. **`_Draw()` override:**
   - For each `(link, src, tgt)` in `_links`:
     - Compute world positions:
       `srcWorld = src.Owner.Position + src.LocalAnchor + src.OutwardNormal * PortStubBoard`
       `tgtWorld = tgt.Owner.Position + tgt.LocalAnchor + tgt.OutwardNormal * PortStubBoard`
     - Since `BoardLinkRenderer` is a direct child of `BoardWorld` at `Position = Vector2.Zero`, world positions equal local positions.
     - Color: if `link == _hoveredLink`, yellow `(0.95, 0.85, 0.20)`; otherwise green `(0.30, 0.85, 0.40)`.
     - Line width: `Mathf.Max(2f, 1.5f)`.
     - `DrawLine(srcWorld, tgtWorld, color, width, antialiased: true)`.
   - If `_dragPreviewFrom != null && _dragPreviewTo != null`:
     - Color: `_dragPreviewValid` ? green `(0.30, 0.85, 0.40)` : red `(0.85, 0.30, 0.30)`.
     - `DrawDashedLine(from, to, color, 2f, 8f)`.

7. **`public (ResourceLink Link, BuildingNode2D.PortData Source, BuildingNode2D.PortData Target)? HitTestEdge(Vector2 worldPos, float hitRadius = 6f)`:**
   - For each link tuple, compute distance from `worldPos` to the line segment between source and target world positions.
   - Return the closest link within `hitRadius`, or null.

8. **Private helper:**
   ```csharp
   private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
   ```
   Same algorithm as the current `PlanetBoardView.DistanceToSegment`: project p onto segment ab, clamp t ∈ [0,1], return distance from p to projected point.

9. **Constant:** `PortStubBoard = 8f` (same as `PlanetBoardView.PortStubBoard`).

**Files to Create:**
- `Scripts/UI/PlanetBoard/BoardLinkRenderer.cs`

**Files to Modify:** None

**Files to Delete:** None

**Acceptance Criteria:**
- `BoardLinkRenderer` compiles and builds.
- `RebuildLinks()` correctly pairs source/target ports across buildings.
- `_Draw()` renders colored lines between linked port positions.
- Hovered link renders in yellow; normal links in green.
- `HitTestEdge()` returns the closest link within hit radius.
- Drag preview draws a dashed line from source port to cursor, colored by validity.
- No dependency on `PlanetBoardView` or `BoardCamera`.
- File-scoped namespace `UI.PlanetBoard`.

**Dependencies:** Ticket 2 (BuildingNode2D — needed for `PortData` type and port position computation)

---

### Ticket 5: Create BoardWorld

**Title:** Create BoardWorld — root Node2D managing BuildingNode2D children, BoardLinkRenderer, signal-driven refresh, and input dispatch

**Description:** `BoardWorld` is the central orchestrator of the new architecture. It's a `Node2D` that sits inside the `SubViewport` and serves as the parent of all `BuildingNode2D` instances and the `BoardLinkRenderer`. It listens to `SignalBus` signals to create/destroy building nodes, coordinates input dispatch to the active `IPlanetBoardMode`, and handles building drag and port interaction.

**Detailed Steps:**

1. Create `Scripts/UI/PlanetBoard/BoardWorld.cs` as `public partial class BoardWorld : Node2D`.

2. **Child node references:**
   ```csharp
   private BoardLinkRenderer _linkRenderer = null!;
   ```

3. **Private state:**
   ```csharp
   private IOrbitalBody? _body;
   private IPlanetBoardMode? _mode;
   private readonly Dictionary<Building, BuildingNode2D> _buildingNodes = new();
   private BuildingNode2D? _draggedBuilding;
   private Vector2 _dragOffset;
   private BuildingNode2D.PortData? _dragSourcePort;
   private Vector2 _dragCursorWorld;
   private bool _dragValid;
   private BuildingNode2D.PortData? _hoveredPort;
   private ResourceLink? _hoveredLink;
   ```

4. **`_Ready()`:**
   - Create `BoardLinkRenderer` as child:
     ```csharp
     _linkRenderer = new BoardLinkRenderer { Name = "BoardLinkRenderer" };
     AddChild(_linkRenderer);
     ```
   - Connect to `SignalBus`:
     ```csharp
     var bus = SignalBus.Instance;
     if (bus != null)
     {
         bus.BuildingConstructed += OnBuildingsChanged;
         bus.BuildingRemoved += OnBuildingsChanged;
         bus.ResourceLinkChanged += OnLinksChanged;
     }
     ```

5. **`_ExitTree()`:** Disconnect from `SignalBus`.

6. **`public void SetBody(IOrbitalBody? body)`:** Store body, call `RefreshFromBody()`.

7. **`public void SetMode(IPlanetBoardMode? mode)`:** Call `_mode?.OnExit()`, set `_mode`, if `_mode != null && _body != null` call `_mode.OnEnter(this, _body)`.

8. **`RefreshFromBody()`:**
   - Clear all `BuildingNode2D` children: iterate `_buildingNodes.Values`, call `QueueFree()` on each, then clear the dictionary.
   - If `_body != null`, get active buildings from `_body.BuildingConstructionMgr.GetActiveBuildings()`.
   - Run `BoardLayoutEngine.Compute(buildings)` to get projected positions.
   - For each `(building, pos)` in `layout.Positions`:
     - Create `BuildingNode2D`, call `Setup(building, pos)`, add as child, add to `_buildingNodes`.
   - Call `_linkRenderer.RebuildLinks(GetAllBuildingNodes())`.

9. **`OnBuildingsChanged(int _)`:** Call `RefreshFromBody()`.

10. **`OnLinksChanged()`:** Call `_linkRenderer.RebuildLinks(GetAllBuildingNodes())`.

11. **`GetAllBuildingNodes()`:** Returns `_buildingNodes.Values.ToList()` as `IReadOnlyList<BuildingNode2D>`.

12. **Input handling — `_UnhandledInput(InputEvent @event)`:**
    - **`InputEventMouseButton mb`:**
      - Get cursor world position: `Vector2 worldPos = CameraController != null ? CameraController.ScreenToBoard(mb.Position) : Vector2.Zero`.
      - **Left press:**
        1. Hit test ports: iterate all `BuildingNode2D`, convert `worldPos` to building-local coords (`worldPos - bn.Position`), call `bn.HitTestPort(localPos)`. If port hit:
           - Try `_mode?.OnPortDragStart(port.Node)`. If accepted → set `_dragSourcePort`, `_dragCursorWorld`, `_dragValid = false`, update `bn.DragSourcePort`, accept event.
           - If not accepted → call `_mode?.OnPortClick(port.Node, mb.ButtonIndex)`, accept event.
        2. If no port hit, hit test building shapes: iterate all `BuildingNode2D`, call `bn.HitTestShape(localPos)`. If building hit:
           - Set `_draggedBuilding = bn`, compute `_dragOffset = worldPos - bn.Position`, call `bn.StartDrag()`, accept event.
        3. If no building hit, hit test edges via `_linkRenderer.HitTestEdge(worldPos)`. If edge hit:
           - Call `_mode?.OnEdgeClick(link, mb.ButtonIndex)`, accept event.
        4. If nothing hit:
           - Call `_mode?.OnEmptyClick(worldPos, mb.ButtonIndex)`, accept event.
      - **Left release:**
        1. If `_dragSourcePort != null`: find drop port (hit test all ports), call `_mode?.OnPortDragEnd(dropPort?.Node)`, clear `_dragSourcePort`, clear `DragSourcePort` on building, `_linkRenderer.SetDragPreview(null, null, false)`.
        2. If `_draggedBuilding != null`: call `_draggedBuilding.EndDrag()`, clear `_draggedBuilding`.
      - **Right press:**
        1. If `_dragSourcePort != null`: cancel → call `_mode?.OnPortDragEnd(null)`, clear state.
        2. Otherwise: hit test edges/ports, call mode methods with `MouseButton.Right`.
    - **`InputEventMouseMotion mm`:**
      1. Get world cursor position.
      2. If `_draggedBuilding != null`: call `_draggedBuilding.DragTo(worldPos - _dragOffset)`.
      3. If `_dragSourcePort != null`:
         - Update `_dragCursorWorld`.
         - Hit test drop port, set `_dragValid` based on `ResourceLink.CanConnect`.
         - Call `_mode?.OnPortDragUpdate(worldPos, dropPort?.Node)`.
         - Update `_linkRenderer.SetDragPreview(...)` with source port world position and cursor.
      4. Otherwise:
         - Hit test ports across all buildings, update `_hoveredPort`, set `bn.HoveredPort` on the relevant building, clear on others.
         - If no port hovered, hit test edges, update `_hoveredLink`, call `_linkRenderer.SetHoveredLink(...)`.

13. **`public Vector2 ScreenToWorld(Vector2 screenPos)`:** Delegate to `CameraController.ScreenToBoard(screenPos)`.

14. **`public BoardCameraController? CameraController`** — set by `PlanetBoardView` after scene setup.

15. **Public accessors for mode use:**
    ```csharp
    public IReadOnlyList<BuildingNode2D> BuildingNodes => _buildingNodes.Values.ToList();
    public BuildingNode2D.PortData? DragSourcePort => _dragSourcePort;
    public Vector2 DragCursorWorld => _dragCursorWorld;
    public bool DragValid => _dragValid;
    public IPlanetBoardMode? ActiveMode => _mode;
    public IOrbitalBody? Body => _body;
    ```

**Files to Create:**
- `Scripts/UI/PlanetBoard/BoardWorld.cs`

**Files to Modify:** None

**Files to Delete:** None

**Acceptance Criteria:**
- `BoardWorld` compiles and builds.
- `SetBody()` creates `BuildingNode2D` children for all active buildings at their projected positions.
- Signal handlers correctly refresh buildings and links.
- Building drag: left-click on a building shape starts drag; motion moves the building; release ends drag.
- Port drag: left-click on a port starts link drag; motion updates preview; release on valid port completes; right-click cancels.
- Hover state updates port highlights and link highlights.
- Mode dispatch: all input events are forwarded to the active `IPlanetBoardMode`.
- `_linkRenderer` is created as a child and kept in sync.
- File-scoped namespace `UI.PlanetBoard`.

**Dependencies:** Tickets 1, 2, 3, 4

---

### Ticket 6: Adapt IPlanetBoardMode and All Mode Implementations

**Title:** Adapt IPlanetBoardMode interface and all mode implementations to work with BoardWorld/BuildingNode2D

**Description:** The `IPlanetBoardMode` interface currently takes `PlanetBoardView` and `BoardCamera` parameters. This ticket updates the interface to accept `BoardWorld` instead of `PlanetBoardView`, and `BoardCameraController` instead of `BoardCamera`. All three mode implementations are updated accordingly.

**Detailed Steps:**

1. **Update `Scripts/UI/PlanetBoard/Modes/IPlanetBoardMode.cs`:**
   - Change `void OnEnter(PlanetBoardView view, IOrbitalBody body)` → `void OnEnter(BoardWorld world, IOrbitalBody body)`.
   - Change `void DrawOverlay(CanvasItem ci, BoardCamera cam)` → `void DrawOverlay(CanvasItem ci, BoardCameraController cam)`.
   - All other method signatures remain unchanged (they use `ResourceNode`, `ResourceLink`, `Vector2`, `MouseButton`).

2. **Update `Scripts/UI/PlanetBoard/Modes/ResourceLinkPlanningMode.cs`:**
   - Change `_view` field from `PlanetBoardView?` to `BoardWorld?` and rename to `_world`.
   - Update `OnEnter(BoardWorld world, IOrbitalBody body)` — store `_world = world`, `_body = body`.
   - In `OnExit()` — clear `_world = null`, `_body = null`.
   - In `OnPortDragEnd(ResourceNode? dropPort)`:
     - Replace `_view.DragSource` with `_world.DragSourcePort?.Node`.
     - Replace `_view.DragValid` with `_world.DragValid`.
   - `DrawOverlay` — signature change only (currently empty body).

3. **Update `Scripts/UI/PlanetBoard/Modes/TransferRoutePlanningMode.cs`:**
   - Change `_view` field from `PlanetBoardView?` to `BoardWorld?` and rename to `_world`.
   - Update `OnEnter(BoardWorld world, IOrbitalBody body)` — store `_world = world`, `_body = body`, set `SelectedBuildingId = ""`, call `world.QueueRedraw()`.
   - In `OnExit()` — clear `_world = null`, `_body = null`, set `SelectedBuildingId = ""`.
   - In `DrawOverlay(CanvasItem ci, BoardCameraController cam)`:
     - Replace `_view.BuildingViews` iteration with `_world.BuildingNodes`.
     - For each `BuildingNode2D bn`:
       - `string id = bn.Building?.Id ?? ""`
       - If `id` is empty or `!_body.HasTransferEndpoint(id)` → skip.
       - `bool isOrigin = id == OriginBuildingId`
       - `bool isSelected = id == SelectedBuildingId`
       - `Vector2 center = cam.BoardToScreen(bn.Position)`
       - `float radiusScreen = bn.Radius * cam.Zoom.X + 6f`
       - Draw arc ring as before with same color logic.
   - In `TryPickFromBuilding(Building? building)`: replace `_view?.QueueRedraw()` with `_world?.QueueRedraw()`.

4. **Update `Scripts/UI/PlanetBoard/Modes/OverviewMode.cs`:**
   - Update `OnEnter(BoardWorld world, IOrbitalBody body)` signature (body remains empty).
   - `DrawOverlay` — signature change only (empty body).

**Files to Modify:**
- `Scripts/UI/PlanetBoard/Modes/IPlanetBoardMode.cs`
- `Scripts/UI/PlanetBoard/Modes/ResourceLinkPlanningMode.cs`
- `Scripts/UI/PlanetBoard/Modes/TransferRoutePlanningMode.cs`
- `Scripts/UI/PlanetBoard/Modes/OverviewMode.cs`

**Files to Create:** None

**Files to Delete:** None

**Acceptance Criteria:**
- `IPlanetBoardMode` interface uses `BoardWorld` and `BoardCameraController`.
- All three mode implementations compile and build.
- `ResourceLinkPlanningMode.OnPortDragEnd` correctly accesses drag state from `BoardWorld`.
- `TransferRoutePlanningMode.DrawOverlay` correctly iterates `BuildingNode2D` nodes and draws selection rings.
- No remaining references to `PlanetBoardView` or `BoardCamera` in any mode file.
- File-scoped namespaces preserved.

**Dependencies:** Tickets 2, 3, 5

---

### Ticket 7: Rewrite PlanetBoardView as SubViewport Wrapper

**Title:** Rewrite PlanetBoardView — from monolithic _Draw() Control to thin SubViewportContainer wrapper managing Camera2D and BoardWorld

**Description:** This is the central integration ticket. `PlanetBoardView` is rewritten from a monolithic `_Draw()`-based `Control` into a thin wrapper that programmatically creates a `SubViewportContainer → SubViewport → Camera2D + BoardWorld` hierarchy in `_Ready()`. All old inner classes (`PortView`, `BuildingNodeView`), drawing methods, hit-testing, input handling, and `BoardCamera` dependency are removed. `PlanetBoardView` now delegates to `BoardWorld` for building management and mode dispatch, and to `BoardCameraController` for camera control.

**Detailed Steps:**

1. **Rewrite `Scripts/UI/PlanetBoard/PlanetBoardView.cs`:**

   - Keep base class as `Control` (not `SubViewportContainer`) so the existing `PlanetBoardWindow` export reference and `.tscn` node type remain valid.
   - Remove inner classes `PortView` and `BuildingNodeView` entirely.
   - Remove all `_Draw()` overrides and drawing methods (`DrawBackground`, `DrawGrid`, `DrawLinks`, `DrawBuildings`, `DrawBuilding`, `DrawPort`, `DrawDragPreview`, `LoopClosed`).
   - Remove `BoardCamera Camera` property.
   - Remove `_buildings`, `_links`, `_hoveredPort`, `_hoveredEdge`, `_panning`, `_lastMousePos`, `_dragSource`, `_dragCursorScreen`, `_dragValid` fields.
   - Remove `HitTestPort`, `HitTestEdge`, `DistanceToSegment` methods.
   - Remove `HandleMouseButton`, `HandleMouseMotion`, `_GuiInput` override.
   - Remove `BuildView`, `RebuildLinkViews`, `FindPort` methods.

   - **New private fields:**
     ```csharp
     private SubViewportContainer? _container;
     private SubViewport? _viewport;
     private Camera2D? _cameraNode;
     private BoardCameraController? _cameraController;
     private BoardWorld? _world;
     ```

   - **`_Ready()`:**
     ```csharp
     FocusMode = FocusModeEnum.All;
     MouseFilter = MouseFilterEnum.Stop;

     _container = new SubViewportContainer
     {
         Name = "BoardSubViewportContainer",
         Stretch = true,
     };
     _container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
     AddChild(_container);

     _viewport = new SubViewport
     {
         Name = "BoardSubViewport",
         RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
     };
     _container.AddChild(_viewport);

     _cameraNode = new Camera2D
     {
         Name = "BoardCamera2D",
         AnchorMode = Camera2D.AnchorModeEnum.DragCenter,
         ProcessCallback = Camera2D.CameraProcessCallback.Idle,
     };
     _cameraController = _cameraNode as BoardCameraController;
     // If Camera2D doesn't auto-attach the script, attach it:
     // Actually, we should create the node with the script attached.
     // Use: _cameraNode = new Camera2D(); then set script, or
     // better: create BoardCameraController directly since it extends Camera2D.
     _cameraNode = new BoardCameraController { Name = "BoardCamera2D" };
     _cameraController = (BoardCameraController)_cameraNode;
     _viewport.AddChild(_cameraNode);
     // Ensure it's the current camera
     _cameraNode.MakeCurrent();

     _world = new BoardWorld { Name = "BoardWorld" };
     _viewport.AddChild(_world);
     _world.CameraController = _cameraController;

     Resized += OnResized;
     OnResized(); // initial sizing
     ```

   - **`OnResized()`:**
     ```csharp
     if (_viewport != null)
         _viewport.Size = Size;
     if (_cameraController != null)
         _cameraController.UpdateViewportSize(Size);
     ```

   - **Public API (preserved for `PlanetBoardWindow` compatibility):**
     ```csharp
     public IOrbitalBody? Body => _world?.Body;
     public IPlanetBoardMode? ActiveMode => _world?.ActiveMode;
     public BoardWorld World => _world!;
     public BoardCameraController CameraController => _cameraController!;
     public BoardCameraController Camera => CameraController; // compatibility alias

     public void SetBody(IOrbitalBody? body) => _world?.SetBody(body);
     public void SetMode(IPlanetBoardMode? mode) => _world?.SetMode(mode);
     public void RefreshFromBody() => _world?.RefreshFromBody();
     public void RefreshLinks() => _world?.OnLinksChanged();

     // Convenience for test scene
     public void ResetCamera()
     {
         // Re-fetch content bbox from layout and fit
         if (_world != null && _cameraController != null)
         {
             var buildings = _world.BuildingNodes;
             if (buildings.Count == 0) return;
             var layout = BoardLayoutEngine.Compute(
                 buildings.Select(bn => bn.Building).Where(b => b != null).ToList()!);
             _cameraController.FitToContent(layout.BoundingBox);
         }
     }
     ```

   - **`_ExitTree()`:** No signal disconnection needed here (BoardWorld handles its own).

2. **Update `UI/PlanetBoard/PlanetBoard.tscn`:**
   - The `BoardView` node remains `type="Control"` with the `PlanetBoardView.cs` script.
   - No structural changes needed in the `.tscn` — the SubViewport hierarchy is created programmatically.

3. **Delete `Scripts/UI/PlanetBoard/BoardCamera.cs`** — fully replaced by `BoardCameraController`.

4. **Delete `Scripts/UI/PlanetBoard/BoardCamera.cs.uid`** if it exists.

**Files to Modify:**
- `Scripts/UI/PlanetBoard/PlanetBoardView.cs` — Complete rewrite

**Files to Create:** None

**Files to Delete:**
- `Scripts/UI/PlanetBoard/BoardCamera.cs`
- `Scripts/UI/PlanetBoard/BoardCamera.cs.uid` (if present)

**Acceptance Criteria:**
- `PlanetBoardView` is a `Control` that programmatically creates `SubViewportContainer → SubViewport → Camera2D (BoardCameraController) + BoardWorld`.
- All old `_Draw()` code, inner classes, and `BoardCamera` dependency are removed.
- Public API (`SetBody`, `SetMode`, `Camera`, `Body`, `ActiveMode`) is preserved or has clear compatibility shims.
- `PlanetBoard.tscn` loads without errors in the Godot editor.
- `dotnet build` succeeds.
- `BoardCamera.cs` is deleted and no remaining code references it.
- No compilation warnings about missing types or methods.

**Dependencies:** Tickets 2, 3, 4, 5, 6

---

### Ticket 8: Update Consumers for New API

**Title:** Update PlanetBoardWindow, TestPlanetBoardScene, and PickDestinationView to work with the new PlanetBoardView/BoardWorld API

**Description:** With `PlanetBoardView` rewritten, several consumers need updates. `PlanetBoardWindow` references `PlanetBoardView` and its properties. `TestPlanetBoardScene` accesses `view.Camera.FitToContent()`. `PickDestinationView` creates a `PlanetBoardView` programmatically.

**Detailed Steps:**

1. **Update `Scripts/UI/PlanetBoard/PlanetBoardWindow.cs`:**
   - The `_viewBoard` export still points to a `PlanetBoardView` — type unchanged, no change needed.
   - `View` property returns `_viewBoard!` — no change.
   - `OpenForBody` calls `_viewBoard?.SetBody(body)` — no change.
   - `SelectMode` calls `_viewBoard?.SetMode(strategy)` — no change.
   - Verify: no other direct property access that changed. Current code does not reference `BuildingViews`, `LinkViews`, `DragSource`, etc. — no changes needed.

2. **Update `Scripts/UI/TestScenes/TestPlanetBoardScene.cs`:**
   - `OnResetCamera()`: Currently calls `view.Camera.FitToContent()`. Change to:
     ```csharp
     private void OnResetCamera()
     {
         _board.View.ResetCamera();
     }
     ```
     The `ResetCamera()` convenience method is on the new `PlanetBoardView` (added in Ticket 7).
   - Add "Pin All" and "Unpin All" test buttons:
     ```csharp
     col.AddChild(MakeButton("Pin All Buildings", OnPinAll));
     col.AddChild(MakeButton("Unpin All Buildings", OnUnpinAll));
     ```
   - Implement:
     ```csharp
     private void OnPinAll()
     {
         foreach (var bn in _body.GetChildrenByType<BuildingNode2D>())
             bn.PinToProjection();
         GameLogger.Info("Pinned all buildings to their projected positions.");
     }

     private void OnUnpinAll()
     {
         foreach (var bn in _body.GetChildrenByType<BuildingNode2D>())
             bn.Unpin();
         GameLogger.Info("Unpinned all buildings — they stay at their current positions.");
     }
     ```
     Note: `GetChildrenByType` may need a utility, or iterate `_board.View.World.BuildingNodes`.
   - `_board.View?.ActiveMode is TransferRoutePlanningMode` — this still works since `ActiveMode` is preserved.

3. **Verify `Scripts/UI/TransferPlanning/PickDestinationView.cs`:**
   - Line 137: `_board = new PlanetBoardView { ... }` — still works since `PlanetBoardView` is still a `Control`.
   - Line 141: `_board.SetMode(_mode)` — still works.
   - Line 55: `_board.SetBody(_body)` — still works.
   - **No changes needed** if the public API is preserved.

**Files to Modify:**
- `Scripts/UI/PlanetBoard/PlanetBoardWindow.cs` — Verify and minor updates if needed
- `Scripts/UI/TestScenes/TestPlanetBoardScene.cs` — Update camera reset, add pin/unpin buttons

**Files to Create:** None

**Files to Delete:** None

**Acceptance Criteria:**
- `PlanetBoardWindow` compiles and all mode switching works.
- `TestPlanetBoardScene` "Reset Camera" button works with the new `BoardCameraController`.
- "Pin All" and "Unpin All" buttons function correctly.
- `PickDestinationView` creates a `PlanetBoardView` programmatically and it renders correctly.
- No references to the old `BoardCamera` class remain in any consumer.
- `dotnet build` succeeds.

**Dependencies:** Ticket 7

---

### Ticket 9: Update and Add Unit Tests

**Title:** Migrate existing tests and add new unit tests for BoardLayoutEngine, BuildingNode2D, BoardCameraController, and BoardLinkRenderer

**Description:** The existing `BoardCameraTest.cs` tests the old `BoardCamera` class which is being deleted. New tests are needed for the simplified `BoardLayoutEngine`, `BuildingNode2D` hit-testing, `BoardCameraController` zoom/pan behavior, and `BoardLinkRenderer` link rebuilding.

**Detailed Steps:**

1. **Delete `Tests/UI/PlanetBoard/BoardCameraTest.cs`** and its `.uid` file — tests the deleted `BoardCamera` class.

2. **Create `Tests/UI/PlanetBoard/BoardLayoutEngineTest.cs`:**
   - `Compute_EmptyList_ReturnsEmptyPositions` — empty list produces 0 positions, valid bbox.
   - `Compute_SingleBuildingWithPrimaryCell_ReturnsProjectedPosition` — one building with a known `PrimaryCell.Center` gets the expected projected position via `Project()`.
   - `Compute_BuildingsWithoutPrimaryCell_GetFallbackRow` — buildings with null `PrimaryCell` are placed in a row below the projected bbox.
   - `Project_KnownVectors` — test `Project(Vector3.Forward)` returns `(0, π/2 * 600)`, `Project(Vector3.Right)` returns `(0, 0)`, `Project(Vector3.Up)` returns `(0, π/2 * 600)`.
   - `Compute_NoRelaxation_OverlappingBuildingsStayAtSamePosition` — two buildings at the same 3D position get the same 2D position (no relaxation pushing them apart).

3. **Create `Tests/UI/PlanetBoard/BoardCameraControllerTest.cs`:**
   - `FitToContent_SetsZoomAndPosition` — verify zoom and position after fitting.
   - `ZoomAtScreen_KeepsCursorInvariant` — zoom and verify the board point under cursor stays fixed.
   - `MinZoom_ComputedFromContentAndViewport` — verify min zoom calculation.
   - `PanDelta_ScalesInverselyWithZoom` — verify pan offset is inversely proportional to zoom.
   - **Note:** These tests need `[RequireGodotRuntime]` since `Camera2D` is a Godot node.

4. **Create `Tests/UI/PlanetBoard/BuildingNode2DTest.cs`:**
   - `Setup_ReadsVisualData` — verify shape, radius, fill color, display name are read correctly.
   - `HitTestPort_ReturnsPortWithinRadius` — verify a local mouse position near a port returns that port.
   - `HitTestPort_ReturnsNullWhenNoneNearby` — verify a far-away position returns null.
   - `HitTestShape_ReturnsTrueInsidePolygon` — verify a point at the center returns true.
   - `HitTestShape_ReturnsFalseOutsidePolygon` — verify a far-away point returns false.
   - `PinToProjection_SnapsPositionBack` — verify `PinToProjection()` resets position to `ProjectedPosition`.
   - `DragTo_UpdatesPosition` — verify `DragTo()` updates `Position`.
   - **Note:** These tests need `[RequireGodotRuntime]` since `BuildingNode2D` extends `Node2D`.

5. **Create `Tests/UI/PlanetBoard/BoardLinkRendererTest.cs`:**
   - `RebuildLinks_NoBuildings_NoLinks` — empty building list produces 0 links.
   - `RebuildLinks_LinkedBuildings_ProducesLinkEntries` — two buildings with a linked port pair produce one link entry.
   - `HitTestEdge_PointNearSegment_ReturnsLink` — verify hit detection near a link line.
   - `HitTestEdge_PointFarFromAll_ReturnsNull` — verify null when no link is near.
   - **Note:** These tests need `[RequireGodotRuntime]` since `BoardLinkRenderer` extends `Node2D`.

6. **Keep `Tests/UI/PlanetBoard/BuildingShapeGeometryTest.cs`** unchanged — `BuildingShapeGeometry` is not modified.

**Files to Create:**
- `Tests/UI/PlanetBoard/BoardLayoutEngineTest.cs`
- `Tests/UI/PlanetBoard/BoardCameraControllerTest.cs`
- `Tests/UI/PlanetBoard/BuildingNode2DTest.cs`
- `Tests/UI/PlanetBoard/BoardLinkRendererTest.cs`

**Files to Delete:**
- `Tests/UI/PlanetBoard/BoardCameraTest.cs`
- `Tests/UI/PlanetBoard/BoardCameraTest.cs.uid`

**Acceptance Criteria:**
- All new test files compile and are discovered by gdUnit4.
- Pure unit tests (`BoardLayoutEngineTest`) run without Godot runtime.
- `[RequireGodotRuntime]` tests are correctly attributed.
- Old `BoardCameraTest.cs` is deleted.
- `BuildingShapeGeometryTest.cs` still passes unchanged.
- Test file structure mirrors `Scripts/UI/PlanetBoard/` layout.
- All tests use gdUnit4 assertions (`AssertThat(...).IsEqual(...)`).
- File-scoped namespace `Tests.UI.PlanetBoard`.

**Dependencies:** Tickets 1, 2, 3, 4

---

### Ticket 10: Integration Testing and Polish

**Title:** Integration testing and polish — validate the complete PlanetBoard scene works end-to-end

**Description:** After all individual components are built and unit-tested, this ticket performs integration validation: load the `PlanetBoard.tscn` in the test scene, spawn buildings, drag them, create links, switch modes, and verify everything works together. Any issues found are fixed here.

**Detailed Steps:**

1. **Launch `TestPlanetBoardScene`** via the Godot editor and verify:
   - Buildings appear at their equirectangular-projected positions.
   - Building polygons, icons, labels, and port dots render correctly.
   - Middle-mouse drag pans the camera smoothly.
   - Scroll wheel zooms with cursor-anchored behavior.
   - Left-click drag on a building shape moves the building freely.
   - "Reset Camera" button fits all content in view.
   - "Pin All" button snaps all buildings back to their projected positions.
   - "Unpin All" button marks all buildings as unpinned (they stay where they are).

2. **Test Resource Link mode:**
   - Drag from an unconnected port to another port — dashed preview line appears.
   - Drop on valid port — link is created, green line renders between ports.
   - Drop on invalid port — toast warning appears, no link created.
   - Drop on empty space — link creation cancelled.
   - Right-click on a link line — link is removed, green line disappears.
   - Hover over a port — white highlight ring appears.
   - Hover over a link — link turns yellow.
   - Right-click while dragging from a port — drag cancelled.

3. **Test Transfer Route mode:**
   - Switch to "Transfer Routes" mode.
   - Click on a transfer station building — orange selection ring appears.
   - Destination card in right panel updates with building info.
   - Continue button becomes enabled when a valid destination is selected.

4. **Test Overview mode:**
   - Switch to "Overview" mode.
   - Verify buildings render normally (no overlay interaction).

5. **Test building drag + pin behavior:**
   - Drag a building to a new position — building stays at new position after release.
   - Building's `IsPinned` becomes `false` after drag.
   - Click "Pin All" — all buildings snap back to their projected positions, `IsPinned = true`.
   - Drag a pinned building — it becomes unpinned and moves to the dragged position.

6. **Test `PickDestinationView` integration:**
   - Verify the programmatically-created `PlanetBoardView` renders and responds to input.
   - Transfer route selection works within the embedded board.

7. **Performance check:**
   - Spawn 20+ buildings with multiple links.
   - Verify frame rate stays above 60fps.
   - `BuildingNode2D._Draw()` approach should be efficient since Godot caches draw commands.

8. **Fix any bugs** found during integration testing.

9. **Verify `dotnet build` succeeds** with no warnings after all fixes.

**Files to Modify:**
- `Scripts/UI/TestScenes/TestPlanetBoardScene.cs` — Add pin/unpin buttons (partially done in Ticket 8)
- Any files with bugs found during integration

**Files to Create:** None

**Files to Delete:** None

**Acceptance Criteria:**
- All features described above work correctly in the test scene.
- Building drag is smooth and responsive.
- Camera pan/zoom works correctly with cursor-anchored zoom.
- Port drag creates links; right-click removes links.
- Mode switching works without errors.
- Transfer route selection rings render correctly.
- `PickDestinationView` integration works.
- No runtime errors or warnings in the Godot output panel.
- Frame rate is acceptable with 20+ buildings.
- `dotnet build` succeeds with no errors.

**Dependencies:** Tickets 8, 9

---

## 7. Dependency Graph

```
Ticket 1 (BoardLayoutEngine simplify)
  │
  ├──→ Ticket 2 (BuildingNode2D)
  │       │
  │       ├──→ Ticket 4 (BoardLinkRenderer)
  │       │       │
  │       │       └──→ Ticket 5 (BoardWorld) ←── Ticket 3 (BoardCameraController)
  │       │               │
  │       │               └──→ Ticket 6 (IPlanetBoardMode adapt)
  │       │                       │
  │       │                       └──→ Ticket 7 (PlanetBoardView rewrite)
  │       │                               │
  │       │                               └──→ Ticket 8 (Consumer updates)
  │       │                                       │
  │       └───────────────────────────────────────│──→ Ticket 9 (Tests)
  │                                               │       │
  │                                               │       └──→ Ticket 10 (Integration)
  │                                               │
  └───────────────────────────────────────────────┘

Parallelizable:
  - Tickets 2 and 3 can be developed simultaneously
  - Ticket 9 test files can be written in parallel with Tickets 5-8
    (they only need the class APIs from Tickets 1-4)
```

## 8. Risk Assessment

| Risk | Mitigation |
|------|------------|
| SubViewport input routing — controls inside SubViewport may not receive input as expected | Test input forwarding early; if `_GuiInput` doesn't fire, use `_UnhandledInput` on `BoardWorld` with `GetViewport().GetMousePosition()` mapping |
| BuildingNode2D `_Draw()` performance with many buildings | Godot caches draw commands per node; each `BuildingNode2D` only redraws on `QueueRedraw()`; link renderer redraws only when links change |
| Equirectangular projection distortion at poles | Document limitation; buildings near poles appear spread out — acceptable for now, future projection switch is a separate concern |
| Port hit-testing requires local-space conversion | `BoardWorld` must correctly transform world→local using `building.GlobalPosition` vs `building.Position`; test with non-origin camera position |
| Dragging a building invalidates linked port positions | `BoardLinkRenderer.RebuildLinks()` must be called after any building position change; `BoardWorld` calls it after `DragTo()` and `EndDrag()` |

## 9. Future Considerations (Out of Scope)

These are **not** part of this plan but are noted for future work:

- **Projection switching**: Allow users to choose between equirectangular, Mercator, or orthographic projections
- **Minimap**: A small overview of the full board in a corner, showing the current camera viewport
- **Building context menus**: Right-click on a building opens a context menu (upgrade, reconfigure, demolish)
- **Undo/redo for drag operations**: Track position changes and allow undo
- **Auto-layout**: An optional auto-arrange button that runs a force-directed layout
- **Smoothing on Camera2D**: Enable `PositionSmoothingEnabled` on the Camera2D for smooth pan transitions
