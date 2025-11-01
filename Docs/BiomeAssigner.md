# BiomeAssigner Documentation

## Overview

The `BiomeAssigner` class is a static utility class in the `MeshGeneration` namespace that provides functionality for assigning biomes to celestial body mesh faces based on environmental factors. It implements biome assignment logic using a Whittaker Diagram approach that considers height, moisture, and latitude.

## Class Structure

```csharp
namespace MeshGeneration;
public static class BiomeAssigner
```

### Constants

#### MAX_MOISTURE
- **Type**: `const float`
- **Value**: `2.7f`
- **Description**: The maximum moisture value used in moisture calculations.

### Methods

#### AssignBiome

```csharp
public static BiomeType AssignBiome(UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
```

**Purpose**: Assigns a biome type based on height, moisture, and latitude using a Whittaker Diagram mapping.

**Parameters**:
- `generator` (`UnifiedCelestialMesh`): The celestial body mesh generator containing height information.
- `height` (`float`): The absolute height value at the location.
- `moisture` (`float`): The moisture level at the location (0-1 range).
- `latitude` (`float`, optional): The latitude of the location (-1 to 1 range, where 0 is equator). Defaults to `0f`.

**Returns**: `BiomeType` - The assigned biome type based on the environmental conditions.

**Remarks**: 
This method normalizes the height to a 0-1 range and then applies a series of conditional checks based on the Whittaker Diagram to determine the appropriate biome. The logic considers:

- **High elevation areas** (normalizedHeight > 0.9f): Become icecaps
- **Mountain areas** (normalizedHeight > 0.78f): Become mountains
- **Polar regions** (normalizedHeight > 0.4f + extreme latitude): Become tundra or taiga
- **Low elevation areas** (normalizedHeight < 0.1f): Become ocean
- **Coastal areas** (normalizedHeight < 0.2f): Become coastal
- **Arid mid-elevation** (normalizedHeight < 0.3f + low moisture): Become desert
- **Temperate mid-elevation** (normalizedHeight < 0.3f + moderate moisture): Become grassland
- **Forested areas** (normalizedHeight > 0.3f + moderate moisture): Become forest
- **High moisture areas**: Become rainforest

#### CalculateMoisture

```csharp
public static float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
```

**Purpose**: Calculates moisture levels for a continent based on geographic factors and random variation.

**Parameters**:
- `continent` (`Continent`): The continent for which to calculate moisture.
- `rng` (`RandomNumberGenerator`): Random number generator for adding variation.
- `baseMoisture` (`float`, optional): The base moisture level to start from. Defaults to `0.5f`.

**Returns**: `float` - A calculated moisture value between 0 and MAX_MOISTURE.

**Remarks**:
The moisture calculation considers several factors:

- **Latitude factor**: Based on the continent's Y-coordinate position, calculated as `continent.averagedCenter.Y / 9f` and clamped to 0-1 range
- **Size factor**: Based on the number of cells in the continent, calculated as `continent.cells.Count / 100f`
- **Random variation**: Adds natural variation between -0.2 and +0.2 using the random number generator

The final value is calculated using the formula:
```csharp
MAX_MOISTURE - (baseMoisture + latitudeFactor + sizeFactor + randomVariation) / MAX_MOISTURE
```

This ensures the moisture value stays within expected ranges while incorporating geographic and random influences.

## Usage Examples

### Assigning a Biome

```csharp
UnifiedCelestialMesh generator = GetGenerator();
float height = 150.5f;
float moisture = 0.6f;
float latitude = 0.3f; // 30 degrees north

BiomeType biome = BiomeAssigner.AssignBiome(generator, height, moisture, latitude);
// Result will be Forest based on the conditions
```

### Calculating Moisture

```csharp
Continent continent = GetContinent();
RandomNumberGenerator rng = new RandomNumberGenerator();
rng.Randomize();

float moisture = BiomeAssigner.CalculateMoisture(continent, rng, 0.5f);
// Returns a moisture value between 0 and 2.7
```

## Dependencies

- `Godot` - For Mathf.Clamp and RandomNumberGenerator
- `Structures` - For BiomeType and Continent classes
- `UtilityLibrary` - For Logger functionality

## Algorithm Details

The biome assignment follows a hierarchical decision tree based on the Whittaker Diagram:

1. **Height-based primary classification**:
   - Ocean (< 0.1 normalized height)
   - Coastal (0.1-0.2 normalized height)
   - Low elevation (0.2-0.3 normalized height)
   - Mid elevation (0.3-0.4 normalized height)
   - High elevation (0.4-0.78 normalized height)
   - Mountain (0.78-0.9 normalized height)
   - Icecap (> 0.9 normalized height)

2. **Moisture-based secondary classification** (for mid-elevation areas):
   - Desert (< 0.2 moisture)
   - Grassland (0.2-0.5 moisture)
   - Forest (0.5-0.7 moisture)
   - Rainforest (> 0.7 moisture)

3. **Latitude-based modifiers** (for high elevation areas):
   - Tundra (extreme latitudes > 0.8 or < -0.8)
   - Taiga (high latitudes > 0.7 or < -0.7)

This creates realistic biome distributions that consider elevation, precipitation, and geographic position.