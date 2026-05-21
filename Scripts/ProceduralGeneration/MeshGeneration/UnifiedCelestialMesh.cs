using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ProceduralGeneration.BiomeSystem;
using ProceduralGeneration.ColorSystem;
using ProceduralGeneration.MeshGeneration.ResourceGeneration;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.Enums;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.TaskSystem;

/// <summary>
/// Unified celestial body mesh generation system that consolidates functionality from CelestialBodyMesh and SatelliteBodyMesh.
/// This class provides a single interface for generating various types of celestial bodies with different generation pipelines.
/// </summary>
/// <remarks>
/// The generation process involves multiple phases that can be dynamically selected based on configuration:
/// 1. Base mesh generation using subdivided icosahedron
/// 2. Optional mesh deformation for realistic terrain
/// 3. Optional non-uniform scaling for ellipsoidal shapes
/// 4. Optional noise-based deformation for irregular surfaces
/// 5. Voronoi cell generation for continent formation
/// 6. Optional tectonic simulation with stress calculations
/// 7. Biome assignment based on height and moisture
/// 8. Final mesh rendering with appropriate materials
///
/// This class extends Godot's MeshInstance3D to provide a complete celestial body generation solution
/// that can be used directly in Godot scenes. The generation process is asynchronous to prevent
/// blocking the main thread during complex calculations.
/// </remarks>
namespace ProceduralGeneration.MeshGeneration;

public partial class UnifiedCelestialMesh : MeshInstance3D
{
    /// <summary>
    /// Enumeration defining different generation pipeline types for celestial bodies.
    /// Each type represents a specific combination of features and processing steps.
    /// </summary>
    public enum BodyGenerationType
    {
        /// <summary>
        /// Generate using only tectonic processes without additional noise or scaling.
        /// Suitable for standard planets with realistic terrain features.
        /// </summary>
        TectonicsOnly,

        /// <summary>
        /// Generate using tectonic processes combined with noise-based surface deformation.
        /// Creates more varied and detailed terrain while maintaining tectonic structure.
        /// </summary>
        TectonicsWithNoise,

        /// <summary>
        /// Generate using non-uniform scaling combined with noise-based deformation.
        /// Suitable for asteroids, moons, and irregular celestial bodies.
        /// </summary>
        ScalingWithNoise,

        /// <summary>
        /// Generate using only noise-based deformation without tectonic processes or scaling.
        /// Creates purely procedural terrain features.
        /// </summary>
        NoiseOnly,

        /// <summary>
        /// Generate using Spherical Harmonics global shape deformation combined with noise-based surface detail.
        /// Suitable for satellite bodies (moons, asteroids) with realistic large-scale shape variation
        /// and fine surface irregularities. SH deformation creates ellipsoidal/irregular base shapes
        /// while noise adds surface texture.
        /// </summary>
        SphericalHarmonicsWithNoise,
    }

    [Signal]
    public delegate void GeneratedVoronoiCellsEventHandler();

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
    /// Random number generator for procedural generation.
    /// Provides deterministic random values for consistent terrain and feature generation.
    /// </summary>
    protected RandomNumberGenerator rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();

    /// <summary>
    /// Structure database containing all mesh data and relationships.
    /// Central repository for all mesh structures including vertices, edges, faces, and their relationships.
    /// </summary>
    public StructureDatabase? StrDb;

    /// <summary>
    /// Tectonic generation system for simulating plate tectonics.
    /// Handles the simulation of tectonic plate movement, stress calculations, and terrain deformation.
    /// </summary>
    protected TectonicGeneration? tectonics;

    private Octree<Point>? _octree;
    private String? _name;

    /// <summary>
    /// Dictionary of continents indexed by their starting cell index.
    /// Populated after mesh generation with tectonics enabled.
    /// </summary>
    public Dictionary<int, Continent>? Continents { get; private set; }

    /// <summary>
    /// The detected generation type based on configuration parameters.
    /// Determines which pipeline will be used for mesh generation.
    /// </summary>
    public BodyGenerationType GenerationType { get; private set; }

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
    public float size = 5;

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
    /// Whether to use cell-based biome for mesh coloring instead of point-based biome.
    /// When enabled, the biome of each Voronoi cell is used for coloring the entire cell.
    /// When disabled, each point retains its individual biome for coloring.
    /// </summary>
    /// <remarks>
    /// Cell-based biome provides more consistent coloring across cells and better reflects
    /// the dominant biome type for gameplay purposes like resource generation.
    /// Default is false to maintain backward compatibility with existing point-based system.
    /// </remarks>
    [Export]
    public bool UseCellBiomeForColoring = true;

    /// <summary>
    /// The body classification used to select biome, color, and resource assigners.
    /// Set by CelestialBody/SatelliteBody after construction.
    /// </summary>
    public BodyClassification? Classification { get; set; }
    private IBiomeAssigner? _biomeAssigner;
    private IColorMapper? _colorMapper;

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

    [ExportCategory("Asteroid Generation")]
    [ExportGroup("Scaling Settings")]
    /// <summary>
    /// Scale factors for non-uniform scaling along X, Y, Z axes.
    /// Values greater than 1 elongate the axis, less than 1 compress it.
    /// Example: (2.0f, 0.5f, 1.0f) creates an elongated ellipsoid.
    /// </summary>
    [Export]
    public Vector3 ScaleFactors { get; set; } = new Vector3(1.0f, 1.0f, 1.0f);

    [ExportGroup("Noise Settings")]
    /// <summary>
    /// Amplitude of noise displacement. Higher values create more exaggerated surface irregularities.
    /// Typical range: 0.1f (subtle) to 0.5f (extreme).
    /// </summary>
    [Export]
    public float NoiseAmplitude { get; set; } = 0.2f;

    /// <summary>
    /// Frequency of the noise pattern. Lower values create broader features, higher values create finer details.
    /// Typical range: 1.0f (broad) to 5.0f (fine).
    /// </summary>
    [Export]
    public float NoiseFrequency { get; set; } = 2.0f;

    /// <summary>
    /// Number of octaves for fractal noise. More octaves add complexity and detail to the surface.
    /// Typical range: 1 (simple) to 6 (highly detailed).
    /// </summary>
    [Export]
    public int NoiseOctaves { get; set; } = 4;

    /// <summary>
    /// Random seed for noise generation. Set to 0 for random seed.
    /// Ensures reproducible noise patterns for consistent asteroid shapes.
    /// </summary>
    [Export]
    public int NoiseSeed { get; set; } = 0;

    /// <summary>
    /// Lacunarity for fractal noise. Controls how much frequency increases per octave.
    /// Standard FBM value is 2.0. Higher values create more detailed high-frequency features.
    /// </summary>
    [Export]
    public float NoiseLacunarity { get; set; } = 2.0f;

    /// <summary>
    /// Gain for fractal noise. Controls how much amplitude decreases per octave.
    /// Standard FBM value is 0.5. Values above 1.0 cause amplitude to grow (chaotic patterns).
    /// </summary>
    [Export]
    public float NoiseGain { get; set; } = 0.5f;

    /// <summary>
    /// Noise generator instance for procedural deformation.
    /// Uses Godot's FastNoiseLite for efficient 3D noise computation.
    /// </summary>
    private FastNoiseLite? noise;

    /// <summary>
    /// Spherical Harmonics deformer for global shape deformation.
    /// Used by SphericalHarmonicsWithNoise pipeline for satellite bodies.
    /// </summary>
    private SphericalHarmonicsDeformer? _shDeformer;

    /// <summary>
    /// Amplitude for Spherical Harmonics deformation. Controls the magnitude
    /// of the global shape variation. Default: 0.3f.
    /// </summary>
    private float _shAmplitude = 0.3f;

    protected Dictionary<string, ResourceDeposit>? _satelliteResources;

    /// <summary>
    /// Gets the satellite-level resource deposits for this body.
    /// Only populated for satellite-type bodies (NoiseOnly/ScalingWithNoise generation).
    /// </summary>
    public Dictionary<string, ResourceDeposit>? SatelliteResources => _satelliteResources;

    /// <summary>
    /// Per-instance ShaderMaterial instances for the cell selection highlight shader.
    /// One per surface (continent) in the ArrayMesh. Used to set the selected_cell_id
    /// uniform independently per body.
    /// </summary>
    private List<ShaderMaterial> _cellHighlightMaterials = new List<ShaderMaterial>();
    private List<ShaderMaterial> _continentHighlightMaterials = new List<ShaderMaterial>();

    private Image? _placementImage;
    private ImageTexture? _placementTexture;
    private byte[]? _placementBuffer;
    private int _placementWidth;
    private int _placementHeight;

    /// <summary>
    /// Gets the list of cell highlight shader materials for this mesh.
    /// Each entry corresponds to one surface in the ArrayMesh.
    /// Use <see cref="SetSelectedCell"/> to update the selected cell across all surfaces.
    /// </summary>
    public IReadOnlyList<ShaderMaterial> CellHighlightMaterials => _cellHighlightMaterials;
    public IReadOnlyList<ShaderMaterial> ContinentHighlightMaterials =>
        _continentHighlightMaterials;

