using System;
using Godot;
using UtilityLibrary;
using MeshGeneration;
using Structures;

namespace PlanetGeneration;

public partial class SatelliteBody : Node3D
{
    Vector3 Velocity;
    float Mass;
    float Size;
    Vector3 TotalForce;
    bool isSatelliteGroup = false;
    SatelliteBodyType SatelliteType;
    SatelliteGroupTypes GroupType;
    UnifiedCelestialMesh Mesh;
    Octree<Point> Oct;
    Godot.Collections.Dictionary bodyDict;
    StructureDatabase StrDb;

    public SatelliteBody(
            CelestialBodyType parentType,
            Godot.Collections.Dictionary bodyDict,
            UnifiedCelestialMesh mesh
    )
    {
        this.bodyDict = bodyDict;
        var type = (String)bodyDict["type"];
        var baseTemplates = (Godot.Collections.Dictionary)bodyDict["template"];
        var mass = (float)baseTemplates["mass"];
        var velocity = (Vector3)baseTemplates["satellite_velocity"];
        var size = (int)baseTemplates["size"];
        var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
        Oct = new Octree<Point>(new Aabb(Vector3.Zero, new Vector3(size, size, size)));

        this.SatelliteType = (SatelliteBodyType)Enum.Parse(typeof(SatelliteBodyType), type);
        this.Mass = mass;
        this.Velocity = velocity;
        this.Mesh = mesh;
        mesh.size = size;
        this.AddChild(mesh);

        switch (SatelliteType)
        {
            case SatelliteBodyType.Asteroid:
                break;
        }
    }

    public SatelliteBody(CelestialBodyType parentType, String satType, float mass, float size, Vector3 velocity, UnifiedCelestialMesh mesh)
    {
        this.bodyDict = null;
        this.Mesh = mesh;
        this.Size = size;
        this.AddChild(mesh);
        this.SatelliteType = (SatelliteBodyType)Enum.Parse(typeof(SatelliteBodyType), satType);
        this.Mass = mass;
        this.Velocity = velocity;
        var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
        Oct = new Octree<Point>(new Aabb(Vector3.Zero, new Vector3(size, size, size)));
    }

    public override void _PhysicsProcess(double delta)
    {
        TotalForce = new Vector3(0.0f, 0.0f, 0.0f);
        var parent = GetParent() as CelestialBody;
        float distance = this.GlobalPosition.DistanceTo(parent.GlobalPosition);
        Vector3 direction = (parent.GlobalPosition - this.GlobalPosition);

        float force = OrbitalMath.GRAVITATIONAL_CONSTANT * parent.Mass / (distance * distance);
        TotalForce += direction.Normalized() * force;

        var deltaV = (TotalForce / Mass) * (float)delta;
        Velocity += deltaV;
        GlobalPosition += Velocity * (float)delta;
    }

    public void GenerateMesh()
    {
        Godot.Collections.Dictionary meshParams = new Godot.Collections.Dictionary();
        // Check if custom mesh data is available in the body dictionary
        if (
            bodyDict != null
        )
        {
            meshParams.Add("Type", bodyDict["type"]);
            meshParams.Add("name", bodyDict["name"]);
            if (bodyDict.ContainsKey("base_mesh") && bodyDict["base_mesh"].Obj is Godot.Collections.Dictionary customMesh)
            {
                CalculateBaseMeshFromParams(customMesh, meshParams);
            }
            if (bodyDict.ContainsKey("scaling_settings") && bodyDict["scaling_settings"].Obj is Godot.Collections.Dictionary scaling)
            {
                CalculateScalingFromParams(scaling, meshParams);
            }
            if (bodyDict.ContainsKey("noise_settings") && bodyDict["noise_settings"].Obj is Godot.Collections.Dictionary noise)
            {
                CalculateNoiseSettingsFromParams(noise, meshParams);
            }
        }
        else
        {
            var t = SystemGenTemplates.GetSatelliteBodyDefaults(SatelliteType);
            var name = PickName((Godot.Collections.Dictionary)t["possible_names"]);
            meshParams.Add("name", name);
            meshParams.Add("type", Enum.GetName(typeof(SatelliteBodyType), SatelliteType));
            var template = (Godot.Collections.Dictionary)t["template"];
            var position = (Vector3)template["position"];
            var velocity = (Vector3)template["velocity"];
            meshParams.Add("position", position);
            meshParams.Add("velocity", velocity);
            var size = (int)template["size"];
            var mass = (float)template["mass"];
            meshParams.Add("size", size);
            meshParams.Add("mass", mass);
            if (t.ContainsKey("base_mesh") && t["base_mesh"].Obj is Godot.Collections.Dictionary customMesh)
            {
                CalculateBaseMeshFromParams(customMesh, meshParams);
            }
            if (t.ContainsKey("scaling_settings") && t["scaling_settings"].Obj is Godot.Collections.Dictionary scaling)
            {
                CalculateScalingFromParams(scaling, meshParams);
            }
            if (t.ContainsKey("noise_settings") && t["noise_settings"].Obj is Godot.Collections.Dictionary noise)
            {
                CalculateNoiseSettingsFromParams(noise, meshParams);
            }
        }
        this.CallDeferred("set_name", (String)meshParams["name"]);
        if (Mass > 0)
        {
            meshParams["mass"] = Mass;
        }
        if (Size > 0)
        {
            meshParams["size"] = Size;
        }
        Mesh.ConfigureFrom(StrDb, meshParams);
        Mesh.GenerateMesh(Oct);
    }

