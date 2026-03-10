using System;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using ProceduralGeneration.MeshGeneration;

namespace Tests;

[TestSuite]
public class ThreadPoolTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void DeformationWithThreadPool()
    {
        var rand = new RandomNumberGenerator();
        rand.Randomize();

        var strDb = new StructureDatabase(0);
        var mesh = new UnifiedCelestialMesh();
        mesh.Name = "TestPlanet";

        var baseMesh = new BaseMeshGeneration(rand, strDb, 1, new int[] { 1 }, mesh);

        baseMesh.PopulateArrays();
        baseMesh.GenerateNonDeformedFaces();
        baseMesh.GenerateTriangleList();

        AssertThat(strDb.BaseVertices.Count).IsGreater(0);
        AssertThat(strDb.BaseTris.Count).IsGreater(0);

        baseMesh.InitiateDeformation(5, 100, 10.0f);

        AssertThat(strDb.BaseVertices.Count).IsGreater(0);
    }
}
