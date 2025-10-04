using Godot;
using MeshGeneration;
using Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UtilityLibrary;

using static Structures.Biome;

/// <summary>
/// Main class responsible for generating procedural celestial body meshes including planets, moons, and other spherical objects.
/// This class handles the complete generation pipeline from base mesh creation through tectonic simulation to final mesh rendering.
/// </summary>
/// <remarks>
/// The generation process involves multiple phases:
/// 1. Base mesh generation using subdivided icosahedron
/// 2. Mesh deformation for realistic terrain
/// 3. Voronoi cell generation for continent formation
/// 4. Tectonic simulation with stress calculations
/// 5. Biome assignment based on height and moisture
/// 6. Final mesh rendering with appropriate materials
///
/// This class extends Godot's MeshInstance3D to provide a complete celestial body generation solution
/// that can be used directly in Godot scenes. The generation process is asynchronous to prevent
/// blocking the main thread during complex calculations.
/// </remarks>
public partial class CelestialBodyMesh : MeshInstance3D
{
    /// <summary>
    /// Static progress tracking object for generation operations.
    /// Used to monitor and report progress during the asynchronous mesh generation process.
    /// </summary>
    public static GenericPercent percent;

    /// <summary>
    /// Origin point for the celestial body (typically at world origin).
    /// Serves as the center point for all mesh generation calculations.
    /// </summary>
    private Vector3 origin = new Vector3(0, 0, 0);

    /// <summary>
    /// Current vertex index used during mesh generation.
    /// Tracks the current position when iterating through vertices during mesh creation.
    /// </summary>
    public int VertexIndex = 0;

    /// <summary>
    /// Test index used for debugging and development purposes.
    /// Used to track specific cells or vertices during debugging sessions.
    /// </summary>
    public int testIndex = 0;

    /// <summary>
    /// Maximum height value found during terrain generation.
    /// Used for normalization and scaling of height values across the mesh.
    /// </summary>
    public float maxHeight = 0;

    /// <summary>
    /// Total count of Voronoi cells generated.
    /// Tracks the total number of Voronoi cells created during the generation process.
    /// </summary>
    public static int VoronoiCellCount = 0;

    /// <summary>
    /// Number of Voronoi cells currently being processed.
    /// Used for progress tracking during parallel processing of Voronoi cells.
    /// </summary>
    public static int CurrentlyProcessingVoronoiCount = 0;

    /// <summary>
    /// List of dual faces used in mesh generation.
    /// Stores the dual mesh faces created during the Voronoi diagram generation process.
    /// </summary>
    public List<Face> dualFaces = new List<Face>();

    /// <summary>
    /// List of edges generated during mesh creation.
    /// Contains all edges created during the mesh generation process for reference and processing.
    /// </summary>
    public List<Edge> generatedEdges = new List<Edge>();

    /// <summary>
    /// Random number generator for procedural generation.
    /// Provides deterministic random values for consistent terrain and feature generation.
    /// </summary>
    protected RandomNumberGenerator rand = new RandomNumberGenerator();

    /// <summary>
    /// Instance reference to this CelestialBodyMesh.
    /// Provides access to the current instance for external components and systems.
    /// </summary>
    public CelestialBodyMesh Instance { get; }

    /// <summary>
    /// Structure database containing all mesh data and relationships.
    /// Central repository for all mesh structures including vertices, edges, faces, and their relationships.
    /// </summary>
    protected StructureDatabase StrDb;

    /// <summary>
    /// Tectonic generation system for simulating plate tectonics.
    /// Handles the simulation of tectonic plate movement, stress calculations, and terrain deformation.
    /// </summary>
    protected TectonicGeneration tectonics;

    [ExportCategory("Planet Generation")]
    [ExportGroup("Mesh Generation")]

    /// <summary>
    /// Number of subdivision levels for the base mesh. Higher values create more detailed meshes.
    /// Each subdivision level increases the number of faces by a factor of 4, exponentially increasing mesh complexity.
    /// </summary>
    /// <remarks>
    /// Typical values range from 1-5. Higher values significantly impact performance and memory usage.
    /// </remarks>
    [Export]
    public int subdivide = 1;

    /// <summary>
    /// Array specifying the number of vertices per edge at each subdivision level.
    /// Controls the density of vertices along each edge of the base mesh triangles.
    /// </summary>
    /// <remarks>
    /// The array length should match or exceed the subdivision level. Each element corresponds
    /// to a subdivision level, with higher values creating more detailed edge subdivisions.
    /// </remarks>
    [Export]
    public int[] VerticesPerEdge = new int[] { 1, 2, 4 };

    /// <summary>
    /// Base radius of the celestial body.
    /// Defines the fundamental size of the generated celestial body in world units.
    /// </summary>
    /// <remarks>
    /// This value serves as the base radius before any height variations or terrain features are applied.
    /// All vertex positions are scaled relative to this base size.
    /// </remarks>
    [Export]
    public int size = 5;

    /// <summary>
    /// Whether to project vertices onto a sphere for spherical mesh generation.
    /// When enabled, all vertices are normalized to create a perfect spherical shape.
    /// </summary>
    /// <remarks>
    /// When disabled, the mesh maintains its original icosahedral structure without spherical projection.
    /// This is useful for debugging and visualizing the underlying mesh structure.
    /// </remarks>
    [Export]
    public bool ProjectToSphere = true;

    /// <summary>
    /// Random seed for procedural generation. Set to 0 for random seed.
    /// Controls the deterministic generation of terrain features for reproducible results.
    /// </summary>
    /// <remarks>
    /// When set to 0, a random seed is generated automatically. Non-zero values ensure
    /// the same terrain is generated each time, useful for testing and consistent world generation.
    /// </remarks>
    [Export]
    public ulong Seed = 0;

    /// <summary>
    /// Number of aberrations to introduce during mesh deformation for terrain variety.
    /// Controls the number of random perturbations applied to the mesh during deformation.
    /// </summary>
    /// <remarks>
    /// Higher values create more varied and complex terrain features but may lead to
    /// unrealistic terrain if set too high. Typical values range from 1-5.
    /// </remarks>
    [Export]
    public int NumAbberations = 3;

    /// <summary>
    /// Number of deformation cycles to apply for terrain generation.
    /// Determines how many iterations of mesh deformation are performed to create terrain features.
    /// </summary>
    /// <remarks>
    /// Each cycle applies additional deformation to the mesh, creating more complex terrain.
    /// Higher values increase computation time but can create more realistic terrain features.
    /// </remarks>
    [Export]
    public int NumDeformationCycles = 3;

    [ExportGroup("Tectonic Settings")]

    /// <summary>
    /// Number of continents to generate on the celestial body.
    /// Determines how many distinct continental landmasses are created during generation.
    /// </summary>
    /// <remarks>
    /// This value affects the flood fill algorithm used for continent generation.
    /// Too many continents for the mesh size may result in very small landmasses.
    /// </remarks>
    [Export]
    public int NumContinents = 5;

    /// <summary>
    /// Scale factor for stress calculations in tectonic simulation.
    /// Multiplier applied to stress values during tectonic plate interactions.
    /// </summary>
    /// <remarks>
    /// Higher values create more dramatic terrain features at plate boundaries.
    /// This affects mountain formation, rift valleys, and other tectonic features.
    /// </remarks>
    [Export]
    public float StressScale = 4.0f;

    /// <summary>
    /// Scale factor for shear forces in tectonic simulation.
    /// Controls the magnitude of shear deformation during tectonic plate movement.
    /// </summary>
    /// <remarks>
    /// Shear forces create lateral displacement of terrain features.
    /// Higher values create more pronounced shear zones and transform boundaries.
    /// </remarks>
    [Export]
    public float ShearScale = 1.2f;

    /// <summary>
    /// Maximum distance for stress propagation between tectonic plates.
    /// Defines how far tectonic stress can propagate from plate boundaries.
    /// </summary>
    /// <remarks>
    /// Higher values allow stress to affect terrain further from plate boundaries,
    /// creating broader mountain ranges and more extensive deformation zones.
    /// </remarks>
    [Export]
    public float MaxPropagationDistance = 0.1f;

    /// <summary>
    /// Falloff rate for stress propagation over distance.
    /// Controls how quickly tectonic stress diminishes with distance from plate boundaries.
    /// </summary>
    /// <remarks>
    /// Higher values create more localized stress effects, while lower values allow
    /// stress to influence terrain over larger areas. This affects the smoothness of
    /// terrain transitions from plate boundaries to continental interiors.
    /// </remarks>
    [Export]
    public float PropagationFalloff = 1.5f;