    /// <summary>
    /// Sets the selected cell ID on all cell highlight materials for this mesh.
    /// Pass -1.0f to clear the selection.
    /// </summary>
    /// <param name="cellId">The VoronoiCell.Index to highlight, or -1.0f for no selection.</param>
    public void SetSelectedCell(float cellId)
    {
        foreach (var mat in _cellHighlightMaterials)
        {
            mat.SetShaderParameter("selected_cell_id", cellId);
        }
    }

    /// <summary>
    /// Uploads per-cell highlight codes for the placement preview, replacing the
    /// previous fixed-size uniform arrays. The buffer is indexed by
    /// <c>VoronoiCell.Index</c>. Codes: 0 = none, 1 = footprint valid, 2 = footprint
    /// invalid, 3 = new coverage, 4 = dominant existing grid, 5 = absorbed existing
    /// grid (see <c>cell_selection_highlight.gdshader</c>).
    /// </summary>
    public void SetPlacementHighlightData(byte[] perCellCode, int width, int height)
    {
        if (_placementImage == null || _placementWidth != width || _placementHeight != height)
        {
            _placementImage = Image.CreateFromData(width, height, false, Image.Format.R8, perCellCode);
            if (_placementTexture == null)
                _placementTexture = ImageTexture.CreateFromImage(_placementImage);
            else
                _placementTexture.SetImage(_placementImage);
            _placementWidth = width;
            _placementHeight = height;
        }
        else
        {
            _placementImage.SetData(width, height, false, Image.Format.R8, perCellCode);
            _placementTexture!.Update(_placementImage);
        }

        var size = new Vector2I(width, height);
        foreach (var mat in _cellHighlightMaterials)
        {
            mat.SetShaderParameter("placement_cell_data", _placementTexture);
            mat.SetShaderParameter("placement_data_size", size);
            mat.SetShaderParameter("placement_data_active", true);
        }
    }

    /// <summary>
    /// Returns (and lazily allocates) a reusable byte buffer sized to hold one code
    /// per Voronoi cell on this mesh. Callers receive a zeroed buffer they can
    /// populate and pass back to <see cref="SetPlacementHighlightData"/>.
    /// </summary>
    public byte[] AcquirePlacementBuffer(out int width, out int height)
    {
        int cellCount = StrDb?.CellCount ?? 0;
        if (cellCount <= 0)
        {
            width = 0;
            height = 0;
            return System.Array.Empty<byte>();
        }
        width = Mathf.Min(cellCount, 4096);
        height = (cellCount + width - 1) / width;
        int needed = width * height;
        if (_placementBuffer == null || _placementBuffer.Length != needed)
            _placementBuffer = new byte[needed];
        else
            System.Array.Clear(_placementBuffer, 0, _placementBuffer.Length);
        return _placementBuffer;
    }

    /// <summary>
    /// Enables or disables the pulse animation on the cell selection highlight.
    /// Used by CellViewPanel to animate the selected cell while the info window is open.
    /// </summary>
    public void SetPulseEnabled(bool enabled)
    {
        foreach (var mat in _cellHighlightMaterials)
        {
            mat.SetShaderParameter("pulse_enabled", enabled);
        }
    }

    /// <summary>
    /// Clears placement highlighting, returning to normal selection mode. Keeps the
    /// data texture allocated for reuse on the next placement preview.
    /// </summary>
    public void ClearPlacementHighlight()
    {
        foreach (var mat in _cellHighlightMaterials)
        {
            mat.SetShaderParameter("placement_data_active", false);
        }
    }

    /// <summary>
    /// Highlights the continent with the given continent index across all materials.
    /// </summary>
    /// <param name="continentIndex">The continent index to highlight (-1 to clear).</param>
    public void SetSelectedContinent(int continentIndex)
    {
        foreach (var mat in _continentHighlightMaterials)
        {
            mat.SetShaderParameter("selected_continent_index", (float)continentIndex);
        }
    }

    /// <summary>
    /// Clears continent selection highlighting on all materials.
    /// </summary>
    public void ClearSelectedContinent()
    {
        foreach (var mat in _continentHighlightMaterials)
        {
            mat.SetShaderParameter("selected_continent_index", -1.0f);
        }
    }

    // Pre-loaded shader resources (loaded on main thread in constructor, safe for background use)
    private static Shader? _rockyPlanetShader;
    private static ShaderMaterial? _outlinerMaterialBase;
    private static ShaderMaterial? _cellHighlightMaterialBase;
    private static ShaderMaterial? _continentHighlightMaterialBase;

    /// <summary>
    /// Ensures the shared shader resources are loaded (must be called on main thread).
    /// Uses lazy initialization so resources are only loaded once across all instances.
    /// </summary>
    private static void EnsureShaderResourcesLoaded()
    {
        _rockyPlanetShader ??= GD.Load<Shader>("res://Shaders/rocky_planet_shader.gdshader");
        _outlinerMaterialBase ??= GD.Load<ShaderMaterial>(
            "res://Materials/voronoi_outliner_material.tres"
        );
        _cellHighlightMaterialBase ??= GD.Load<ShaderMaterial>(
            "res://Materials/cell_selection_highlight_material.tres"
        );
        _continentHighlightMaterialBase ??= GD.Load<ShaderMaterial>(
            "res://Materials/continent_selection_highlight_material.tres"
        );
    }

    /// <summary>
    /// Creates per-instance material duplicates for one surface of the mesh.
    /// Must be called on the main thread (called from constructor or _Ready).
    /// The returned ShaderMaterial is the root material to set on the SurfaceTool.
    /// </summary>
    private ShaderMaterial CreateSurfaceMaterials()
    {
        EnsureShaderResourcesLoaded();

        var material = new ShaderMaterial();
        material.Shader = _rockyPlanetShader;

        // Duplicate all overlay materials so each body/surface has independent uniforms.
        var outlinerInstance = (ShaderMaterial)_outlinerMaterialBase!.Duplicate();
        var cellHighlightInstance = (ShaderMaterial)_cellHighlightMaterialBase!.Duplicate();
        cellHighlightInstance.SetShaderParameter("selected_cell_id", -1.0f);
        _cellHighlightMaterials.Add(cellHighlightInstance);

        var continentHighlightInstance = (ShaderMaterial)
            _continentHighlightMaterialBase!.Duplicate();
        continentHighlightInstance.SetShaderParameter("continent_selected", false);
        _continentHighlightMaterials.Add(continentHighlightInstance);

        // Chain: rocky_planet_shader -> voronoi_outliner -> cell_selection_highlight -> continent_selection_highlight
        cellHighlightInstance.NextPass = continentHighlightInstance;
        outlinerInstance.NextPass = cellHighlightInstance;
        material.NextPass = outlinerInstance;

        return material;
    }

    [ExportCategory("Thread Pool Settings")]
    [Export]
    public bool UseThreadPool = true;

    [Export]
    public TaskPriority TaskPriority = TaskPriority.High;

    public UnifiedCelestialMesh()
    {
        // Pre-load shader resources on the main thread so they're cached
        // for later use by GenerateSurfaceMesh (which may run on a background thread).
        EnsureShaderResourcesLoaded();

        // Initialize noise generator
        noise = new FastNoiseLite();
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        noise.FractalOctaves = NoiseOctaves;
        noise.FractalLacunarity = NoiseLacunarity;
        noise.FractalGain = NoiseGain;
    }

    public void StartMeshGeneration(
        IOrbitalBody parent,
        Octree<Point> oct,
        Action<UnifiedCelestialMesh> onCompleted,
        Action<UnifiedCelestialMesh, string> onFailed
    )
    {
        this.CallDeferred("set_mesh", new ArrayMesh());

        _octree = oct;

        if (
            GenerationType == BodyGenerationType.TectonicsOnly
            || GenerationType == BodyGenerationType.TectonicsWithNoise
        )
        {
            tectonics = new TectonicGeneration(
                StrDb!,
                rand,
                StressScale,
                ShearScale,
                MaxPropagationDistance,
                PropagationFalloff,
                InactiveStressThreshold,
                GeneralHeightScale,
                GeneralShearScale,
                GeneralCompressionScale
            );
        }

        QueueMeshGenerationPackage(
            parent,
            oct,
            () => OnMeshGenerationComplete(onCompleted),
            (error) => OnMeshGenerationFailed(onFailed, error)
        );
    }

    private void QueueMeshGenerationPackage(
        IOrbitalBody parent,
        Octree<Point> oct,
        Action onCompleted,
        Action<string> onFailed
    )
    {
        var builder = new WorkPackageBuilder()
            .WithName(_name!)
            .WithPriority(UtilityLibrary.TaskSystem.TaskPriority.High);

        AddFirstPassSteps(builder);
        builder.AddStep(
            "IncrementMeshState",
            () =>
            {
                StrDb!.IncrementMeshState();
                return 0;
            }
        );
        AddSecondPassSteps(builder, oct);
        AddTexturePassSteps(builder, parent);

        builder.OnCompleted(name =>
        {
            SignalBus.Instance?.CallDeferred("EmitStopTimer", name);
            onCompleted?.Invoke();
        });

        builder.OnFailed(
            (name, error) =>
            {
                SignalBus.Instance?.CallDeferred("EmitStopTimer", name);
                onFailed?.Invoke(error);
            }
        );

        var package = builder.Build();
        SignalBus.Instance!.EmitQueuePackage(package);
    }

