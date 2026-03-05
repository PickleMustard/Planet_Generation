using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using PlanetGeneration;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using UtilityLibrary;
using UtilityLibrary.TaskSystem;

namespace ProceduralGeneration.PlanetGeneration;

public partial class SystemGenerator : Node
{
    [Signal]
    public delegate void SystemGenerationCompleteEventHandler();

    [Export]
    public Node SystemContainer;

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

    public override void _Ready()
    {
        GD.Print(this.GetPath());
        var GenerateButton = GetTree().GetFirstNodeInGroup("GenerationMenu");
        ((UI.PlanetSystemGenerator)GenerateButton).GeneratePressed += GenerateMesh;

        // ThreadPooler is now an autoload, no manual initialization needed
        GD.Print($"SystemGenerator ready, ThreadPooler available: {UtilityLibrary.TaskSystem.ThreadPooler.Instance != null}");
    }

    private void GenerateMesh(Godot.Collections.Array<Godot.Collections.Dictionary> bodies)
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

        // Start generation session
        totalBodiesToGenerate = bodies.Count;
        bodiesCompleted = 0;

        GD.Print($"Generating System: {bodies}");

        foreach (Godot.Collections.Dictionary body in bodies)
        {
            CreateAndQueueCelestialBody(body);
        }

        GD.Print(
            $"System generation started: {totalBodiesToGenerate} bodies queued"
        );
    }

    private void CreateAndQueueCelestialBody(Godot.Collections.Dictionary body)
    {
        var mesh = new UnifiedCelestialMesh();
        CelestialBody celBody = CelestialBody.Builder.BuildFromBodyDict(body, mesh);

        string bodyType = body["type"].AsString();
        string bodyName = $"{bodyType}_{bodiesCompleted + 1}";
        celBody.Name = bodyName;
        mesh.TimerName = bodyName;

        SystemContainer.AddChild(celBody);
        celBody.Position = (Vector3)((Godot.Collections.Dictionary)body["template"])["position"];

        celBody.StartMeshGeneration(
            onCompleted: (completedBody) => OnBodyGenerationComplete(completedBody, celBody, body),
            onFailed: (failedBody, error) => OnBodyGenerationFailed(failedBody, error, celBody)
        );
    }

    private void OnBodyGenerationComplete(CelestialBody completedBody, CelestialBody celBody, Godot.Collections.Dictionary bodyDict)
    {
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
            EmitSignal(SignalName.SystemGenerationComplete);
        }
    }

    private void OnBodyGenerationFailed(CelestialBody failedBody, string error, CelestialBody celBody)
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

    private void GenerateSingleSatellite(
        Godot.Collections.Dictionary sat,
        CelestialBody parentBody
    )
    {
        var templateDict = (Godot.Collections.Dictionary)sat["template"];
        var position = (Vector3)templateDict["base_position"];
        var mesh = new UnifiedCelestialMesh();
        SatelliteBody satBody = SatelliteBody.Builder.BuildFromBodyDict(parentBody.Type, sat, mesh);

        string satType = sat["type"].AsString();
        string satName = $"{parentBody.Name}_{satType}_{satBody.GetIndex()}";
        satBody.Name = satName;
        mesh.TimerName = satName;

        parentBody.AddChild(satBody);
        satBody.Position = position;

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
        SatelliteBeltBody beltBody = SatelliteBeltBody.Builder.BuildFromBodyDict(
            parentBody.Type,
            satBelt
        );
        var sats = beltBody.GenerateSatelliteBelt(parentBody);

        int satIndex = 0;
        foreach (var sat in sats)
        {
            string satName = $"{parentBody.Name}_Asteroid_{satIndex++}";
            sat.Name = satName;
            sat.TimerName = satName;

            GD.Print($"Generating {sat.Name}, Position: {sat.Position}");
            parentBody.AddChild(sat);
            sat.Position = sat.Position;

            sat.StartMeshGeneration(
                onCompleted: (completedSat) =>
                {
                    GD.Print($"Generated satellite belt body: {completedSat.Name}");
                },
                onFailed: (failedSat, error) =>
                {
                    GD.PrintErr($"Satellite belt body generation failed: {failedSat.Name}, error: {error}");
                    failedSat.QueueFree();
                }
            );
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
