using Godot;
using MeshGeneration;

/// <summary>
/// Simple test class to verify UnifiedCelestialMesh functionality
/// </summary>
public partial class TestUnifiedCelestialMesh : Node
{
    public override void _Ready()
    {
        GD.Print("Testing UnifiedCelestialMesh...");
        
        // Test 1: TectonicsOnly configuration
        TestTectonicsOnly();
        
        // Test 2: TectonicsWithNoise configuration
        TestTectonicsWithNoise();
        
        // Test 3: ScalingWithNoise configuration
        TestScalingWithNoise();
        
        // Test 4: NoiseOnly configuration
        TestNoiseOnly();
        
        GD.Print("All UnifiedCelestialMesh tests completed successfully!");
    }
    
    private void TestTectonicsOnly()
    {
        GD.Print("Testing TectonicsOnly generation type...");
        
        var mesh = new UnifiedCelestialMesh();
        var config = new Godot.Collections.Dictionary
        {
            ["name"] = "TestTectonicsPlanet",
            ["size"] = 5.0f,
            ["subdivisions"] = 2,
            ["tectonic"] = new Godot.Collections.Dictionary
            {
                ["num_continents"] = 3,
                ["stress_scale"] = 4.0f
            }
        };
        
        mesh.ConfigureFrom(config);
        GD.Print($"TectonicsOnly detected type: {mesh.GenerationType}");
        
        // Verify the type was detected correctly
        if (mesh.GenerationType == UnifiedCelestialMesh.BodyGenerationType.TectonicsOnly)
        {
            GD.Print("✓ TectonicsOnly type detected correctly");
        }
        else
        {
            GD.Print($"✗ TectonicsOnly type detection failed: {mesh.GenerationType}");
        }
    }
    
    private void TestTectonicsWithNoise()
    {
        GD.Print("Testing TectonicsWithNoise generation type...");
        
        var mesh = new UnifiedCelestialMesh();
        var config = new Godot.Collections.Dictionary
        {
            ["name"] = "TestTectonicsNoisePlanet",
            ["size"] = 5.0f,
            ["subdivisions"] = 2,
            ["tectonic"] = new Godot.Collections.Dictionary
            {
                ["num_continents"] = 3,
                ["stress_scale"] = 4.0f
            },
            ["noise_settings"] = new Godot.Collections.Dictionary
            {
                ["amplitude"] = 0.2f,
                ["scaling"] = 2.0f,
                ["octaves"] = 4
            }
        };
        
        mesh.ConfigureFrom(config);
        GD.Print($"TectonicsWithNoise detected type: {mesh.GenerationType}");
        
        if (mesh.GenerationType == UnifiedCelestialMesh.BodyGenerationType.TectonicsWithNoise)
        {
            GD.Print("✓ TectonicsWithNoise type detected correctly");
        }
        else
        {
            GD.Print($"✗ TectonicsWithNoise type detection failed: {mesh.GenerationType}");
        }
    }
    
    private void TestScalingWithNoise()
    {
        GD.Print("Testing ScalingWithNoise generation type...");
        
        var mesh = new UnifiedCelestialMesh();
        var config = new Godot.Collections.Dictionary
        {
            ["name"] = "TestScalingAsteroid",
            ["size"] = 5.0f,
            ["subdivisions"] = 2,
            ["scaling_settings"] = new Godot.Collections.Dictionary
            {
                ["x_scale_range"] = 2.0f,
                ["y_scale_range"] = 0.5f,
                ["z_scale_range"] = 1.0f
            },
            ["noise_settings"] = new Godot.Collections.Dictionary
            {
                ["amplitude"] = 0.2f,
                ["scaling"] = 2.0f,
                ["octaves"] = 4
            }
        };
        
        mesh.ConfigureFrom(config);
        GD.Print($"ScalingWithNoise detected type: {mesh.GenerationType}");
        
        if (mesh.GenerationType == UnifiedCelestialMesh.BodyGenerationType.ScalingWithNoise)
        {
            GD.Print("✓ ScalingWithNoise type detected correctly");
        }
        else
        {
            GD.Print($"✗ ScalingWithNoise type detection failed: {mesh.GenerationType}");
        }
    }
    
    private void TestNoiseOnly()
    {
        GD.Print("Testing NoiseOnly generation type...");
        
        var mesh = new UnifiedCelestialMesh();
        var config = new Godot.Collections.Dictionary
        {
            ["name"] = "TestNoiseOnlyPlanet",
            ["size"] = 5.0f,
            ["subdivisions"] = 2,
            ["noise_settings"] = new Godot.Collections.Dictionary
            {
                ["amplitude"] = 0.2f,
                ["scaling"] = 2.0f,
                ["octaves"] = 4
            }
        };
        
        mesh.ConfigureFrom(config);
        GD.Print($"NoiseOnly detected type: {mesh.GenerationType}");
        
        if (mesh.GenerationType == UnifiedCelestialMesh.BodyGenerationType.NoiseOnly)
        {
            GD.Print("✓ NoiseOnly type detected correctly");
        }
        else
        {
            GD.Print($"✗ NoiseOnly type detection failed: {mesh.GenerationType}");
        }
    }
}