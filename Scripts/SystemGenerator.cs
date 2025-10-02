using Godot;
using System;
using PlanetGeneration;

public partial class SystemGenerator : Node
{
    private Node root;

    override public void _Ready()
    {
        root = GetNode("..");
        var GenerateButton = GetTree().GetFirstNodeInGroup("GenerationMenu");
        ((UI.PlanetSystemGenerator)GenerateButton).GeneratePressed += GenerateMesh;
    }

    private void GenerateMesh(Godot.Collections.Array<Godot.Collections.Dictionary> bodies)
    {
        foreach (Godot.Collections.Dictionary body in bodies)
        {
            var type = (String)body["Type"];
            var mass = (float)body["Mass"];
            var velocity = (Vector3)body["Velocity"];
            var position = (Vector3)body["Position"];
            var mesh = new CelestialBodyMesh();
            CelestialBody celBody = new CelestialBody(type, mass, velocity, mesh);
            root.AddChild(celBody);
            celBody.Position = position;
            celBody.GenerateMesh();
        }
    }

}
