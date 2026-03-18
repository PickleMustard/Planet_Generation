# Plan: Extend Stable Orbit Calculations to Celestial Bodies

## Overview

This plan outlines the implementation of a unified system for calculating stable orbital velocities for celestial bodies in the planetary system generator. The goal is to consolidate the "most influential body" logic into `OrbitalMath` and extend the perigee/apogee-based velocity calculation used by satellites to all celestial bodies in the system.

---

## Background

### Current State

1. **Satellite Bodies**: Already use perigee/apogee to automatically calculate angular velocity for stable orbits around their parent body.

2. **Celestial Bodies**: Each has its own UI item in the generation menu, with velocity specified manually in templates. This makes system generation more error-prone and less intuitive.

3. **Existing Implementations**: There are currently two similar implementations of "most influential body" logic in different places:
   - `PlanetSystemGenerator.FindDominantBody` - finds dominant body for UI velocity calculations
   - `TrajectoryPlanner.FindCentralBody` - finds central body for trajectory planning

### Problem

- Duplicate code for finding the most gravitationally influential body
- Celestial bodies don't benefit from automatic stable orbit calculations
- System generation requires manual velocity specification

### Solution

1. Consolidate most influential body logic into `OrbitalMath`
2. Extend SystemGenerator to track the system center point
3. Use perigee/apogee approach to calculate velocities for celestial bodies

---

## Requirements

### 1. Most Influential Body Definition

The most influential body is defined as:

> The Celestial Body which produces the largest magnitude gravitational attraction for the other celestial bodies in the system.

**Special Case - Multiple Dominant Bodies**: If there are multiple highly influential bodies (within one order of magnitude / exponent of each other), the center point between their locations and their gravitational attractions should be used as the orbital center.

### 2. Recalculation Timing

The most influential body should be recalculated **after each body is added** to ensure the system center is always accurate.

### 3. Orbital Velocity Approach

Use the **perigee/apogee approach** - treating the distance from the system center to each body as one side of an elliptical orbit, similar to how satellites calculate their velocity.

---

## Implementation Details

### Phase 1: Add Most Influential Body Logic to OrbitalMath

#### File: `Scripts/UtilityLibrary/OrbitalMath.cs`

##### 1.1 Core Method: CalculateSystemCenter

Add a new method that finds the dominant body or calculates the barycenter when multiple bodies are competitive:

```csharp
/// <summary>
/// Calculates the system center point (barycenter) based on gravitational influence.
/// If one body is significantly more influential than all others, returns that body alone.
/// If multiple bodies are competitive (within threshold), calculates mass-weighted center.
/// </summary>
/// <param name="bodies">List of celestial bodies to analyze</param>
/// <returns>Tuple containing: center point position, total mass for orbital calculations, 
///          and list of indices of dominant/competitive bodies</returns>
public static (Vector3 centerPoint, float totalMass, List<int> dominantIndices) 
    CalculateSystemCenter(List<CelestialBody> bodies)
{
    // Algorithm:
    // 1. Calculate pairwise gravitational influences between all bodies
    // 2. Sum influences for each body (how much it influences all other bodies)
    // 3. Find the maximum influence value
    // 4. Identify all bodies within the competitive threshold (10x)
    // 5. If only one dominant body: return its position and mass
    // 6. If multiple: calculate mass-weighted barycenter
    
    const float COMPETITIVE_THRESHOLD = 10f; // One order of magnitude
    
    if (bodies == null || bodies.Count == 0)
        return (Vector3.Zero, 0f, new List<int>());
    
    if (bodies.Count == 1)
        return (bodies[0].GlobalPosition, bodies[0].Mass, new List<int> { 0 });
    
    // Calculate total gravitational influence for each body
    // Influence = sum of (G * other_mass / distance^2) for all other bodies
    List<float> totalInfluences = new List<float>(bodies.Count);
    List<float> distancesToOrigin = new List<float>(bodies.Count);
    
    for (int i = 0; i < bodies.Count; i++)
    {
        float influence = 0f;
        Vector3 posI = bodies[i].GlobalPosition;
        
        for (int j = 0; j < bodies.Count; j++)
        {
            if (i == j) continue;
            
            Vector3 posJ = bodies[j].GlobalPosition;
            float distSq = posI.DistanceSquaredTo(posJ);
            
            if (distSq > 0.001f) // Avoid division by zero
            {
                influence += GRAVITATIONAL_CONSTANT * bodies[j].Mass / distSq;
            }
        }
        
        totalInfluences.Add(influence);
    }
    
    // Find maximum influence and identify competitive bodies
    float maxInfluence = 0f;
    foreach (float inf in totalInfluences)
    {
        if (inf > maxInfluence) maxInfluence = inf;
    }
    
    List<int> competitiveIndices = new List<int>();
    for (int i = 0; i < bodies.Count; i++)
    {
        if (totalInfluences[i] >= maxInfluence / COMPETITIVE_THRESHOLD)
        {
            competitiveIndices.Add(i);
        }
    }
    
    // Calculate barycenter for competitive bodies
    Vector3 centerPoint = Vector3.Zero;
    float totalMass = 0f;
    
    if (competitiveIndices.Count == 1)
    {
        // Single dominant body
        int idx = competitiveIndices[0];
        centerPoint = bodies[idx].GlobalPosition;
        totalMass = bodies[idx].Mass;
    }
    else
    {
        // Multiple competitive bodies - calculate barycenter
        foreach (int idx in competitiveIndices)
        {
            float mass = bodies[idx].Mass;
            centerPoint += bodies[idx].GlobalPosition * mass;
            totalMass += mass;
        }
        
        if (totalMass > 0f)
        {
            centerPoint /= totalMass;
        }
    }
    
    return (centerPoint, totalMass, competitiveIndices);
}
```

