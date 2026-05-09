# Rendering

## Overview

The Rendering system handles visual effects that are not part of the core mesh generation. This includes GPU-based orbital body indicators that replace traditional billboard sprites at distance, and texture generation for celestial body surfaces. The orbital indicator system uses a custom compute/effect pipeline to render hundreds of bodies as colored dots with dynamically updated position and texture data.

## Key Classes

### OrbitalIndicatorCoordinator
- **Location**: `Scripts/Rendering/OrbitalIndicatorCoordinator.cs`
- **Purpose**: Gathers orbital body positions and textures from the scene tree each frame and feeds them to `OrbitalIndicatorEffect`.
- **Key Responsibilities**:
  - Maintains a byte buffer of body data (position, radius, color, etc.).
  - Maps each body to a texture layer for appearance.
  - Falls back to classification-based colors when no texture is available.

### OrbitalIndicatorEffect
- **Location**: `Scripts/Rendering/OrbitalIndicatorEffect.cs`
- **Purpose**: Custom rendering effect that draws orbital indicators. Receives data from `OrbitalIndicatorCoordinator` and renders them efficiently.

### BodyBillboardTextures
- **Location**: `Scripts/ProceduralGeneration/TextureGeneration/BodyBillboardTextures.cs`
- **Purpose**: Generates and caches billboard textures for distant body rendering.

### TextureGeneratorFactory
- **Location**: `Scripts/ProceduralGeneration/TextureGeneration/TextureGeneratorFactory.cs`
- **Purpose**: Factory that creates the correct `ITextureGenerator` for a given body type.

### ITextureGenerator
- **Location**: `Scripts/ProceduralGeneration/TextureGeneration/ITextureGenerator.cs`
- **Purpose**: Interface for all body texture generators.

### Generator Implementations
- **Location**: `Scripts/ProceduralGeneration/TextureGeneration/Generators/`
- **Purpose**: Per-body-type texture generation:
  - `StarTextureGenerator`: Procedural star surfaces.
  - `RockyPlanetTextureGenerator`: Rocky planet albedo and normal maps.
  - `GasGiantTextureGenerator`: Banded gas giant atmospheres.
  - `IceGiantTextureGenerator`: Ice giant patterns.
  - `BlackHoleTextureGenerator`: Accretion disk and event horizon visuals.
  - `DwarfPlanetTextureGenerator`: Small body surfaces.
  - `MeshSnapshotTextureGenerator`: Renders a mesh to a texture.

### MeshRasterizer
- **Location**: `Scripts/ProceduralGeneration/TextureGeneration/MeshRasterizer.cs`
- **Purpose**: Rasterizes a 3D mesh into a 2D texture for use as a billboard or surface map.

### NoiseHelpers
- **Location**: `Scripts/ProceduralGeneration/TextureGeneration/Helpers/NoiseHelpers.cs`
- **Purpose**: Shared noise generation utilities used by texture generators.

## Related Documentation
- [Procedural Generation](ProceduralGeneration.md)
- [Mesh Generation](MeshGeneration.md)
