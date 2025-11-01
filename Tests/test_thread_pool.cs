using System;
using System.Threading.Tasks;
using Godot;
using MeshGeneration;
using PlanetGeneration;
using Structures;

public partial class TestThreadPool : Node
{
    public override void _Ready()
    {
        // Test the thread pool integration
        TestDeformationWithThreadPool();
    }
    
    private async void TestDeformationWithThreadPool()
    {
        GD.Print("=== Testing Thread Pool Integration ===");
        
        try
        {
            // Create test data
            var rand = new RandomNumberGenerator();
            rand.Randomize();
            
            var strDb = new StructureDatabase(0);
            var mesh = new UnifiedCelestialMesh();
            mesh.Name = "TestPlanet";
            
            // Create BaseMeshGeneration instance
            var baseMesh = new BaseMeshGeneration(rand, strDb, 1, new int[] { 1 }, mesh);
            
            // Initialize basic mesh structure
            baseMesh.PopulateArrays();
            baseMesh.GenerateNonDeformedFaces();
            baseMesh.GenerateTriangleList();
            
            GD.Print("Base mesh created successfully");
            GD.Print($"Vertices: {strDb.BaseVertices.Count}");
            GD.Print($"Triangles: {strDb.BaseTris.Count}");
            
            // Test deformation with thread pool
            GD.Print("Starting deformation with thread pool...");
            var startTime = DateTime.Now;
            
            await baseMesh.InitiateDeformation(5, 100, 10.0f);
            
            var endTime = DateTime.Now;
            var duration = (endTime - startTime).TotalSeconds;
            
            GD.Print($"Deformation completed in {duration:F2} seconds");
            GD.Print("=== Thread Pool Test Completed Successfully ===");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Thread pool test failed: {ex.Message}");
            GD.PrintErr($"Stack trace: {ex.StackTrace}");
        }
        
        // Clean up
        GetTree().Quit();
    }
}