##### 1.2 Dictionary-Based Helper Method

Add a version that works with Dictionary-based body data (for UI compatibility):

```csharp
/// <summary>
/// Calculates the system center from Dictionary-based body data.
/// Expected Dictionary keys: "position" (Vector3), "mass" (float)
/// </summary>
public static (Vector3 centerPoint, float totalMass, List<int> dominantIndices)
    CalculateSystemCenterFromDicts(List<Godot.Collections.Dictionary> bodyDicts)
{
    const float COMPETITIVE_THRESHOLD = 10f;
    
    if (bodyDicts == null || bodyDicts.Count == 0)
        return (Vector3.Zero, 0f, new List<int>());
    
    if (bodyDicts.Count == 1)
    {
        Vector3 pos = ((Godot.Collections.Array)bodyDicts[0]["position"]).ToVector3();
        float mass = Convert.ToSingle(bodyDicts[0]["mass"]);
        return (pos, mass, new List<int> { 0 });
    }
    
    // Convert to position/mass arrays for calculation
    List<Vector3> positions = new List<Vector3>();
    List<float> masses = new List<float>();
    
    foreach (var dict in bodyDicts)
    {
        positions.Add(((Godot.Collections.Array)dict["position"]).ToVector3());
        masses.Add(Convert.ToSingle(dict["mass"]));
    }
    
    // Calculate total gravitational influence for each body
    List<float> totalInfluences = new List<float>(bodyDicts.Count);
    
    for (int i = 0; i < bodyDicts.Count; i++)
    {
        float influence = 0f;
        
        for (int j = 0; j < bodyDicts.Count; j++)
        {
            if (i == j) continue;
            
            float distSq = positions[i].DistanceSquaredTo(positions[j]);
            
            if (distSq > 0.001f)
            {
                influence += GRAVITATIONAL_CONSTANT * masses[j] / distSq;
            }
        }
        
        totalInfluences.Add(influence);
    }
    
    // Find maximum influence
    float maxInfluence = 0f;
    foreach (float inf in totalInfluences)
    {
        if (inf > maxInfluence) maxInfluence = inf;
    }
    
    // Identify competitive bodies
    List<int> competitiveIndices = new List<int>();
    for (int i = 0; i < bodyDicts.Count; i++)
    {
        if (totalInfluences[i] >= maxInfluence / COMPETITIVE_THRESHOLD)
        {
            competitiveIndices.Add(i);
        }
    }
    
    // Calculate barycenter
    Vector3 centerPoint = Vector3.Zero;
    float totalMass = 0f;
    
    if (competitiveIndices.Count == 1)
    {
        int idx = competitiveIndices[0];
        centerPoint = positions[idx];
        totalMass = masses[idx];
    }
    else
    {
        foreach (int idx in competitiveIndices)
        {
            float mass = masses[idx];
            centerPoint += positions[idx] * mass;
            totalMass += mass;
        }
        
        if (totalMass > 0f)
        {
            centerPoint /= totalMass;
        }
    }
    
    return (centerPoint, totalMass, competitiveIndices);
}
```