    public String PickName(Godot.Collections.Dictionary nameDict)
    {
        if (nameDict == null || nameDict.Count == 0)
            return "";

        var categories = new Godot.Collections.Array(nameDict.Keys);
        if (categories.Count == 0)
            return "";

        var random = UtilityLibrary.Randomizer.rng;
        var selectedCategory = (string)categories[random.RandiRange(0, categories.Count - 1)];

        var names = (Godot.Collections.Array)nameDict[selectedCategory];
        if (names == null || names.Count == 0)
            return "";

        return (string)names[random.RandiRange(0, names.Count - 1)];
    }

    private void CalculateBaseMeshFromParams(Godot.Collections.Dictionary definedMesh, Godot.Collections.Dictionary meshParams)
    {
        meshParams.Add("subdivisions", (int)definedMesh["subdivisions"]);
        var vpeArray = (Godot.Collections.Array<Godot.Collections.Array<int>>)definedMesh["vertices_per_edge"];
        int[] vertices_per_edge = new int[(int)definedMesh["subdivisions"]];
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        GD.Print($"VPE Array: {vpeArray}");
        for (int i = 0; i < vertices_per_edge.Length; i++)
        {
            if (vpeArray.Count - 1 > i)//Defined subdivisions
            {
                vertices_per_edge[i] = rng.RandiRange(vpeArray[i][0], vpeArray[i][1]);
            }
            else
            {
                vertices_per_edge[i] = rng.RandiRange(vpeArray[vpeArray.Count - 1][0], vpeArray[vpeArray.Count - 1][1]);
            }
        }
        meshParams.Add("vertices_per_edge", vertices_per_edge);
        meshParams.Add("num_abberations", (int)definedMesh["num_abberations"]);
        meshParams.Add("num_deformation_cycles", (int)definedMesh["num_deformation_cycles"]);
    }

    private void CalculateScalingFromParams(Godot.Collections.Dictionary definedScaling, Godot.Collections.Dictionary meshParams)
    {
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        var scalingDict = new Godot.Collections.Dictionary();
        float[] xScaleRange = (float[])definedScaling["x_scale_range"];
        scalingDict.Add("scaling_range_x", rng.RandfRange(xScaleRange[0], xScaleRange[1]));
        float[] yScaleRange = (float[])definedScaling["y_scale_range"];
        scalingDict.Add("scaling_range_y", rng.RandfRange(yScaleRange[0], yScaleRange[1]));
        float[] zScaleRange = (float[])definedScaling["z_scale_range"];
        scalingDict.Add("scaling_range_z", rng.RandfRange(zScaleRange[0], zScaleRange[1]));
        meshParams.Add("scaling_settings", scalingDict);
    }

    private void CalculateNoiseSettingsFromParams(Godot.Collections.Dictionary definedNoise, Godot.Collections.Dictionary meshParams)
    {
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        var noiseDict = new Godot.Collections.Dictionary();
        float[] amplitude = (float[])definedNoise["amplitude_range"];
        noiseDict.Add("amplitude", rng.RandfRange(amplitude[0], amplitude[1]));
        float[] scaling = (float[])definedNoise["scaling_range"];
        noiseDict.Add("scaling", rng.RandfRange(scaling[0], scaling[1]));
        int[] octaves = (int[])definedNoise["octave_range"];
        noiseDict.Add("octaves", rng.RandiRange(octaves[0], octaves[1]));
        meshParams.Add("noise_settings", noiseDict);
    }
}
