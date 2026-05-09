# Player Interaction

## Overview

The Player Interaction system handles all direct player input in the game world. It manages camera control, Voronoi cell selection via shader-based highlighting, building placement, ship movement commands, and orbital body selection. Input is captured by `InputHandler`, processed by `PlayerController`, and routed to specialized managers like `CellSelectionManager`.

## Key Classes

### CellSelectionManager
- **Location**: `Scripts/PlayerInteraction/CellSelection/CellSelectionManager.cs`
- **Purpose**: Autoload singleton that manages Voronoi cell selection state and GPU-driven highlight rendering.
- **Key Responsibilities**:
  - Tracks the currently selected cell, body, and continent.
  - Updates shader uniforms on the target body's mesh material to produce an outline/fill effect.
  - Emits signals: `CellSelected`, `SelectionCleared`, `ContinentSelected`.
  - Works with both `CelestialBody` and `SatelliteBody`.

### InputHandler
- **Location**: `Scripts/PlayerInteraction/InputHandler.cs`
- **Purpose**: Low-level input capture. Maps raw input events to game actions and delegates to the appropriate systems.

### PlayerController
- **Location**: `Scripts/PlayerInteraction/PlayerController.cs`
- **Purpose**: High-level controller that interprets input actions and drives the camera, selection, and command systems.

### ShipMovement
- **Location**: `Scripts/PlayerInteraction/ShipMovement.cs`
- **Purpose**: Handles player-initiated movement commands for logistics units, including point-and-click trajectory planning.

## Related Documentation
- [UI and State Machine](UIAndStateMachine.md)
- [Logistics and Movement](LogisticsAndMovement.md)