##### 1.3 Simple Wrapper Methods (Backward Compatibility)

Add simple index-returning methods for existing code:

```csharp
/// <summary>
/// Finds the index of the most gravitationally influential body relative to a test position.
/// Uses the test position to calculate influence from each body.
/// </summary>
/// <param name="testPosition">Position to calculate influence from</param>
/// <param name="bodies">List of celestial bodies</param>
/// <returns>Index of the most influential body, or -1 if none found</returns>
public static int GetMostInfluentialBodyIndex(Vector3 testPosition, Godot.Collections.Array<CelestialBody> bodies)
{
    if (bodies == null || bodies.Count == 0)
        return -1;
    
    float maxInfluence = 0f;
    int dominantIndex = -1;
    
    for (int i = 0; i < bodies.Count; i++)
    {
        float distanceSq = testPosition.DistanceSquaredTo(bodies[i].GlobalPosition);
        
        if (distanceSq > 0.001f)
        {
            float influence = GRAVITATIONAL_CONSTANT * bodies[i].Mass / distanceSq;
            
            if (influence > maxInfluence)
            {
                maxInfluence = influence;
                dominantIndex = i;
            }
        }
    }
    
    return dominantIndex;
}

/// <summary>
/// Finds the index of the most gravitationally influential body relative to a test position.
/// Dictionary format: "position" (Vector3), "mass" (float)
/// </summary>
public static int GetMostInfluentialBodyIndex(Vector3 testPosition, Godot.Collections.Array<Godot.Collections.Dictionary> bodyDicts)
{
    if (bodyDicts == null || bodyDicts.Count == 0)
        return -1;
    
    float maxInfluence = 0f;
    int dominantIndex = -1;
    
    for (int i = 0; i < bodyDicts.Count; i++)
    {
        Vector3 pos = ((Godot.Collections.Array)bodyDicts[i]["position"]).ToVector3();
        float distanceSq = testPosition.DistanceSquaredTo(pos);
        
        if (distanceSq > 0.001f)
        {
            float mass = Convert.ToSingle(bodyDicts[i]["mass"]);
            float influence = GRAVITATIONAL_CONSTANT * mass / distanceSq;
            
            if (influence > maxInfluence)
            {
                maxInfluence = influence;
                dominantIndex = i;
            }
        }
    }
    
    return dominantIndex;
}
```

---

### Phase 2: Update PlanetSystemGenerator

#### File: `UI/PlanetSystemGenerator.cs`

##### 2.1 Replace FindDominantBody Method

Replace the existing `FindDominantBody` method (lines 233-256) to delegate to OrbitalMath:

```csharp
/// <summary>
/// Finds the index of the most gravitationally dominant body relative to the given position.
/// </summary>
private int FindDominantBody(
    Vector3 position,
    Godot.Collections.Array<Godot.Collections.Dictionary> bodies
)
{
    // Convert Array<Dictionary> to List<IDictionary> for OrbitalMath
    var bodyList = new List<IDictionary>();
    foreach (var body in bodies)
    {
        bodyList.Add(body);
    }
    
    return OrbitalMath.GetMostInfluentialBodyIndex(position, bodyList);
}
```

**Note**: The existing method signature remains the same for backward compatibility with other code that calls it.

##### 2.2 Update CalculateStableVelocity (No Changes Needed)

The existing `CalculateStableVelocity` method already correctly uses the dominant body from `FindDominantBody`. Since we're replacing that method with a call to OrbitalMath, this should continue to work.

---

