using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Constructables.ArtificialSatellites;
using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using Structures.GameState;
using Structures.MeshGeneration;
using UtilityLibrary;
#if DEBUG
using UI.Debug;
using UI.Debug.Console;
using UI.Debug.DatabaseViewer;
#endif

namespace ProceduralGeneration.PlanetGeneration;

///<class>CelestialBody</class>
///<summary>A CelestialBody is a single point in space that has a mass and velocity.
///It has mass and gravity that will be used to calculate the attrational force on other objects.
///Its position can be modified by the forces acting upon it</summary>
#if DEBUG
[DebugData("CelestialBody", Category = "Game")]
#endif
[GlobalClass]
public partial class CelestialBody : Node3D, IOrbitalBody
#if DEBUG
        , IDebugDataProvider
#endif
{
    private float _mass;
    private float _radius;

    public static Barycenter barycenter = new Barycenter(Vector3.Zero, Vector3.Zero, 0f);

    public Builder builder()
    {
        return new Builder();
    }

    [Export]
    public Vector3 Velocity;

    [Export]
    public float Mass
    {
        get => _mass;
        set => _mass = value;
    }

    [Export]
    public float Radius
    {
        get => _radius;
        set => _radius = value;
    }

    [Export]
    public Vector3 TotalForce;
    private Vector3 _savedForce;
    public CelestialBodyType Type;
    public RockyPlanetType? RockyType;
    public UnifiedCelestialMesh? Mesh;
    public Octree<Point> Oct;
    private Godot.Collections.Dictionary? bodyDict;
    private StructureDatabase StrDb;

    // Orbit System
    [Export]
    public OrbitConfiguration? OrbitConfig { get; private set; }

    [Export]
    public Godot.Collections.Array<OrbitBand> OrbitBands { get; private set; } = new();

    [Export]
    public Node3D SatellitesContainer { get; private set; } = null!;
    private Godot.Collections.Dictionary<int, int> _bandSatelliteCounts = new();

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

    private CelestialBody(Builder builder)
    {
        this.Velocity = builder._velocity ?? Vector3.Zero;
        this.Mass = builder._mass ?? 0f;
        this.Type = builder._type ?? CelestialBodyType.RockyPlanet;
        this.RockyType = builder._rockyType;
        this.Mesh = builder._mesh;
        this.bodyDict = builder._bodyDict;
        this.TotalForce = Vector3.Zero;
        this.Name = builder._name ?? "";

        if (this.Mesh != null)
        {
            this.AddChild(this.Mesh);
        }

        if (this.bodyDict != null)
        {
            var baseTemplates = (Godot.Collections.Dictionary)this.bodyDict["template"];
            var size = (int)baseTemplates["size"];
            Vector3 aabbSize = new Vector3(size, size, size) * 1.2f;
            Vector3 aabbBegin = Vector3.Zero - aabbSize;
            var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
            StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
            Oct = new Octree<Point>(new Aabb(aabbBegin, aabbSize * 2f));

            if (this.Mesh != null)
            {
                this.Mesh.size = size;
            }
        }
        else
        {
            var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
            StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
            Oct = new Octree<Point>(new Aabb(Vector3.One * -10, Vector3.One * 20));
        }

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
        barycenter.RegisterBody();
    }

    /// <summary>
    /// Initializes the orbit system based on the body's mass.
    /// Creates orbit bands and sets up the satellites container.
    /// </summary>
    public void InitializeOrbitSystem()
    {
        // Calculate body radius from scale (assuming sphere)
        float bodyRadius = Mesh!.size;

        // Create orbit configuration from mass
        OrbitConfig = OrbitConfiguration.CreateFromMass(Mass, bodyRadius);

        // Create all orbit bands
        OrbitBands = OrbitConfig.CreateAllOrbitBands(bodyRadius);

        // Initialize satellite counts for each band
        _bandSatelliteCounts.Clear();
        for (int i = 0; i < OrbitBands.Count; i++)
        {
            _bandSatelliteCounts[i] = 0;
        }

        // Create the satellites container
        SatellitesContainer = new Node3D { Name = "SatellitesContainer" };
        CallDeferred("add_child", SatellitesContainer);

        GameLogger.Debug($"OrbitSystem initialized: {OrbitBands.Count} bands for mass {Mass}");
    }

    /// <summary>
    /// Creates a station satellite in the specified orbit band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band (0-based)</param>
    /// <param name="name">Name for the station</param>
    /// <returns>The created station satellite, or null if invalid band</returns>
    public StationSatellite? CreateStation(int bandIndex, string name)
    {
        if (!CanAddToBand(bandIndex))
        {
            GameLogger.Warning($"Cannot add station to band {bandIndex}: band is full or invalid");
            return null;
        }

        var station = new StationSatellite { Name = name };

        SatellitesContainer.AddChild(station);
        station.Initialize(this, bandIndex);

        _bandSatelliteCounts[bandIndex]++;

        GameLogger.Debug($"Created station '{name}' in band {bandIndex}");
        return station;
    }

    /// <summary>
    /// Creates a ship in a random valid orbit band.
    /// </summary>
    /// <param name="name">Name for the ship</param>
    /// <returns>The created ship satellite, or null if no valid band available</returns>
    public StationSatellite? CreateShip(string name)
    {
        // Find a band with available capacity
        for (int i = 0; i < OrbitBands.Count; i++)
        {
            if (CanAddToBand(i))
            {
                return CreateStation(i, name);
            }
        }

        GameLogger.Warning($"Cannot create ship '{name}': no available bands");
        return null;
    }

#if DEBUG
    /// <summary>
    /// Debug command to spawn a station satellite in an orbit band.
    /// </summary>
    /// <param name="ctx">Command context for console output</param>
    /// <param name="args">Arguments: [0] = band index, [1] = optional station name</param>
    /// <returns>0 on success, 1 on failure</returns>
    [DebugCommand(
        "spawn_station",
        "Spawn a station satellite in an orbit band",
        "spawn_station <band_index> [name]",
        Category = "Modification",
        RequiresTarget = true
    )]
    public int SpawnStationCommand(CommandContext ctx, string[] args)
    {
        if (args.Length < 1)
        {
            ctx.WriteError("Usage: spawn_station <band_index> [name]");
            return 1;
        }

        if (!int.TryParse(args[0], out var bandIndex))
        {
            ctx.WriteError($"Invalid band index: '{args[0]}'. Must be an integer.");
            return 1;
        }

        var bandCount = GetBandCount();
        if (bandCount == 0)
        {
            ctx.WriteError("This body has no orbit bands configured.");
            return 1;
        }

        if (bandIndex < 0)
        {
            ctx.WriteError($"Invalid band index: {bandIndex}. Valid range: 0-{bandCount - 1}");
            return 1;
        }

        if (bandIndex >= bandCount)
        {
            ctx.WriteError(
                $"Band index {bandIndex} out of range. Available bands: {bandCount} (0-{bandCount - 1})"
            );
            return 1;
        }

        if (!CanAddToBand(bandIndex))
        {
            var capacity = OrbitBands[bandIndex].Capacity;
            var current = GetBandSatelliteCount(bandIndex);
            ctx.WriteError($"Band {bandIndex} is at capacity ({current}/{capacity})");
            return 1;
        }

        var name = args.Length > 1 ? args[1] : $"Station_{DateTime.Now.Ticks % 10000}";
        var station = CreateStation(bandIndex, name);

        if (station == null)
        {
            ctx.WriteError($"Failed to create station in band {bandIndex}");
            return 1;
        }

        ctx.WriteLine($"[color=green]Station '{name}' created in band {bandIndex}[/color]");
        return 0;
    }

    /// <summary>
    /// Debug command to show orbit band information.
    /// </summary>
    /// <param name="ctx">Command context for console output</param>
    /// <param name="args">Arguments (unused)</param>
    /// <returns>0 on success</returns>
    [DebugCommand(
        "orbit_bands",
        "Show orbit band information",
        "orbit_bands",
        Category = "Query",
        RequiresTarget = true
    )]
    public int GetOrbitBands(CommandContext ctx, string[] args)
    {
        var bandCount = GetBandCount();

        if (bandCount == 0)
        {
            ctx.WriteLine("No orbit bands configured.");
            return 0;
        }

        ctx.WriteLine($"[color=cyan]Orbit Bands for {Name}:[/color]");

        for (int i = 0; i < bandCount; i++)
        {
            var band = OrbitBands[i];
            var current = GetBandSatelliteCount(i);
            var canAdd = CanAddToBand(i);
            var status = canAdd ? "[color=green]available[/color]" : "[color=red]full[/color]";

            ctx.WriteLine(
                $"  Band {i}: radius={band.Radius:F1}, {current}/{band.Capacity} {status}"
            );
        }

        return 0;
    }
#endif

    /// <summary>
    /// Returns the number of available orbit bands.
    /// </summary>
    public int GetBandCount()
    {
        return OrbitBands?.Count ?? 0;
    }

    /// <summary>
    /// Checks if a satellite can be added to the specified band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band</param>
    /// <returns>True if the band exists and has capacity</returns>
    public bool CanAddToBand(int bandIndex)
    {
        if (OrbitBands == null || bandIndex < 0 || bandIndex >= OrbitBands.Count)
        {
            return false;
        }

        int currentCount = _bandSatelliteCounts.ContainsKey(bandIndex)
            ? _bandSatelliteCounts[bandIndex]
            : 0;
        int capacity = OrbitBands[bandIndex].Capacity;

        return currentCount < capacity;
    }

    /// <summary>
    /// Gets the current count of satellites in a band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band</param>
    /// <returns>Number of satellites in the band, or -1 if band is invalid</returns>
    public int GetBandSatelliteCount(int bandIndex)
    {
        if (bandIndex < 0 || bandIndex >= OrbitBands?.Count)
        {
            return -1;
        }

        return _bandSatelliteCounts.ContainsKey(bandIndex) ? _bandSatelliteCounts[bandIndex] : 0;
    }

    /// <summary>
    /// Minimum mass threshold for the centripetal correcting force.
    /// Bodies with mass at or below this value are not subject to the correction.
    /// </summary>
    private const float CENTRIPETAL_MASS_THRESHOLD = 100000f;

    /// <summary>
    /// Scaling coefficient for the centripetal correcting force.
    /// The force magnitude is <c>CENTRIPETAL_FORCE_COEFFICIENT * Mass * distanceSquared</c>,
    /// which causes it to grow quadratically with distance from the origin.
    /// </summary>
    private const float CENTRIPETAL_FORCE_COEFFICIENT = 0.00001f;

    public override void _PhysicsProcess(double delta)
    {
        TotalForce = new Vector3(0.0f, 0.0f, 0.0f);
        float totalMass = 0f;
        var bodies = GetTree().GetNodesInGroup("CelestialBody");
        float deltaT = (float)delta;
        foreach (CelestialBody body in bodies)
        {
            if (body != this)
            {
                Vector3 direction = (body.GlobalPosition - this.GlobalPosition);
                float distanceSq = direction.LengthSquared();

                // Guard against division by zero when bodies overlap or are at the
                // same position. Without this, distanceSq == 0 produces force = Inf,
                // and Inf * Normalized(Zero) = NaN, which permanently corrupts the
                // velocity and position of every body in the system.
                if (distanceSq < 1e-6f)
                    continue;

                float force = OrbitalMath.GRAVITATIONAL_CONSTANT * Mass * body.Mass / distanceSq;
                TotalForce += direction.Normalized() * force;
                totalMass += body.Mass;
            }
        }

        // Apply a centripetal correcting force towards the origin for massive bodies.
        // This prevents long-running simulations from drifting celestial objects
        // away from the system centre. The force grows with the square of the
        // distance so that far-flung bodies experience a stronger pull back.
        //if (Mass > CENTRIPETAL_MASS_THRESHOLD)
        //{
        //    Vector3 toOrigin = -GlobalPosition;
        //    float distanceSq = toOrigin.LengthSquared();
        //    if (distanceSq > 0f)
        //    {
        //        float centripetalMagnitude = CENTRIPETAL_FORCE_COEFFICIENT * Mass * distanceSq;
        //        TotalForce += toOrigin.Normalized() * centripetalMagnitude;
        //    }
        //}
        //
        //float alpha = 0.01f;
        //float beta = 0.01f;
        //Vector3 acceleration = TotalForce / Mass;
        //Velocity += acceleration * deltaT;
        ////Baumgarte Stabilization
        //Vector3 velocityCorrection =
        //    -alpha * barycenter.Velocity - beta * barycenter.Position / deltaT;
        //Velocity += (totalMass) / (totalMass + Mass) * velocityCorrection;
        ////GlobalPosition += (totalMass) / (totalMass + Mass) * velocityCorrection;
        //GlobalPosition += Velocity * deltaT;
        //barycenter.AddEntry(GlobalPosition, Velocity, Mass);
        //Velocity Verlet
        Vector3 acceleration = _savedForce / Mass;
        Velocity += 0.5f * acceleration * deltaT;
        GlobalPosition += Velocity * deltaT;
        Vector3 newAcceleration = TotalForce / Mass;
        Velocity += 0.5f * newAcceleration * deltaT;
        _savedForce = TotalForce;
    }

    public async Task GenerateMesh()
    {
        StartMeshGeneration(
            onCompleted: (_) => { },
            onFailed: (_, error) => GD.PrintErr($"Mesh generation failed: {error}")
        );
        while (Mesh?.Mesh == null)
        {
            await Task.Delay(10);
        }
    }

    public void StartMeshGeneration(
        Action<CelestialBody>? onCompleted = null,
        Action<CelestialBody, string>? onFailed = null
    )
    {
        Godot.Collections.Dictionary meshParams = new Godot.Collections.Dictionary();
        // Check if custom mesh data is available in the body dictionary
        if (bodyDict != null)
        {
            meshParams.Add("type", bodyDict["type"]);
            meshParams.Add("name", bodyDict["name"]);
            if (
                bodyDict.ContainsKey("base_mesh")
                && bodyDict["base_mesh"].Obj is Godot.Collections.Dictionary customMesh
            )
            {
                CalculateBaseMeshFromParams(customMesh, meshParams);
            }
            if (
                bodyDict.ContainsKey("tectonics")
                && bodyDict["tectonics"].Obj is Godot.Collections.Dictionary tectonics
            )
            {
                CalculateTectonicMeshFromParams(tectonics, meshParams);
            }
            if (
                bodyDict.ContainsKey("spherical_harmonics_settings")
                && bodyDict["spherical_harmonics_settings"].Obj
                    is Godot.Collections.Dictionary shSettings
            )
            {
                CalculateSphericalHarmonicsFromParams(shSettings, meshParams);
            }
            if (
                bodyDict.ContainsKey("resources")
                && bodyDict["resources"].Obj is Godot.Collections.Dictionary resources
            )
            {
                meshParams.Add("resources", resources);
                GD.Print(
                    $"[ResourceDebug] CelestialBody.GenerateMesh: Found resources in bodyDict, keys: {string.Join(", ", resources.Keys)}"
                );
            }
            else
            {
                // Fallback to template resources if not provided in bodyDict
                var t = TemplateHelpers.GetCelestialBodyDefaults(Type);
                if (
                    t.ContainsKey("resources")
                    && t["resources"].Obj is Godot.Collections.Dictionary templateResources
                )
                {
                    meshParams.Add("resources", templateResources);
                    GD.Print(
                        $"[ResourceDebug] CelestialBody.GenerateMesh: No resources in bodyDict, loaded from template, keys: {string.Join(", ", templateResources.Keys)}"
                    );
                }
                else
                {
                    GD.Print(
                        $"[ResourceDebug] CelestialBody.GenerateMesh: No resources in bodyDict or template"
                    );
                }
            }
        }
        //else
        //{
        //    var t = TemplateHelpers.GetCelestialBodyDefaults(Type);
        //    var name = PickName((Godot.Collections.Dictionary)t["possible_names"]);
        //    meshParams.Add("name", name);
        //    meshParams.Add("type", Enum.GetName(typeof(SatelliteBodyType), Type)!);
        //    var template = (Godot.Collections.Dictionary)t["template"];

        //    // Handle both position/velocity (dominant bodies) and orbital params (planetary bodies)
        //    if (template.ContainsKey("position"))
        //    {
        //        meshParams.Add("position", (Vector3)template["position"]);
        //        meshParams.Add("velocity", (Vector3)template["velocity"]);
        //    }
        //    else
        //    {
        //        // Orbital params present — no parent context in this fallback path,
        //        // so use default position; actual position is calculated by SystemGenerator
        //        meshParams.Add("position", Vector3.Zero);
        //        meshParams.Add("velocity", Vector3.Zero);
        //    }

        //    var size = (int)template["size"];
        //    var mass = (float)template["mass"];
        //    meshParams.Add("size", size);
        //    meshParams.Add("mass", mass);
        //    if (
        //        t.ContainsKey("base_mesh")
        //        && t["base_mesh"].Obj is Godot.Collections.Dictionary customMesh
        //    )
        //    {
        //        CalculateBaseMeshFromParams(customMesh, meshParams);
        //    }
        //    if (
        //        t.ContainsKey("tectonics")
        //        && t["tectonics"].Obj is Godot.Collections.Dictionary tectonics
        //    )
        //    {
        //        CalculateTectonicMeshFromParams(tectonics, meshParams);
        //    }
        //    if (
        //        t.ContainsKey("resources")
        //        && t["resources"].Obj is Godot.Collections.Dictionary resources
        //    )
        //    {
        //        meshParams.Add("resources", resources);
        //        GD.Print(
        //            $"[ResourceDebug] CelestialBody.GenerateMesh: Found resources in template, keys: {string.Join(", ", resources.Keys)}"
        //        );
        //    }
        //    else
        //    {
        //        GD.Print(
        //            $"[ResourceDebug] CelestialBody.GenerateMesh: No resources in template (containsKey: {t.ContainsKey("resources")})"
        //        );
        //    }
        //}
        GD.Print($"Mesh Params: {meshParams}");
        Mesh!.ConfigureFrom(StrDb, meshParams);

        Mesh.StartMeshGeneration(
            Oct,
            onCompleted: (mesh) =>
            {
                StrDb.FinalizeDB();
                GD.Print($"Generated mesh for {meshParams["name"]}");
                onCompleted?.Invoke(this);
            },
            onFailed: (mesh, error) =>
            {
                GD.PrintErr($"Mesh generation failed for {meshParams["name"]}: {error}");
                onFailed?.Invoke(this, error);
            }
        );
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

    public Point? FindNearest(Vector3 position)
    {
        var result = FindNearestCell(position);
        return result?.Point;
    }

    public CellSelectionResult? FindNearestCell(Vector3 position)
    {
        Vector3 localSpace = (position - this.GlobalPosition);
        Point desired = new Point(localSpace, 0);
        Point? result = Oct.FindNearest(desired);
        if (result is null)
            return null;
        PolygonRendererSDL.DrawPoint(this, 1, result.ToVector3(), 0.05f, Colors.Red);

        var cells = StrDb.PlanetMap[result];
        Godot.Collections.Array<VoronoiCell> contains = new Godot.Collections.Array<VoronoiCell>();
        foreach (var cell in cells)
        {
            desired.Position = desired.Position.Normalized() * (Mesh!.size + cell.Height);
            if (cell.BoundingBox.HasPoint(desired.Position))
                contains.Add(cell);
        }
        if (contains.Count == 0)
        {
            return null;
        }
        else if (contains.Count == 1)
        {
            var cell = contains[0];
            var continent = Mesh!.GetContinent(cell.ContinentIndex);
            return new CellSelectionResult
            {
                Point = result,
                Cell = contains[0],
                CellContinent = continent,
            };
        }
        else
        {
            float minDist = float.MaxValue;
            VoronoiCell? minCell = null;
            foreach (var cell in contains)
            {
                float dist = (cell.Center - desired.Position).LengthSquared();
                if (dist < minDist)
                {
                    minDist = dist;
                    minCell = cell;
                }
            }
            var continent = Mesh!.GetContinent(minCell!.ContinentIndex);
            return new CellSelectionResult
            {
                Point = result,
                Cell = contains[0],
                CellContinent = continent,
            };
        }
    }

    public class CellSelectionResult
    {
        public required Point Point { get; set; }
        public required VoronoiCell Cell { get; set; }
        public required Continent CellContinent { get; set; }
    }

    public MeshInstance3D CreateDebugWireframe(Aabb aabb)
    {
        Vector3[] corners = new Vector3[]
        {
            aabb.GetEndpoint(0),
            aabb.GetEndpoint(1),
            aabb.GetEndpoint(2),
            aabb.GetEndpoint(3),
            aabb.GetEndpoint(4),
            aabb.GetEndpoint(5),
            aabb.GetEndpoint(6),
            aabb.GetEndpoint(7),
        };
        var lineVertices = new Vector3[]
        {
            // Bottom face
            corners[0],
            corners[1],
            corners[1],
            corners[5],
            corners[5],
            corners[4],
            corners[4],
            corners[0],
            // Top face
            corners[2],
            corners[3],
            corners[3],
            corners[7],
            corners[7],
            corners[6],
            corners[6],
            corners[2],
            // Vertical edges
            corners[0],
            corners[2],
            corners[1],
            corners[3],
            corners[4],
            corners[6],
            corners[5],
            corners[7],
        };

        // 3. Create the ArrayMesh
        var mesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)ArrayMesh.ArrayType.Max);
        arrays[(int)ArrayMesh.ArrayType.Vertex] = lineVertices;

        // 4. Add the vertices as a surface with a line primitive type
        mesh.AddSurfaceFromArrays(ArrayMesh.PrimitiveType.Lines, arrays);

        // 5. Create the MeshInstance3D node
        var meshInstance = new MeshInstance3D { Mesh = mesh, Name = "AABB_Wireframe" };

        var material = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            // Use unshaded mode to make the color constant regardless of lighting
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
        };
        meshInstance.MaterialOverride = material;

        return meshInstance;
    }

    private void CalculateTectonicMeshFromParams(
        Godot.Collections.Dictionary definedMesh,
        Godot.Collections.Dictionary meshParams
    )
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
        tectDict.Add(
            "max_propagation_distance",
            rng.RandfRange(maxPropagationDistance[0], maxPropagationDistance[1])
        );
        float[] propagationFalloff = (float[])definedMesh["propagation_falloff"];
        tectDict.Add(
            "propagation_falloff",
            rng.RandfRange(propagationFalloff[0], propagationFalloff[1])
        );
        float[] inactiveStressThreshold = (float[])definedMesh["inactive_stress_threshold"];
        tectDict.Add(
            "inactive_stress_threshold",
            rng.RandfRange(inactiveStressThreshold[0], inactiveStressThreshold[1])
        );
        float[] generalHeightScale = (float[])definedMesh["general_height_scale"];
        tectDict.Add(
            "general_height_scale",
            rng.RandfRange(generalHeightScale[0], generalHeightScale[1])
        );
        float[] generalShearScale = (float[])definedMesh["general_shear_scale"];
        tectDict.Add(
            "general_shear_scale",
            rng.RandfRange(generalShearScale[0], generalShearScale[1])
        );
        float[] generalCompressionScale = (float[])definedMesh["general_compression_scale"];
        tectDict.Add(
            "general_compression_scale",
            rng.RandfRange(generalCompressionScale[0], generalCompressionScale[1])
        );
        float[] generalTransformScale = (float[])definedMesh["general_transform_scale"];
        tectDict.Add(
            "general_transform_scale",
            rng.RandfRange(generalTransformScale[0], generalTransformScale[1])
        );
        meshParams.Add("tectonic", tectDict);
    }

    private void CalculateBaseMeshFromParams(
        Godot.Collections.Dictionary definedMesh,
        Godot.Collections.Dictionary meshParams
    )
    {
        meshParams.Add("subdivisions", (int)definedMesh["subdivisions"]);
        var vpeArray = (Godot.Collections.Array<Godot.Collections.Array<int>>)
            definedMesh["vertices_per_edge"];
        int[] vertices_per_edge = new int[(int)definedMesh["subdivisions"]];
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        GD.Print($"VPE Array: {vpeArray}");
        for (int i = 0; i < vertices_per_edge.Length; i++)
        {
            if (vpeArray.Count - 1 > i) //Defined subdivisions
            {
                vertices_per_edge[i] = rng.RandiRange(vpeArray[i][0], vpeArray[i][1]);
            }
            else
            {
                vertices_per_edge[i] = rng.RandiRange(
                    vpeArray[vpeArray.Count - 1][0],
                    vpeArray[vpeArray.Count - 1][1]
                );
            }
        }
        meshParams.Add("vertices_per_edge", vertices_per_edge);
        meshParams.Add("num_abberations", (int)definedMesh["num_abberations"]);
        meshParams.Add("num_deformation_cycles", (int)definedMesh["num_deformation_cycles"]);
    }

    private void CalculateSphericalHarmonicsFromParams(
        Godot.Collections.Dictionary definedSH,
        Godot.Collections.Dictionary meshParams
    )
    {
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        var shDict = new Godot.Collections.Dictionary();

        // Randomize amplitude from range
        float[] amplitudeRange = (float[])definedSH["amplitude_range"];
        shDict.Add("amplitude", rng.RandfRange(amplitudeRange[0], amplitudeRange[1]));

        meshParams.Add("spherical_harmonics", shDict);
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

    public class Builder
    {
        internal Vector3? _velocity;
        internal float? _mass;
        internal CelestialBodyType? _type;
        internal RockyPlanetType? _rockyType;
        internal UnifiedCelestialMesh? _mesh;
        internal Godot.Collections.Dictionary? _bodyDict;
        internal string? _name;

        public Builder WithVelocity(Vector3 velocity)
        {
            _velocity = velocity;
            return this;
        }

        public Builder WithMass(float mass)
        {
            _mass = mass;
            return this;
        }

        public Builder WithType(CelestialBodyType type)
        {
            _type = type;
            return this;
        }

        public Builder WithName(string name)
        {
            _name = name;
            return this;
        }

        public Builder WithRockyType(RockyPlanetType? rockyType)
        {
            _rockyType = rockyType;
            return this;
        }

        public Builder WithMesh(UnifiedCelestialMesh mesh)
        {
            _mesh = mesh;
            return this;
        }

        public Builder WithBodyDict(Godot.Collections.Dictionary bodyDict)
        {
            _bodyDict = bodyDict;
            return this;
        }

        public Builder FromBodyDict(
            Godot.Collections.Dictionary bodyDict,
            UnifiedCelestialMesh mesh
        )
        {
            _bodyDict = bodyDict;
            _mesh = mesh;

            if (bodyDict != null)
            {
                var baseTemplates = (Godot.Collections.Dictionary)bodyDict["template"];
                var type = (String)bodyDict["type"];
                var mass = (float)baseTemplates["mass"];
                var velocity = (Vector3)baseTemplates["velocity"];

                _type = (CelestialBodyType)Enum.Parse(typeof(CelestialBodyType), type);
                _mass = mass;
                _velocity = velocity;
                _name = (String)bodyDict["name"];

                if (mesh != null)
                {
                    var size = (int)baseTemplates["size"];
                    mesh.size = size;
                }
            }

            return this;
        }

        private void ValidateRequiredFields()
        {
            if (!_velocity.HasValue)
                throw new InvalidOperationException("Velocity is required");
            if (!_mass.HasValue)
                throw new InvalidOperationException("Mass is required");
            if (!_type.HasValue)
                throw new InvalidOperationException("Type is required");
            if (_mesh == null)
                throw new InvalidOperationException("Mesh is required");
        }

        public CelestialBody Build()
        {
            ValidateRequiredFields();
            return new CelestialBody(this);
        }

        public static CelestialBody BuildFromBodyDict(
            Godot.Collections.Dictionary bodyDict,
            UnifiedCelestialMesh mesh
        )
        {
            return new Builder().FromBodyDict(bodyDict, mesh).Build();
        }
    }

#if DEBUG
    string IDataProvider.Name => Name;
    string IDataProvider.Category => "Celestial";
    bool IDataProvider.NeedsRefresh => true;

    object IDebugDataProvider.SourceObject => this;

    string IDebugDataProvider.InstanceNamespace
    {
        get
        {
            // Sanitize name: remove all non-alphanumeric characters
            string nameStr = Name.ToString();
            var sanitized = new string(nameStr.Where(c => char.IsLetterOrDigit(c)).ToArray());
            return string.IsNullOrEmpty(sanitized)
                ? $"CelestialBody._{nameStr.GetHashCode()}"
                : $"CelestialBody.{sanitized}";
        }
    }

    bool IDebugDataProvider.IsSourceValid => IsInstanceValid(this);

    DebugDataNode IDataProvider.GetData()
    {
        var node = new DebugDataNode(Name.ToString())
            .AddProperty("Type", Type.ToString())
            .AddProperty("Mass", Mass)
            .AddProperty("Velocity", Velocity)
            .AddProperty("Position", GlobalPosition);

        if (Mesh != null)
        {
            node.AddProperty("Mesh Size", Mesh.size);
        }

        return node;
    }

    void IDataProvider.Refresh() { }

    IEnumerable<string> IDataProvider.Search(string pattern)
    {
        var results = new List<string>();

        // Search by name
        if (Name.ToString().Contains(pattern, StringComparison.OrdinalIgnoreCase))
        {
            results.Add(Name.ToString());
        }

        // Search by type
        if (Type.ToString().Contains(pattern, StringComparison.OrdinalIgnoreCase))
        {
            results.Add($"Type:{Type}");
        }

        return results;
    }
#endif
}