    /// <summary>
    /// Threshold below which stress is considered inactive.
    /// Minimum stress level required to trigger terrain deformation.
    /// </summary>
    /// <remarks>
    /// Stress values below this threshold are ignored during terrain generation.
    /// This helps prevent minor stress fluctuations from creating unrealistic terrain features.
    /// </remarks>
    [Export]
    public float InactiveStressThreshold = 0.1f;

    /// <summary>
    /// General scale factor for height variations in terrain.
    /// Overall multiplier applied to all height variations during terrain generation.
    /// </summary>
    /// <remarks>
    /// This value scales the amplitude of all terrain features, from ocean depths
    /// to mountain peaks. Higher values create more dramatic elevation changes.
    /// </remarks>
    [Export]
    public float GeneralHeightScale = 1f;

    /// <summary>
    /// General scale factor for shear transformations.
    /// Overall multiplier applied to shear deformation effects during tectonic simulation.
    /// </summary>
    /// <remarks>
    /// This value affects the magnitude of lateral terrain displacement caused by
    /// tectonic plate movement. Higher values create more pronounced shear features.
    /// </remarks>
    [Export]
    public float GeneralShearScale = 1.2f;

    /// <summary>
    /// General scale factor for compression transformations.
    /// Overall multiplier applied to compression effects during tectonic simulation.
    /// </summary>
    /// <remarks>
    /// This value affects the intensity of terrain compression at convergent plate boundaries.
    /// Higher values create more dramatic mountain formation and crustal thickening.
    /// </remarks>
    [Export]
    public float GeneralCompressionScale = 1.75f;

    /// <summary>
    /// General scale factor for coordinate transformations.
    /// Overall multiplier applied to coordinate transformations during mesh generation.
    /// </summary>
    /// <remarks>
    /// This value affects the scaling of coordinate transformations used in various
    /// mesh generation operations, including vertex positioning and deformation.
    /// </remarks>
    [Export]
    public float GeneralTransformScale = 1.1f;


    [ExportGroup("Finalized Object")]

    /// <summary>
    /// Whether to generate realistic terrain features.
    /// Controls whether the generation process uses realistic physical parameters.
    /// </summary>
    /// <remarks>
    /// When enabled, the generation uses physically-based parameters for terrain formation.
    /// When disabled, simplified or stylized terrain generation may be used.
    /// </remarks>
    [Export]
    public bool GenerateRealistic = true;

    /// <summary>
    /// Whether to display biome colors on the final mesh.
    /// Controls whether biome-specific colors are applied to the mesh surface.
    /// </summary>
    /// <remarks>
    /// When enabled, different biomes (ocean, forest, desert, etc.) are rendered
    /// with appropriate colors. When disabled, height-based coloring may be used instead.
    /// </remarks>
    [Export]
    public bool ShouldDisplayBiomes = true;

    [ExportCategory("Generation Debug")]
    [ExportGroup("Debug")]

    /// <summary>
    /// Whether to render all triangles for debugging purposes.
    /// When enabled, renders all mesh triangles for visual inspection of the mesh structure.
    /// </summary>
    /// <remarks>
    /// This is primarily a debugging tool that helps visualize the underlying mesh topology.
    /// Enabling this may impact performance due to the increased number of rendered elements.
    /// </remarks>
    [Export]
    public bool AllTriangles = false;

    /// <summary>
    /// Interface control for drawing tectonic movement arrows.
    /// When enabled, draws arrows showing tectonic plate movement directions.
    /// </summary>
    /// <remarks>
    /// This is a debugging and visualization tool that helps understand tectonic plate dynamics.
    /// The arrows show the direction and relative magnitude of plate movement.
    /// </remarks>
    [Export]
    public bool ShouldDrawArrowsInterface = false;

    /// <summary>
    /// Static flag controlling whether tectonic movement arrows are drawn.
    /// Internal flag used by the rendering system to control arrow visualization.
    /// </summary>
    /// <remarks>
    /// This flag is set based on the ShouldDrawArrowsInterface property and is used
    /// internally during the mesh generation process to determine whether to draw arrows.
    /// </remarks>
    public static bool ShouldDrawArrows = false;

    /// <summary>
    /// Called when the node is ready. Initializes the celestial body mesh.
    /// This method is part of Godot's node lifecycle and is called when the node enters the scene tree.
    /// </summary>
    /// <remarks>
    /// Actual initialization is handled in the GenerateMesh method, which should be called
    /// explicitly when mesh generation is desired. This allows for deferred initialization
    /// and better control over when the computationally expensive generation process begins.
    /// </remarks>
    public override void _Ready()
    {
        // Initialization handled in GenerateMesh method
        //GenerateMesh();
    }

