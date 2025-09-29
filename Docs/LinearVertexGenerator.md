# LinearVertexGenerator Documentation

## Overview

The `LinearVertexGenerator` class is responsible for generating vertices that are linearly distributed between two points in 3D space. It implements the `IVertexGenerator` interface to create evenly spaced vertices along a straight line segment between a start and end point.

## Class: LinearVertexGenerator

```csharp
namespace MeshGeneration
{
    /// <summary>
    /// Generates vertices linearly distributed between two points in 3D space.
    /// </summary>
    /// <remarks>
    /// This class implements the IVertexGenerator interface to create evenly spaced vertices
    /// along a straight line segment between a start and end point. The vertices are generated
    /// using linear interpolation (lerp) and are stored in a StructureDatabase for reuse.
    /// </remarks>
    public class LinearVertexGenerator : IVertexGenerator
    {
        // Methods documented below
    }
}
```

### Purpose

The `LinearVertexGenerator` provides a straightforward way to generate vertices that are evenly spaced along a line segment. This is useful for mesh generation scenarios where you need to subdivide edges or create linear distributions of points between two existing vertices.

## Methods

### GenerateVertices

```csharp
public Point[] GenerateVertices(int count, Point start, Point end, StructureDatabase db)
```

**Purpose:** Generates a specified number of vertices linearly distributed between two points.

**Parameters:**
- `count` (int): The number of vertices to generate between the start and end points.
- `start` (Point): The starting point of the line segment.
- `end` (Point): The ending point of the line segment.
- `db` (StructureDatabase): The StructureDatabase used to store and retrieve points.

**Returns:** An array of Point objects representing the linearly distributed vertices.

**Remarks:**
- The vertices are evenly spaced along the line segment, with the first vertex appearing at 1/(count+1) of the distance from start to end, and the last vertex appearing at count/(count+1) of the distance.
- If count is 0 or negative, returns an empty array.
- Uses linear interpolation (lerp) to calculate vertex positions.
- Vertices are stored in the StructureDatabase for potential reuse.

### GetParameterization

```csharp
public float GetParameterization(float index, float total)
```

**Purpose:** Calculates the parameterization value for a vertex at a given index.

**Parameters:**
- `index` (float): The zero-based index of the vertex.
- `total` (float): The total number of vertices being generated.

**Returns:** A float value between 0 and 1 representing the relative position along the line segment.

**Remarks:**
- This method returns the parameter t used in linear interpolation, where 0 represents the start point and 1 represents the end point.
- The formula (index + 1)/(total + 1) ensures that vertices are evenly distributed without including the endpoints.
- Useful for understanding the relative position of each vertex along the line segment.

### ValidateConfiguration

```csharp
public bool ValidateConfiguration(int vertexCount)
```

**Purpose:** Validates whether the specified vertex count configuration is valid.

**Parameters:**
- `vertexCount` (int): The number of vertices to validate.

**Returns:** True if the vertex count is valid (non-negative); otherwise, false.

**Remarks:**
- This method ensures that the vertex count is not negative, which would be an invalid configuration for vertex generation.
- Simple validation check to prevent invalid input before vertex generation.

## Usage Example

```csharp
// Create a linear vertex generator
var generator = new LinearVertexGenerator();

// Define start and end points
Point startPoint = db.GetOrCreatePoint(new Vector3(0, 0, 0));
Point endPoint = db.GetOrCreatePoint(new Vector3(10, 0, 0));

// Validate configuration
if (generator.ValidateConfiguration(5))
{
    // Generate 5 vertices between start and end points
    Point[] vertices = generator.GenerateVertices(5, startPoint, endPoint, db);
    
    // Get parameterization for the third vertex (index 2)
    float t = generator.GetParameterization(2, 5);
    // t will be (2 + 1)/(5 + 1) = 3/6 = 0.5
}
```

## Key Features

- **Linear Distribution:** Vertices are evenly spaced along the line segment
- **Database Integration:** Uses StructureDatabase for point storage and reuse
- **Parameterization:** Provides mathematical parameter values for each vertex position
- **Input Validation:** Ensures valid vertex count configurations
- **Logging:** Integrated with Logger for debugging and monitoring

## Dependencies

- `Godot.Vector3` - For 3D vector operations and linear interpolation
- `Structures.Point` - The point structure used throughout the mesh generation system
- `UtilityLibrary.Logger` - For function logging and debugging
- `MeshGeneration.StructureDatabase` - For point storage and management
- `MeshGeneration.IVertexGenerator` - Interface implementation requirement