using System;
using System.Collections.Generic;
using Godot;
using PlanetGeneration;

public partial class SystemGenerator : Node
{
    [Export]
    public Node SystemContainer;

    public override void _Ready()
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
            var mesh = new CelestialBodyMesh();
            CelestialBody celBody = new CelestialBody(body, mesh);
            SystemContainer.AddChild(celBody);
            celBody.Position = (Vector3)
                ((Godot.Collections.Dictionary)body["Template"])["position"];
            celBody.GenerateMesh();

            // Generate satellites if present
            if (
                body.ContainsKey("Satellites")
                && body["Satellites"].Obj is Godot.Collections.Array satellites
            )
            {
                GD.Print("Generating Satellites");
                GenerateSatellites(celBody, satellites);
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
                Enum.TryParse<SatelliteGroupTypes>((String)satBelt["Type"], out var beltType);

                Vector3 positionMin = (Vector3)satBelt["PositionMin"];
                Vector3 positionMax = (Vector3)satBelt["PositionMax"];
                Vector3 velocityMin = (Vector3)satBelt["VelocityMin"];
                Vector3 velocityMax = (Vector3)satBelt["VelocityMax"];
                float sizeMin = (float)satBelt["SizeMin"];
                float sizeMax = (float)satBelt["SizeMax"];
                float massMin = (float)satBelt["MassMin"];
                float massMax = (float)satBelt["MassMax"];
                int upperRange = (int)satBelt["UpperRange"];
                int lowerRange = (int)satBelt["LowerRange"];
                int beltNumber = GD.RandRange(lowerRange, upperRange); // Generate 2-5 satellites per table item for stars and black holes
                var rng = new RandomNumberGenerator();

                for (int i = 0; i < beltNumber; i++)
                {
                    var type = beltType switch
                    {
                        SatelliteGroupTypes.AsteroidBelt => "Asteroid",
                        SatelliteGroupTypes.Comet => "Comet",
                        SatelliteGroupTypes.IceBelt => "Comet",
                        _ => "Asteroid",
                    };
                    var size = rng.RandfRange(sizeMin, sizeMax);
                    var mass = rng.RandfRange(massMin, massMax);
                    var position = new Vector3(
                        rng.RandfRange(positionMin.X, positionMax.X),
                        rng.RandfRange(positionMin.Y, positionMax.Y),
                        rng.RandfRange(positionMin.Z, positionMax.Z)
                    );
                    var velocity = new Vector3(
                        rng.RandfRange(velocityMin.X, velocityMax.X),
                        rng.RandfRange(velocityMin.Y, velocityMax.Y),
                        rng.RandfRange(velocityMin.Z, velocityMax.Z)
                    );

                    var mesh = new CelestialBodyMesh();
                    SatelliteBody satBody = new SatelliteBody(
                        parentBody.Type,
                        type,
                        mass,
                        velocity,
                        mesh
                    );
                    parentBody.AddChild(satBody);
                    satBody.Position = position;
                    satBody.GenerateMesh();
                }
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
            var position = (Vector3)body["Position"];
            var velocity = (Vector3)body["Velocity"];
            var mass = (float)body["Mass"];
            var size = (float)body["Size"];
            GD.Print(
                $"Body: {body["Type"]} Position: {position} Velocity: {velocity} Mass: {mass} Size: {size}"
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

    private class BodyState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Mass;
        public float Size;
    }
}
