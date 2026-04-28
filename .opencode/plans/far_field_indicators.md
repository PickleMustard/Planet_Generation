# Plan: Far-Field Orbital Body Indicators (2D Screen-Space)

## Overview
Implement 2D screen-space icons to represent orbital bodies that are outside the camera's immediate visual range. These icons will use existing generated billboard textures and only appear when the body is more than 100 units beyond the camera's far plane.

## Requirements
- Use `IOrbitalBody.BillboardTextures` for the icon imagery.
- Condition for visibility: `distance > (camera.Far + 100)`.
- Projection: Use `camera.UnprojectPosition()` to map 3D position to 2D screen coordinates.
- Frustum Behavior:
    - If inside frustum but occluded by 3D geometry (planets/ships) $\rightarrow$ Show 2D icon at projected position.
    - If outside frustum $\rightarrow$ Clamp icon to screen edge, pointing toward the body.
- Occlusion: Use `PhysicsRayQueryParameters3D` to simulate 3D occlusion for the 2D icons.
- Z-Ordering: Sort 2D icons by distance to the camera so closer bodies are drawn on top.

## Implementation Steps

### 1. UI Component Creation
- Create `FarFieldIcon.tscn`:
    - Root: `Control`
    - `TextureRect`: For the body's billboard texture.
    - `Label`: For the body's name.
- Implement `FarFieldIcon.cs` to handle simple updates (texture assignment, label text).

### 2. `FarFieldIndicatorManager` Implementation
- Create `FarFieldIndicatorManager.cs` (attached to `MainGameUI` or a dedicated `CanvasLayer`).
- **Target Tracking**: Maintain a list of all `IOrbitalBody` objects.
- **Process Loop**:
    - Filter bodies where `distance > (camera.Far + 100)`.
    - For each valid body:
        1. **Projection**: Calculate `screenPos` via `UnprojectPosition`.
        2. **Clamping**: If `!IsPositionInFrustum`, clamp `screenPos` to viewport edges.
        3. **Occlusion**: Perform a staggered RayCast check from camera to body.
        4. **Texture Selection**: Query `BillboardTextures.GetTextureForDistance`.
        5. **Z-Index**: Update order based on distance.
- **Performance**: Implement round-robin updates for RayCasts to avoid frame spikes.

### 3. Integration
- Add `FarFieldIndicatorManager` to the `MainGameUI` scene.
- Ensure it is instantiated on a `CanvasLayer` with a high layer index to appear above the 3D world.
- Hook into the system loading process to populate the body list.

## Verification
- Place a planet far beyond the far plane and verify the icon appears.
- Move the camera so the planet is behind the camera and verify the icon clamps to the edge.
- Place a large body between the camera and a distant body to verify the occlusion RayCast hides/shows the icon appropriately.
- Verify that the textures used in the 2D icons match the `BillboardTextures` of the corresponding body.