### Phase 3: Update TrajectoryPlanner

#### File: `Scripts/Constructables/ArtificialSatellites/TrajectoryPlanner.cs`

##### 3.1 Replace FindCentralBody Method

Replace the existing `FindCentralBody` method (lines 251-305) to delegate to OrbitalMath:

```csharp
/// <summary>
/// Finds the most gravitationally dominant body in the system relative to the given body.
/// </summary>
/// <param name="origin">The body to find the central body for.</param>
/// <returns>The most gravitationally dominant body.</returns>
private CelestialBody FindCentralBody(CelestialBody origin)
{
    if (origin == null)
    {
        GameLogger.Warning("TrajectoryPlanner.FindCentralBody: Origin body is null");
        return origin;
    }

    // Get all celestial bodies in the system via the "CelestialBody" group
    var nodes = origin.GetTree().GetNodesInGroup("CelestialBody");
    
    if (nodes == null || nodes.Count == 0)
    {
        GameLogger.Warning("TrajectoryPlanner.FindCentralBody: No bodies found in system");
        return origin;
    }

    // Convert to List<CelestialBody>
    var bodies = new List<CelestialBody>();
    foreach (Node node in nodes)
    {
        if (node is CelestialBody body && body != origin)
        {
            bodies.Add(body);
        }
    }

    if (bodies.Count == 0)
    {
        // No other bodies found, use origin
        return origin;
    }

    // Use OrbitalMath to find the most influential body
    int dominantIndex = OrbitalMath.GetMostInfluentialBodyIndex(origin.GlobalPosition, bodies);
    
    if (dominantIndex < 0 || dominantIndex >= bodies.Count)
    {
        GameLogger.Debug($"TrajectoryPlanner.FindCentralBody: Using {origin.Name} as central body (no dominant body found)");
        return origin;
    }

    CelestialBody dominantBody = bodies[dominantIndex];
    GameLogger.Debug($"TrajectoryPlanner.FindCentralBody: Found dominant body {dominantBody.Name} for {origin.Name}");
    return dominantBody;
}
```

---

### Phase 4: Update SystemGenerator

#### File: `Scripts/ProceduralGeneration/SystemGenerator.cs`

##### 4.1 Add Storage Fields

Add new private fields to track the system center:

```csharp
// System center tracking
private List<CelestialBody> _celestialBodies = new();
private Vector3 _systemCenterPoint;
private float _centerMass;
private List<int> _dominantBodyIndices = new();
```

##### 4.2 Update CreateAndQueueCelestialBody Method

Add body tracking and center recalculation:

```csharp
private void CreateAndQueueCelestialBody(Godot.Collections.Dictionary body)
{
    var mesh = new UnifiedCelestialMesh();
    CelestialBody celBody = CelestialBody.Builder.BuildFromBodyDict(body, mesh);

    SystemContainer!.AddChild(celBody);
    celBody.Position = (Vector3)((Godot.Collections.Dictionary)body["template"])["position"];

    // Track celestial body for system center calculation
    _celestialBodies.Add(celBody);
    
    // Recalculate system center after adding this body
    RecalculateSystemCenter();

    celBody.StartMeshGeneration(
        onCompleted: (completedBody) => OnBodyGenerationComplete(completedBody, celBody, body),
        onFailed: (failedBody, error) => OnBodyGenerationFailed(failedBody, error, celBody)
    );
}
```

##### 4.3 Add RecalculateSystemCenter Method

Add a new method to calculate and update the system center:

```csharp
/// <summary>
/// Recalculates the system center point based on all tracked celestial bodies.
/// Uses gravitational influence to determine if there's a single dominant body
/// or if multiple bodies form a competitive group requiring a barycenter.
/// </summary>
private void RecalculateSystemCenter()
{
    if (_celestialBodies.Count == 0)
    {
        _systemCenterPoint = Vector3.Zero;
        _centerMass = 0f;
        _dominantBodyIndices.Clear();
        return;
    }
    
    var result = OrbitalMath.CalculateSystemCenter(_celestialBodies);
    _systemCenterPoint = result.centerPoint;
    _centerMass = result.totalMass;
    _dominantBodyIndices = result.dominantIndices;
    
    GameLogger.Debug(
        $"SystemGenerator: Recalculated center - Position: {_systemCenterPoint}, " +
        $"Mass: {_centerMass}, Dominant bodies: {string.Join(", ", _dominantBodyIndices)}"
    );
}
```

