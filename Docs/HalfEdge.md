# HalfEdge Class Documentation

## Overview

The `HalfEdge` class provides a directed half-edge representation for canonical mesh topology traversal. It is a fundamental component in the mesh generation system, enabling efficient navigation and manipulation of triangular mesh structures.

## Class Definition

```csharp
namespace MeshGeneration
{
    public class HalfEdge
    {
        // Properties and methods
    }
}
```

## Properties

### Id
- **Type**: `int`
- **Access**: Public getter, internal setter
- **Description**: Gets the unique identifier for this half-edge.

### From
- **Type**: `Point`
- **Access**: Public getter, internal setter
- **Description**: Gets the starting point (vertex) of this half-edge.

### To
- **Type**: `Point`
- **Access**: Public getter, internal setter
- **Description**: Gets the ending point (vertex) of this half-edge.

### Twin
- **Type**: `HalfEdge`
- **Access**: Public getter, internal setter
- **Description**: Gets the twin half-edge that points in the opposite direction. The twin edge connects the same vertices but in reverse order (To -> From).

### Left
- **Type**: `Triangle`
- **Access**: Public getter, internal setter
- **Description**: Gets the triangle that lies on the left side of this directed edge. When traversing the edge from From to To, this triangle is to the left.

### Key
- **Type**: `EdgeKey`
- **Access**: Public getter, internal setter
- **Description**: Gets the edge key that uniquely identifies the undirected edge between two points. This key is used for edge lookup and comparison operations.

## Constructors

### HalfEdge(int id, Point from, Point to)
- **Access**: Internal
- **Parameters**:
  - `id` (int): The unique identifier for this half-edge.
  - `from` (Point): The starting point (vertex) of the edge.
  - `to` (Point): The ending point (vertex) of the edge.
- **Description**: Initializes a new instance of the HalfEdge class.
- **Remarks**: This constructor is internal and should only be called by mesh generation systems. The edge key is automatically generated from the from and to points.

## Methods

### ToString()
- **Return Type**: `string`
- **Description**: Returns a string representation of this half-edge.
- **Returns**: A string containing the edge ID and the indices of the from and to points in the format: `"HalfEdge(Id={Id}, From={From.Index}, To={To.Index})"`

## Usage and Purpose

The HalfEdge class is a core component in the half-edge data structure, which is widely used in computational geometry and mesh processing. Key characteristics:

1. **Directed Edges**: Each half-edge represents a directed edge between two vertices.
2. **Twin Relationship**: Every half-edge has a twin that represents the same undirected edge in the opposite direction.
3. **Adjacency Information**: The `Left` property provides immediate access to the adjacent triangle, enabling efficient mesh traversal.
4. **Edge Identification**: The `EdgeKey` property allows for quick lookup and comparison of edges.

This structure is particularly useful for:
- Mesh generation and subdivision
- Topological operations on triangular meshes
- Efficient neighbor finding and mesh traversal
- Geometric computations and mesh analysis

## Example Usage

```csharp
// Half-edges are typically created by mesh generation systems
HalfEdge edge = new HalfEdge(1, pointA, pointB);

// Access edge properties
int edgeId = edge.Id;
Point startPoint = edge.From;
Point endPoint = edge.To;
HalfEdge oppositeEdge = edge.Twin;
Triangle leftTriangle = edge.Left;
EdgeKey edgeKey = edge.Key;

// Get string representation
string edgeInfo = edge.ToString();
```

## Thread Safety

The HalfEdge class properties have internal setters, which means they can only be modified within the MeshGeneration namespace. This provides some level of encapsulation and control over when and how edge properties are modified.

## Dependencies

- `System`: Basic system types
- `Structures`: Contains the `Point`, `Triangle`, and `EdgeKey` types used by this class