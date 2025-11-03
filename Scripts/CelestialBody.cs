using System;
using Godot;
using UtilityLibrary;
using Structures;
using MeshGeneration;

namespace PlanetGeneration;

///<class>CelestialBody</class>
///<summary>A CelestialBody is a single point in space that has a mass and velocity.
///It has mass and gravity that will be used to calculate the attrational force on other objects.
///Its position can be modified by the forces acting upon it</summary>
public partial class CelestialBody : Node3D
{
    public Vector3 Velocity;
    public float Mass;
    public Vector3 TotalForce;
    public CelestialBodyType Type;
    public UnifiedCelestialMesh Mesh;
    public Octree<Point> Oct;
    private Godot.Collections.Dictionary bodyDict;
    private StructureDatabase StrDb;

    public CelestialBody(Godot.Collections.Dictionary bodyDict, UnifiedCelestialMesh mesh)
    {
        GD.Print($"BodyDict: {bodyDict}");
        this.bodyDict = bodyDict;
        var baseTemplates = (Godot.Collections.Dictionary)bodyDict["template"];
        var type = (String)bodyDict["type"];
        var mass = (float)baseTemplates["mass"];
        var velocity = (Vector3)baseTemplates["velocity"];
        var size = (int)baseTemplates["size"];
        Vector3 aabbSize = new Vector3(size, size, size) * 1.2f;
        Vector3 aabbBegin = Vector3.Zero - aabbSize;
        var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
        Oct = new Octree<Point>(new Aabb(aabbBegin, aabbSize * 2f));

        this.Type = (CelestialBodyType)Enum.Parse(typeof(CelestialBodyType), type);
        this.Mass = mass;
        this.Velocity = velocity;
        this.Mesh = mesh;
        mesh.size = size;
        this.AddChild(mesh);

        switch (Type)
        {
            case CelestialBodyType.Star:
                //Add a omnidirectional light source
                OmniLight3D emision = new OmniLight3D();
                emision.OmniRange = 4096f;
                emision.OmniAttenuation = .14f;
                this.AddChild(emision);
                break;
        }
    }

    public override void _Ready()
    {
        AddToGroup("CelestialBody");
    }

    public override void _PhysicsProcess(double delta)
    {
        TotalForce = new Vector3(0.0f, 0.0f, 0.0f);
        var bodies = GetTree().GetNodesInGroup("CelestialBody");
        foreach (CelestialBody body in bodies)
        {
            if (body != this)
            {
                float distance = this.GlobalPosition.DistanceTo(body.GlobalPosition);
                Vector3 direction = (body.GlobalPosition - this.GlobalPosition);

                float force =
                    OrbitalMath.GRAVITATIONAL_CONSTANT * Mass * body.Mass / (distance * distance);
                TotalForce += direction.Normalized() * force;
            }
        }

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
            meshParams.Add("type", bodyDict["type"]);
            meshParams.Add("name", bodyDict["name"]);
            if (bodyDict.ContainsKey("base_mesh") && bodyDict["base_mesh"].Obj is Godot.Collections.Dictionary customMesh)
            {
                CalculateBaseMeshFromParams(customMesh, meshParams);
            }
            if (bodyDict.ContainsKey("tectonics") && bodyDict["tectonics"].Obj is Godot.Collections.Dictionary tectonics)
            {
                CalculateTectonicMeshFromParams(tectonics, meshParams);
            }
        }
        else
        {
            var t = SystemGenTemplates.GetCelestialBodyDefaults(Type);
            var name = PickName((Godot.Collections.Dictionary)t["possible_names"]);
            meshParams.Add("name", name);
            meshParams.Add("type", Enum.GetName(typeof(SatelliteBodyType), Type));
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
            if (t.ContainsKey("tectonics") && t["tectonics"].Obj is Godot.Collections.Dictionary tectonics)
            {
                CalculateTectonicMeshFromParams(tectonics, meshParams);
            }
        }
        this.CallDeferred("set_name", (String)meshParams["name"]);
        GD.Print($"Mesh Params: {meshParams}");
        Mesh.ConfigureFrom(StrDb, meshParams);
        Mesh.GenerateMesh(Oct);
        StrDb.FinalizeDB();
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

    public Point FindNearest(Vector3 position)
    {
        GD.Print($"Global Position: {this.GlobalPosition}");
        Vector3 localSpace = (position - this.GlobalPosition);
        GD.Print($"Local Space: {localSpace}");
        Point desired = new Point(localSpace, 0);
        Point result = Oct.FindNearest(desired);
        PolygonRendererSDL.DrawPoint(this, 1, result.ToVector3(), 0.05f, Colors.Red);

        var cells = StrDb.PlanetMap[result];
        Godot.Collections.Array<VoronoiCell> contains = new Godot.Collections.Array<VoronoiCell>();
        foreach (var cell in cells)
        {
            desired.Position = desired.Position.Normalized() * (Mesh.size + cell.Height);
            if (cell.BoundingBox.HasPoint(desired.Position)) contains.Add(cell);
        }
        if (contains.Count == 0)
        {
            return null;
        }
        else if (contains.Count == 1)
        {
            GD.Print($"Point is in a single cell: {contains[0]}");
        }
        else if (contains.Count > 1)
        {
            float minDist = float.MaxValue;
            VoronoiCell minCell = null;
            foreach (var cell in contains)
            {
                float dist = (cell.Center - desired.Position).LengthSquared();
                if (dist < minDist)
                {
                    minDist = dist;
                    minCell = cell;
                }
            }
            GD.Print($"Point is in multiple cells: {minCell}");
        }
        return result;
    }