##### 4.4 Update OnBodyGenerationComplete

Modify to calculate orbital velocity using perigee/apogee approach:

```csharp
private void OnBodyGenerationComplete(
    CelestialBody completedBody,
    CelestialBody celBody,
    Godot.Collections.Dictionary bodyDict
)
{
    // Register the body with the debug system after mesh generation completes
    // Use the sanitized namespace from IDebugDataProvider to exclude non-alphanumeric characters
#if DEBUG
    var bodyNamespace = ((UI.Debug.DatabaseViewer.IDebugDataProvider)celBody).InstanceNamespace;
    UI.Debug.Console.InstanceRegistry.Register(celBody, bodyNamespace);
#endif

    // Calculate orbital velocity using perigee/apogee approach if not already set
    CalculateOrbitalVelocityForBody(celBody, bodyDict);

    completedBody.InitializeOrbitSystem();
    bodiesCompleted++;
    if (ShowProgressUI)
    {
        GD.Print(
            $"Generated {bodiesCompleted}/{totalBodiesToGenerate} bodies ({(float)bodiesCompleted / totalBodiesToGenerate * 100:F1}%)"
        );
    }

    // Handle satellites if present
    if (
        bodyDict.ContainsKey("satellites")
        && bodyDict["satellites"].Obj is Godot.Collections.Array satellites
    )
    {
        QueueSatelliteGeneration(celBody, satellites);
    }

    // Check if all bodies are complete
    if (bodiesCompleted >= totalBodiesToGenerate)
    {
        GD.Print(
            $"System generation complete: {bodiesCompleted}/{totalBodiesToGenerate} bodies generated"
        );
        CallDeferred("emit_signal", SignalName.SystemGenerationComplete);
    }
}
```

##### 4.5 Add CalculateOrbitalVelocityForBody Method

Add a new method to calculate velocity based on perigee/apogee from system center:

```csharp
/// <summary>
/// Calculates and sets orbital velocity for a celestial body based on its distance
/// from the system center point using the perigee/apogee approach.
/// </summary>
/// <param name="celBody">The celestial body to calculate velocity for</param>
/// <param name="bodyDict">The body dictionary containing template data</param>
private void CalculateOrbitalVelocityForBody(CelestialBody celBody, Godot.Collections.Dictionary bodyDict)
{
    var templateDict = (Godot.Collections.Dictionary)bodyDict["template"];
    
    // Check if velocity is already set (non-zero) - use existing value if so
    Vector3 existingVelocity = celBody.Velocity;
    if (existingVelocity.LengthSquared() > 0.01f)
    {
        // Velocity already specified in template - check if we should override
        // For now, skip if velocity is manually specified
        GameLogger.Debug($"CelestialBody {celBody.Name}: Using template velocity {existingVelocity}");
        return;
    }
    
    // Calculate distance from system center
    Vector3 bodyPosition = celBody.GlobalPosition;
    Vector3 directionFromCenter = bodyPosition - _systemCenterPoint;
    float distanceFromCenter = directionFromCenter.Length();
    
    if (distanceFromCenter < 0.001f)
    {
        GameLogger.Warning($"CelestialBody {celBody.Name}: At system center, cannot calculate orbit");
        return;
    }
    
    if (_centerMass <= 0f)
    {
        GameLogger.Warning($"SystemGenerator: No center mass for orbital calculation");
        return;
    }
    
    // Use perigee/apogee approach:
    // Treat distance as one side of an ellipse - use same value for perigee and apogee
    // This creates a circular orbit
    float perigee = distanceFromCenter;
    float apogee = distanceFromCenter;
    
    // Calculate orbital plane basis vectors
    // pHat points from center to body
    Vector3 pHat = directionFromCenter.Normalized();
    
    // qHat is perpendicular to pHat in the orbital plane (using Up as reference)
    Vector3 up = Vector3.Up;
    if (Mathf.Abs(pHat.Dot(up)) > 0.99f)
    {
        up = Vector3.Right;
    }
    Vector3 qHat = pHat.Cross(up).Normalized();
    
    // Calculate starting angle from the center to body position
    float startingAngle = Mathf.Atan2(pHat.Z, pHat.X);
    
    // Calculate velocity using OrbitalMath
    Vector3 velocity = OrbitalMath.CalculateEllipticalOrbitalVelocity(
        pHat,
        qHat,
        _centerMass,
        apogee,
        perigee,
        startingAngle,
        false // counter-clockwise
    );
    
    // Apply the velocity
    celBody.Velocity = velocity;
    
    GameLogger.Debug(
        $"CelestialBody {celBody.Name}: Calculated orbital velocity {velocity.Length():F2} m/s " +
        $"(distance: {distanceFromCenter:F0}, center mass: {_centerMass:F0})"
    );
}
```