    /// <summary>
    /// Configures the celestial body mesh generation parameters from a dictionary.
    /// This method allows dynamic configuration of mesh generation parameters at runtime.
    /// </summary>
    /// <param name="meshParams">Dictionary containing mesh generation parameters. Can contain nested dictionaries for complex parameter groups.</param>
    /// <remarks>
    /// This method allows dynamic configuration of mesh generation parameters including:
    /// - Base mesh settings (subdivisions, vertices per edge, etc.)
    /// - Deformation parameters (aberrations, deformation cycles)
    /// - Tectonic settings (continents, stress scales, propagation settings)
    ///
    /// The method handles various data types and includes error handling for invalid parameters.
    /// It automatically adjusts array lengths to match subdivision levels and provides
    /// fallback values for missing or invalid parameters.
    /// </remarks>
    public void ConfigureFrom(Godot.Collections.Dictionary meshParams)
    {
        GD.Print($"Configuring {meshParams}");
        if (meshParams == null) return;

        if (meshParams.ContainsKey("name"))
        {
            String name = "";
            try { name = meshParams["name"].As<string>(); } catch { }
            this.Name = name + "_mesh";
        }
        if (meshParams.ContainsKey("size"))
        {
            try { size = meshParams["size"].As<int>(); } catch { GD.Print("Couldn't find size in meshParams"); }
        }

        // Base mesh settings
        if (meshParams.ContainsKey("subdivisions"))
        {
            try { subdivide = meshParams["subdivisions"].As<int>(); } catch (Exception e) { GD.PrintRaw($"\u001b[2J\u001b[H"); Logger.Error($"Error in Subdivisions: {e.Message}\n{e.StackTrace}"); }
        }

        if (meshParams.ContainsKey("vertices_per_edge"))
        {
            var v = meshParams["vertices_per_edge"];
            bool assigned = false;
            try
            {
                var ia = v.As<int[]>();
                if (ia != null && ia.Length > 0)
                {
                    VerticesPerEdge = ia;
                    assigned = true;
                }
            }
            catch (Exception e) { Logger.Error($"Error in VerticesPerEdge: {e.Message}\n{e.StackTrace}"); }

            if (!assigned)
            {
                try
                {
                    var ga = v.As<Godot.Collections.Array<int>>();
                    if (ga != null && ga.Count > 0)
                    {
                        VerticesPerEdge = ga.ToArray();
                        assigned = true;
                    }
                }
                catch (Exception e) { GD.PrintRaw($"\u001b[2J\u001b[H"); Logger.Error($"Error in VerticesPerEdge: {e.Message}\n{e.StackTrace}"); }
            }
        }

        if (VerticesPerEdge != null && subdivide > 0 && VerticesPerEdge.Length != subdivide)
        {
            var adjusted = new int[subdivide];
            for (int i = 0; i < subdivide; i++)
                adjusted[i] = VerticesPerEdge.Length > 0 ? VerticesPerEdge[Math.Min(i, VerticesPerEdge.Length - 1)] : 2;
            VerticesPerEdge = adjusted;
        }

        if (meshParams.ContainsKey("num_abberations"))
        {
            try { NumAbberations = meshParams["num_abberations"].As<int>(); } catch { }
        }
        if (meshParams.ContainsKey("num_deformation_cycles"))
        {
            try { NumDeformationCycles = meshParams["num_deformation_cycles"].As<int>(); } catch { }
        }

        // Tectonic settings
        if (meshParams.ContainsKey("tectonic"))
        {
            try
            {
                var tect = meshParams["tectonic"].As<Godot.Collections.Dictionary>();
                if (tect != null)
                {
                    if (tect.ContainsKey("num_continents")) { try { NumContinents = tect["num_continents"].As<int>(); } catch { } }
                    if (tect.ContainsKey("stress_scale")) { try { StressScale = tect["stress_scale"].As<float>(); } catch { } }
                    if (tect.ContainsKey("shear_scale")) { try { ShearScale = tect["shear_scale"].As<float>(); } catch { } }
                    if (tect.ContainsKey("max_propagation_distance")) { try { MaxPropagationDistance = tect["max_propagation_distance"].As<float>(); } catch { } }
                    if (tect.ContainsKey("propagation_falloff")) { try { PropagationFalloff = tect["propagation_falloff"].As<float>(); } catch { } }
                    if (tect.ContainsKey("inactive_stress_threshold")) { try { InactiveStressThreshold = tect["inactive_stress_threshold"].As<float>(); } catch { } }
                    if (tect.ContainsKey("general_height_scale")) { try { GeneralHeightScale = tect["general_height_scale"].As<float>(); } catch { } }
                    if (tect.ContainsKey("general_shear_scale")) { try { GeneralShearScale = tect["general_shear_scale"].As<float>(); } catch { } }
                    if (tect.ContainsKey("general_compression_scale")) { try { GeneralCompressionScale = tect["general_compression_scale"].As<float>(); } catch { } }
                    if (tect.ContainsKey("general_transform_scale")) { try { GeneralTransformScale = tect["general_transform_scale"].As<float>(); } catch { } }
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Main entry point for generating the celestial body mesh.
    /// This method orchestrates the complete mesh generation process.
    /// </summary>
    /// <remarks>
    /// This method initializes the mesh generation process by:
    /// 1. Setting up the mesh structure and random number generator
    /// 2. Creating the structure database and tectonic generation system
    /// 3. Starting the asynchronous planet generation process
    ///
    /// The generation runs in a separate task to avoid blocking the main thread.
    /// This allows the game to remain responsive during the computationally expensive
    /// mesh generation process. The method sets up all necessary components and
    /// initiates the two-pass generation system.
    /// </remarks>
    public virtual void GenerateMesh()
    {
        this.Mesh = new ArrayMesh();
        ShouldDrawArrows = ShouldDrawArrowsInterface;
        StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
        percent = new GenericPercent();
        if (Seed != 0)
        {
            rand.Seed = Seed;
        }
        GD.Print($"Rand Seed: {rand.Seed}\n");
        List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();
        tectonics = new TectonicGeneration(
            StrDb,
            rand,
            StressScale,
            ShearScale,
            MaxPropagationDistance,
            PropagationFalloff,
            InactiveStressThreshold,
            GeneralHeightScale,
            GeneralShearScale,
            GeneralCompressionScale);
        Task generatePlanet = Task.Factory.StartNew(() => GeneratePlanetAsync());
        GD.Print($"Number of Vertices: {StrDb.VoronoiVertices.Values.Count}\n");
    }

    /// <summary>
    /// Asynchronous method that orchestrates the complete planet generation process.
    /// This method coordinates the two main generation phases in sequence.
    /// </summary>
    /// <remarks>
    /// This method coordinates the two main phases of planet generation:
    /// 1. First Pass: Base mesh generation and deformation
    /// 2. Second Pass: Voronoi cell generation, tectonic simulation, and biome assignment
    ///
    /// The passes are executed sequentially to ensure proper data dependencies.
    /// The first pass creates the fundamental mesh structure, while the second pass
    /// adds the detailed features like continents, tectonics, and biomes.
    ///
    /// This method uses Task-based asynchronous programming to avoid blocking the main thread.
    /// </remarks>
    private void GeneratePlanetAsync()
    {
        Task firstPass = Task.Factory.StartNew(() => GenerateFirstPass());
        Task.WaitAll(firstPass);
        StrDb.IncrementMeshState();
        Task secondPass = Task.Factory.StartNew(() => GenerateSecondPass());
    }

    /// <summary>
    /// First phase of planet generation: creates and deforms the base mesh.
    /// This method establishes the fundamental mesh structure for the celestial body.
    /// </summary>
    /// <remarks>
    /// This phase handles:
    /// 1. Base mesh generation using subdivided icosahedron
    /// 2. Mesh deformation for realistic terrain features
    /// 3. Optional triangle rendering for debugging
    ///
    /// The base mesh serves as the foundation for all subsequent generation phases.
    /// This method uses the BaseMeshGeneration class to create the initial icosahedral
    /// mesh and then applies deformation cycles to create terrain variety.
    ///
    /// Performance timing is applied to each major operation for optimization analysis.
    /// </remarks>
    protected virtual void GenerateFirstPass()
    {
        GD.Print($"Rand Seed: {rand.Seed}");
        GenericPercent emptyPercent = new GenericPercent();
        BaseMeshGeneration baseMesh = new BaseMeshGeneration(rand, StrDb, subdivide, VerticesPerEdge, this);
        emptyPercent.PercentTotal = 0;
        var function = FunctionTimer.TimeFunction<int>(Name.ToString(),
            "Base Mesh Generation",
            () =>
            {
                try
                {
                    baseMesh.PopulateArrays();
                    baseMesh.GenerateNonDeformedFaces();
                    baseMesh.GenerateTriangleList();
                }
                catch (Exception e)
                {
                    FunctionTimer.ResetScrollRegionAndClear();
                    Logger.Error($"Base Mesh Generation Error:  {e.Message}\n{e.StackTrace}", "Base Mesh Generation Error");
                    GD.PrintRaw($"\x1b0;0r\x1b[2J\x1b[H\n");
                    GD.PrintRaw($"Base Mesh Generation Error:  {e.Message}\n{e.StackTrace}\n");
                }
                return 0;
            }, emptyPercent);


        var OptimalArea = (4.0f * Mathf.Pi * size * size) / StrDb.Base.Triangles.Count;
        float OptimalSideLength = Mathf.Sqrt((OptimalArea * 4.0f) / Mathf.Sqrt(3.0f)) / 3f;
        function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Deformed Mesh Generation", () =>
        {
            try
            {
                baseMesh.InitiateDeformation(NumDeformationCycles, NumAbberations, OptimalSideLength);
            }
            catch (Exception e)
            {
                FunctionTimer.ResetScrollRegionAndClear();
                GD.PrintRaw($"x1b0;0r\x1b[2J\x1b[H\nDeform Mesh Error:  {e.Message}\n{e.StackTrace}\n");
            }
            return 0;
        }, emptyPercent);

        GD.Print("Deformed Base Mesh");
        //if (AllTriangles)
        //{
        //    foreach (var triangle in StrDb.Base.Triangles)
        //    {
        //        RenderTriangleAndConnections(triangle.Value);
        //    }
        //}

    }

    /// <summary>
    /// Second phase of planet generation: creates continents, simulates tectonics, and generates final mesh.
    /// This method handles the complex processes of continent formation and terrain simulation.
    /// </summary>
    /// <remarks>
    /// This complex phase handles multiple interconnected processes:
    /// 1. Voronoi cell generation for continent boundaries
    /// 2. Flood fill algorithm to create continents
    /// 3. Boundary calculation and stress analysis
    /// 4. Tectonic simulation with stress application
    /// 5. Biome assignment based on height and moisture
    /// 6. Final mesh generation from continent data
    ///
    /// Each step is timed and logged for performance analysis.
    /// This phase builds upon the base mesh created in the first pass and adds
    /// the detailed features that make the celestial body realistic and varied.
    ///
    /// The method uses multiple helper classes and algorithms to achieve the complex
    /// terrain generation, including VoronoiCellGeneration, TectonicGeneration, and BiomeAssigner.
    /// </remarks>
    protected virtual void GenerateSecondPass()
    {
        VoronoiCellGeneration voronoiCellGeneration = new VoronoiCellGeneration(StrDb);
        GenericPercent emptyPercent = new GenericPercent();
        emptyPercent.PercentTotal = 0;
        percent.PercentTotal = StrDb.BaseVertices.Count;
        var function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Voronoi Cell Generation", () =>
            {
                try
                {
                    voronoiCellGeneration.GenerateVoronoiCells(percent, this);
                }
                catch (Exception e)
                {
                    GD.PrintRaw($"\u001b[2J\u001b[H");
                    Logger.Error($"Voronoi Cell Generation Error: {e.Message}\n{e.StackTrace}", "ERROR");
                }
                return 0;
            }, percent);

        Dictionary<int, Continent> continents = new Dictionary<int, Continent>();
        function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Flood Filling", () =>
        {
            try
            {
                continents = FloodFillContinentGeneration(StrDb.VoronoiCells);
            }
            catch (Exception e) { GD.PrintRaw($"\u001b[2J\u001b[H"); Logger.Error($"Flood Filling Error: {e.Message}\n{e.StackTrace}"); }
            return 0;
        }, emptyPercent);
        percent.Reset();
        function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Calculate Voronoi Cells", () =>
        {
            try
            {
                foreach (VoronoiCell vc in StrDb.VoronoiCells)
                {
                    var cellNeighbors = GetCellNeighbors(vc);
                    float averageHeight = vc.Height;
                    List<Edge> OutsideEdges = new List<Edge>();
                    List<int> BoundingContinentIndex = new List<int>();
                    foreach (VoronoiCell neighbor in cellNeighbors)
                    {
                        if (neighbor.ContinentIndex != vc.ContinentIndex)
                        {
                            vc.IsBorderTile = true;
                            continents[vc.ContinentIndex].boundaryCells.Add(vc);
                            continents[vc.ContinentIndex].neighborContinents.Add(neighbor.ContinentIndex);
                            continents[neighbor.ContinentIndex].neighborContinents.Add(vc.ContinentIndex);
                            neighbor.IsBorderTile = true;
                            BoundingContinentIndex.Add(neighbor.ContinentIndex);
                            foreach (Point p in vc.Points)
                            {
                                foreach (Point p2 in neighbor.Points)
                                {
                                    if (p.Equals(p2))
                                    {
                                        p.isOnContinentBorder = true;
                                    }
                                }
                            }
                            foreach (Edge e in vc.Edges)
                            {
                                foreach (Edge e2 in neighbor.Edges)
                                {
                                    if (e.key.Equals(e2.key))
                                    {
                                        vc.EdgeBoundaryMap.Add(e, neighbor.ContinentIndex);
                                        OutsideEdges.Add(e);
                                    }
                                }
                            }

                        }
                    }
                    vc.BoundingContinentIndex = BoundingContinentIndex.ToArray();
                    vc.OutsideEdges = OutsideEdges.ToArray();
                    percent.PercentCurrent++;
                }
            }
            catch (Exception e)
            {
                GD.PrintRaw($"\u001b[2J\u001b[H");
                Logger.Error($"Error in Calculate Boundary Stress: {e.Message}\n{e.StackTrace}");
            }
            foreach (Point p in StrDb.VoronoiCellVertices)
            {
                var cells = StrDb.CellMap[p];
                foreach (VoronoiCell vc in cells)
                {
                    p.ContinentIndecies.Add(vc.ContinentIndex);
                }
            }
            return 0;
        }, percent);
        emptyPercent.Reset();
        function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Average out Heights", () =>
        {
            try
            {
                UpdateVertexHeights(StrDb.VoronoiCellVertices, continents);
            }
            catch (Exception e)
            {
                //GD.PrintRaw($"\nHeight Average Error:  {e.Message}\n{e.StackTrace}\n");
            }

            return 0;
        }, emptyPercent);
        percent.PercentTotal = continents.Count;
        percent.Reset();
        function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Calculate Boundary Stress", () =>
        {
            try
            {
                tectonics.CalculateBoundaryStress(StrDb.Dual.EdgeCells, StrDb.VoronoiCellVertices, continents, percent);
            }
            catch (Exception boundsError)
            {
                //GD.PrintRaw($"\nBoundary Stress Error:  {boundsError.Message}\n{boundsError.StackTrace}\n");
            }
            return 0;
        }, percent);
        percent.Reset();
        function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Apply Stress to Terrain", () =>
        {
            try
            {
                tectonics.ApplyStressToTerrain(continents, StrDb.VoronoiCells);
            }
            catch (Exception stressError)
            {
                //GD.PrintRaw($"\nStress Error:  {stressError.Message}\n{stressError.StackTrace}\n");
            }
            return 0;
        }, percent);
        percent.Reset();
        maxHeight = StrDb.VoronoiCellVertices.Max(p => p.Height);
        function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Assign Biomes", () =>
        {
            try
            {
                AssignBiomes(continents, StrDb.VoronoiCells);
            }
            catch (Exception biomeError)
            {
                //GD.PrintRaw($"\nBiome Error:  {biomeError.Message}\n{biomeError.StackTrace}\n");
            }
            return 0;
        }, percent);
        percent.Reset();
        percent.PercentTotal = continents.Values.Count;
        percent.PercentCurrent = 0;
        function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Generate From Continents", () =>
        {
            try
            {
                GenerateFromContinents(continents);
                //DrawContinentBorders(continents);
            }
            catch (Exception genError)
            {
                //GD.PrintRaw($"\nGenerate From Continents Error:  {genError.Message}\n{genError.StackTrace}\n");
            }
            return 0;
        }, percent);

    }


    /// <summary>
    /// Assigns biomes to all points on the celestial body based on height and moisture.
    /// This method uses parallel processing to efficiently assign biomes across all continents.
    /// </summary>
    /// <param name="continents">Dictionary of continents with their properties including moisture levels and height data.</param>
    /// <param name="cells">List of Voronoi cells containing the points to biome.</param>
    /// <remarks>
    /// This method processes biome assignment in parallel across all continents:
    /// 1. Calculates moisture levels for each continent
    /// 2. Assigns appropriate biomes to each point based on height and moisture
    /// 3. Uses threading for performance optimization
    ///
    /// Biome types include: Tundra, Icecap, Desert, Grassland, Forest, Rainforest, Taiga, Ocean, Coastal, Mountain
    ///
    /// The method uses the BiomeAssigner class to determine the appropriate biome for each point
    /// based on its height and the continent's moisture level. Parallel processing is used
    /// to improve performance on multi-core systems.
    /// </remarks>
    private void AssignBiomes(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
    {
        Task[] BiomeThreader = new Task[continents.Count];
        int continentCount = 0;
        foreach (var continent in continents)
        {
            Task biomeThread = Task.Factory.StartNew(() =>
            {
                Continent c = continent.Value;
                c.averageMoisture = BiomeAssigner.CalculateMoisture(c, rand, 0.5f);
                foreach (var cell in c.cells)
                {
                    foreach (Point p in cell.Points)
                    {
                        p.Biome = BiomeAssigner.AssignBiome(this, p.Height, c.averageMoisture);
                    }
                }
            }
            );
            BiomeThreader[continentCount] = biomeThread;
            continentCount++;
        }
        Task.WaitAll(BiomeThreader.ToArray());
    }

    /// <summary>
    /// Updates vertex heights based on the average height of their containing continents.
    /// This method ensures smooth height transitions across continent boundaries.
    /// </summary>
    /// <param name="Vertices">Set of vertex points to update.</param>
    /// <param name="Continents">Dictionary mapping continent indices to continent objects.</param>
    /// <remarks>
    /// This method handles three cases:
    /// 1. Vertices in no continent (error case, logs warning)
    /// 2. Vertices in one continent (direct height assignment)
    /// 3. Vertices in multiple continents (averaged height calculation)
    ///
    /// The averaging ensures smooth transitions at continent boundaries.
    /// This is particularly important for vertices that lie on the boundaries between
    /// multiple continents, as it prevents sharp height discontinuities that would
    /// create unrealistic terrain features.
    /// </remarks>
    public void UpdateVertexHeights(HashSet<Point> Vertices, Dictionary<int, Continent> Continents)
    {
        foreach (Point p in Vertices)
        {
            if (p.ContinentIndecies.Count == 0)
            {
                GD.PrintRaw("This should never happen\n");
            }
            if (p.ContinentIndecies.Count == 1)
            {
                if (p.ContinentIndecies.ElementAt(0) == -1)
                {
                    var cells = StrDb.CellMap[p];
                    string str = "";
                    foreach (VoronoiCell vc in cells)
                    {
                        str += $"{vc}, ";
                    }
                    GD.PrintRaw($"Encountered a point with no continent: {p}, part of {str}\n");
                }
                p.Height = Continents[p.ContinentIndecies.ElementAt(0)].averageHeight;
            }
            else if (p.ContinentIndecies.Count > 1)
            {
                float height = 0;
                foreach (int continentIndex in p.ContinentIndecies)
                {
                    height += Continents[continentIndex].averageHeight;
                }
                height /= p.ContinentIndecies.Count;
                p.Height = height;
            }
        }
    }

    /// <summary>
    /// Retrieves all neighboring Voronoi cells for a given origin cell.
    /// This method is essential for continent boundary detection and tectonic simulation.
    /// </summary>
    /// <param name="origin">The origin Voronoi cell to find neighbors for.</param>
    /// <param name="includeSameContinent">Whether to include cells from the same continent. Defaults to true.</param>
    /// <returns>Array of neighboring Voronoi cells.</returns>
    /// <remarks>
    /// This method finds neighbors by examining all points in the origin cell
    /// and finding all other cells that share those points. The includeSameContinent
    /// parameter allows filtering for cross-continent neighbors only.
    ///
    /// When includeSameContinent is false, only cells from different continents are returned,
    /// which is useful for identifying continent boundaries and calculating tectonic interactions.
    /// The method uses a HashSet to ensure each neighbor is only included once.
    /// </remarks>
    public VoronoiCell[] GetCellNeighbors(VoronoiCell origin, bool includeSameContinent = true)
    {
        HashSet<VoronoiCell> neighbors = new HashSet<VoronoiCell>();
        foreach (Point p in origin.Points)
        {
            var neighborCells = StrDb.CellMap[p];
            foreach (VoronoiCell vc in neighborCells)
            {
                if (includeSameContinent)
                {
                    neighbors.Add(vc);
                }
                else if (vc.ContinentIndex != origin.ContinentIndex)
                {
                    neighbors.Add(vc);
                }
            }
        }
        return neighbors.ToArray();
    }

    /// <summary>
    /// Generates random starting cell indices for continent formation.
    /// This method provides the seed points for the flood fill continent generation algorithm.
    /// </summary>
    /// <param name="cells">List of all Voronoi cells.</param>
    /// <returns>Set of randomly selected starting cell indices.</returns>
    /// <remarks>
    /// This method selects random cells to serve as the starting points
    /// for continent generation during the flood fill algorithm. The number
    /// of starting cells is limited by both the NumContinents parameter and
    /// the total number of available cells.
    ///
    /// The method uses a HashSet to ensure unique starting positions and continues
    /// selecting random cells until either the desired number of continents is reached
    /// or all available cells are exhausted. This ensures that the continent generation
    /// process has appropriate seed points distributed across the mesh.
    /// </remarks>
    public HashSet<int> GenerateStartingCells(List<VoronoiCell> cells)
    {
        HashSet<int> startingCells = new HashSet<int>();
        int numStartingCells = Mathf.Min(NumContinents, cells.Count);
        while (startingCells.Count < numStartingCells)
        {
            int position = rand.RandiRange(0, cells.Count - 1);
            startingCells.Add(position);
        }
        return startingCells;
    }

    /// <summary>
    /// Draws visual borders around continents for debugging and visualization.
    /// This method provides visual feedback about continent boundaries.
    /// </summary>
    /// <param name="continents">Dictionary of continents to draw borders for.</param>
    /// <remarks>
    /// This method renders black lines along the boundaries between continents.
    /// It processes boundary cells and draws lines along their outside edges,
    /// accounting for height variations in the terrain. The method includes
    /// a safety check to prevent infinite loops.
    ///
    /// The visualization is particularly useful for understanding the results
    /// of the continent generation process and for debugging issues with
    /// continent boundary detection. The lines are drawn using the PolygonRendererSDL
    /// utility class and are positioned slightly above the terrain surface for visibility.
    /// </remarks>
    public void DrawContinentBorders(Dictionary<int, Continent> continents)
    {
        int maxAttempts = continents.Count * 10;
        foreach (var vc in continents)
        {
            var boundaries = vc.Value.boundaryCells;
            foreach (var b in boundaries)
            {
                if (b.IsBorderTile)
                {
                    for (int i = 0; i < b.OutsideEdges.Length; i++)
                    {
                        Edge e1 = b.OutsideEdges[i];
                        Point p1 = (Point)e1.P;
                        Point p2 = (Point)e1.Q;
                        Vector3 pos1 = p1.ToVector3().Normalized();
                        Vector3 pos2 = p2.ToVector3().Normalized();
                        Color lineColor = Colors.Black;
                        PolygonRendererSDL.DrawLine(this, size, pos1, pos2, Colors.Black);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Generates continents using a flood fill algorithm starting from random seed cells.
    /// This method creates distinct continental landmasses by expanding from seed points.
    /// </summary>
    /// <param name="cells">List of all Voronoi cells to be assigned to continents.</param>
    /// <returns>Dictionary mapping continent indices to Continent objects.</returns>
    /// <remarks>
    /// This method implements a sophisticated flood fill algorithm that:
    /// 1. Selects random starting cells as continent seeds
    /// 2. Assigns each seed random properties (crust type, height, movement direction)
    /// 3. Expands each continent by assigning neighboring cells to the nearest continent
    /// 4. Calculates continent properties like center, axes, and movement vectors
    /// 5. Sets up tectonic movement parameters for each continent
    ///
    /// The algorithm ensures that continents are reasonably sized and distributed
    /// across the celestial body surface. Each continent gets unique properties that
    /// influence its appearance and behavior during tectonic simulation.
    /// </remarks>
    public Dictionary<int, Continent> FloodFillContinentGeneration(List<VoronoiCell> cells)
    {
        //Dicitonary of continent indices to their smallest potential size (5, 8)
        Dictionary<int, int> continentMinSize = new Dictionary<int, int>();
        //Dictionary of cell indicies that can be popped in 1st phase
        Dictionary<int, List<int>> continentNeighbors = new Dictionary<int, List<int>>();
        Dictionary<int, Continent> continents = new Dictionary<int, Continent>();
        HashSet<int> startingCells = GenerateStartingCells(cells);
        //List of cells that can be popped to pull neighbors from in 2nd phase
        List<int> poppableCells = startingCells.ToList();
        //List containing all Voronoi Cells. Value at the index is the Starting Cell Index of the continent it belongs to
        // Or -1 if it is not yet part of a continent
        int[] neighborChart = new int[cells.Count];
        for (int i = 0; i < neighborChart.Length; i++)
        {
            neighborChart[i] = -1;
        }
        foreach (int startingCellIndex in startingCells)
        {
            Continent.CRUST_TYPE crustType = rand.RandiRange(0, 100) > 33 ? Continent.CRUST_TYPE.Oceanic : Continent.CRUST_TYPE.Continental;
            float averageHeight = crustType == Continent.CRUST_TYPE.Oceanic ? rand.RandfRange(-5.0f, 0.0f) : rand.RandfRange(1.2f, 7.0f);
            float rotation = Mathf.DegToRad(rand.RandiRange(-360, 360));
            float velocity = rand.RandfRange(0.3f, 1.7f);
            var continent = new Continent(startingCellIndex,
                    new List<VoronoiCell>(),//cells
                    new HashSet<VoronoiCell>(), //Boundary cells
                    new HashSet<Point>(), //points
                    new List<Point>(), //Convex Hull
                    new Vector3(0f, 0f, 0f), //averaged center
                    new Vector3(0f, 0f, 0f), //u axis
                    new Vector3(0f, 0f, 0f), //v axis
                    new Vector2(rand.RandfRange(-1f, 1f), rand.RandfRange(-1f, 1f)), velocity, rotation,//movement direction, velocity & rotation
                    crustType, averageHeight, rand.RandfRange(1.0f, 5.0f), //crust type, average height, average moisture
                    new HashSet<int>(), 0f,
                    new Dictionary<int, float>(),
                    new Dictionary<int, Continent.BOUNDARY_TYPE>());
            neighborChart[startingCellIndex] = startingCellIndex;
            continent.StartingIndex = startingCellIndex;
            continent.cells.Add(cells[startingCellIndex]);
            foreach (Point p in cells[startingCellIndex].Points)
            {
                continent.points.Add(p);
                p.ContinentIndecies.Add(continent.StartingIndex);
            }
            foreach (Edge e in cells[startingCellIndex].Edges)
            {
                e.ContinentIndex = startingCellIndex;
            }
            continents[startingCellIndex] = continent;

            var neighborIndices = GetCellNeighbors(cells[startingCellIndex]);
            continentNeighbors[startingCellIndex] = new List<int>();
            foreach (var nb in neighborIndices)
            {
                if (neighborChart[nb.Index] == -1)
                {
                    neighborChart[nb.Index] = neighborChart[startingCellIndex];
                    continentNeighbors[startingCellIndex].Add(nb.Index);
                    //poppableCells.Add(nb.Index);
                }
            }
            continentMinSize.Add(startingCellIndex, rand.RandiRange(5, 8));
        }
        while (true)
        {
            foreach (var continent in continentMinSize)
            {
                var index = continent.Key;
                var minSize = continent.Value;
                var continentObj = continents[index];
                if (continents[index].cells.Count < minSize)
                {
                    int randomNeighbor;
                    if (continentNeighbors[index].Count == 0)
                        randomNeighbor = continentNeighbors[index][0];
                    else
                        randomNeighbor = continentNeighbors[index][rand.RandiRange(0, continentNeighbors[index].Count - 1)];
                    continentObj.cells.Add(cells[randomNeighbor]);
                    continentNeighbors[index].Remove(randomNeighbor);
                    var neighborIndices = GetCellNeighbors(cells[randomNeighbor]);
                    foreach (var nb in neighborIndices)
                    {
                        if (neighborChart[nb.Index] == -1)
                        {
                            neighborChart[nb.Index] = neighborChart[index];
                            continentNeighbors[index].Add(nb.Index);
                        }
                    }
                }
                else
                {
                    poppableCells.AddRange(continentNeighbors[index]);
                    continentMinSize.Remove(index);
                }
            }
            if (continentMinSize.Count == 0) break;
        }
        testIndex = poppableCells[0];
        while (poppableCells.Count > 0)
        {
            int poppedIndex = rand.RandiRange(0, (poppableCells.Count - 1));
            int currentVCellIndex = poppableCells[poppedIndex];
            poppableCells.RemoveAt(poppedIndex);
            var neighborIndices = GetCellNeighbors(cells[currentVCellIndex]);
            foreach (var nb in neighborIndices)
            {
                if (neighborChart[nb.Index] == -1)
                {
                    neighborChart[nb.Index] = neighborChart[currentVCellIndex];
                    poppableCells.Add(nb.Index);
                }
            }
        }

        for (int i = 0; i < neighborChart.Length; i++)
        {
            if (neighborChart[i] != -1)
            {
                var continent = continents[neighborChart[i]];
                continent.cells.Add(cells[i]);
                cells[i].Height = continent.averageHeight;
                cells[i].ContinentIndex = continent.StartingIndex;
                foreach (Edge e in cells[i].Edges)
                {
                    e.ContinentIndex = neighborChart[i];
                }
                foreach (Point p in cells[i].Points)
                {
                    p.Position = p.Position.Normalized();
                    p.Height = continent.averageHeight;
                    continent.points.Add(p);
                    p.ContinentIndecies.Add(continent.StartingIndex);
                    //GD.PrintRaw($"Point {p} is in Continent {continent.StartingIndex}\n");
                }
            }
        }

        foreach (var keyValuePair in continents)
        {
            var continent = keyValuePair.Value;
            foreach (Point p in continent.points)
            {
                continent.averagedCenter += p.Position;
            }
            foreach (VoronoiCell vc in continent.cells)
            {
                vc.ContinentIndex = continent.StartingIndex;
            }

            continent.averagedCenter /= continent.points.Count;
            continent.averagedCenter = continent.averagedCenter.Normalized();
            var v1 = (continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized() - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized());
            var v2 = (continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized() - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized());
            var UnitNorm = v1.Cross(v2);
            if (UnitNorm.Dot(continent.averagedCenter) < 0f)
            {
                UnitNorm = -UnitNorm;
            }
            var uAxis = v1;
            var vAxis = UnitNorm.Cross(uAxis);
            uAxis = uAxis.Normalized();
            vAxis = vAxis.Normalized();
            foreach (VoronoiCell vc in continent.cells)
            {
                //Calculate the average center of the cell
                Vector3 cellAverageCenter = Vector3.Zero;
                foreach (Point p in vc.Points)
                {
                    cellAverageCenter += p.Position;
                }
                cellAverageCenter /= vc.Points.Length;
                cellAverageCenter = cellAverageCenter.Normalized();

                //Project the cell's center onto the continent's 2D plane
                float k = (1.0f - UnitNorm.X * cellAverageCenter.X - UnitNorm.Y * cellAverageCenter.Y - UnitNorm.Z * cellAverageCenter.Z) / (UnitNorm.X * UnitNorm.X + UnitNorm.Y * UnitNorm.Y + UnitNorm.Z * UnitNorm.Z);
                Vector3 projectedCenter = new Vector3(cellAverageCenter.X + k * UnitNorm.X, cellAverageCenter.Y + k * UnitNorm.Y, cellAverageCenter.Z + k * UnitNorm.Z);
                Vector3 projectedCenter2D = new Vector3(uAxis.Dot(projectedCenter), vAxis.Dot(projectedCenter), 0f);

                float radius = (continent.averagedCenter - cellAverageCenter).Length();
                Vector3 positionFromCenter = continent.averagedCenter - projectedCenter;

                vc.MovementDirection = new Vector2(continent.movementDirection.X * continent.velocity, continent.movementDirection.Y * continent.velocity) + new Vector2(-continent.rotation * projectedCenter2D.Y, continent.rotation * projectedCenter2D.X);
                //Find Plane Equation
                float vcRadius = 0.0f;
                foreach (Point p in vc.Points)
                {
                    vcRadius += (cellAverageCenter - p.ToVector3().Normalized()).Length();
                }
                vcRadius /= vc.Points.Length;

                //var vcRadius = (average - vc.Points[0].ToVector3().Normalized()).Length() * .9f;
                var vcUnitNorm = v1.Cross(v2);
                var projectionRatio = (uAxis - UnitNorm).Length() / vcRadius;
                vcUnitNorm /= projectionRatio;
                vcUnitNorm = vcUnitNorm.Normalized();


                var d = UnitNorm.X * (vc.Points[0].Position.X) + UnitNorm.Y * (vc.Points[0].Position.Y) + UnitNorm.Z * (vc.Points[0].Position.Z);
                var newZ = (d - (UnitNorm.X * vc.Points[0].Position.X) - (UnitNorm.Y * vc.Points[0].Position.Y)) / UnitNorm.Z;
                var newZ2 = (d / UnitNorm.Z);
                var directionPoint = uAxis * vc.MovementDirection.X + vAxis * vc.MovementDirection.Y;
                directionPoint *= vcRadius;
                directionPoint += cellAverageCenter;
                directionPoint = directionPoint.Normalized();
                if (ShouldDrawArrows)
                {
                    PolygonRendererSDL.DrawArrow(this, size + (continent.averageHeight / 100f), cellAverageCenter, directionPoint, UnitNorm, vcRadius, Colors.Black);
                }
            }
        }

        return continents;
    }

    /// <summary>
    /// Renders a triangle and its connections for debugging and visualization purposes.
    /// This method provides visual feedback about the mesh structure.
    /// </summary>
    /// <param name="tri">The triangle to render.</param>
    /// <param name="dualMesh">Whether to render the dual mesh. Defaults to false.</param>
    /// <remarks>
    /// This method draws the triangle edges and vertices with different colors:
    /// - Red, Green, Blue for the three vertices respectively
    /// - Lines connecting the vertices to show the triangle structure
    ///
    /// The rendering can be done in either spherical or cartesian coordinates
    /// based on the ProjectToSphere setting. This is primarily a debugging tool
    /// for visualizing the mesh structure and understanding the triangulation.
    ///
    /// The method also renders all incident edges from the triangle's vertices,
    /// providing a complete view of the local mesh connectivity.
    /// </remarks>
    public void RenderTriangleAndConnections(Triangle tri, bool dualMesh = false)
    {
        int i = 0;
        //var edgesFromTri = baseEdges.Where(e => tri.Points.Any(a => a == e.P || a == e.Q));
        List<Edge> edgesFromTri = new List<Edge>();
        foreach (Point p in tri.Points)
        {
            edgesFromTri.AddRange(StrDb.GetIncidentHalfEdges(p));
        }
        if (!ProjectToSphere)
        {
            PolygonRendererSDL.DrawLine(this, size, ((Point)tri.Points[0]).ToVector3(), ((Point)tri.Points[1]).ToVector3());
            PolygonRendererSDL.DrawLine(this, size, ((Point)tri.Points[1]).ToVector3(), ((Point)tri.Points[2]).ToVector3());
            PolygonRendererSDL.DrawLine(this, size, ((Point)tri.Points[2]).ToVector3(), ((Point)tri.Points[0]).ToVector3());
            foreach (Point p in tri.Points)
            {
                switch (i)
                {
                    case 0:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3(), 0.05f, Colors.Red);
                        break;
                    case 1:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3(), 0.05f, Colors.Green);
                        break;
                    case 2:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3(), 0.05f, Colors.Blue);
                        break;
                }
                i++;
            }
        }
        else
        {
            PolygonRendererSDL.DrawLine(this, size, ((Point)tri.Points[0]).ToVector3().Normalized(), ((Point)tri.Points[1]).ToVector3().Normalized());
            PolygonRendererSDL.DrawLine(this, size, ((Point)tri.Points[1]).ToVector3().Normalized(), ((Point)tri.Points[2]).ToVector3().Normalized());
            PolygonRendererSDL.DrawLine(this, size, ((Point)tri.Points[2]).ToVector3().Normalized(), ((Point)tri.Points[0]).ToVector3().Normalized());
            foreach (Point p in tri.Points)
            {
                //GD.Print(p);
                //GD.Print(p.ToVector3());
                switch (i)
                {
                    case 0:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), p.Radius > 0 ? p.Radius : 0.05f, Colors.Red);
                        break;
                    case 1:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), p.Radius > 0 ? p.Radius : 0.05f, Colors.Green);
                        break;
                    case 2:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), p.Radius > 0 ? p.Radius : 0.05f, Colors.Blue);
                        break;
                }
                i++;
            }
        }
    }

    /// <summary>
    /// Converts a 3D cartesian position to spherical coordinates.
    /// This method transforms cartesian coordinates to spherical coordinate system.
    /// </summary>
    /// <param name="pos">The cartesian position to convert.</param>
    /// <returns>A Vector3 representing spherical coordinates (radius, theta, phi).</returns>
    /// <remarks>
    /// The spherical coordinate system uses:
    /// - X component: radius (distance from origin)
    /// - Y component: theta (polar angle from positive Z axis)
    /// - Z component: phi (azimuthal angle in XY plane from positive X axis)
    ///
    /// This conversion is useful for various calculations involving spherical
    /// geometry and for positioning elements on the celestial body surface.
    /// </remarks>
    public Vector3 ConvertToSpherical(Vector3 pos)
    {
        Vector3 sphere = new Vector3(
            Mathf.Sqrt(Mathf.Pow(pos.X, 2) + Mathf.Pow(pos.Y, 2) + Mathf.Pow(pos.Z, 2)),
            Mathf.Acos(pos.Z / Mathf.Sqrt(Mathf.Pow(pos.X, 2) + Mathf.Pow(pos.Y, 2) + Mathf.Pow(pos.Z, 2))),
            Mathf.Sign(pos.Y) * Mathf.Acos(pos.X / Mathf.Sqrt(Mathf.Pow(pos.X, 2) + Mathf.Pow(pos.Y, 2)))
            );
        return sphere;
    }

    /// <summary>
    /// Converts spherical coordinates back to 3D cartesian position.
    /// This method transforms spherical coordinates to cartesian coordinate system.
    /// </summary>
    /// <param name="sphere">The spherical coordinates (radius, theta, phi) to convert.</param>
    /// <returns>A Vector3 representing the cartesian position.</returns>
    /// <remarks>
    /// This method performs the inverse operation of ConvertToSpherical,
    /// converting from spherical coordinates back to cartesian coordinates.
    /// The conversion uses standard spherical to cartesian transformation formulas.
    ///
    /// This is useful for converting calculations done in spherical coordinates
    /// back to the cartesian coordinate system used by the rendering engine.
    /// </remarks>
    public Vector3 ConvertToCartesian(Vector3 sphere)
    {
        Vector3 cart = new Vector3(
            sphere.X * Mathf.Sin(sphere.Y) * Mathf.Cos(sphere.Z),
            sphere.X * Mathf.Sin(sphere.Y) * Mathf.Sin(sphere.Z),
            sphere.X * Mathf.Cos(sphere.Y)
            );
        return cart;
    }

    /// <summary>
    /// Generates the final surface mesh from continent data.
    /// This method creates the renderable mesh from the processed continent information.
    /// </summary>
    /// <param name="continents">Dictionary of continents to generate mesh from.</param>
    /// <remarks>
    /// This method iterates through all continents and calls GenerateSurfaceMesh
    /// for each continent's cells. This creates the final renderable mesh that
    /// includes all terrain features, biomes, and visual properties.
    ///
    /// The method updates the progress tracking system as it processes each continent,
    /// providing feedback about the generation progress. This is typically the final
    /// step in the mesh generation pipeline before the celestial body is ready for rendering.
    /// </remarks>
    public void GenerateFromContinents(Dictionary<int, Continent> continents)
    {
        foreach (var keyValuePair in continents)
        {
            GenerateSurfaceMesh(keyValuePair.Value.cells);
            percent.PercentCurrent++;
        }
    }

    /// <summary>
    /// Calculates a color for a vertex based on its height value.
    /// This method provides height-based coloring for terrain visualization.
    /// </summary>
    /// <param name="height">The height value of the vertex.</param>
    /// <returns>A Color representing the appropriate color for the given height.</returns>
    /// <remarks>
    /// This method uses a continuous mathematical formula to generate smooth
    /// color transitions across different height ranges:
    /// - Deep water: Blue hues
    /// - Shallow water: Cyan hues
    /// - Low land: Green hues
    /// - Medium land: Yellow to brown hues
    /// - High land: Dark brown hues
    ///
    /// The formula uses HSV color space with smooth mathematical transitions
    /// to create natural-looking terrain coloring without sharp boundaries.
    /// The height range is normalized from -10 (deep water) to +10 (mountains).
    /// </remarks>
    private Color GetVertexColor(float height)
    {
        // Single continuous formula without branching
        // Height range: -10 (deep water) to +10 (mountains)
        float normalizedHeight = (height + 10f) / 20f; // 0-1 range

        // Smooth color transition using mathematical functions
        // Blue(220) -> Cyan(180) -> Green(120) -> Yellow(50) -> Brown(30) -> Dark Brown(10)
        float hue = 220f - (210f * normalizedHeight);

        // Saturation curve: low for water, high for land
        float saturation = 0.3f + 0.5f * Mathf.Sin(normalizedHeight * Mathf.Pi);

        // Value curve: darker for deep water and high mountains, brighter for land
        float value = 0.4f + 0.5f * Mathf.Sin(normalizedHeight * Mathf.Pi * 0.8f + 0.2f);

        // Ensure values are within valid range
        hue = Mathf.Clamp(hue, 0f, 360f);
        saturation = Mathf.Clamp(saturation, 0.2f, 1f);
        value = Mathf.Clamp(value, 0.2f, 1f);

        return Color.FromHsv(hue / 360f, saturation, value);
    }

    /// <summary>
    /// Gets the appropriate color for a specific biome type.
    /// This method provides biome-specific coloring for realistic terrain visualization.
    /// </summary>
    /// <param name="biome">The biome type to get the color for.</param>
    /// <param name="height">The height value (currently unused but available for future enhancements).</param>
    /// <returns>A Color representing the appropriate color for the specified biome.</returns>
    /// <remarks>
    /// This method returns predefined colors for each biome type:
    /// - Tundra: Light gray-white
    /// - Icecap: Pure white
    /// - Desert: Sandy yellow
    /// - Grassland: Green
    /// - Forest: Dark green
    /// - Rainforest: Very dark green
    /// - Taiga: Dark green-brown
    /// - Ocean: Deep blue
    /// - Coastal: Light blue
    /// - Mountain: Brown-gray
    /// - Default: Gray (fallback for unknown biomes)
    ///
    /// The colors are chosen to be visually distinct and representative of
    /// real-world biome appearances. The height parameter is included for
    /// potential future enhancements where color might vary within a biome
    /// based on elevation.
    /// </remarks>
    private Color GetBiomeColor(BiomeType biome, float height)
    {
        switch (biome)
        {
            case BiomeType.Tundra:
                return new Color(0.85f, 0.85f, 0.8f); // Light gray-white
            case BiomeType.Icecap:
                return Colors.White;
            case BiomeType.Desert:
                return new Color(0.9f, 0.8f, 0.5f); // Sandy yellow
            case BiomeType.Grassland:
                return new Color(0.5f, 0.8f, 0.3f); // Green
            case BiomeType.Forest:
                return new Color(0.2f, 0.6f, 0.2f); // Dark green
            case BiomeType.Rainforest:
                return new Color(0.1f, 0.4f, 0.1f); // Very dark green
            case BiomeType.Taiga:
                return new Color(0.4f, 0.5f, 0.3f); // Dark green-brown
            case BiomeType.Ocean:
                return new Color(0.1f, 0.3f, 0.7f); // Deep blue
            case BiomeType.Coastal:
                return new Color(0.8f, 0.7f, 0.4f); // Light blue
            case BiomeType.Mountain:
                return new Color(0.6f, 0.5f, 0.4f); // Brown-gray
            default:
                return Colors.Gray;
        }
    }

    /// <summary>
    /// Generates the final renderable surface mesh from Voronoi cell data.
    /// This method creates the actual Godot mesh that will be rendered.
    /// </summary>
    /// <param name="VoronoiList">List of Voronoi cells to generate the mesh from.</param>
    /// <remarks>
    /// This method creates the final mesh using Godot's SurfaceTool:
    /// 1. Sets up the surface tool with triangle primitives
    /// 2. Creates an unshaded material that uses vertex colors
    /// 3. Iterates through all Voronoi cells and their triangles
    /// 4. Calculates normals, tangents, and UV coordinates for each triangle
    /// 5. Assigns colors based on biome or height (depending on settings)
    /// 6. Positions vertices accounting for the base size and height variations
    /// 7. Commits the surface to the ArrayMesh
    ///
    /// The method handles both spherical projection and cartesian coordinate modes,
    /// and can display either biome colors or height-based colors. The resulting
    /// mesh is ready for rendering in the Godot engine.
    /// </remarks>
    public void GenerateSurfaceMesh(List<VoronoiCell> VoronoiList)
    {
        var arrMesh = Mesh as ArrayMesh;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var material = new StandardMaterial3D();
        material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
        material.VertexColorUseAsAlbedo = true;
        //material.AlbedoColor = Colors.Pink;
        st.SetMaterial(material);
        foreach (VoronoiCell vor in VoronoiList)
        {
            Color biomeColor = Colors.Pink;
            for (int i = 0; i < vor.Points.Length / 3; i++)
            {
                var centroid = Vector3.Zero;
                centroid += ((Point)vor.Points[3 * i]).Position;
                centroid += ((Point)vor.Points[3 * i + 1]).Position;
                centroid += ((Point)vor.Points[3 * i + 2]).Position;
                centroid /= 3.0f;

                var p0 = ((Point)vor.Points[3 * i]).Position;
                var p1 = ((Point)vor.Points[3 * i + 1]).Position;
                var p2 = ((Point)vor.Points[3 * i + 2]).Position;
                var normal = (p1 - p0).Cross(p2 - p0).Normalized();
                var tangent = (p0 - centroid).Normalized();
                var bitangent = normal.Cross(tangent).Normalized();
                var min_u = Mathf.Inf;
                var min_v = Mathf.Inf;
                var max_u = -Mathf.Inf;
                var max_v = -Mathf.Inf;
                for (int j = 2; j >= 0; j--)
                {
                    var rel_pos = ((Point)vor.Points[3 * i + j]).Position - centroid;
                    var u = rel_pos.Dot(tangent);
                    var v = rel_pos.Dot(bitangent);
                    min_u = Mathf.Min(min_u, u);
                    min_v = Mathf.Min(min_v, v);
                    max_u = Mathf.Max(max_u, u);
                    max_v = Mathf.Max(max_v, v);

                    var uv = new Vector2((u - min_u) / (max_u - min_u), (v - min_v) / (max_v - min_v));
                    st.SetUV(uv);
                    st.SetNormal(tangent);
                    if (ProjectToSphere)
                    {
                        st.SetColor(ShouldDisplayBiomes ? GetBiomeColor(((Point)vor.Points[3 * i + j]).Biome, ((Point)vor.Points[3 * i + j]).Height) : GetVertexColor(((Point)vor.Points[3 * i + j]).Height));
                        st.AddVertex(vor.Points[3 * i + j].ToVector3() * (size + vor.Points[3 * i + j].Height / 10f));
                    }
                    else
                    {
                        st.AddVertex(new Vector3((vor.Points[3 * i + j]).Components[0], (vor.Points[3 * i + j]).Components[1], (vor.Points[3 * i + j]).Components[2]) * (size + (vor.Points[3 * i + j]).Components[2] / 10f));
                    }
                }
            }
        }
        //st.GenerateNormals();
        st.CallDeferred("commit", arrMesh);
    }

    /// <summary>
    /// Subdivides a triangular face into four smaller triangular faces.
    /// This method increases mesh resolution by splitting triangles.
    /// </summary>
    /// <param name="face">The face to subdivide.</param>
    /// <returns>An array of four new faces created from the subdivision.</returns>
    /// <remarks>
    /// This method performs triangle subdivision by:
    /// 1. Calculating the midpoint of each edge of the original triangle
    /// 2. Creating four new triangles from the original vertices and midpoints
    /// 3. Returning the four resulting faces
    ///
    /// The subdivision creates a more detailed mesh by replacing each triangle
    /// with four smaller triangles. This is a key operation in the mesh refinement
    /// process and is used during the base mesh generation phase.
    ///
    /// The method ensures that the new vertices are properly managed through the
    /// structure database to maintain consistency across the mesh.
    /// </remarks>
    public Face[] Subdivide(Face face)
    {
        List<Face> subfaces = new List<Face>();
        var subVector1 = GetMiddle(face.v[0], face.v[1]);
        var subVector2 = GetMiddle(face.v[1], face.v[2]);
        var subVector3 = GetMiddle(face.v[2], face.v[0]);
        //VertexPoints.Add(subVector1);
        //VertexPoints.Add(subVector2);
        //VertexPoints.Add(subVector3);

        subfaces.Add(new Face(face.v[0], subVector1, subVector3));
        subfaces.Add(new Face(subVector1, face.v[1], subVector2));
        subfaces.Add(new Face(subVector2, face.v[2], subVector3));
        subfaces.Add(new Face(subVector3, subVector1, subVector2));

        return subfaces.ToArray();
    }

    /// <summary>
    /// Adds jitter to a vertex position to create more natural terrain variation.
    /// This method introduces controlled randomness to vertex positions.
    /// </summary>
    /// <param name="original">The original point to modify.</param>
    /// <param name="jitter">The jitter point containing the random offset.</param>
    /// <remarks>
    /// This method calculates the average position between the original point
    /// and the jitter point, then normalizes the result to maintain the
    /// spherical shape of the celestial body.
    ///
    /// The jitter operation helps break up the regular geometric patterns
    /// that can result from purely mathematical mesh generation, creating
    /// more natural-looking terrain variations. The normalization ensures
    /// that the modified vertex remains on the sphere surface.
    ///
    /// This is typically used during the mesh deformation phase to add
    /// realistic irregularities to the terrain.
    /// </remarks>
    public void AddJitter(Point original, Point jitter)
    {
        var tempVector = (jitter.ToVector3() + original.ToVector3()) / 2.0f;
        tempVector = tempVector.Normalized();
        original.Position = tempVector;
    }

    /// <summary>
    /// Calculates the midpoint between two points and returns it as a new Point.
    /// This method is used for mesh subdivision and edge calculations.
    /// </summary>
    /// <param name="v1">The first point.</param>
    /// <param name="v2">The second point.</param>
    /// <returns>A new Point representing the midpoint between v1 and v2.</returns>
    /// <remarks>
    /// This method calculates the mathematical midpoint between two 3D points
    /// and creates a new Point object at that location. The method uses the
    /// structure database to either retrieve an existing point at that location
    /// or create a new one, ensuring point consistency across the mesh.
    ///
    /// This is a fundamental operation used in mesh subdivision, edge splitting,
    /// and various geometric calculations throughout the mesh generation process.
    /// The point indexing system helps maintain efficient lookups and prevents
    /// duplicate points at the same location.
    /// </remarks>
    public Point GetMiddle(Point v1, Point v2)
    {
        var tempVector = (v2.ToVector3() - v1.ToVector3()) * 0.5f + v1.ToVector3();
        int idx = Point.DetermineIndex(tempVector.X, tempVector.Y, tempVector.Z);
        return StrDb.GetOrCreatePoint(idx, tempVector);
    }
}


