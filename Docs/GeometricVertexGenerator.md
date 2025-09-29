# GeometricVertexGenerator Documentation

## Overview

The `GeometricVertexGenerator` class is a vertex generation implementation that creates vertices along a line segment using geometric parameterization. It provides non-linear spacing of vertices based on an exponential function, allowing for controlled distribution patterns between two endpoints.

## Class: GeometricVertexGenerator

**Namespace:** `MeshGeneration`  
**Implements:** `IVertexGenerator`

### Description

The `GeometricVertexGenerator` generates vertices with customizable distribution patterns along a line segment. Unlike linear distribution, this generator uses geometric progression to control vertex spacing, making it useful for creating meshes with varying vertex densities.

### Properties

#### Exponent
- **Type:** `float`
- **Default Value:** `2.0f`
- **Description:** Controls the distribution pattern of vertices along the line segment. Higher values create more clustered vertices near the start point, while lower values create more uniform distribution.

### Methods

#### GenerateVertices
```csharp
public Point[] GenerateVertices(int count, Point start, Point end, StructureDatabase db)
```

**Parameters:**
- `count` (`int`): The number of vertices to generate
- `start` (`Point`): The starting point of the line segment
- `end` (`Point`): The ending point of the line segment
- `db` (`StructureDatabase`): The structure database (unused in this implementation)

**Returns:**
- `Point[]`: An array of Point objects representing the generated vertices

**Remarks:**
- If count is 0 or negative, returns an empty array
- The first vertex is positioned at the start point, and the last vertex is positioned at the end point
- Intermediate vertices are distributed according to the geometric parameterization function

#### GetParameterization
```csharp
public float GetParameterization(float index, float total)
```

**Parameters:**
- `index` (`float`): The index of the vertex (0-based)
- `total` (`float`): The total number of vertices to generate

**Returns:**
- `float`: A parameterization value between 0.0 and 1.0 for linear interpolation

**Remarks:**
- The parameterization uses the formula: `pow(index / (total + 1.0), 1.0 / Exponent)`
- This creates non-linear spacing that can be controlled by the Exponent property

#### ValidateConfiguration
```csharp
public bool ValidateConfiguration(int vertexCount)
```

**Parameters:**
- `vertexCount` (`int`): The number of vertices to validate

**Returns:**
- `bool`: True if the configuration is valid, false otherwise

**Remarks:**
- Currently only validates that vertexCount is non-negative
- Additional validation logic can be added as needed

## Usage Examples

### Basic Usage
```csharp
var generator = new GeometricVertexGenerator();
generator.Exponent = 2.0f; // Default value

Point start = new Point(new Vector3(0, 0, 0));
Point end = new Point(new Vector3(10, 0, 0));
Point[] vertices = generator.GenerateVertices(5, start, end, database);
```

### Custom Exponent
```csharp
var generator = new GeometricVertexGenerator();
generator.Exponent = 3.0f; // More clustering near start

// Generate 10 vertices with high clustering near start
Point[] vertices = generator.GenerateVertices(10, startPoint, endPoint, database);
```

### Uniform Distribution
```csharp
var generator = new GeometricVertexGenerator();
generator.Exponent = 1.0f; // Nearly linear distribution

// Generate vertices with more uniform spacing
Point[] vertices = generator.GenerateVertices(8, startPoint, endPoint, database);
```

## Implementation Details

### Geometric Parameterization

The generator uses geometric progression to determine vertex positions:

1. **Normalization:** Each vertex index is normalized to a value between 0 and 1
2. **Exponential Transformation:** The normalized value is raised to the power of `1.0 / Exponent`
3. **Linear Interpolation:** The resulting parameter is used for linear interpolation between start and end points

### Mathematical Formula

The parameterization function is:
```
t = (index / (total + 1.0))^(1.0 / Exponent)
```

Where:
- `t` is the interpolation parameter (0.0 to 1.0)
- `index` is the vertex index (0-based)
- `total` is the total number of vertices
- `Exponent` is the configurable exponent property

### Exponent Effects

- **Exponent = 1.0:** Nearly linear distribution
- **Exponent > 1.0:** Vertices cluster near the start point
- **Exponent < 1.0:** Vertices cluster near the end point
- **Exponent → ∞:** All vertices cluster very close to the start point
- **Exponent → 0:** All vertices cluster very close to the end point

## Performance Considerations

- The algorithm runs in O(n) time where n is the number of vertices
- Memory usage is O(n) for storing the generated vertices
- The mathematical operations are lightweight and suitable for real-time generation
- Logging is included for debugging but can be disabled in production builds

## Integration

This class implements the `IVertexGenerator` interface, making it compatible with:
- Mesh generation systems
- Procedural content generation pipelines
- Terrain generation algorithms
- Any system requiring configurable vertex distribution along line segments

## Dependencies

- `System`: For mathematical operations
- `Godot`: For Vector3 and engine integration
- `Structures`: For Point and StructureDatabase types
- `UtilityLibrary`: For Logger functionality