using System;
using System.Collections.Generic;
using System.Threading;
using Godot;
using ProceduralGeneration.ColorSystem;
using ProceduralGeneration.MeshGeneration;
using Structures;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.GameMath.Orbital;

namespace ProceduralGeneration.PlanetGeneration;

public partial class SystemGenerator : Node
{
    [Signal]
    public delegate void SystemGenerationCompleteEventHandler();

    [Export]
    public Node? SystemContainer;

    [Export]
    public Node? TargetContainer;

    [ExportCategory("Thread Pool Settings")]
    [Export]
    public int MaxConcurrentThreads = -1; // -1 = auto-detect

    [Export]
    public bool EnableThreading = true;

    [Export]
    public int MemoryThresholdMB = 1024; // Memory threshold for thread pool activation

    [Export]
    public bool ShowProgressUI = true;

    // Progress tracking, split by orbital-body category.
    // Totals are set on the main thread during GenerateMesh before any completion can fire.
    private int _dominantTotal = 0;
    private int _planetaryTotal = 0;

    // Satellite total is not known up front (belt sizes resolve at generation time); it is
    // incremented via Interlocked as each satellite's mesh generation is queued.
    private int _satelliteTotal = 0;

    // Completed counts (success + failure) are mutated from worker threads — use Interlocked.
    private int _dominantDone = 0;
    private int _planetaryDone = 0;
    private int _satelliteDone = 0;

    // Set true once GenerateMesh has finished queueing every body, so a fast body finishing
    // mid-queue cannot fire completion before later bodies' satellites are queued.
    private volatile bool _queueingComplete = false;

    // Emit-once guard for SystemGenerationComplete (CompareExchange from worker threads).
    private int _completionEmitted = 0;

    private float _totalMass = 0f;
    private Dictionary<String, CelestialBody> _parentBodies = new();
    private NBodyCoordinator? _coordinator;

    /// <summary>
    /// Gets the effective container to use for adding bodies.
    /// Uses TargetContainer if set, otherwise falls back to SystemContainer.
    /// </summary>
    private Node GetEffectiveContainer()
    {
        return TargetContainer ?? SystemContainer!;
    }

    public override void _Ready()
    {
        GD.Print(this.GetPath());

        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.GenerateSystemRequested += GenerateMesh;
        }

