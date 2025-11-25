using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using PlanetGeneration;
using Structures.Enums;
using ProceduralGeneration.MeshGeneration;
using UtilityLibrary;

namespace ProceduralGeneration.PlanetGeneration;

public partial class SystemGenerator : Node
{
    [Export]
    public Node SystemContainer;

    [ExportCategory("Thread Pool Settings")]
    [Export] public int MaxConcurrentThreads = -1; // -1 = auto-detect
    [Export] public bool EnableThreading = true;
    [Export] public int MemoryThresholdMB = 1024; // Memory threshold for thread pool activation
    [Export] public bool ShowProgressUI = true;

    // Progress tracking
    private int totalBodiesToGenerate = 0;
    private int bodiesCompleted = 0;

    public override void _Ready()
    {
        GD.Print(this.GetPath());
        var GenerateButton = GetTree().GetFirstNodeInGroup("GenerationMenu");
        ((UI.PlanetSystemGenerator)GenerateButton).GeneratePressed += GenerateMesh;

        // Initialize thread pool
        if (EnableThreading && MeshGenerationThreadPool.Instance == null)
        {
            var threadPool = new MeshGenerationThreadPool();
            AddChild(threadPool);
            threadPool.Initialize();
            GD.Print("Thread pool initialized");
        }
    }

    private async void GenerateMesh(Godot.Collections.Array<Godot.Collections.Dictionary> bodies)
    {
        // Clear existing bodies
        if (SystemContainer.GetChildCount() > 0)
        {
            var children = SystemContainer.GetChildren();
            foreach (Node child in children)
            {
                child.RemoveFromGroup("CelestialBody");
                child.QueueFree();
            }
        }

        // Reset progress tracking
        totalBodiesToGenerate = bodies.Count;
        bodiesCompleted = 0;

        // Queue all celestial body generation tasks
        var generationTasks = new List<Task>();
        GD.Print($"Generating System: {bodies}");

        foreach (Godot.Collections.Dictionary body in bodies)
        {
            var task = GenerateCelestialBodyAsync(body);
            generationTasks.Add(task);
        }

        // Wait for all bodies to complete generation
        await Task.WhenAll(generationTasks);

        // Final progress update
        if (ShowProgressUI)
        {
            GD.Print($"System generation complete: {bodiesCompleted}/{totalBodiesToGenerate} bodies generated");
        }
    }

    private async Task GenerateCelestialBodyAsync(Godot.Collections.Dictionary body)
    {
        var mesh = new UnifiedCelestialMesh();
        CelestialBody celBody = CelestialBody.Builder.BuildFromBodyDict(body, mesh);
        SystemContainer.AddChild(celBody);
        celBody.Position = (Vector3)((Godot.Collections.Dictionary)body["template"])["position"];

        if (EnableThreading && MeshGenerationThreadPool.Instance != null)
        {
            // Queue mesh generation as high priority
            await MeshGenerationThreadPool.Instance.EnqueueTask(
                () => { celBody.GenerateMesh(); },
                celBody.Name,
                TaskPriority.High,
                celBody.Name
            );
        }
        else
        {
            // Fallback to synchronous generation
            celBody.GenerateMesh();
        }

        // Handle satellites if present
        if (body.ContainsKey("satellites") && body["satellites"].Obj is Godot.Collections.Array satellites)
        {
            await GenerateSatellitesAsync(celBody, satellites);
        }

        // Update progress
        bodiesCompleted++;
        if (ShowProgressUI)
        {
            GD.Print($"Generated {bodiesCompleted}/{totalBodiesToGenerate} bodies ({(float)bodiesCompleted / totalBodiesToGenerate * 100:F1}%)");
        }
    }

    private async Task GenerateSatellitesAsync(CelestialBody parentBody, Godot.Collections.Array satellites)
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

    private void GenerateSatellites(CelestialBody parentBody, Godot.Collections.Array satellites)
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
                var type = sat["Type"];
                var templateDict = (Godot.Collections.Dictionary)sat["Template"];
                var position = templateDict["BasePosition"];
                var velocity = templateDict["SatelliteVelocity"];
                var mass = templateDict["Mass"];
                var size = templateDict["Size"];
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
            Godot.Collections.Dictionary templateDict = (Godot.Collections.Dictionary)body["template"];
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

    private async void GenerateSingleSatellite(Godot.Collections.Dictionary sat, CelestialBody parentBody)
    {
        var templateDict = (Godot.Collections.Dictionary)sat["template"];
        var position = (Vector3)templateDict["base_position"];
        var mesh = new UnifiedCelestialMesh();
        SatelliteBody satBody = SatelliteBody.Builder.BuildFromBodyDict(parentBody.Type, sat, mesh);
        parentBody.AddChild(satBody);
        satBody.Position = position;
        if (EnableThreading && MeshGenerationThreadPool.Instance != null)
        {
            await MeshGenerationThreadPool.Instance.EnqueueTask(
                () => { satBody.GenerateMesh(); },
                satBody.Name,
                TaskPriority.Medium,
                satBody.Name
            );
            GD.Print($"Enqueued {satBody.Name}");
        }
        else
        {
            satBody.GenerateMesh();
        }

    }

    private async void GenerateSatelliteBelt(Godot.Collections.Dictionary satBelt, CelestialBody parentBody)
    {
        SatelliteBeltBody beltBody = SatelliteBeltBody.Builder.BuildFromBodyDict(parentBody.Type, satBelt);
        string name = $"{parentBody.Name}_{beltBody.GroupType}";
        var sats = beltBody.GenerateSatelliteBelt(parentBody);
        if (EnableThreading && MeshGenerationThreadPool.Instance != null)
        {
            foreach (var sat in sats)
            {
                GD.Print($"Enqueued {sat.Name}, Position: {sat.Position}");
                parentBody.AddChild(sat);
                sat.Position = sat.Position;
                await MeshGenerationThreadPool.Instance.EnqueueTask(() =>
                {
                    sat.GenerateMesh();
                },
                name,
                TaskPriority.Medium,
                name);
            }
        }
    }

    private class BodyState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Mass;
        public float Size;
    }
}
