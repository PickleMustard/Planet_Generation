# Delaunay Triangulation Implementation Comparison

## Critical Differences Found

### 1. **InCircle Test Implementation** ❌
**Current Implementation (Line 106-114):** Uses determinant method with length squared
```csharp
float[][] m = new float[][] {
    new float[] { a.X, b.X, c.X, d.X },
    new float[] { a.Y, b.Y, c.Y, d.Y },
    new float[] { a.ToVector3().LengthSquared(), b.ToVector3().LengthSquared(), 
                  c.ToVector3().LengthSquared(), d.ToVector3().LengthSquared() },
    new float[] { 1.0f, 1.0f, 1.0f, 1.0f }
};
return Det4x4(m) > 0.0f;
```

**Reference Implementation:** Uses lifted coordinates method
```csharp
float adx = ax - dx, ady = ay - dy;
float bdx = bx - dx, bdy = by - dy;
float cdx = cx - dx, cdy = cy - dy;
float abdet = adx * bdy - bdx * ady;
float bcdet = bdx * cdy - cdx * bdy;
float cadet = cdx * ady - adx * cdy;
float alift = adx * adx + ady * ady;
float blift = bdx * bdx + bdy * bdy;
float clift = cdx * cdx + cdy * cdy;
return alift * bcdet + blift * cadet + clift * abdet < 0;
```
**Issue:** The sign and computation method are different. Reference returns `< 0`, current returns `> 0`.

### 2. **InCircle Call Parameter Order** ⚠️
**Current Implementation (Lines 145, 161):**
```csharp
// Line 145 - LeftCandidate
InCircle(baseEdge.Destination, baseEdge.Origin, leftCandidate.Destination, 
         leftCandidate.Onext().Destination)

// Line 161 - RightCandidate  
InCircle(baseEdge.Origin, baseEdge.Destination, rightCandidate.Destination, 
         rightCandidate.Oprev().Destination)
```

**Reference Implementation:**
Both use consistent order: `InCircle(basel.Destination, basel.Origin, ...)`

### 3. **MergeHulls Selection Logic** ❌
**Current Implementation (Line 186):**
```csharp
else if (!Valid(leftCandidate, baseEdge) || 
         (Valid(rightCandidate, baseEdge) && 
          InCircle(leftCandidate.Origin, leftCandidate.Destination, 
                   rightCandidate.Origin, rightCandidate.Destination)))
```

**Reference Implementation:**
```csharp
if (!Valid(lcand, basel) || 
    (Valid(rcand, basel) && 
     InCircle(lcand.Destination, lcand.Origin, rcand.Origin, rcand.Destination)))
```
**Issue:** InCircle test uses wrong vertices (Origin/Destination vs Destination/Origin).

### 4. **Edge Management** ⚠️
**Current Implementation:** 
- Adds both `e` and `e.Sym()` to Edges list separately
- Doesn't track quad-edge structure properly
- Line 189-194: Only adds `baseEdge`, not its symmetric edge

**Reference Implementation:**
- Manages quad-edge as a single unit
- Properly maintains all four edges in the quad-edge structure

### 5. **Triangle Extraction** ❌
**Current Implementation (tryFormTriangle, Line 324-347):**
- Uses `Lnext()` navigation only
- Checks if `thirdEdge.Lnext() == edge`

**Reference Implementation:**
- More robust triangle validation
- Handles all edges around a face systematically
- Marks visited edges to avoid duplicates

### 6. **Edge Collection Method** ⚠️
**Current Implementation (CollectAllEdges, Line 377-405):**
- Uses BFS with 5 different edge navigations (Onext, Sym, Oprev, Lnext, Rnext)
- May visit edges multiple times

**Reference Implementation:**
- Iterates through stored edges list directly
- More efficient and predictable

### 7. **Base Case Handling**
**Current Implementation:**
- Returns edge lists for 2-point case
- Complex triangle primitive with immediate rendering

**Reference Implementation:**
- Cleaner separation of concerns
- No rendering mixed with triangulation logic

## Recommended Fixes

### Priority 1: Fix InCircle Test
Replace the current InCircle implementation with the reference version's lifted coordinates method and ensure correct sign.

### Priority 2: Fix MergeHulls Logic
Correct the InCircle parameter order in the merge selection condition (Line 186).

### Priority 3: Standardize Edge Management
- Track quad-edges properly
- Add both edge and its sym when creating new edges in MergeHulls

### Priority 4: Improve Triangle Extraction
Use a more systematic approach to extract triangles, avoiding the current complex navigation.

## Summary
The inconsistent edge counts are likely caused by:
1. **Incorrect InCircle test** causing wrong Delaunay decisions
2. **Inconsistent edge management** in MergeHulls (not adding symmetric edges)
3. **Triangle extraction issues** potentially missing or duplicating triangles

The core algorithm structure is correct, but these implementation details are causing the inconsistency.