        // ThreadPooler is now an autoload, no manual initialization needed
        GD.Print(
            $"SystemGenerator ready, ThreadPooler available: {UtilityLibrary.TaskSystem.ThreadPooler.Instance != null}"
        );
    }

    public override void _ExitTree()
    {
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.GenerateSystemRequested -= GenerateMesh;
        }

        base._ExitTree();
    }

    private void GenerateMesh(
        Godot.Collections.Array<Godot.Collections.Dictionary> dominantBodies,
        Godot.Collections.Array<Godot.Collections.Dictionary> satelliteBelts,
        Godot.Collections.Array<Godot.Collections.Dictionary> planetaryBodies,
        Godot.Collections.Array<Godot.Collections.Dictionary> satelliteBodies,
        Barycenter barycenter
    )
    {
        // Clean up previous coordinator
        if (_coordinator != null)
        {
            _coordinator.QueueFree();
            _coordinator = null;
        }

        // Clear existing bodies
        var effectiveContainer = GetEffectiveContainer();
        if (effectiveContainer.GetChildCount() > 0)
        {
            var children = effectiveContainer.GetChildren();
            foreach (Node child in children)
            {
                child.RemoveFromGroup("CelestialBody");
                child.QueueFree();
            }
        }

        effectiveContainer.AddChild(barycenter);

        // Reset all tracking state for a fresh generation
        _dominantTotal = dominantBodies.Count;
        _planetaryTotal = planetaryBodies.Count;
        _satelliteTotal = 0;
        _dominantDone = 0;
        _planetaryDone = 0;
        _satelliteDone = 0;
        _queueingComplete = false;
        _completionEmitted = 0;
        _totalMass = 0f;
        _parentBodies.Clear();

        int totalBodies = dominantBodies.Count + satelliteBelts.Count + planetaryBodies.Count;
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

        // Then, create planetary bodies with orbital calculations
        foreach (Godot.Collections.Dictionary body in planetaryBodies)
        {
            CreateAndQueuePlanetaryBody(body, barycenter);
        }

        // Finally, create flattened satellites in topological (parent-before-child) order so a
        // moon-of-a-moon resolves its parent. Each built satellite registers into _parentBodies.
        ProcessFlattenedSatellites(satelliteBodies);

        // Create the N-body physics coordinator for synchronized integration
        _coordinator = new NBodyCoordinator();
        GetEffectiveContainer().AddChild(_coordinator);

        // All bodies (including satellites) are now queued. Open the completion gate and
        // check once, in case every body finished synchronously during queueing.
        _queueingComplete = true;
        GD.Print(
            $"System generation started: {_dominantTotal + _planetaryTotal + Volatile.Read(ref _satelliteTotal)} bodies queued"
        );
        CheckSystemComplete();
    }

    private void CreateAndQueuePlanetaryBody(
        Godot.Collections.Dictionary body,
        Barycenter barycenter
    )
    {
        GD.Print($"Generating {body["name"]}: {body}");
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
        var templateDict = (Godot.Collections.Dictionary)body["template"];
        if (Single.IsNaN(position.X) || Single.IsNaN(position.Y) || Single.IsNaN(position.Z))
        {
            position = Vector3.Zero;
        }
        if (Single.IsNaN(velocity.X) || Single.IsNaN(velocity.Y) || Single.IsNaN(velocity.Z))
        {
            velocity = Vector3.Zero;
        }
        float mass = (float)templateDict["mass"];
        String type = (String)body["type"];

        // Cumulative distance from the system center, retained for resource/mesh-param lookups.
        float distanceAU = OrbitalDistanceCalculator.CalculateDistanceFromStarAU(body);
        var bodyType = (OrbitalBodyType)
            Enum.Parse(typeof(OrbitalBodyType), (String)body["type"]);

        String name = (String)body["name"];

        // Subtype is authored per body in the system template (subtype / subtype_weights).
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        BodyClassification classification = SubtypeResolver.Resolve(body, bodyType, rng);

        var mesh = new UnifiedCelestialMesh();
        var celBodyBuilder = new CelestialBody.Builder();
        celBodyBuilder
            .WithVelocity(velocity)
            .WithMass(mass)
            .WithBodyDict(body)
            .WithMesh(mesh)
            .WithClassification(classification)
            .WithName(name);
        CelestialBody celBody = celBodyBuilder.Build();
        // Planetary body: depth 1, cumulative AU is its own distance from the system center.
        celBody.Depth = 1;
        celBody.EffectiveAU = distanceAU;

        GetEffectiveContainer().AddChild(celBody);
        celBody.Position = position;
        celBody.OrbitalParent =
            !string.IsNullOrEmpty(parentName)
            && parentName != "barycenter"
            && _parentBodies.TryGetValue(parentName, out var parentForOrbit)
                ? parentForOrbit
                : null;
        _parentBodies.Add(celBody.Name, celBody);
        _totalMass += celBody.Mass;

        celBody.StartMeshGeneration(
            onCompleted: (completedBody) =>
                OnBodyGenerationComplete(completedBody, celBody, isDominant: false),
            onFailed: (failedBody, error) =>
                OnBodyGenerationFailed(failedBody, error, celBody, isDominant: false)
        );

        // Satellites are generated separately from the flattened top-level section
        // (see ProcessFlattenedSatellites), after every planetary body is registered.
    }

    private void CreateAndQueueSatelliteBelt(
        Godot.Collections.Dictionary belt,
        Barycenter barycenter
    )
    {
        // Get orbital center index (-1 = barycenter, 0+ = dominant body index)
        int orbitalCenterIndex = belt.ContainsKey("orbital_center_index")
            ? (int)belt["orbital_center_index"]
            : -1;

        // Resolve the parent body by index from _parentBodies
        CelestialBody? parentBody = null;

        if (orbitalCenterIndex >= 0)
        {
            // Find the N-th dominant body added to _parentBodies
            int idx = 0;
            foreach (var kvp in _parentBodies)
            {
                if (idx == orbitalCenterIndex)
                {
                    parentBody = kvp.Value;
                    break;
                }
                idx++;
            }
        }
        else
        {
            // Barycenter: use the first parent body if available
            foreach (var kvp in _parentBodies)
            {
                parentBody = kvp.Value;
                break;
            }
        }

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
        var bodyType = (OrbitalBodyType)Enum.Parse(typeof(OrbitalBodyType), type);
        String name = (String)body["name"];

        // Select subtype for dominant body (stars, black holes, neutron stars) from its
        // per-body subtype / subtype_weights in the system template.
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        BodyClassification classification = SubtypeResolver.Resolve(body, bodyType, rng);

        var mesh = new UnifiedCelestialMesh();
        CelestialBody.Builder celBodyBuilder = new CelestialBody.Builder();
        celBodyBuilder
            .WithVelocity(velocity)
            .WithMass(mass)
            .WithBodyDict(body)
            .WithMesh(mesh)
            .WithClassification(classification)
            .WithName(name);

        var celBody = celBodyBuilder.Build();
        // Dominant body: root of the orbital hierarchy.
        celBody.Depth = 0;
        celBody.EffectiveAU = 0f;

        GetEffectiveContainer().AddChild(celBody);
        celBody.Position = position;
        celBody.OrbitalParent = null;
        GD.Print($"CelBody: {celBody.Name}");
        _parentBodies.Add(celBody.Name, celBody);
        _totalMass += celBody.Mass;

        celBody.StartMeshGeneration(
            onCompleted: (completedBody) =>
                OnBodyGenerationComplete(completedBody, celBody, isDominant: true),
            onFailed: (failedBody, error) =>
                OnBodyGenerationFailed(failedBody, error, celBody, isDominant: true)
        );
    }

    private void OnBodyGenerationComplete(
        CelestialBody completedBody,
        CelestialBody celBody,
        bool isDominant
    )
    {
        // Register the body with the debug system after mesh generation completes
        // Use the sanitized namespace from IDebugDataProvider to exclude non-alphanumeric characters
#if DEBUG
        var bodyNamespace = ((Debug.DatabaseViewer.IDebugDataProvider)celBody).InstanceNamespace;
        Debug.Console.InstanceRegistry.Register(celBody, bodyNamespace);
#endif

        completedBody.InitializeOrbitSystem();
        if (isDominant)
            Interlocked.Increment(ref _dominantDone);
        else
            Interlocked.Increment(ref _planetaryDone);

        CheckSystemComplete();
    }

    private void OnBodyGenerationFailed(
        CelestialBody failedBody,
        string error,
        CelestialBody celBody,
        bool isDominant
    )
    {
        GD.PrintErr($"Body generation failed: {celBody.Name}, error: {error}");
        celBody.QueueFree();

        // Count failures toward the gate so generation cannot hang on a failed body.
        if (isDominant)
            Interlocked.Increment(ref _dominantDone);
        else
            Interlocked.Increment(ref _planetaryDone);

        CheckSystemComplete();
    }

    /// <summary>
    /// Fires SystemGenerationComplete exactly once, when every orbital body (dominant,
    /// planetary, and satellite) has finished generating. Safe to call from worker threads;
    /// the _queueingComplete gate prevents an early fire while bodies are still being queued.
    /// </summary>
    private void CheckSystemComplete()
    {
        if (!_queueingComplete)
            return;

        int done =
            Volatile.Read(ref _dominantDone)
            + Volatile.Read(ref _planetaryDone)
            + Volatile.Read(ref _satelliteDone);
        int total = _dominantTotal + _planetaryTotal + Volatile.Read(ref _satelliteTotal);

        if (done >= total && Interlocked.CompareExchange(ref _completionEmitted, 1, 0) == 0)
        {
            GD.Print($"System generation complete: {done}/{total} bodies generated");
            CallDeferred(nameof(EmitSystemGenerationCompleteViaSignalBus));
        }
    }

    /// <summary>
    /// Generates the flattened top-level satellites in topological (parent-before-child) order.
    /// Each entry names its parent via <c>parent</c>; a satellite is generated only once its parent
    /// is registered in <c>_parentBodies</c>, and is itself registered afterward so moon-of-a-moon
    /// chains resolve. Entries whose parent never appears are skipped with a warning.
    /// </summary>
    private void ProcessFlattenedSatellites(
        Godot.Collections.Array<Godot.Collections.Dictionary> satellites
    )
    {
        var pending = new List<Godot.Collections.Dictionary>(satellites);

        bool progress = true;
        while (pending.Count > 0 && progress)
        {
            progress = false;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var sat = pending[i];
                string parentName = sat.ContainsKey("parent") ? (string)sat["parent"] : "";
                if (string.IsNullOrEmpty(parentName) || !_parentBodies.TryGetValue(parentName, out var parentBody))
                    continue;

                CelestialBody satBody = GenerateSingleSatellite(sat, parentBody);
                if (satBody != null && !string.IsNullOrEmpty(satBody.Name) && !_parentBodies.ContainsKey(satBody.Name))
                    _parentBodies.Add(satBody.Name, satBody);

                pending.RemoveAt(i);
                progress = true;
            }
        }

        foreach (var sat in pending)
        {
            string parentName = sat.ContainsKey("parent") ? (string)sat["parent"] : "";
            GD.PrintErr(
                $"Satellite '{(sat.ContainsKey("name") ? sat["name"] : sat["type"])}' references unknown parent '{parentName}' — skipping"
            );
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

    private CelestialBody GenerateSingleSatellite(Godot.Collections.Dictionary sat, CelestialBody parentBody)
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
        var (position, velocity) = CelestialBody.CalculateOrbitalState(
            apogee,
            perigee,
            startingAngle,
            verticalOffset,
            parentBody.Mass
        );

        var mesh = new UnifiedCelestialMesh();

        // Subtype is authored per satellite in the system template (subtype / subtype_weights).
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        var satType = (OrbitalBodyType)Enum.Parse(typeof(OrbitalBodyType), (string)sat["type"]);
        // Cumulative distance from the system center, retained for resource/mesh-param lookups:
        // parent's cumulative AU plus this satellite's own distance from its parent.
        float effectiveAU =
            parentBody.EffectiveAU
            + OrbitalMath.ConvertUnitsToAU((apogee + perigee) / 2f);
        BodyClassification classification = SubtypeResolver.Resolve(sat, satType, rng);

        CelestialBody satBody = new CelestialBody.Builder()
            .FromBodyDict(sat, mesh)
            .WithClassification(classification)
            .WithDepth(parentBody.Depth + 1)
            .Build();
        satBody.EffectiveAU = effectiveAU;

        parentBody.CallDeferred("add_child", satBody);

        // Override position with calculated orbital position
        satBody.Position = position;
        // Override velocity with calculated orbital velocity
        satBody.Velocity = velocity;
        satBody.OrbitalParent = parentBody;

        Interlocked.Increment(ref _satelliteTotal);
        satBody.StartMeshGeneration(
            onCompleted: (completedSat) =>
            {
                GD.Print($"Generated {completedSat.Name}");
                Interlocked.Increment(ref _satelliteDone);
                CheckSystemComplete();
            },
            onFailed: (failedSat, error) =>
            {
                GD.PrintErr($"Satellite generation failed: {failedSat.Name}, error: {error}");
                failedSat.QueueFree();
                Interlocked.Increment(ref _satelliteDone);
                CheckSystemComplete();
            }
        );

        return satBody;
    }

    private void GenerateSatelliteBelt(
        Godot.Collections.Dictionary satBelt,
        CelestialBody parentBody
    )
    {
        var parentDominantType = (OrbitalBodyType)
            Enum.Parse(typeof(OrbitalBodyType), parentBody.Type.ToString());
        SatelliteBeltBody beltBody = SatelliteBeltBody.Builder.BuildFromBodyDict(
            parentDominantType,
            satBelt
        );
        var sats = beltBody.GenerateSatelliteBelt(parentBody);

        foreach (CelestialBody sat in sats)
        {
            parentBody.CallDeferred("add_child", sat);
            sat.OrbitalParent = parentBody;
            // Position was set on the body in SatelliteBeltBody.CreateSatellite (local, retained
            // after parenting).
            GD.Print($"Generating {sat.Name}, Position: {sat.Position}");

            Interlocked.Increment(ref _satelliteTotal);
            sat.StartMeshGeneration(
                onCompleted: (completedSat) =>
                {
                    GD.Print($"Generated satellite belt body: {completedSat.Name}");
                    Interlocked.Increment(ref _satelliteDone);
                    CheckSystemComplete();
                },
                onFailed: (failedSat, error) =>
                {
                    GD.PrintErr(
                        $"Satellite belt body generation failed: {failedSat.Name}, error: {error}"
                    );
                    failedSat.QueueFree();
                    Interlocked.Increment(ref _satelliteDone);
                    CheckSystemComplete();
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
                    kvp.Value.Type == OrbitalBodyType.Star
                    || kvp.Value.Type == OrbitalBodyType.BlackHole
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
                dominantBody.Type != OrbitalBodyType.Star
                && dominantBody.Type != OrbitalBodyType.BlackHole
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
            if (body.Type != OrbitalBodyType.Star && body.Type != OrbitalBodyType.BlackHole)
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

    private void EmitSystemGenerationCompleteViaSignalBus()
    {
        string batchId = "system_" + GetInstanceId();
        int total = _dominantTotal + _planetaryTotal + Volatile.Read(ref _satelliteTotal);
        int done =
            Volatile.Read(ref _dominantDone)
            + Volatile.Read(ref _planetaryDone)
            + Volatile.Read(ref _satelliteDone);
        SignalBus.Instance?.EmitSystemGenerationComplete(batchId, total, done);
    }

    private class BodyState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Mass;
        public float Size;
    }
}
