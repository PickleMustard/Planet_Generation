# SphericalTriangulation Class Documentation

## Overview

The `SphericalTriangulation` class provides base functionality and interfaces for spherical triangulation operations in the planetary generation system. This static class serves as a utility and configuration hub for triangulating points on spherical surfaces.

## Namespace

`MeshGeneration`

## Purpose

This class is designed to support the unique challenges of spherical geometry, including:
- Handling numerical precision issues with floating-point coordinates
- Providing safety limits for iterative algorithms
- Offering guidance on appropriate triangulation methods based on input characteristics
- Validating input data to ensure triangulation success

## Constants

### DefaultTolerance

```csharp
public const float DefaultTolerance = 1e-6f;
```

**Description:** Represents the default tolerance value for geometric computations in spherical triangulation.

**Value:** A float value of 1e-6f (0.000001f) used for floating-point comparisons.

**Remarks:** This tolerance is critical for handling numerical precision issues that arise when working with spherical coordinates and geometric projections. It is used in various operations including:
- Point duplicate detection
- Collinearity testing
- Distance comparisons
- Geometric predicate evaluations

The value of 1e-6f provides a good balance between precision and performance for most planetary generation scenarios while avoiding false positives due to floating-point errors.

### MaxIterations

```csharp
public const int MaxIterations = 100000;
```

**Description:** Represents the maximum number of iterations allowed for triangulation algorithms.

**Value:** An integer value of 100,000 iterations.

**Remarks:** This safety limit prevents infinite loops in iterative triangulation algorithms, particularly during edge flipping operations in Delaunay triangulation. The limit is set high enough to handle complex geometries but low enough to detect and prevent algorithmic failures.

Algorithms that may hit this limit include:
- Delaunay edge flipping operations
- Constrained triangulation refinement
- Point insertion optimization loops

If this limit is reached, it typically indicates a problem with the input data or algorithm implementation rather than a legitimate need for more iterations.

## Methods

### GetRecommendedMethod

```csharp
public static string GetRecommendedMethod(int pointCount, bool isConvexHull)
```

**Description:** Gets the recommended triangulation method based on point count and geometry type.

**Parameters:**
- `pointCount` (int): The number of points to triangulate. Must be a non-negative integer.
- `isConvexHull` (bool): Whether the points form a convex hull geometry.

**Returns:** A string describing the recommended triangulation approach for the given parameters.

**Remarks:** This method provides algorithmic guidance based on the characteristics of the input data. The recommendation logic is as follows:

- Less than 3 points: Returns error message indicating insufficient points
- Exactly 3 points: Returns trivial single triangle case
- 4-6 points with convex hull: Recommends fan triangulation from centroid for efficiency
- More than 6 points with convex hull: Recommends spherical Delaunay triangulation
- Non-convex hull geometries: Recommends constrained Delaunay triangulation

This guidance helps optimize performance by selecting the most appropriate algorithm for the specific geometric characteristics of the input data.

**Exceptions:**
- `System.ArgumentException`: Thrown when pointCount is negative.

### ValidatePoints

```csharp
public static bool ValidatePoints(IEnumerable<Point> points)
```

**Description:** Validates that a set of points is suitable for spherical triangulation.

**Parameters:**
- `points` (IEnumerable<Point>): The collection of points to validate. Cannot be null.

**Returns:** True if the points are valid for triangulation; otherwise, false.

**Remarks:** This method performs comprehensive validation of input points to ensure they meet the requirements for successful spherical triangulation. The validation criteria include:

**Minimum Point Count:**
- At least 3 points are required to form any triangulation

**Point Uniqueness:**
- No duplicate points are allowed within the DefaultTolerance (1e-6f)
- Duplicate points can cause triangulation algorithms to fail or produce degenerate results

**Note:** This method currently checks for minimum point count and duplicates. Future enhancements may include additional validation for:
- Collinearity detection
- Spherical coordinate validity
- Point distribution analysis

The method uses an O(n²) algorithm for duplicate detection, which is acceptable for typical planetary generation scenarios where point counts are reasonable.

**Exceptions:**
- `System.ArgumentNullException`: Thrown when the points parameter is null.

## Usage Examples

### Getting Recommended Triangulation Method

```csharp
int pointCount = 50;
bool isConvexHull = true;

string recommendation = SphericalTriangulation.GetRecommendedMethod(pointCount, isConvexHull);
// Returns: "Spherical Delaunay triangulation"
```

### Validating Points for Triangulation

```csharp
List<Point> points = GetPointsFromSurface(); // Your point collection method

bool isValid = SphericalTriangulation.ValidatePoints(points);
if (isValid)
{
    // Proceed with triangulation
}
else
{
    // Handle invalid points (insufficient count or duplicates)
}
```

### Using Constants in Custom Algorithms

```csharp
float tolerance = SphericalTriangulation.DefaultTolerance;
int maxIterations = SphericalTriangulation.MaxIterations;

// Use in custom triangulation algorithm
for (int i = 0; i < maxIterations; i++)
{
    // Algorithm logic using tolerance for comparisons
}
```

## Integration with Other Classes

The `SphericalTriangulation` class works in conjunction with:

- **SphericalDelaunayTriangulation**: For convex hull triangulation on spheres
- **ConstrainedDelauneyTriangulation**: For constrained triangulation with boundary preservation

These classes together provide comprehensive mesh generation capabilities for planetary surface modeling and other spherical geometry applications.

## Best Practices

1. **Always validate points** before attempting triangulation using `ValidatePoints()`
2. **Use GetRecommendedMethod()** to select the appropriate algorithm for your data
3. **Respect the MaxIterations limit** when implementing custom iterative algorithms
4. **Use DefaultTolerance** for consistent floating-point comparisons across the system
5. **Handle edge cases** such as insufficient points or duplicate points gracefully

## Performance Considerations

- The `ValidatePoints()` method uses O(n²) complexity for duplicate detection
- Constants are optimized for typical planetary generation scenarios
- Method recommendations are designed to balance quality and performance based on input characteristics