##### 4.6 Add Reset Method

Add a method to reset tracking when generating a new system:

```csharp
private void GenerateMesh(Godot.Collections.Array<Godot.Collections.Dictionary> bodies)
{
    // Clear existing bodies
    if (SystemContainer!.GetChildCount() > 0)
    {
        var children = SystemContainer.GetChildren();
        foreach (Node child in children)
        {
            child.RemoveFromGroup("CelestialBody");
            child.QueueFree();
        }
    }

    // Reset system center tracking
    _celestialBodies.Clear();
    _systemCenterPoint = Vector3.Zero;
    _centerMass = 0f;
    _dominantBodyIndices.Clear();

    // Reset progress tracking
    totalBodiesToGenerate = bodies.Count;
    bodiesCompleted = 0;

    // ... rest of existing code
}
```

---

## Algorithm Details

### Gravitational Influence Calculation

The gravitational influence of body B on body A at position P is calculated as:

```
influence = G × mass_B / distance_A_to_B²
```

Where:

- G is the gravitational constant (6.7394967f in our units)
- mass_B is the mass of the influencing body
- distance is the distance between positions

### Competitive Threshold

Bodies are considered "competitive" if their influence is within **10x** (one order of magnitude) of the maximum influence. This threshold prevents minor bodies from affecting the center calculation while capturing cases where multiple significant bodies exist (e.g., binary systems).

### Barycenter Calculation

When multiple bodies are competitive, the system center (barycenter) is calculated as:

```
centerPoint = Σ(mass_i × position_i) / Σ(mass_i)
totalMass = Σ(mass_i)
```

This is a mass-weighted average position, which gives more weight to more massive bodies.

---

## File Changes Summary

| File | Changes |
|------|---------|
| `Scripts/UtilityLibrary/OrbitalMath.cs` | Add `CalculateSystemCenter`, `CalculateSystemCenterFromDicts`, `GetMostInfluentialBodyIndex` methods |
| `UI/PlanetSystemGenerator.cs` | Update `FindDominantBody` to use OrbitalMath |
| `Scripts/Constructables/ArtificialSatellites/TrajectoryPlanner.cs` | Update `FindCentralBody` to use OrbitalMath |
| `Scripts/ProceduralGeneration/SystemGenerator.cs` | Add storage fields, tracking logic, and velocity calculation |

---

## Testing Recommendations

1. **Unit Tests for OrbitalMath**:
   - Test `CalculateSystemCenter` with single body
   - Test with two bodies (one dominant)
   - Test with three bodies of equal mass (barycenter)
   - Test competitive threshold logic

2. **Integration Tests**:
   - Generate a system with a single star
   - Generate a system with star + planets
   - Generate a binary star system
   - Verify orbital velocities are calculated correctly

3. **Manual Testing**:
   - Use the UI to generate systems
   - Verify stability indicator works
   - Check that saved templates load correctly

---

## Backward Compatibility

- Existing saved system templates will continue to work
- The new velocity calculation only applies when velocity is not already specified in the template
- The UI's `FindDominantBody` method maintains its original signature
- `TrajectoryPlanner.FindCentralBody` maintains its original signature and return type