    private void AddFirstPassSteps(WorkPackageBuilder builder)
    {
        BaseMeshGeneration baseMesh = new BaseMeshGeneration(
            rand,
            StrDb!,
            subdivide,
            VerticesPerEdge,
            this
        );
        builder.AddStep(
            "PopulateArrays",
            () =>
            {
                GameLogger.EnterFunction("PopulateArrays", "");
                try
                {
                    baseMesh.PopulateArrays();
                    GameLogger.ExitFunction(
                        "PopulateArrays",
                        $"Vertices: {StrDb!.BaseVertices.Count}, Edges: {StrDb!.UsedEdges.Count}"
                    );
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error($"PopulateArrays Error: {e.Message}\n{e.StackTrace}");
                    GD.PrintErr($"PopulateArrays Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );

        builder.AddStep(
            "GenerateNonDeformedFaces",
            () =>
            {
                GameLogger.EnterFunction("GenerateNonDeformedFaces", "");
                try
                {
                    baseMesh.GenerateNonDeformedFaces();
                    GameLogger.ExitFunction(
                        "GenerateNonDeformedFaces",
                        $"Triangles: {StrDb!.BaseTris.Count}, HalfEdges: {StrDb!.HalfEdgeById.Count}"
                    );
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error(
                        $"GenerateNonDeformedFaces Error: {e.Message}\n{e.StackTrace}"
                    );
                    GD.PrintErr($"GenerateNonDeformedFaces Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );

        builder.AddStep(
            "GenerateTriangleList",
            () =>
            {
                GameLogger.EnterFunction("GenerateTriangleList", "");
                try
                {
                    baseMesh.GenerateTriangleList();
                    GameLogger.ExitFunction(
                        "GenerateTriangleList",
                        $"Triangles: {StrDb!.Base.Triangles.Count}"
                    );
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error($"GenerateTriangleList Error: {e.Message}\n{e.StackTrace}");
                    GD.PrintErr($"GenerateTriangleList Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );

        builder.AddStep(
            "DeformMesh",
            () =>
            {
                GameLogger.EnterFunction(
                    "DeformMesh",
                    $"Cycles: {NumDeformationCycles}, Aberrations: {NumAbberations}"
                );
                try
                {
                    var OptimalArea = (4.0f * Mathf.Pi * size * size) / StrDb!.Base.Triangles.Count;
                    float OptimalSideLength =
                        Mathf.Sqrt((OptimalArea * 4.0f) / Mathf.Sqrt(3.0f)) / 3f;
                    baseMesh.InitiateDeformation(
                        NumDeformationCycles,
                        NumAbberations,
                        OptimalSideLength
                    );
                    GameLogger.ExitFunction(
                        "DeformMesh",
                        $"OptimalSideLength: {OptimalSideLength:F4}"
                    );
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error($"DeformMesh Error: {e.Message}\n{e.StackTrace}");
                    GD.PrintErr($"DeformMesh Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );
    }

    private void AddSecondPassSteps(WorkPackageBuilder builder, Octree<Point> oct)
    {
        builder.AddStep(
            "GenerateVoronoiCells",
            () =>
            {
                GameLogger.EnterFunction("GenerateVoronoiCells", "");
                try
                {
                    GenerateVoronoiCellsInternal(oct);
                    GameLogger.ExitFunction(
                        "GenerateVoronoiCells",
                        $"VoronoiCells: {StrDb!.VoronoiCells.Count}, Vertices: {StrDb!.VoronoiCellVertices.Count}"
                    );
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error($"GenerateVoronoiCells Error: {e.Message}\n{e.StackTrace}");
                    GD.PrintErr($"GenerateVoronoiCells Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );

        builder.AddStep(
            "FloodFillContinents",
            () =>
            {
                GameLogger.EnterFunction("FloodFillContinents", $"NumContinents: {NumContinents}");
                try
                {
                    var continents = FloodFillContinentsInternal();
                    GameLogger.ExitFunction(
                        "FloodFillContinents",
                        $"Continents: {continents?.Count ?? 0}"
                    );
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error($"FloodFillContinents Error: {e.Message}\n{e.StackTrace}");
                    GD.PrintErr($"FloodFillContinents Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );

        builder.AddStep(
            "CalculateBoundaryCells",
            () =>
            {
                GameLogger.EnterFunction("CalculateBoundaryCells", "");
                try
                {
                    if (Continents == null)
                    {
                        GameLogger.ExitFunction(
                            "CalculateBoundaryCells",
                            "Skipped - no continents"
                        );
                        return 0;
                    }
                    CalculateBoundaryCellsInternal(Continents);
                    GameLogger.ExitFunction(
                        "CalculateBoundaryCells",
                        $"Processed {StrDb!.VoronoiCells.Count} cells"
                    );
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error($"CalculateBoundaryCells Error: {e.Message}\n{e.StackTrace}");
                    GD.PrintErr($"CalculateBoundaryCells Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );

        builder.AddStep(
            "CalculateInteriorness",
            () =>
            {
                GameLogger.EnterFunction("CalculateInteriorness", "");
                try
                {
                    if (Continents == null)
                    {
                        GameLogger.ExitFunction("CalculateInteriorness", "Skipped - no continents");
                        return 0;
                    }
                    CalculateInteriornessInternal(Continents);
                    GameLogger.ExitFunction(
                        "CalculateInteriorness",
                        "Interiorness calculated for all cells"
                    );
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error($"CalculateInteriorness Error: {e.Message}\n{e.StackTrace}");
                    GD.PrintErr($"CalculateInteriorness Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );

        if (
            GenerationType == BodyGenerationType.TectonicsOnly
            || GenerationType == BodyGenerationType.TectonicsWithNoise
        )
        {
            builder.AddStep(
                "UpdateVertexHeights",
                () =>
                {
                    GameLogger.EnterFunction("UpdateVertexHeights", "");
                    try
                    {
                        if (Continents == null)
                        {
                            GameLogger.ExitFunction(
                                "UpdateVertexHeights",
                                "Skipped - no continents"
                            );
                            return 0;
                        }
                        UpdateVertexHeights(StrDb!.VoronoiCellVertices, Continents);
                        GameLogger.ExitFunction(
                            "UpdateVertexHeights",
                            $"Updated {StrDb!.VoronoiCellVertices.Count} vertices"
                        );
                        return 0;
                    }
                    catch (Exception e)
                    {
                        GameLogger.Error($"UpdateVertexHeights Error: {e.Message}\n{e.StackTrace}");
                        GD.PrintErr($"UpdateVertexHeights Error: {e.Message}\n{e.StackTrace}");
                        return 1;
                    }
                }
            );

            builder.AddStep(
                "CalculateBoundaryStress",
                () =>
                {
                    GameLogger.EnterFunction(
                        "CalculateBoundaryStress",
                        $"StressScale: {StressScale}, ShearScale: {ShearScale}"
                    );
                    try
                    {
                        if (Continents == null || tectonics == null)
                        {
                            GameLogger.ExitFunction(
                                "CalculateBoundaryStress",
                                "Skipped - no continents or tectonics"
                            );
                            return 0;
                        }
                        tectonics!.CalculateBoundaryStress(
                            StrDb!.Dual.EdgeCells,
                            StrDb!.VoronoiCellVertices,
                            Continents
                        );
                        GameLogger.ExitFunction(
                            "CalculateBoundaryStress",
                            $"Processed {Continents.Count} continents"
                        );
                        return 0;
                    }
                    catch (Exception e)
                    {
                        GameLogger.Error(
                            $"CalculateBoundaryStress Error: {e.Message}\n{e.StackTrace}"
                        );
                        GD.PrintErr($"CalculateBoundaryStress Error: {e.Message}\n{e.StackTrace}");
                        return 1;
                    }
                }
            );

            builder.AddStep(
                "ApplyStressToTerrain",
                () =>
                {
                    GameLogger.EnterFunction("ApplyStressToTerrain", "");
                    try
                    {
                        if (Continents == null || tectonics == null)
                        {
                            GameLogger.ExitFunction(
                                "ApplyStressToTerrain",
                                "Skipped - no continents or tectonics"
                            );
                            return 0;
                        }
                        tectonics!.ApplyStressToTerrain(Continents, StrDb!.VoronoiCells);
                        GameLogger.ExitFunction(
                            "ApplyStressToTerrain",
                            $"Stress applied to {StrDb!.VoronoiCells.Count} cells"
                        );
                        return 0;
                    }
                    catch (Exception e)
                    {
                        GameLogger.Error(
                            $"ApplyStressToTerrain Error: {e.Message}\n{e.StackTrace}"
                        );
                        GD.PrintErr($"ApplyStressToTerrain Error: {e.Message}\n{e.StackTrace}");
                        return 1;
                    }
                }
            );

            builder.AddStep(
                "FinalizeVertexHeights",
                () =>
                {
                    GameLogger.EnterFunction("FinalizeVertexHeights", "Iterations: 7");
                    try
                    {
                        if (Continents == null)
                        {
                            GameLogger.ExitFunction(
                                "FinalizeVertexHeights",
                                "Skipped - no continents"
                            );
                            return 0;
                        }
                        for (int i = 0; i < 7; i++)
                        {
                            FinalzeVertexHeights(StrDb!.VoronoiCellVertices, Continents);
                        }
                        GameLogger.ExitFunction(
                            "FinalizeVertexHeights",
                            $"Finalized {StrDb!.VoronoiCellVertices.Count} vertices"
                        );
                        return 0;
                    }
                    catch (Exception e)
                    {
                        GameLogger.Error(
                            $"FinalizeVertexHeights Error: {e.Message}\n{e.StackTrace}"
                        );
                        GD.PrintErr($"FinalizeVertexHeights Error: {e.Message}\n{e.StackTrace}");
                        return 1;
                    }
                }
            );
        }

        if (GenerationType == BodyGenerationType.ScalingWithNoise)
        {
            builder.AddStep(
                "ApplySecondPassScaling",
                () =>
                {
                    GameLogger.EnterFunction("ApplySecondPassScaling", $"Scaling: {ScaleFactors}");
                    try
                    {
                        ApplyScalingToVoronoiVertices();
                        GameLogger.ExitFunction(
                            "ApplySecondPassNoise",
                            $"Processed {StrDb!.VoronoiCellVertices.Count} vertices"
                        );
                        return 0;
                    }
                    catch (Exception e)
                    {
                        GameLogger.Error(
                            $"ApplySecondPassScaling Error: {e.Message}\n{e.StackTrace}"
                        );
                        return 1;
                    }
                }
            );
        }
        if (
            GenerationType == BodyGenerationType.TectonicsWithNoise
            || GenerationType == BodyGenerationType.ScalingWithNoise
            || GenerationType == BodyGenerationType.NoiseOnly
        )
        {
            builder.AddStep(
                "ApplySecondPassNoise",
                () =>
                {
                    GameLogger.EnterFunction(
                        "ApplySecondPassNoise",
                        $"Amplitude: {NoiseAmplitude * 0.5f}"
                    );
                    try
                    {
                        ApplyNoiseToVoronoiVertices(0.5f);
                        GameLogger.ExitFunction(
                            "ApplySecondPassNoise",
                            $"Processed {StrDb!.VoronoiCellVertices.Count} vertices"
                        );
                        return 0;
                    }
                    catch (Exception e)
                    {
                        GameLogger.Error(
                            $"ApplySecondPassNoise Error: {e.Message}\n{e.StackTrace}"
                        );
                        GD.PrintErr($"ApplySecondPassNoise Error: {e.Message}\n{e.StackTrace}");
                        return 1;
                    }
                }
            );
        }

        // SH pipeline: apply noise at 50% amplitude while preserving SH shape
        // (no scaling step - Voronoi inherits shape from base mesh)
        if (GenerationType == BodyGenerationType.SphericalHarmonicsWithNoise)
        {
            builder.AddStep(
                "ApplySecondPassNoisePreserveSH",
                () =>
                {
                    GameLogger.EnterFunction(
                        "ApplySecondPassNoisePreserveSH",
                        $"Amplitude: {NoiseAmplitude * 0.5f}"
                    );
                    try
                    {
                        ApplyNoiseToVoronoiVerticesPreserveSH(0.5f);
                        GameLogger.ExitFunction(
                            "ApplySecondPassNoisePreserveSH",
                            $"Processed {StrDb!.VoronoiCellVertices.Count} vertices"
                        );
                        return 0;
                    }
                    catch (Exception e)
                    {
                        GameLogger.Error(
                            $"ApplySecondPassNoisePreserveSH Error: {e.Message}\n{e.StackTrace}"
                        );
                        GD.PrintErr(
                            $"ApplySecondPassNoisePreserveSH Error: {e.Message}\n{e.StackTrace}"
                        );
                        return 1;
                    }
                }
            );
        }

        builder.AddStep(
            "AssignBiomes",
            () =>
            {
                GameLogger.EnterFunction("AssignBiomes", "");
                try
                {
                    if (Continents == null)
                    {
                        GameLogger.ExitFunction("AssignBiomes", "Skipped - no continents");
                        return 0;
                    }
                    maxHeight = StrDb!.VoronoiCellVertices.Max(p => p.Height);
                    AssignBiomes(Continents, StrDb!.VoronoiCells);
                    GameLogger.ExitFunction(
                        "AssignBiomes",
                        $"MaxHeight: {maxHeight:F4}, Continents: {Continents.Count}"
                    );
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error($"AssignBiomes Error: {e.Message}\n{e.StackTrace}");
                    GD.PrintErr($"AssignBiomes Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );

        builder.AddStep(
            "AssignGeothermalVents",
            () =>
            {
                GameLogger.EnterFunction("AssignGeothermalVents", "");
                try
                {
                    if (StrDb == null || StrDb.VoronoiCells == null || StrDb.VoronoiCells.Count == 0)
                    {
                        GameLogger.ExitFunction("AssignGeothermalVents", "Skipped - no cells");
                        return 0;
                    }
                    GeothermalVentGenerator.Generate(StrDb, rand);
                    GameLogger.ExitFunction("AssignGeothermalVents", "OK");
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error($"AssignGeothermalVents Error: {e.Message}\n{e.StackTrace}");
                    GD.PrintErr($"AssignGeothermalVents Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );

        builder.AddStep(
            "AssignResources",
            () =>
            {
                GameLogger.EnterFunction("AssignResources", "");
                try
                {
                    var resDb = ResourceGenerationConfigDatabase.Instance;
                    if (
                        !resDb.IsLoaded
                        || resDb.PlanetaryResources == null
                        || resDb.BiomeResources == null
                    )
                    {
                        GD.PrintErr(
                            "AssignResources: ResourceGenerationConfigDatabase not loaded, skipping"
                        );
                        GameLogger.Warning(
                            "AssignResources: ResourceGenerationConfigDatabase not loaded, skipping"
                        );
                        return 1;
                    }

                    // Determine which resource pipeline to use based on generation type
                    if (
                        GenerationType == BodyGenerationType.ScalingWithNoise
                        || GenerationType == BodyGenerationType.SphericalHarmonicsWithNoise
                        || GenerationType == BodyGenerationType.NoiseOnly
                    )
                    {
                        // Satellite pipeline - store at body level
                        _satelliteResources = SatelliteResourceGenerator.GenerateResources(
                            resDb.PlanetaryResources,
                            Classification,
                            rand
                        );
                        GameLogger.Info(
                            $"Assigned satellite resources: {_satelliteResources?.Count ?? 0} deposits"
                        );
                        return 0;
                    }
                    else
                    {
                        // Cell-level pipeline - generates per VoronoiCell based on biome and tags
                        if (Continents == null || Continents.Count == 0 || Classification == null)
                            return 1;

                        CellResourceGenerator.GenerateResources(
                            Continents,
                            resDb.PlanetaryResources,
                            resDb.BiomeResources,
                            Classification,
                            rand
                        );
                        GameLogger.Info(
                            $"Assigned cell resources across {Continents.Count} continents"
                        );
                        return 0;
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"AssignResources Error: {e.Message}\n{e.StackTrace}");
                    GameLogger.Error($"AssignResources Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );

        builder.AddStep(
            "GenerateSurfaceMesh",
            () =>
            {
                GameLogger.EnterFunction("GenerateSurfaceMesh", "");
                try
                {
                    if (Continents == null)
                    {
                        GameLogger.ExitFunction("GenerateSurfaceMesh", "Skipped - no continents");
                        return 0;
                    }
                    GenerateSurfaceMesh(StrDb!.VoronoiCells, oct);
                    GameLogger.ExitFunction(
                        "GenerateSurfaceMesh",
                        $"Generated mesh with {StrDb!.VoronoiCells.Count} cells"
                    );
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error($"GenerateSurfaceMesh Error: {e.Message}\n{e.StackTrace}");
                    GD.PrintErr($"GenerateSurfaceMesh Error: {e.Message}\n{e.StackTrace}");
                    return 1;
                }
            }
        );
    }

    private void AddTexturePassSteps(WorkPackageBuilder builder, IOrbitalBody parent)
    {
        builder.AddStep(
            "GenerateBillboardTextures",
            () =>
            {
                GameLogger.EnterFunction("GenerateBillboardTextures", "");
                try
                {
                    var generator = TextureGeneratorFactory.GetGenerator(parent.Classification);
                    parent.BillboardTextures.GenerateAllTextures(generator, parent);
                    return 0;
                }
                catch (Exception e)
                {
                    GameLogger.Error(
                        $"GenerateBillboardTextures Error: {e.Message}\n{e.StackTrace}"
                    );
                    return 1;
                }
            }
        );
    }

    private void OnMeshGenerationComplete(Action<UnifiedCelestialMesh> callback)
    {
        GD.Print($"Number of Vertices: {StrDb!.VoronoiVertices.Values.Count}\n");
        CreateCollisions(_octree!);
        callback?.Invoke(this);
    }

    private void OnMeshGenerationFailed(Action<UnifiedCelestialMesh, string> callback, string error)
    {
        GD.PrintErr($"Mesh generation failed: {error}");
        callback?.Invoke(this, error);
    }

    /// <summary>
    /// Dynamically detects the generation type based on configuration parameters.
    /// This method analyzes the configuration dictionary to determine which generation pipeline to use.
    /// </summary>
    /// <param name="meshParams">Dictionary containing mesh generation parameters.</param>
    /// <returns>The detected BodyGenerationType.</returns>
    /// <remarks>
    /// The detection logic follows these rules (priority order):
    /// - If "tectonic" key exists: TectonicsOnly or TectonicsWithNoise (depending on noise_settings)
    /// - If "spherical_harmonics" key exists: SphericalHarmonicsWithNoise
    /// - If "scaling_settings" key exists: ScalingWithNoise (if noise_settings exists) or fallback
    /// - If "noise_settings" key exists without tectonic or scaling: NoiseOnly
    /// - Default: TectonicsOnly
    /// </remarks>
    private BodyGenerationType DetectGenerationType(Godot.Collections.Dictionary meshParams)
    {
        if (meshParams == null)
            return BodyGenerationType.TectonicsOnly;

        bool hasTectonic = meshParams.ContainsKey("tectonic");
        bool hasSH = meshParams.ContainsKey("spherical_harmonics");
        bool hasScaling = meshParams.ContainsKey("scaling_settings");
        bool hasNoise = meshParams.ContainsKey("noise_settings");

        // Priority order: Tectonics > Spherical Harmonics > Scaling > Noise
        if (hasTectonic)
        {
            return hasNoise
                ? BodyGenerationType.TectonicsWithNoise
                : BodyGenerationType.TectonicsOnly;
        }
        else if (hasSH)
        {
            return BodyGenerationType.SphericalHarmonicsWithNoise;
        }
        else if (hasScaling)
        {
            return hasNoise ? BodyGenerationType.ScalingWithNoise : BodyGenerationType.NoiseOnly; // Fallback
        }
        else if (hasNoise)
        {
            return BodyGenerationType.NoiseOnly;
        }

        return BodyGenerationType.TectonicsOnly; // Default
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
    /// - Scaling settings for non-uniform scaling
    /// - Noise settings for procedural deformation
    ///
    /// The method handles various data types and includes error handling for invalid parameters.
    /// It automatically adjusts array lengths to match subdivision levels and provides
    /// fallback values for missing or invalid parameters.
    /// </remarks>
    public virtual void ConfigureFrom(
        StructureDatabase strDb,
        Godot.Collections.Dictionary meshParams
    )
    {
        this.StrDb = strDb;
        if (meshParams == null)
            return;
        GD.Print($"ConfigureFrom: {meshParams}");

        // Detect generation type first
        GenerationType = DetectGenerationType(meshParams);
        GD.Print($"Detected generation type: {GenerationType}");

        if (meshParams.ContainsKey("name"))
        {
            String name = "";
            try
            {
                name = meshParams["name"].As<string>();
                _name = name;
            }
            catch { }
            this.CallDeferred("set_name", name + "_mesh");
        }
        if (meshParams.ContainsKey("size"))
        {
            try
            {
                size = meshParams["size"].As<float>();
            }
            catch
            {
                GD.PrintErr("Couldn't find size in meshParams");
            }
        }

        // Base mesh settings
        if (meshParams.ContainsKey("subdivisions"))
        {
            try
            {
                subdivide = meshParams["subdivisions"].As<int>();
            }
            catch (Exception e)
            {
                GD.PrintErr($"\u001b[2J\u001b[H");
                GameLogger.Error($"Error in Subdivisions: {e.Message}\n{e.StackTrace}");
            }
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
            catch (Exception e)
            {
                GameLogger.Error($"Error in VerticesPerEdge: {e.Message}\n{e.StackTrace}");
            }

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
                catch (Exception e)
                {
                    GD.PrintErr($"\u001b[2J\u001b[H");
                    GameLogger.Error($"Error in VerticesPerEdge: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        if (VerticesPerEdge != null && subdivide > 0 && VerticesPerEdge.Length != subdivide)
        {
            var adjusted = new int[subdivide];
            for (int i = 0; i < subdivide; i++)
                adjusted[i] =
                    VerticesPerEdge.Length > 0
                        ? VerticesPerEdge[Math.Min(i, VerticesPerEdge.Length - 1)]
                        : 2;
            VerticesPerEdge = adjusted;
        }

        if (meshParams.ContainsKey("num_abberations"))
        {
            try
            {
                NumAbberations = meshParams["num_abberations"].As<int>();
            }
            catch { }
        }
        if (meshParams.ContainsKey("num_deformation_cycles"))
        {
            try
            {
                NumDeformationCycles = meshParams["num_deformation_cycles"].As<int>();
            }
            catch { }
        }

        // Tectonic settings
        if (meshParams.ContainsKey("tectonic"))
        {
            try
            {
                var tect = meshParams["tectonic"].As<Godot.Collections.Dictionary>();
                if (tect != null)
                {
                    if (tect.ContainsKey("num_continents"))
                    {
                        try
                        {
                            NumContinents = tect["num_continents"].As<int>();
                        }
                        catch { }
                    }
                    if (tect.ContainsKey("stress_scale"))
                    {
                        try
                        {
                            StressScale = tect["stress_scale"].As<float>();
                        }
                        catch { }
                    }
                    if (tect.ContainsKey("shear_scale"))
                    {
                        try
                        {
                            ShearScale = tect["shear_scale"].As<float>();
                        }
                        catch { }
                    }
                    if (tect.ContainsKey("max_propagation_distance"))
                    {
                        try
                        {
                            MaxPropagationDistance = tect["max_propagation_distance"].As<float>();
                        }
                        catch { }
                    }
                    if (tect.ContainsKey("propagation_falloff"))
                    {
                        try
                        {
                            PropagationFalloff = tect["propagation_falloff"].As<float>();
                        }
                        catch { }
                    }
                    if (tect.ContainsKey("inactive_stress_threshold"))
                    {
                        try
                        {
                            InactiveStressThreshold = tect["inactive_stress_threshold"].As<float>();
                        }
                        catch { }
                    }
                    if (tect.ContainsKey("general_height_scale"))
                    {
                        try
                        {
                            GeneralHeightScale = tect["general_height_scale"].As<float>();
                        }
                        catch { }
                    }
                    if (tect.ContainsKey("general_shear_scale"))
                    {
                        try
                        {
                            GeneralShearScale = tect["general_shear_scale"].As<float>();
                        }
                        catch { }
                    }
                    if (tect.ContainsKey("general_compression_scale"))
                    {
                        try
                        {
                            GeneralCompressionScale = tect["general_compression_scale"].As<float>();
                        }
                        catch { }
                    }
                    if (tect.ContainsKey("general_transform_scale"))
                    {
                        try
                        {
                            GeneralTransformScale = tect["general_transform_scale"].As<float>();
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
        if (meshParams.ContainsKey("scaling_settings"))
        {
            var scaling = meshParams["scaling_settings"].As<Godot.Collections.Dictionary>();
            try
            {
                var xScaleRange = (float)scaling["scaling_range_x"];
                var yScaleRange = (float)scaling["scaling_range_y"];
                var zScaleRange = (float)scaling["scaling_range_z"];
                ScaleFactors = new Vector3(xScaleRange, yScaleRange, zScaleRange);
            }
            catch (Exception e)
            {
                GD.PrintRaw($"\u001b[2J\u001b[H");
                GameLogger.Error($"Error in Scaling Settings: {e.Message}\n{e.StackTrace}");
            }
        }
        if (meshParams.ContainsKey("noise_settings"))
        {
            var noiseSettings = meshParams["noise_settings"].As<Godot.Collections.Dictionary>();
            try
            {
                var amplitude = (float)noiseSettings["amplitude"];
                var scaling = (float)noiseSettings["scaling"];
                var octaves = (int)noiseSettings["octaves"];

                NoiseAmplitude = amplitude;
                NoiseFrequency = scaling;
                noise!.FractalOctaves = octaves;

                // Parse lacunarity and gain from config if present
                if (noiseSettings.ContainsKey("lacunarity"))
                {
                    NoiseLacunarity = (float)noiseSettings["lacunarity"];
                }
                if (noiseSettings.ContainsKey("gain"))
                {
                    NoiseGain = (float)noiseSettings["gain"];
                }
                noise.FractalLacunarity = NoiseLacunarity;
                noise.FractalGain = NoiseGain;
            }
            catch (Exception e)
            {
                GD.PrintRaw($"\u001b[2J\u001b[H");
                GameLogger.Error($"Error in Noise Settings: {e.Message}\n{e.StackTrace}");
            }
        }

        // Parse spherical harmonics settings
        if (meshParams.ContainsKey("spherical_harmonics"))
        {
            try
            {
                var shConfig = meshParams["spherical_harmonics"].As<Godot.Collections.Dictionary>();
                if (shConfig != null && shConfig.ContainsKey("amplitude"))
                {
                    _shAmplitude = (float)shConfig["amplitude"];

                    // Warn if amplitude is outside expected range
                    if (_shAmplitude < 0.1f || _shAmplitude > 2.0f)
                    {
                        GameLogger.Warning(
                            $"SH amplitude {_shAmplitude:F3} is outside expected range [0.1, 2.0]"
                        );
                    }
                }
                // Default amplitude is already set to 0.3f in field declaration

                // Initialize SH deformer with random coefficients scaled by amplitude
                _shDeformer = new SphericalHarmonicsDeformer();
                _shDeformer.GenerateCoefficients(rand, _shAmplitude);

                GameLogger.Info(
                    $"SH configured: amplitude={_shAmplitude:F3}, coefficients generated"
                );
            }
            catch (Exception e)
            {
                GD.PrintRaw($"\u001b[2J\u001b[H");
                GameLogger.Error(
                    $"Error in Spherical Harmonics Settings: {e.Message}\n{e.StackTrace}"
                );
            }
        }

        // If both scaling_settings AND spherical_harmonics exist, prefer spherical_harmonics (log warning)
        if (
            meshParams.ContainsKey("scaling_settings")
            && meshParams.ContainsKey("spherical_harmonics")
        )
        {
            GameLogger.Warning(
                "Both scaling_settings and spherical_harmonics found in config; using spherical_harmonics"
            );
        }

        // Resource generation uses group-based configs via ResourceGenerationConfigDatabase
        // No per-body resource config needed from templates
    }

    public Continent GetContinent(int index)
    {
        return Continents![index];
    }

    // Include all the helper methods from the original classes
    private void AssignBiomes(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
    {
        // Use subtype-specific biome assigner if classification is set, else fall back to static
        _biomeAssigner ??=
            Classification != null
                ? BiomeAssignerFactory.GetAssigner(Classification)
                : new DefaultBiomeAssigner();

        foreach (var continent in continents)
        {
            Continent c = continent.Value;
            c.averageMoisture = _biomeAssigner.CalculateMoisture(c, rand, 0.5f);
            foreach (var cell in c.cells)
            {
                foreach (Point p in cell.Points)
                {
                    float latitude = p.Position.Normalized().Y;
                    p.Biome = _biomeAssigner.AssignBiome(this, p.Height, c.averageMoisture, latitude);
                }
                cell.CalculateCellBiome();
            }
        }
    }

    private void AssignResources(Dictionary<int, Continent> continents)
    {
        if (continents == null || continents.Count == 0 || Classification == null)
            return;

        var resDb = ResourceGenerationConfigDatabase.Instance;
        if (!resDb.IsLoaded || resDb.PlanetaryResources == null || resDb.BiomeResources == null)
        {
            GameLogger.Warning("AssignResources: ResourceGenerationConfigDatabase not loaded");
            return;
        }

        CellResourceGenerator.GenerateResources(
            continents,
            resDb.PlanetaryResources,
            resDb.BiomeResources,
            Classification,
            rand
        );
    }

    public void UpdateVertexHeights(HashSet<Point> Vertices, Dictionary<int, Continent> Continents)
    {
        foreach (Point p in Vertices)
        {
            float height = 0;
            foreach (int continentIndex in p.ContinentIndecies!)
            {
                height += Continents[continentIndex].averageHeight;
            }
            height /= p.ContinentIndecies.Count;
            p.Height = height;
        }
    }

    public void FinalzeVertexHeights(HashSet<Point> Vertices, Dictionary<int, Continent> Continents)
    {
        foreach (Point p in Vertices)
        {
            var edges = StrDb!.GetIncidentHalfEdges(p);
            float averagedHeight = p.Height;
            int num = 1;
            foreach (Edge e in edges)
            {
                averagedHeight = averagedHeight + (e.Q.Height - p.Height) / num;
                num++;
            }
            p.Height = averagedHeight;
        }
    }

    public static VoronoiCell[] GetCellNeighbors(
        VoronoiCell origin,
        StructureDatabase StrDb,
        bool includeSameContinent = true
    )
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

    public Dictionary<int, Continent> FloodFillContinentGeneration(List<VoronoiCell> cells)
    {
        Dictionary<int, int> continentMinSize = new Dictionary<int, int>();
        Dictionary<int, List<int>> continentNeighbors = new Dictionary<int, List<int>>();
        Dictionary<int, Continent> continents = new Dictionary<int, Continent>();
        HashSet<int> startingCells = GenerateStartingCells(cells);
        List<int> poppableCells = startingCells.ToList();
        int[] neighborChart = new int[cells.Count];
        for (int i = 0; i < neighborChart.Length; i++)
        {
            neighborChart[i] = -1;
        }
        foreach (int startingCellIndex in startingCells)
        {
            Continent.CRUST_TYPE crustType =
                rand.RandiRange(0, 100) > 33
                    ? Continent.CRUST_TYPE.Oceanic
                    : Continent.CRUST_TYPE.Continental;
            float averageHeight =
                crustType == Continent.CRUST_TYPE.Oceanic
                    ? rand.RandfRange(-10.0f, -3.0f)
                    : rand.RandfRange(1.2f, 7.0f);
            float rotation = Mathf.DegToRad(rand.RandiRange(-360, 360));
            float velocity = rand.RandfRange(0.3f, 1.7f);
            var continent = new Continent(
                startingCellIndex,
                new List<VoronoiCell>(),
                new HashSet<VoronoiCell>(),
                new HashSet<Point>(),
                new List<Point>(),
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 0f),
                new Vector2(rand.RandfRange(-1f, 1f), rand.RandfRange(-1f, 1f)),
                velocity,
                rotation,
                crustType,
                averageHeight,
                rand.RandfRange(1.0f, 5.0f),
                new HashSet<int>(),
                0f,
                new Dictionary<int, float>(),
                new Dictionary<int, Continent.BOUNDARY_TYPE>()
            );
            neighborChart[startingCellIndex] = startingCellIndex;
            continent.StartingIndex = startingCellIndex;
            continent.cells!.Add(cells[startingCellIndex]);
            foreach (Point p in cells[startingCellIndex].Points)
            {
                continent.points!.Add(p);
                p.ContinentIndecies!.Add(continent.StartingIndex);
            }
            foreach (Edge e in cells[startingCellIndex].Edges)
            {
                e.ContinentIndex = startingCellIndex;
            }
            continents[startingCellIndex] = continent;

            var neighborIndices = GetCellNeighbors(cells[startingCellIndex], StrDb!);
            continentNeighbors[startingCellIndex] = new List<int>();
            foreach (var nb in neighborIndices)
            {
                if (neighborChart[nb.Index] == -1)
                {
                    neighborChart[nb.Index] = neighborChart[startingCellIndex];
                    continentNeighbors[startingCellIndex].Add(nb.Index);
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
                    {
                        continentMinSize.Remove(index);
                        continue;
                    }
                    else
                        randomNeighbor = continentNeighbors[index][
                            rand.RandiRange(0, continentNeighbors[index].Count - 1)
                        ];
                    continentObj.cells.Add(cells[randomNeighbor]);
                    continentNeighbors[index].Remove(randomNeighbor);
                    var neighborIndices = GetCellNeighbors(cells[randomNeighbor], StrDb!);
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
            if (continentMinSize.Count == 0)
                break;
        }
        while (poppableCells.Count > 0)
        {
            int poppedIndex = rand.RandiRange(0, (poppableCells.Count - 1));
            int currentVCellIndex = poppableCells[poppedIndex];
            poppableCells.RemoveAt(poppedIndex);
            var neighborIndices = GetCellNeighbors(cells[currentVCellIndex], StrDb!);
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
                    continent.points!.Add(p);
                    p.ContinentIndecies!.Add(continent.StartingIndex);
                }
            }
        }

        // Normalize cell heights into [0, 1] for placement-constraint comparisons.
        // Raw Height stays in world units for tectonics; NormalizedHeight is what
        // YAML min_elevation/max_elevation values match against.
        float minH = float.MaxValue, maxH = float.MinValue;
        for (int i = 0; i < neighborChart.Length; i++)
        {
            if (neighborChart[i] == -1) continue;
            minH = Mathf.Min(minH, cells[i].Height);
            maxH = Mathf.Max(maxH, cells[i].Height);
        }
        float range = Mathf.Max(maxH - minH, 0.0001f);
        for (int i = 0; i < neighborChart.Length; i++)
        {
            if (neighborChart[i] == -1) continue;
            cells[i].NormalizedHeight = (cells[i].Height - minH) / range;
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
            var v1 = (
                continent
                    .points.ElementAt(rand.RandiRange(0, continent.points.Count - 1))
                    .ToVector3()
                    .Normalized()
                - continent
                    .points.ElementAt(rand.RandiRange(0, continent.points.Count - 1))
                    .ToVector3()
                    .Normalized()
            );
            var v2 = (
                continent
                    .points.ElementAt(rand.RandiRange(0, continent.points.Count - 1))
                    .ToVector3()
                    .Normalized()
                - continent
                    .points.ElementAt(rand.RandiRange(0, continent.points.Count - 1))
                    .ToVector3()
                    .Normalized()
            );
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
                Vector3 cellAverageCenter = Vector3.Zero;
                foreach (Point p in vc.Points)
                {
                    cellAverageCenter += p.Position;
                }
                cellAverageCenter /= vc.Points.Length;
                cellAverageCenter = cellAverageCenter.Normalized();

                float k =
                    (
                        1.0f
                        - UnitNorm.X * cellAverageCenter.X
                        - UnitNorm.Y * cellAverageCenter.Y
                        - UnitNorm.Z * cellAverageCenter.Z
                    )
                    / (UnitNorm.X * UnitNorm.X + UnitNorm.Y * UnitNorm.Y + UnitNorm.Z * UnitNorm.Z);
                Vector3 projectedCenter = new Vector3(
                    cellAverageCenter.X + k * UnitNorm.X,
                    cellAverageCenter.Y + k * UnitNorm.Y,
                    cellAverageCenter.Z + k * UnitNorm.Z
                );
                Vector3 projectedCenter2D = new Vector3(
                    uAxis.Dot(projectedCenter),
                    vAxis.Dot(projectedCenter),
                    0f
                );

                float radius = (continent.averagedCenter - cellAverageCenter).Length();
                Vector3 positionFromCenter = continent.averagedCenter - projectedCenter;

                vc.MovementDirection =
                    new Vector2(
                        continent.movementDirection.X * continent.velocity,
                        continent.movementDirection.Y * continent.velocity
                    )
                    + new Vector2(
                        -continent.rotation * projectedCenter2D.Y,
                        continent.rotation * projectedCenter2D.X
                    );
                float vcRadius = 0.0f;
                foreach (Point p in vc.Points)
                {
                    vcRadius += (cellAverageCenter - p.ToVector3().Normalized()).Length();
                }
                vcRadius /= vc.Points.Length;

                var vcUnitNorm = v1.Cross(v2);
                var projectionRatio = (uAxis - UnitNorm).Length() / vcRadius;
                vcUnitNorm /= projectionRatio;
                vcUnitNorm = vcUnitNorm.Normalized();

                var d =
                    UnitNorm.X * (vc.Points[0].Position.X)
                    + UnitNorm.Y * (vc.Points[0].Position.Y)
                    + UnitNorm.Z * (vc.Points[0].Position.Z);
                var newZ =
                    (
                        d
                        - (UnitNorm.X * vc.Points[0].Position.X)
                        - (UnitNorm.Y * vc.Points[0].Position.Y)
                    ) / UnitNorm.Z;
                var newZ2 = (d / UnitNorm.Z);
                var directionPoint =
                    uAxis * vc.MovementDirection.X + vAxis * vc.MovementDirection.Y;
                directionPoint *= vcRadius;
                directionPoint += cellAverageCenter;
                directionPoint = directionPoint.Normalized();
            }
        }

        return continents;
    }

    public void GenerateFromContinents(Dictionary<int, Continent> continents, Octree<Point> oct)
    {
        foreach (var keyValuePair in continents)
        {
            GD.Print($"Generating continent {keyValuePair.Key} | {keyValuePair.Value.cells.Count}");
            GenerateSurfaceMesh(keyValuePair.Value.cells, oct);
        }
    }

    private Color GetVertexColor(float height)
    {
        float normalizedHeight = (height + 10f) / 20f;
        float hue = 220f - (210f * normalizedHeight);
        float saturation = 0.3f + 0.5f * Mathf.Sin(normalizedHeight * Mathf.Pi);
        float value = 0.4f + 0.5f * Mathf.Sin(normalizedHeight * Mathf.Pi * 0.8f + 0.2f);
        hue = Mathf.Clamp(hue, 0f, 360f);
        saturation = Mathf.Clamp(saturation, 0.2f, 1f);
        value = Mathf.Clamp(value, 0.2f, 1f);
        return Color.FromHsv(hue / 360f, saturation, value);
    }

    private Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        _colorMapper ??=
            Classification != null
                ? ColorMapperFactory.GetMapper(Classification)
                : new DataDrivenColorMapper();

        return _colorMapper.GetBiomeColor(biome, height);
    }

    public virtual void GenerateSurfaceMesh(List<VoronoiCell> VoronoiList, Octree<Point> oct)
    {
        var arrMesh = Mesh as ArrayMesh;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbFloat);

        // Create per-surface material instances using pre-loaded shader resources.
        // Resources are loaded on main thread in constructor; duplicates are safe to
        // create from any thread since they're not yet in the scene tree.
        var material = CreateSurfaceMaterials();
        st.SetMaterial(material);
        int faceIndex = 0;
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

                    var uv = new Vector2(
                        (u - min_u) / (max_u - min_u),
                        (v - min_v) / (max_v - min_v)
                    );
                    st.SetUV(uv);
                    float borderFlag = ((Point)vor.Points[3 * i + j]).isOnContinentBorder
                        ? 1.0f
                        : 0.0f;
                    st.SetNormal(tangent);
                    st.SetCustom(0, new Color(vor.Index, borderFlag, vor.ContinentIndex));
                    if (ProjectToSphere)
                    {
                        // Check generation type to determine projection method
                        if (
                            GenerationType == BodyGenerationType.SphericalHarmonicsWithNoise
                            || GenerationType == BodyGenerationType.ScalingWithNoise
                            || GenerationType == BodyGenerationType.NoiseOnly
                        )
                        {
                            // Non-spherical bodies: preserve actual position, add small height offset
                            Vector3 direction = vor.Points[3 * i + j].Position.Normalized();
                            float currentRadius = vor.Points[3 * i + j].Position.Length();
                            vor.Points[3 * i + j].Position =
                                direction * (size + vor.Points[3 * i + j].Height);
                        }
                        else
                        {
                            // Tectonic bodies (planets): existing spherical projection
                            vor.Points[3 * i + j].Position =
                                vor.Points[3 * i + j].Position.Normalized()
                                * (size + vor.Points[3 * i + j].Height / 10f);
                        }
                        // Get biome color and apply resource tinting
                        Color baseBiomeColor;
                        if (UseCellBiomeForColoring)
                        {
                            // Use cell-level biome for consistent coloring across the cell
                            baseBiomeColor = GetBiomeColor(vor.Biome, vor.Height);
                        }
                        else
                        {
                            // Use point-level biome for per-point coloring
                            baseBiomeColor = GetBiomeColor(
                                ((Point)vor.Points[3 * i + j]).Biome,
                                ((Point)vor.Points[3 * i + j]).Height
                            );
                        }
                        //Color tintedColor = ResourceVisualizer.ApplyResourceTint(
                        //    baseBiomeColor,
                        //    vor.Resources
                        //);
                        st.SetColor(baseBiomeColor);
                        st.AddVertex(vor.Points[3 * i + j].Position);
                        StrDb!.AddPointForCellPlanet(vor.Points[3 * i + j], vor);
                        oct.Insert(vor.Points[3 * i + j]);
                    }
                    else
                    {
                        st.AddVertex(
                            new Vector3(
                                (vor.Points[3 * i + j]).Components[0],
                                (vor.Points[3 * i + j]).Components[1],
                                (vor.Points[3 * i + j]).Components[2]
                            ) * (size + (vor.Points[3 * i + j]).Components[2] / 10f)
                        );
                    }
                }
                StrDb!.AddFaceToCellMap(faceIndex, vor);
                faceIndex++;
            }
            vor.GenerateBoundingBox();
        }
        GD.Print($"FaceIndex: {faceIndex}");
        st.CallDeferred("commit", arrMesh!);
    }

    private void GenerateVoronoiCellsInternal(Octree<Point> oct)
    {
        VoronoiCellGeneration voronoiCellGeneration = new VoronoiCellGeneration(StrDb!);

        try
        {
            voronoiCellGeneration.GenerateVoronoiCells(this, oct);
            float sizeMultiplier = 2.5f * StrDb!.VoronoiCells.Count / 10000f;
            if (sizeMultiplier > 1.0f)
            {
                size *= sizeMultiplier;
                oct.Grow(sizeMultiplier);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Voronoi Cell Generation Error: {e.Message}\n{e.StackTrace}", "ERROR");
            GameLogger.Error(
                $"Voronoi Cell Generation Error: {e.Message}\n{e.StackTrace}",
                "ERROR"
            );
            throw;
        }
    }

    private Dictionary<int, Continent> FloodFillContinentsInternal()
    {
        Dictionary<int, Continent> continents = new Dictionary<int, Continent>();
        try
        {
            continents = FloodFillContinentGeneration(StrDb!.VoronoiCells);
            Continents = continents;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Flood Filling Error: {e.Message}\n{e.StackTrace}");
            throw;
        }
        return continents;
    }

    private void CalculateBoundaryCellsInternal(Dictionary<int, Continent> continents)
    {
        try
        {
            foreach (VoronoiCell vc in StrDb!.VoronoiCells)
            {
                var cellNeighbors = GetCellNeighbors(vc, StrDb!);
                float averageHeight = vc.Height;
                List<Edge> OutsideEdges = new List<Edge>();
                List<int> BoundingContinentIndex = new List<int>();
                foreach (VoronoiCell neighbor in cellNeighbors)
                {
                    if (neighbor.ContinentIndex != vc.ContinentIndex)
                    {
                        vc.IsBorderTile = true;
                        vc.Interiorness = 1;
                        continents[vc.ContinentIndex].boundaryCells.Add(vc);
                        continents[vc.ContinentIndex]
                            .neighborContinents.Add(neighbor.ContinentIndex);
                        continents[neighbor.ContinentIndex]
                            .neighborContinents.Add(vc.ContinentIndex);
                        neighbor.IsBorderTile = true;
                        neighbor.Interiorness = 1;
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
                                    continents[vc.ContinentIndex].boundaryEdges.Add(e);
                                }
                            }
                        }
                    }
                }
                vc.BoundingContinentIndex = BoundingContinentIndex.ToArray();
                vc.OutsideEdges = OutsideEdges.ToArray();
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error in Calculate Boundary Cells: {e.Message}\n{e.StackTrace}");
            GameLogger.Error($"Error in Calculate Boundary Cells: {e.Message}\n{e.StackTrace}");
            throw;
        }
    }

    private void CalculateInteriornessInternal(Dictionary<int, Continent> continents)
    {
        try
        {
            HashSet<VoronoiCell> visited = new HashSet<VoronoiCell>();
            var continent = continents.First().Value;
            GD.Print($"Calculating Interiorness for {continent.StartingIndex}");
            GD.Print($"# Edges: {continent.boundaryEdges.Count}");
            PriorityQueue<VoronoiCell, int> BreadthSearch = new PriorityQueue<VoronoiCell, int>();
            foreach (var vc in continent.boundaryCells)
            {
                BreadthSearch.Enqueue(vc, vc.Interiorness);
            }
            while (BreadthSearch.Count > 0)
            {
                var current = BreadthSearch.Dequeue();
                visited.Add(current);
                var neighbors = GetCellNeighbors(current, StrDb!)
                    .Where(nc => nc.ContinentIndex == current.ContinentIndex)
                    .ToList();
                if (current.Interiorness != 1)
                {
                    int interiorness = neighbors.Min(e => e.Interiorness) + 1;
                    current.Interiorness = interiorness;
                }
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        BreadthSearch.Enqueue(neighbor, neighbor.Interiorness);
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error in Calculate Interiorness: {e.Message}\n{e.StackTrace}");
            GameLogger.Error($"Error in Calculate Interiorness: {e.Message}\n{e.StackTrace}");
            throw;
        }

        foreach (Point p in StrDb!.VoronoiCellVertices)
        {
            var cells = StrDb!.CellMap[p];
            foreach (VoronoiCell vc in cells)
            {
                p.ContinentIndecies!.Add(vc.ContinentIndex);
            }
        }
    }

    private void ApplyNoiseToBaseVertices()
    {
        foreach (var vertex in StrDb!.BaseVertices.Values)
        {
            float noiseValue = noise!.GetNoise3D(
                vertex.Position.X * NoiseFrequency,
                vertex.Position.Y * NoiseFrequency,
                vertex.Position.Z * NoiseFrequency
            );

            Vector3 normal = vertex.Position.Normalized();
            Vector3 displacement = normal * noiseValue * NoiseAmplitude;
            vertex.Position += displacement;
            //vertex.Height += displacement.Length();
            vertex.Height += displacement.Dot(vertex.Position.Normalized());
        }
    }

    private void ApplyScalingToBaseVertices()
    {
        foreach (var vertex in StrDb!.BaseVertices.Values)
        {
            vertex.Position *= ScaleFactors;
        }
    }

    private void ApplyScalingToVoronoiVertices()
    {
        foreach (var vertex in StrDb!.VoronoiCellVertices)
        {
            vertex.Position *= ScaleFactors;
        }
    }

    /// <summary>
    /// Applies Spherical Harmonics deformation to base vertices only.
    /// The Voronoi vertices inherit the shape naturally through circumcenter computation.
    /// </summary>
    /// <remarks>
    /// This method applies SH deformation ONLY to base vertices. Unlike scaling which is
    /// applied twice (base + Voronoi), SH is applied once because Voronoi vertices are
    /// circumcenters computed from the deformed base mesh, naturally inheriting the SH shape.
    ///
    /// The height is stored as signed displacement (positive = outward bulge, negative = inward depression).
    /// Radius is clamped to prevent vertices from collapsing past the origin (minimum 30% of base size).
    /// </remarks>
    private void ApplySphericalHarmonicsToBaseVertices()
    {
        GameLogger.EnterFunction("ApplySphericalHarmonicsToBaseVertices", "");
        foreach (var vertex in StrDb!.BaseVertices.Values)
        {
            Vector3 direction = vertex.Position.Normalized();
            float displacement = _shDeformer!.Evaluate(direction);
            float newRadius = displacement;
            // Floor: prevent radius from going below 30% of size
            newRadius = Mathf.Clamp(newRadius, size * 0.3f, size * 1.3f);
            vertex.Position = direction * newRadius;
            //vertex.Height = displacement; // Signed height — positive=outward, negative=inward
        }
        GameLogger.ExitFunction(
            "ApplySphericalHarmonicsToBaseVertices",
            $"Vertices: {StrDb.BaseVertices.Count}, Amplitude: {_shAmplitude}"
        );
    }

    private void ApplyNoiseToVoronoiVertices(float amplitudeMultiplier = 1.0f)
    {
        foreach (Point p in StrDb!.VoronoiCellVertices)
        {
            float noiseValue = noise!.GetNoise3D(
                p.Position.X * NoiseFrequency,
                p.Position.Y * NoiseFrequency,
                p.Position.Z * NoiseFrequency
            );

            Vector3 normal = p.Position.Normalized();
            Vector3 displacement = normal * noiseValue * NoiseAmplitude * amplitudeMultiplier;
            p.Position += displacement;
            //p.Height += displacement.Length();
            p.Height += displacement.Dot(p.Position.Normalized());
        }

        float maxDistance = StrDb.VoronoiCellVertices.Max(p => p.Position.Length());
        foreach (Point p in StrDb.VoronoiCellVertices)
        {
            float currentDistance = p.Position.Length();
            float scaleFactor = size * (currentDistance / maxDistance);
            p.Position = p.Position.Normalized() * scaleFactor;
        }
    }

    /// <summary>
    /// Applies noise to Voronoi vertices while preserving the Spherical Harmonics shape.
    /// Unlike ApplyNoiseToVoronoiVertices, this does NOT normalize all vertices back to a sphere.
    /// Instead, it preserves relative proportions and only clamps extreme outliers.
    /// </summary>
    /// <param name="amplitudeMultiplier">Multiplier for noise amplitude (typically 0.5 for second pass).</param>
    private void ApplyNoiseToVoronoiVerticesPreserveSH(float amplitudeMultiplier = 1.0f)
    {
        GameLogger.EnterFunction(
            "ApplyNoiseToVoronoiVerticesPreserveSH",
            $"Amplitude: {NoiseAmplitude * amplitudeMultiplier}"
        );

        foreach (Point p in StrDb!.VoronoiCellVertices)
        {
            Vector3 direction = p.Position * size;
            float displacement = _shDeformer!.Evaluate(direction);
            // Floor: prevent radius from going below 30% of size
            p.Height = displacement * rand.RandfRange(0.8f * size, 1.5f * size); // Signed height — positive=outward, negative=inward

            //Vector3 normal = p.Position.Normalized();
            //Vector3 displacement = normal * noiseValue * NoiseAmplitude * amplitudeMultiplier;
            //p.Position += displacement;
            ////p.Height += displacement.Length();
            //p.Height += displacement.Dot(p.Position.Normalized());
        }

        // Preserve SH shape: only rescale if distance exceeds 2x average (safety clamp)
        // This prevents extreme outliers while maintaining the global shape
        //float avgDistance = StrDb.VoronoiCellVertices.Average(p => p.Position.Length());
        //foreach (Point p in StrDb.VoronoiCellVertices)
        //{
        //    float currentDistance = p.Position.Length();
        //    if (currentDistance > avgDistance * 2.0f)
        //    {
        //        p.Position = p.Position.Normalized() * (avgDistance * 2.0f);
        //    }
        //}

        GameLogger.ExitFunction(
            "ApplyNoiseToVoronoiVerticesPreserveSH",
            $"Processed {StrDb.VoronoiCellVertices.Count} vertices, AvgDistance: "
        );
    }

    private void CreateCollisions(Octree<Point> oct)
    {
        GameLogger.Info(
            $"Creating collision mesh: {StrDb!.VoronoiCellVertices.Count} Voronoi vertices, {oct.GetPoints().Count} octree points"
        );
        MeshConvexDecompositionSettings settings = new MeshConvexDecompositionSettings();
        settings.ConvexHullApproximation = false;
        settings.NormalizeMesh = false;
        settings.ConvexHullDownsampling = 16;
        settings.MaxNumVerticesPerConvexHull = 256;
        settings.Mode = MeshConvexDecompositionSettings.ModeEnum.Voxel;
        //this.CallDeferred("create_multiple_convex_collisions", settings);
        this.CallDeferred("create_trimesh_collision");
    }
}
