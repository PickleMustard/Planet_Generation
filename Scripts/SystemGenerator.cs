using Godot;
using System;
using System.Collections.Generic;
using PlanetGeneration;

public partial class SystemGenerator : Node
{
    [Export]
    public Node SystemContainer;

    override public void _Ready()
    {
        GD.Print(this.GetPath());
        var GenerateButton = GetTree().GetFirstNodeInGroup("GenerationMenu");
        ((UI.PlanetSystemGenerator)GenerateButton).GeneratePressed += GenerateMesh;
    }

     private void GenerateMesh(Godot.Collections.Array<Godot.Collections.Dictionary> bodies)
     {
         if (SystemContainer.GetChildCount() > 0)
         {
             var children = SystemContainer.GetChildren();
             foreach (Node child in children)
             {
                 child.QueueFree();
             }
         }
         foreach (Godot.Collections.Dictionary body in bodies)
         {
             var type = (String)body["Type"];
             var mass = (float)body["Mass"];
             var velocity = (Vector3)body["Velocity"];
             var position = (Vector3)body["Position"];
             var size = (int)body["Size"];

             // Handle special body types that consist of multiple satellites
             if (type == "AsteroidBelt" || type == "Comet" || type == "IceBelt")
             {
                 GenerateSatelliteGroup(type, mass, velocity, position, size, body);
             }
             else
             {
                 var mesh = new CelestialBodyMesh();
                 CelestialBody celBody = new CelestialBody(type, mass, velocity, size, mesh);
                 SystemContainer.AddChild(celBody);
                 celBody.Position = position;
                 celBody.GenerateMesh();
             }
         }
     }

     private void GenerateSatelliteGroup(string type, float mass, Vector3 velocity, Vector3 position, int size, Godot.Collections.Dictionary bodyParams)
     {
         // Create a parent CelestialBody with no mass to act as the center
         var parentMesh = new CelestialBodyMesh();
         CelestialBody parentBody = new CelestialBody(type, 0.0f, velocity, size, parentMesh); // No mass
         parentBody.Position = position;
         SystemContainer.AddChild(parentBody);
         parentBody.GenerateMesh();

         // For simplicity, create a few satellites around the parent
         int numSatellites = 5;
         float radius = size * 2.0f; // Orbital radius based on size

         for (int i = 0; i < numSatellites; i++)
         {
             float angle = (i * 2 * Mathf.Pi) / numSatellites;
             Vector3 satellitePosition = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0);

             // Calculate orbital velocity
             float orbitalSpeed = Mathf.Sqrt(OrbitalMath.GRAVITATIONAL_CONSTANT * mass / radius);
             Vector3 satelliteVelocity = new Vector3(-orbitalSpeed * Mathf.Sin(angle), orbitalSpeed * Mathf.Cos(angle), 0);

             var mesh = new CelestialBodyMesh();
             SatelliteBody satellite = new SatelliteBody("Asteroid", 0.1f, satelliteVelocity, mesh);
             satellite.Position = satellitePosition;
             parentBody.AddChild(satellite);
             satellite.GenerateMesh();
         }
     }

    public bool CheckGravitationalStability(Godot.Collections.Array<Godot.Collections.Dictionary> bodies, float simulationTime = 1000000.0f, int steps = 1000000)
    {
        var bodyStates = new List<BodyState>();
        foreach (var body in bodies)
        {
            var position = (Vector3)body["Position"];
            var velocity = (Vector3)body["Velocity"];
            var mass = (float)body["Mass"];
            var size = (float)body["Size"];
            GD.Print($"Body: {body["Type"]} Position: {position} Velocity: {velocity} Mass: {mass} Size: {size}");

            bodyStates.Add(new BodyState { Position = position, Velocity = velocity, Mass = mass, Size = size });
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
                            var forceMagnitude = OrbitalMath.GRAVITATIONAL_CONSTANT * bodyStates[i].Mass * bodyStates[j].Mass / (distance * distance);
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
                        var distSqr = (bodyStates[x].Position - bodyStates[y].Position).LengthSquared();
                        var instersectionLensVolume = (Mathf.Pi * Mathf.Pow(bodyStates[x].Size + bodyStates[y].Size - distance, 2) *
                                (distSqr + 2f * distance * bodyStates[y].Size - 3f * Mathf.Pow(bodyStates[y].Size, 2) + 2f *
                                 distance * bodyStates[x].Size + 6f * bodyStates[y].Size * bodyStates[x].Size - 3f * Mathf.Pow(bodyStates[x].Size, 2))) / (12f * distance);
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

    private class BodyState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Mass;
        public float Size;
    }

}
