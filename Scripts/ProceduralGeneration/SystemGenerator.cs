using System;
using System.Collections.Generic;
using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using UtilityLibrary;
#if DEBUG
using UI.Debug.Console;
#endif

namespace ProceduralGeneration.PlanetGeneration;

public partial class SystemGenerator : Node
{
    [Signal]
    public delegate void SystemGenerationCompleteEventHandler();

    [Export]
    public Node? SystemContainer;

    [ExportCategory("Thread Pool Settings")]
    [Export]
    public int MaxConcurrentThreads = -1; // -1 = auto-detect

    [Export]
    public bool EnableThreading = true;

    [Export]
    public int MemoryThresholdMB = 1024; // Memory threshold for thread pool activation

    [Export]
    public bool ShowProgressUI = true;

    // Progress tracking
    private int totalBodiesToGenerate = 0;
    private int bodiesCompleted = 0;
    private float _totalMass = 0f;
    private Dictionary<String, CelestialBody> _parentBodies = new();

    public override void _Ready()
    {
        GD.Print(this.GetPath());
        var GenerateButton = GetTree().GetFirstNodeInGroup("GenerationMenu");
        ((UI.PlanetSystemGenerator)GenerateButton).GeneratePressed += GenerateMesh;

        // ThreadPooler is now an autoload, no manual initialization needed
        GD.Print(
            $"SystemGenerator ready, ThreadPooler available: {UtilityLibrary.TaskSystem.ThreadPooler.Instance != null}"
        );
    }

    private void GenerateMesh(
        Godot.Collections.Array<Godot.Collections.Dictionary> dominantBodies,
        Godot.Collections.Array<Godot.Collections.Dictionary> satelliteBelts,
        Godot.Collections.Array<Godot.Collections.Dictionary> planetaryBodies,
        Barycenter barycenter
    )
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

        // Reset all tracking state for a fresh generation
        int totalBodies = dominantBodies.Count + satelliteBelts.Count + planetaryBodies.Count;
        totalBodiesToGenerate = totalBodies;
        bodiesCompleted = 0;
        _totalMass = 0f;
        _parentBodies.Clear();

        GD.Print($"Generating System: {totalBodies} bodies");
        GD.Print(
            $"Dominant: {dominantBodies.Count}, Belts: {satelliteBelts.Count}, Planets: {planetaryBodies.Count}"
        );
        GD.Print($"Barycenter: {barycenter}");

        // First, create dominant bodies (Stars, BlackHoles) - these are reference points
        foreach (Godot.Collections.Dictionary body in dominantBodies)
        {
            CreateAndQueueCelestialBody(body, barycenter);
        }

        // Then, create satellite belts
        foreach (Godot.Collections.Dictionary belt in satelliteBelts)
        {
            CreateAndQueueSatelliteBelt(belt, barycenter);
        }

        // Finally, create planetary bodies with orbital calculations
        foreach (Godot.Collections.Dictionary body in planetaryBodies)
        {
            CreateAndQueuePlanetaryBody(body, barycenter);
        }

