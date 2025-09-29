# EdgeKey Documentation

## Overview

The `EdgeKey` struct provides a canonical, direction-agnostic identity for undirected edges in mesh generation algorithms. It ensures stable value semantics by normalizing vertex indices so that edges can be consistently identified regardless of vertex order.

## Namespace

`MeshGeneration`

## Struct: EdgeKey

### Summary

Represents a canonical, direction-agnostic identity for an undirected edge in mesh generation. The key is normalized such that A ≤ B to ensure stable value semantics and consistent edge identification regardless of vertex order. This struct is immutable and implements value equality semantics.

### Remarks

EdgeKey is designed to provide a consistent way to identify edges in mesh structures where the direction of the edge (from vertex A to B vs B to A) should not matter. The normalization ensures that EdgeKey(a, b) and EdgeKey(b, a) always produce the same key representation. This is particularly useful in mesh algorithms where edges need to be uniquely identified for operations like edge collapse, subdivision, or adjacency queries.

### Properties

#### A

**Type:** `int`  
**Access:** Read-only

Gets the first vertex index. Always the smaller value due to normalization.

**Value:**  
The smaller of the two vertex indices that define this edge.

#### B

**Type:** `int`  
**Access:** Read-only

Gets the second vertex index. Always the larger value due to normalization.

**Value:**  
The larger of the two vertex indices that define this edge.

### Constructors

#### EdgeKey(int a, int b)

Initializes a new EdgeKey with automatic normalization to ensure A ≤ B.

**Parameters:**
- `a` (int): First vertex index.
- `b` (int): Second vertex index.

**Remarks:**  
The constructor automatically normalizes the vertex indices so that A is always the smaller value and B is always the larger value. This ensures that edges created with the same vertices in different orders will have identical keys.

### Methods

#### From(Point a, Point b)

Creates an EdgeKey from two Point objects using their indices.

**Parameters:**
- `a` (Point): First point.
- `b` (Point): Second point.

**Returns:**  
A normalized EdgeKey representing the edge between the points.

**Remarks:**  
This factory method provides a convenient way to create EdgeKeys directly from Point objects, extracting their indices automatically. The resulting EdgeKey will be normalized according to the same rules as the constructor.

#### From(int a, int b)

Creates an EdgeKey from two integer indices.

**Parameters:**
- `a` (int): First vertex index.
- `b` (int): Second vertex index.

**Returns:**  
A normalized EdgeKey representing the edge between the indices.

**Remarks:**  
This factory method provides an alternative way to create EdgeKeys that is semantically equivalent to calling the constructor directly. It can be useful in scenarios where a more explicit factory method pattern is preferred.

#### Equals(EdgeKey other)

Determines whether this EdgeKey is equal to another EdgeKey.

**Parameters:**
- `other` (EdgeKey): The EdgeKey to compare with this EdgeKey.

**Returns:**  
`true` if the EdgeKeys are equal; otherwise, `false`.

**Remarks:**  
Two EdgeKeys are considered equal if their A and B properties are identical. Due to normalization, this means they represent the same undirected edge regardless of the original vertex order.

#### Equals(object obj)

Determines whether this EdgeKey is equal to another object.

**Parameters:**
- `obj` (object): The object to compare with this EdgeKey.

**Returns:**  
`true` if the object is an EdgeKey and is equal to this EdgeKey; otherwise, `false`.

**Remarks:**  
This method provides value equality comparison with any object. If the object is not an EdgeKey, the method returns false. Otherwise, it delegates to the strongly-typed Equals method for the actual comparison.

#### GetHashCode()

Returns the hash code for this EdgeKey.

**Returns:**  
A hash code for the current EdgeKey.

**Remarks:**  
The hash code is computed using a combination of the A and B property values. This ensures that equal EdgeKeys always produce the same hash code, which is essential for proper operation in hash-based collections like HashSet and Dictionary.

#### ToString()

Returns the string representation of this EdgeKey.

**Returns:**  
A string in the format "EdgeKey(A,B)".

**Remarks:**  
The string representation follows the format "EdgeKey(A,B)" where A and B are the normalized vertex indices. This format is useful for debugging, logging, and display purposes. The normalized format ensures consistent string representation for edges that are logically equivalent.

### Operators

#### operator ==(EdgeKey left, EdgeKey right)

Determines whether two specified EdgeKeys are equal.

**Parameters:**
- `left` (EdgeKey): The first EdgeKey to compare.
- `right` (EdgeKey): The second EdgeKey to compare.

**Returns:**  
`true` if the EdgeKeys are equal; otherwise, `false`.

**Remarks:**  
This operator provides a convenient syntax for comparing EdgeKeys for equality. It delegates to the Equals method to ensure consistent comparison behavior.

#### operator !=(EdgeKey left, EdgeKey right)

Determines whether two specified EdgeKeys are not equal.

**Parameters:**
- `left` (EdgeKey): The first EdgeKey to compare.
- `right` (EdgeKey): The second EdgeKey to compare.

**Returns:**  
`true` if the EdgeKeys are not equal; otherwise, `false`.

**Remarks:**  
This operator provides a convenient syntax for comparing EdgeKeys for inequality. It returns the negation of the equality comparison result.

### Interfaces

#### IEquatable<EdgeKey>

The EdgeKey struct implements the IEquatable<EdgeKey> interface to provide strongly-typed equality comparison, which can improve performance and type safety when comparing EdgeKey instances.

## Usage Examples

### Creating EdgeKeys

```csharp
// Create from vertex indices
EdgeKey key1 = new EdgeKey(5, 10);  // Normalized to A=5, B=10
EdgeKey key2 = new EdgeKey(10, 5);  // Also normalized to A=5, B=10

// Create using factory methods
Point pointA = new Point(0, Vector3.Zero);
Point pointB = new Point(1, Vector3.One);
EdgeKey key3 = EdgeKey.From(pointA, pointB);
EdgeKey key4 = EdgeKey.From(2, 7);
```

### Comparing EdgeKeys

```csharp
EdgeKey keyA = new EdgeKey(1, 5);
EdgeKey keyB = new EdgeKey(5, 1);
EdgeKey keyC = new EdgeKey(2, 5);

// These are equal due to normalization
bool equal1 = keyA == keyB;  // true
bool equal2 = keyA.Equals(keyB);  // true

// These are not equal
bool equal3 = keyA == keyC;  // false
```

### Using in Collections

```csharp
// EdgeKeys work well in hash-based collections
HashSet<EdgeKey> edgeSet = new HashSet<EdgeKey>();
edgeSet.Add(new EdgeKey(1, 2));
edgeSet.Add(new EdgeKey(2, 1));  // Won't be added - already exists

Dictionary<EdgeKey, EdgeData> edgeDataMap = new Dictionary<EdgeKey, EdgeData>();
edgeDataMap[new EdgeKey(3, 4)] = new EdgeData();
```

## Thread Safety

The EdgeKey struct is immutable and therefore thread-safe. Multiple threads can safely access and use EdgeKey instances concurrently without any synchronization mechanisms.

## Performance Considerations

- Lightweight struct with minimal memory overhead
- Efficient hash code generation for fast dictionary lookups
- No heap allocations, suitable for performance-critical code
- Value type semantics avoid boxing in most scenarios

## Dependencies

- `System`: For IEquatable<T> interface and HashCode.Combine
- `Structures`: For Point type reference in factory methods