    public MeshInstance3D CreateDebugWireframe(Aabb aabb)
    {
        Vector3[] corners = new Vector3[] { aabb.GetEndpoint(0), aabb.GetEndpoint(1), aabb.GetEndpoint(2), aabb.GetEndpoint(3), aabb.GetEndpoint(4), aabb.GetEndpoint(5), aabb.GetEndpoint(6), aabb.GetEndpoint(7) };
        var lineVertices = new Vector3[]
        {
            // Bottom face
            corners[0], corners[1],
            corners[1], corners[5],
            corners[5], corners[4],
            corners[4], corners[0],
            // Top face
            corners[2], corners[3],
            corners[3], corners[7],
            corners[7], corners[6],
            corners[6], corners[2],
            // Vertical edges
            corners[0], corners[2],
            corners[1], corners[3],
            corners[4], corners[6],
            corners[5], corners[7]
        };

        // 3. Create the ArrayMesh
        var mesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)ArrayMesh.ArrayType.Max);
        arrays[(int)ArrayMesh.ArrayType.Vertex] = lineVertices;

        // 4. Add the vertices as a surface with a line primitive type
        mesh.AddSurfaceFromArrays(ArrayMesh.PrimitiveType.Lines, arrays);

        // 5. Create the MeshInstance3D node
        var meshInstance = new MeshInstance3D
        {
            Mesh = mesh,
            Name = "AABB_Wireframe"
        };

        var material = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            // Use unshaded mode to make the color constant regardless of lighting
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded
        };
        meshInstance.MaterialOverride = material;

        return meshInstance;

    }

    private void CalculateTectonicMeshFromParams(Godot.Collections.Dictionary definedMesh, Godot.Collections.Dictionary meshParams)
    {
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        var tectDict = new Godot.Collections.Dictionary();
        int[] numContinents = (int[])definedMesh["num_continents"];
        tectDict.Add("num_continents", rng.RandiRange(numContinents[0], numContinents[1]));
        float[] stressScale = (float[])definedMesh["stress_scale"];
        tectDict.Add("stress_scale", rng.RandfRange(stressScale[0], stressScale[1]));
        float[] shearScale = (float[])definedMesh["shear_scale"];
        tectDict.Add("shear_scale", rng.RandfRange(shearScale[0], shearScale[1]));
        float[] maxPropagationDistance = (float[])definedMesh["max_propagation_distance"];
        tectDict.Add("max_propagation_distance", rng.RandfRange(maxPropagationDistance[0], maxPropagationDistance[1]));
        float[] propagationFalloff = (float[])definedMesh["propagation_falloff"];
        tectDict.Add("propagation_falloff", rng.RandfRange(propagationFalloff[0], propagationFalloff[1]));
        float[] inactiveStressThreshold = (float[])definedMesh["inactive_stress_threshold"];
        tectDict.Add("inactive_stress_threshold", rng.RandfRange(inactiveStressThreshold[0], inactiveStressThreshold[1]));
        float[] generalHeightScale = (float[])definedMesh["general_height_scale"];
        tectDict.Add("general_height_scale", rng.RandfRange(generalHeightScale[0], generalHeightScale[1]));
        float[] generalShearScale = (float[])definedMesh["general_shear_scale"];
        tectDict.Add("general_shear_scale", rng.RandfRange(generalShearScale[0], generalShearScale[1]));
        float[] generalCompressionScale = (float[])definedMesh["general_compression_scale"];
        tectDict.Add("general_compression_scale", rng.RandfRange(generalCompressionScale[0], generalCompressionScale[1]));
        float[] generalTransformScale = (float[])definedMesh["general_transform_scale"];
        tectDict.Add("general_transform_scale", rng.RandfRange(generalTransformScale[0], generalTransformScale[1]));
        meshParams.Add("tectonic", tectDict);
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

    private Godot.Collections.Dictionary ConvertCustomMeshToParams(
        Godot.Collections.Dictionary customMesh
    )
    {
        GD.Print($"Converting Custom Mesh: {customMesh}");
        var meshParams = new Godot.Collections.Dictionary();
        // Convert custom mesh data to the format expected by Mesh.ConfigureFrom
        if (
            customMesh.ContainsKey("base_mesh")
            && customMesh["base_mesh"].Obj is Godot.Collections.Dictionary baseMesh
        )
        {
            meshParams["subdivisions"] = baseMesh["subdivisions"];
            meshParams["num_abberations"] = baseMesh["num_abberations"];
            meshParams["num_deformation_cycles"] = baseMesh["num_deformation_cycles"];
            meshParams["vertices_per_edge"] = baseMesh["vertices_per_edge"];
        }
        if (
            customMesh.ContainsKey("tectonic")
            && customMesh["tectonic"].Obj is Godot.Collections.Dictionary tectonic
        )
        {
            meshParams["tectonic"] = tectonic;
        }
        // Add other sections if present (scaling, noise_settings, etc.)
        if (customMesh.ContainsKey("scaling"))
        {
            meshParams["scaling"] = customMesh["scaling"];
        }
        if (customMesh.ContainsKey("noise_settings"))
        {
            meshParams["noise_settings"] = customMesh["noise_settings"];
        }
        return meshParams;
    }
}