        GD.Print($"System generation started: {totalBodiesToGenerate} bodies queued");
    }

    private void CreateAndQueuePlanetaryBody(
        Godot.Collections.Dictionary body,
        Barycenter barycenter
    )
    {
        // Get orbital parameters
        float apogee = 1000f;
        float perigee = 500f;
        float startingAngle = 0f;
        float verticalOffset = 0f;
        int orbitalCenterIndex = -1;
        String parentName = "";

        if (body.ContainsKey("orbital_parameters"))
        {
            var orbitalParams = (Godot.Collections.Dictionary)body["orbital_parameters"];
            parentName = orbitalParams.ContainsKey("parent_body")
                ? (String)orbitalParams["parent_body"]
                : "";
            apogee = orbitalParams.ContainsKey("apogee") ? (float)orbitalParams["apogee"] : 1000f;
            perigee = orbitalParams.ContainsKey("perigee") ? (float)orbitalParams["perigee"] : 500f;
            startingAngle = orbitalParams.ContainsKey("starting_angle")
                ? (float)orbitalParams["starting_angle"]
                : 0f;
            verticalOffset = orbitalParams.ContainsKey("vertical_offset")
                ? (float)orbitalParams["vertical_offset"]
                : 0f;
            orbitalCenterIndex = orbitalParams.ContainsKey("orbital_center_index")
                ? (int)orbitalParams["orbital_center_index"]
                : -1;
        }

        // Create a local barycenter for this body's orbital calculation.
        // IMPORTANT: We must NOT mutate the shared barycenter reference, because it is
        // a Resource (reference type) shared across all planetary body iterations.
        // Mutating it would corrupt orbital calculations for subsequent bodies.
        Barycenter localBarycenter;
        if (parentName != "barycenter" && _parentBodies.ContainsKey(parentName))
        {
            // Orbit a specific named body — use that body's position and mass directly.
            localBarycenter = new Barycenter(
                _parentBodies[parentName].GlobalPosition,
                Vector3.Zero,
                _parentBodies[parentName].Mass
            );
        }
        else
        {
            // Orbit the system barycenter. Use the barycenter position for geometry,
            // but compute an effective central mass from the actual gravitational field
            // of all dominant bodies. Using the raw totalMass at the barycenter distance
            // produces incorrect vis-viva velocities because the mass is distributed
            // among individual stars, not concentrated at the barycenter.
            float effectiveMass = ComputeEffectiveCentralMass(
                barycenter.Position,
                apogee,
                perigee,
                startingAngle,
                verticalOffset
            );
            localBarycenter = new Barycenter(barycenter.Position, Vector3.Zero, effectiveMass);
        }

        // Calculate position and velocity from orbital parameters
        var (position, velocity) = OrbitalMath.CalculateOrbitalStateFromParams(
            apogee,
            perigee,
            startingAngle,
            verticalOffset,
            localBarycenter
        );

        // Update the body template with calculated position/velocity
        var template = (Godot.Collections.Dictionary)body["template"];
        if (Single.IsNaN(position.X) || Single.IsNaN(position.Y) || Single.IsNaN(position.Z))
        {
            position = Vector3.Zero;
        }
        if (Single.IsNaN(velocity.X) || Single.IsNaN(velocity.Y) || Single.IsNaN(velocity.Z))
        {
            velocity = Vector3.Zero;
        }
        template["position"] = position;
        template["velocity"] = velocity;
        body["template"] = template;

        var mesh = new UnifiedCelestialMesh();
        CelestialBody celBody = CelestialBody.Builder.BuildFromBodyDict(body, mesh);

        SystemContainer!.AddChild(celBody);
        celBody.Position = position;
        _parentBodies.Add(celBody.Name, celBody);
        _totalMass += celBody.Mass;

        celBody.StartMeshGeneration(
            onCompleted: (completedBody) => OnBodyGenerationComplete(completedBody, celBody, body),
            onFailed: (failedBody, error) => OnBodyGenerationFailed(failedBody, error, celBody)
        );
    }

    private void CreateAndQueueSatelliteBelt(
        Godot.Collections.Dictionary belt,
        Barycenter barycenter
    )
    {
        // Get orbital center
        int orbitalCenterIndex = belt.ContainsKey("orbital_center_index")
            ? (int)belt["orbital_center_index"]
            : -1;

        // For now, satellite belts in the standalone section need a parent celestial body
        // We'll need to handle this by either:
        // 1. Finding an existing dominant body as parent
        // 2. Creating a placeholder body

        // Try to find a parent body from the system container
        String parentName = belt.ContainsKey("parent_body") ? (String)belt["parent_body"] : "";
        CelestialBody? parentBody = _parentBodies[parentName];

        // If we found a parent, generate the belt
        if (parentBody != null)
        {
            GenerateSatelliteBelt(belt, parentBody);
        }
        else
        {
            GD.PrintErr("Could not find parent body for satellite belt generation");
        }
    }

    private void CreateAndQueueCelestialBody(
        Godot.Collections.Dictionary body,
        Barycenter barycenter
    )
    {
        GD.Print($"Generating {body["name"]}: {body}");
        Godot.Collections.Dictionary templateDict = (Godot.Collections.Dictionary)body["template"];
        float mass = (float)templateDict["mass"];
        Vector3 position = (Vector3)templateDict["position"];
        Vector3 velocity = (Vector3)templateDict["velocity"];
        String type = (String)body["type"];
        Godot.Collections.Dictionary centralParameters = (Godot.Collections.Dictionary)
            body["central_parameters"];
        if (Single.IsNaN(position.X) || Single.IsNaN(position.Y) || Single.IsNaN(position.Z))
        {
            GD.PrintErr($"Body {body["name"]} has invalid position: {position}");
            position = Vector3.Zero;
        }
        if (Single.IsNaN(velocity.X) || Single.IsNaN(velocity.Y) || Single.IsNaN(velocity.Z))
        {
            GD.PrintErr($"Body {body["name"]} has invalid velocity: {velocity}");
            velocity = Vector3.Zero;
        }
        String name = (String)body["name"];
        var mesh = new UnifiedCelestialMesh();
        CelestialBody.Builder celBodyBuilder = new CelestialBody.Builder();
        CelestialBody celBody = celBodyBuilder
            .WithVelocity(velocity)
            .WithMass(mass)
            .WithBodyDict(body)
            .WithMesh(mesh)
            .WithType((CelestialBodyType)Enum.Parse(typeof(CelestialBodyType), type))
            .WithName(name)
            .Build();

        SystemContainer!.AddChild(celBody);
        celBody.Position = position;
        GD.Print($"CelBody: {celBody.Name}");
        _parentBodies.Add(celBody.Name, celBody);
        _totalMass += celBody.Mass;

        celBody.StartMeshGeneration(
            onCompleted: (completedBody) => OnBodyGenerationComplete(completedBody, celBody, body),
            onFailed: (failedBody, error) => OnBodyGenerationFailed(failedBody, error, celBody)
        );
    }

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

    private void OnBodyGenerationFailed(
        CelestialBody failedBody,
        string error,
        CelestialBody celBody
    )
    {
        GD.PrintErr($"Body generation failed: {celBody.Name}, error: {error}");
        celBody.QueueFree();

        bodiesCompleted++;
        if (bodiesCompleted >= totalBodiesToGenerate)
        {
            EmitSignal(SignalName.SystemGenerationComplete);
        }
    }

    private void QueueSatelliteGeneration(
        CelestialBody parentBody,
        Godot.Collections.Array satellites
    )
    {
        if (
            parentBody.Type == CelestialBodyType.Star
            || parentBody.Type == CelestialBodyType.BlackHole
        )
        {
            foreach (Godot.Collections.Dictionary satBelt in satellites)
            {
                GenerateSatelliteBelt(satBelt, parentBody);
            }
        }
        else
        {
            foreach (Godot.Collections.Dictionary sat in satellites)
            {
                GenerateSingleSatellite(sat, parentBody);
            }
        }
    }

    public bool CheckGravitationalStability(
        Godot.Collections.Array<Godot.Collections.Dictionary> bodies,
        float simulationTime = 1000000.0f,
        int steps = 1000000
    )
    {
        var bodyStates = new List<BodyState>();
        foreach (var body in bodies)
        {
            GD.Print($"Chekcing gravitational stability for {body}");
            Godot.Collections.Dictionary templateDict = (Godot.Collections.Dictionary)
                body["template"];
            var position = (Vector3)templateDict["position"];
            var velocity = (Vector3)templateDict["velocity"];
            var mass = (float)templateDict["mass"];
            var size = (float)templateDict["size"];
            GD.Print(
                $"Body: {body["type"]} Position: {position} Velocity: {velocity} Mass: {mass} Size: {size}"
            );

            bodyStates.Add(
                new BodyState
                {
                    Position = position,
                    Velocity = velocity,
                    Mass = mass,
                    Size = size,
                }
            );
        }

        float dt = simulationTime / steps;

        for (int step = 0; step < steps; step++)
        {
            // Calculate forces and update velocities and positions
            for (int i = 0; i < bodyStates.Count; i++)
            {
                var totalForce = Vector3.Zero;

                for (int j = 0; j < bodyStates.Count; j++)
                {
                    if (i != j)
                    {
                        var direction = bodyStates[j].Position - bodyStates[i].Position;
                        var distance = direction.Length();

                        if (distance > 0.001f) // Avoid division by zero
                        {
                            var forceMagnitude =
                                OrbitalMath.GRAVITATIONAL_CONSTANT
                                * bodyStates[i].Mass
                                * bodyStates[j].Mass
                                / (distance * distance);
                            totalForce += direction.Normalized() * forceMagnitude;
                        }
                    }
                }

                // Update velocity and position using Euler integration
                bodyStates[i].Velocity += (totalForce / bodyStates[i].Mass) * dt;
                bodyStates[i].Position += bodyStates[i].Velocity * dt;
                // Check for collisions
            }
            for (int x = 0; x < bodyStates.Count; x++)
            {
                for (int y = x + 1; y < bodyStates.Count; y++)
                {
                    var distance = (bodyStates[x].Position - bodyStates[y].Position).Length();
                    if (distance <= bodyStates[x].Size + bodyStates[y].Size)
                    {
                        var distSqr = (
                            bodyStates[x].Position - bodyStates[y].Position
                        ).LengthSquared();
                        var instersectionLensVolume =
                            (
                                Mathf.Pi
                                * Mathf.Pow(bodyStates[x].Size + bodyStates[y].Size - distance, 2)
                                * (
                                    distSqr
                                    + 2f * distance * bodyStates[y].Size
                                    - 3f * Mathf.Pow(bodyStates[y].Size, 2)
                                    + 2f * distance * bodyStates[x].Size
                                    + 6f * bodyStates[y].Size * bodyStates[x].Size
                                    - 3f * Mathf.Pow(bodyStates[x].Size, 2)
                                )
                            ) / (12f * distance);
                        if (instersectionLensVolume > 0.0f)
                        {
                            return false; // Collision detected
                        }
                    }
                }
            }
        }

        return true; // No collisions detected
    }

    private void GenerateSingleSatellite(Godot.Collections.Dictionary sat, CelestialBody parentBody)
    {
        var templateDict = (Godot.Collections.Dictionary)sat["template"];

        // Read orbital parameters
        float apogee = templateDict.ContainsKey("apogee") ? (float)templateDict["apogee"] : 500f;
        float perigee = templateDict.ContainsKey("perigee") ? (float)templateDict["perigee"] : 300f;
        float startingAngle = templateDict.ContainsKey("starting_angle")
            ? (float)templateDict["starting_angle"]
            : 0f;
        float verticalOffset = templateDict.ContainsKey("vertical_offset")
            ? (float)templateDict["vertical_offset"]
            : 0f;

        // Calculate position and velocity from orbital parameters
        var (position, velocity) = SatelliteBody.CalculateOrbitalState(
            apogee,
            perigee,
            startingAngle,
            verticalOffset,
            parentBody.Mass
        );

        var mesh = new UnifiedCelestialMesh();
        var parentPlanetaryType = (PlanetaryBodyType)Enum.Parse(
            typeof(PlanetaryBodyType),
            parentBody.Type.ToString()
        );
        SatelliteBody satBody = SatelliteBody.Builder.BuildFromBodyDict(
            parentPlanetaryType,
            sat,
            mesh
        );

        parentBody.CallDeferred("add_child", satBody);

        // Override position with calculated orbital position
        satBody.Position = position;
        // Override velocity with calculated orbital velocity
        satBody.Velocity = velocity;

        satBody.StartMeshGeneration(
            onCompleted: (completedSat) =>
            {
                GD.Print($"Generated {completedSat.Name}");
            },
            onFailed: (failedSat, error) =>
            {
                GD.PrintErr($"Satellite generation failed: {failedSat.Name}, error: {error}");
                failedSat.QueueFree();
            }
        );
    }

    private void GenerateSatelliteBelt(
        Godot.Collections.Dictionary satBelt,
        CelestialBody parentBody
    )
    {
        var parentDominantType = (DominantBodyType)Enum.Parse(
            typeof(DominantBodyType),
            parentBody.Type.ToString()
        );
        SatelliteBeltBody beltBody = SatelliteBeltBody.Builder.BuildFromBodyDict(
            parentDominantType,
            satBelt
        );
        var sats = beltBody.GenerateSatelliteBelt(parentBody);

        foreach (SatelliteBody sat in sats)
        {
            parentBody.CallDeferred("add_child", sat);
            var templateDict = (Godot.Collections.Dictionary)sat.bodyDict!["template"];
            sat.Position = (Vector3)templateDict["base_position"];
            GD.Print($"Generating {sat.Name}, Position: {sat.Position}");

            sat.StartMeshGeneration(
                onCompleted: (completedSat) =>
                {
                    GD.Print($"Generated satellite belt body: {completedSat.Name}");
                },
                onFailed: (failedSat, error) =>
                {
                    GD.PrintErr(
                        $"Satellite belt body generation failed: {failedSat.Name}, error: {error}"
                    );
                    failedSat.QueueFree();
                }
            );
        }
    }

    /// <summary>
    /// Computes an effective central mass for a planetary body orbiting the system barycenter.
    ///
    /// The vis-viva equation requires a single central mass at the orbital focus, but the
    /// actual gravitational field comes from multiple dominant bodies (stars) distributed
    /// around the barycenter. Using the raw total system mass produces velocities that are
    /// too high when the planet is close to one star and far from others, because the point-
    /// mass approximation breaks down.
    ///
    /// This method estimates the planet's position from its orbital parameters, sums the
    /// gravitational acceleration from all dominant bodies at that position, and computes
    /// the equivalent single central mass that would produce the same radial acceleration
    /// at the planet's distance from the barycenter:
    ///
    ///   a_net = sum(G * M_i / r_i^2)  (radial component only, toward barycenter)
    ///   M_eff = a_net * R^2 / G        where R = distance from barycenter
    ///
    /// For a single dominant body at the barycenter, M_eff equals that body's mass exactly.
    /// For widely separated stars with a planet far away, M_eff approaches total system mass.
    /// For a planet near one star in a binary, M_eff is dominated by the nearest star's mass.
    /// </summary>
    private float ComputeEffectiveCentralMass(
        Vector3 barycenterPosition,
        float apogee,
        float perigee,
        float startingAngleDeg,
        float verticalOffsetDeg
    )
    {
        // Estimate the planet's approximate position using a simplified orbital placement.
        // We use the semi-latus rectum radius at the starting angle for the position estimate.
        float semiMajorAxis = (apogee + perigee) / 2f;
        float eccentricity = OrbitalMath.CalculateEccentricity(apogee, perigee);
        float theta = Mathf.DegToRad(startingAngleDeg);
        float semiLatusRectum = semiMajorAxis * (1f - eccentricity * eccentricity);
        float denominator = 1f + eccentricity * Mathf.Cos(theta);
        if (Mathf.Abs(denominator) < 1e-10f)
            denominator = 1f;
        float r = semiLatusRectum / denominator;

        // Build an approximate position in the XZ plane relative to the barycenter
        float iRad = Mathf.DegToRad(verticalOffsetDeg);
        Vector3 estimatedPosition =
            barycenterPosition
            + new Vector3(
                r * Mathf.Cos(theta) * Mathf.Cos(iRad),
                r * Mathf.Sin(iRad),
                r * Mathf.Sin(theta) * Mathf.Cos(iRad)
            );

        // Sum the radial gravitational acceleration from all dominant bodies at this position.
        // "Radial" here means the component directed toward the barycenter, since that is what
        // the vis-viva orbit model assumes — a central force toward the focus (barycenter).
        Vector3 toBary = barycenterPosition - estimatedPosition;
        float distToBarySq = toBary.LengthSquared();
        float distToBary = Mathf.Sqrt(distToBarySq);

        if (distToBary < 1e-6f)
        {
            // Planet is at the barycenter — just sum all dominant body masses as fallback
            float totalDominantMass = 0f;
            foreach (var kvp in _parentBodies)
            {
                if (
                    kvp.Value.Type == CelestialBodyType.Star
                    || kvp.Value.Type == CelestialBodyType.BlackHole
                )
                {
                    totalDominantMass += kvp.Value.Mass;
                }
            }
            return totalDominantMass > 0f ? totalDominantMass : _totalMass;
        }

        Vector3 baryDir = toBary / distToBary; // unit vector toward barycenter
        float radialAcceleration = 0f;

        foreach (var kvp in _parentBodies)
        {
            CelestialBody dominantBody = kvp.Value;
            // Only consider dominant bodies (Stars, BlackHoles) for the effective mass
            if (
                dominantBody.Type != CelestialBodyType.Star
                && dominantBody.Type != CelestialBodyType.BlackHole
            )
            {
                continue;
            }

            Vector3 toBody = dominantBody.GlobalPosition - estimatedPosition;
            float distSq = toBody.LengthSquared();
            if (distSq < 1e-6f)
                continue;

            // Gravitational acceleration toward this body: a = G * M / r^2
            float accel = OrbitalMath.GRAVITATIONAL_CONSTANT * dominantBody.Mass / distSq;
            Vector3 accelDir = toBody.Normalized();

            // Project onto the radial direction (toward barycenter) — this is the component
            // that the vis-viva orbit model accounts for.
            radialAcceleration += accel * accelDir.Dot(baryDir);
        }

        // If net radial acceleration is non-positive (e.g., planet pulled away from barycenter
        // by a nearby star on the opposite side), fall back to nearest dominant body mass.
        if (radialAcceleration <= 0f)
        {
            return FindNearestDominantBodyMass(estimatedPosition);
        }

        // M_eff = a_radial * R^2 / G
        float effectiveMass =
            radialAcceleration * distToBarySq / OrbitalMath.GRAVITATIONAL_CONSTANT;
        return effectiveMass;
    }

    /// <summary>
    /// Finds the mass of the nearest dominant body (Star or BlackHole) to a given position.
    /// Used as a fallback when the effective mass computation produces invalid results.
    /// </summary>
    private float FindNearestDominantBodyMass(Vector3 position)
    {
        float nearestDistSq = float.MaxValue;
        float nearestMass = 0f;

        foreach (var kvp in _parentBodies)
        {
            CelestialBody body = kvp.Value;
            if (body.Type != CelestialBodyType.Star && body.Type != CelestialBodyType.BlackHole)
                continue;

            float distSq = (body.GlobalPosition - position).LengthSquared();
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearestMass = body.Mass;
            }
        }

        return nearestMass > 0f ? nearestMass : _totalMass;
    }

    private class BodyState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Mass;
        public float Size;
    }
}
