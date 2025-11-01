using Godot;
using MeshGeneration;
using Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UtilityLibrary;
using PlanetGeneration;

using static Structures.Biome;

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
        NoiseOnly
    }

    /// <summary>
    /// Static progress tracking object for generation operations.
    /// Used to monitor and report progress during the asynchronous mesh generation process.
    /// </summary>
    public static GenericPercent percent;


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
    /// Instance reference to this UnifiedCelestialMesh.
    /// Provides access to the current instance for external components and systems.
    /// </summary>
    public UnifiedCelestialMesh Instance { get; }

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
    /// Noise generator instance for procedural deformation.
    /// Uses Godot's FastNoiseLite for efficient 3D noise computation.
    /// </summary>
    private FastNoiseLite noise;

    [ExportCategory("Thread Pool Settings")]
    [Export] public bool UseThreadPool = true;
    [Export] public TaskPriority TaskPriority = TaskPriority.High;

    public override void _Ready()
    {
        // Initialize noise generator
        noise = new FastNoiseLite();
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.ValueCubic;
        noise.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
        noise.FractalOctaves = NoiseOctaves;
        noise.FractalLacunarity = 5.0f;
        noise.FractalGain = 4.5f;
    }

    /// <summary>
    /// Dynamically detects the generation type based on configuration parameters.
    /// This method analyzes the configuration dictionary to determine which generation pipeline to use.
    /// </summary>
    /// <param name="meshParams">Dictionary containing mesh generation parameters.</param>
    /// <returns>The detected BodyGenerationType.</returns>
    /// <remarks>
    /// The detection logic follows these rules:
    /// - If "tectonic" key exists: TectonicsOnly or TectonicsWithNoise (depending on noise_settings)
    /// - If "scaling_settings" key exists: ScalingWithNoise (if noise_settings exists) or fallback
    /// - If "noise_settings" key exists without tectonic or scaling: NoiseOnly
    /// - Default: TectonicsOnly
    /// </remarks>
    private BodyGenerationType DetectGenerationType(Godot.Collections.Dictionary meshParams)
    {
        if (meshParams == null)
            return BodyGenerationType.TectonicsOnly;

        bool hasTectonic = meshParams.ContainsKey("tectonic");
        bool hasScaling = meshParams.ContainsKey("scaling_settings");
        bool hasNoise = meshParams.ContainsKey("noise_settings");

        // Priority order: Tectonics > Scaling > Noise
        if (hasTectonic)
        {
            return hasNoise ? BodyGenerationType.TectonicsWithNoise : BodyGenerationType.TectonicsOnly;
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
    public virtual void ConfigureFrom(Godot.Collections.Dictionary meshParams)
    {
        if (meshParams == null) return;
        GD.Print($"ConfigureFrom: {meshParams}");

        // Detect generation type first
        GenerationType = DetectGenerationType(meshParams);
        GD.Print($"Detected generation type: {GenerationType}");

        if (meshParams.ContainsKey("name"))
        {
            String name = "";
            try { name = meshParams["name"].As<string>(); } catch { }
            this.CallDeferred("set_name", name + "_mesh");
        }
        if (meshParams.ContainsKey("size"))
        {
            try { size = meshParams["size"].As<float>(); } catch { GD.PrintErr("Couldn't find size in meshParams"); }
        }

        // Base mesh settings
        if (meshParams.ContainsKey("subdivisions"))
        {
            try { subdivide = meshParams["subdivisions"].As<int>(); } catch (Exception e) { GD.PrintErr($"\u001b[2J\u001b[H"); Logger.Error($"Error in Subdivisions: {e.Message}\n{e.StackTrace}"); }
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
                catch (Exception e) { GD.PrintErr($"\u001b[2J\u001b[H"); Logger.Error($"Error in VerticesPerEdge: {e.Message}\n{e.StackTrace}"); }
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
        if (meshParams.ContainsKey("scaling_settings"))
        {
            var scaling = meshParams["scaling_settings"].As<Godot.Collections.Dictionary>();
            try
            {
                var xScaleRange = (float)scaling["x_scale_range"];
                var yScaleRange = (float)scaling["y_scale_range"];
                var zScaleRange = (float)scaling["z_scale_range"];
                ScaleFactors = new Vector3(xScaleRange, yScaleRange, zScaleRange);
            }
            catch (Exception e) { GD.PrintRaw($"\u001b[2J\u001b[H"); Logger.Error($"Error in Scaling Settings: {e.Message}\n{e.StackTrace}"); }
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
                noise.FractalOctaves = octaves;

            }
            catch (Exception e) { GD.PrintRaw($"\u001b[2J\u001b[H"); Logger.Error($"Error in Noise Settings: {e.Message}\n{e.StackTrace}"); }
        }
    }

    /// <summary>
    /// Main entry point for generating the celestial body mesh.
    /// This method orchestrates the complete mesh generation process using the detected generation type.
    /// </summary>
    /// <remarks>
    /// This method initializes the mesh generation process by:
    /// 1. Setting up the mesh structure and random number generator
    /// 2. Creating the structure database and tectonic generation system (if needed)
    /// 3. Starting the asynchronous planet generation process using the appropriate pipeline
    ///
    /// The generation runs in a separate task to avoid blocking the main thread.
    /// This allows the game to remain responsive during the computationally expensive
    /// mesh generation process. The method sets up all necessary components and
    /// initiates the generation system based on the detected generation type.
    /// </remarks>
    public virtual void GenerateMesh()
    {
        this.CallDeferred("set_mesh", new ArrayMesh());
        StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
        percent = new GenericPercent();
        if (Seed != 0)
        {
            rand.Seed = Seed;
        }
        GD.Print($"Rand Seed: {rand.Seed}\n");

        // Initialize tectonics only if needed
        if (GenerationType == BodyGenerationType.TectonicsOnly || GenerationType == BodyGenerationType.TectonicsWithNoise)
        {
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
        }

        Task generatePlanet = Task.Factory.StartNew(() => GeneratePlanetAsync());
        GD.Print($"Number of Vertices: {StrDb.VoronoiVertices.Values.Count}\n");
    }

    /// <summary>
    /// Unified asynchronous method that orchestrates the complete planet generation process.
    /// This method routes to the appropriate generation pipeline based on the detected generation type.
    /// </summary>
    /// <remarks>
    /// This method coordinates the generation process by selecting the appropriate pipeline:
    /// - TectonicsOnly: Standard tectonic generation without noise
    /// - TectonicsWithNoise: Tectonic generation with noise-based deformation
    /// - ScalingWithNoise: Non-uniform scaling with noise-based deformation
    /// - NoiseOnly: Pure noise-based deformation
    ///
    /// Each pipeline implements the appropriate combination of features while maintaining
    /// compatibility with the existing mesh generation infrastructure.
    /// </remarks>
    protected virtual async void GeneratePlanetAsync()
    {
        switch (GenerationType)
        {
            case BodyGenerationType.TectonicsOnly:
                await GenerateTectonicsOnlyPipeline();
                break;
            case BodyGenerationType.TectonicsWithNoise:
                await GenerateTectonicsWithNoisePipeline();
                break;
            case BodyGenerationType.ScalingWithNoise:
                await GenerateScalingWithNoisePipeline();
                break;
            case BodyGenerationType.NoiseOnly:
                await GenerateNoiseOnlyPipeline();
                break;
            default:
                await GenerateTectonicsOnlyPipeline(); // Fallback
                break;
        }
    }

    /// <summary>
    /// Pipeline for generating celestial bodies using only tectonic processes.
    /// This is the standard planet generation pipeline without additional noise or scaling.
    /// </summary>
    private async Task GenerateTectonicsOnlyPipeline()
    {
        if (UseThreadPool && MeshGenerationThreadPool.Instance != null)
        {
            GD.Print($"Using thread pool for TectonicsOnly mesh generation");
            await MeshGenerationThreadPool.Instance.EnqueueTask(
                () => { try { GenerateFirstPass(); return 0; } catch (Exception e) { GD.PrintErr($"Error in GenerateFirstPass: {e.Message}\n{e.StackTrace}"); return 1; } },
                $"{Name}_firstpass",
                TaskPriority.High,
                Name
            );

            StrDb.IncrementMeshState();

            await MeshGenerationThreadPool.Instance.EnqueueTask(
                () => { try { GenerateSecondPass(); return 0; } catch (Exception e) { GD.PrintErr($"Error in GenerateSecondPass: {e.Message}\n{e.StackTrace}"); return 1; } },
                $"{Name}_secondpass",
                TaskPriority.High,
                Name
            );
        }
        else
        {
            Task firstPass = Task.Factory.StartNew(() => GenerateFirstPass());
            Task.WaitAll(firstPass);
            StrDb.IncrementMeshState();
            Task secondPass = Task.Factory.StartNew(() => GenerateSecondPass());
        }
    }

    /// <summary>
    /// Pipeline for generating celestial bodies using tectonic processes with noise-based deformation.
    /// This combines realistic tectonic features with procedural noise for enhanced detail.
    /// </summary>
    private async Task GenerateTectonicsWithNoisePipeline()
    {
        if (UseThreadPool && MeshGenerationThreadPool.Instance != null)
        {
            GD.Print($"Using thread pool for TectonicsWithNoise mesh generation");
            await MeshGenerationThreadPool.Instance.EnqueueTask(
                () => { try { GenerateFirstPassWithNoise(); return 0; } catch (Exception e) { GD.PrintErr($"Error in GenerateFirstPassWithNoise: {e.Message}\n{e.StackTrace}"); return 1; } },
                $"{Name}_firstpass_noise",
                TaskPriority.High,
                Name
            );

            StrDb.IncrementMeshState();

            await MeshGenerationThreadPool.Instance.EnqueueTask(
                () => { try { GenerateSecondPassWithNoise(); return 0; } catch (Exception e) { GD.PrintErr($"Error in GenerateSecondPassWithNoise: {e.Message}\n{e.StackTrace}"); return 1; } },
                $"{Name}_secondpass_noise",
                TaskPriority.High,
                Name
            );
        }
        else
        {
            Task firstPass = Task.Factory.StartNew(() => GenerateFirstPassWithNoise());
            Task.WaitAll(firstPass);
            StrDb.IncrementMeshState();
            Task secondPass = Task.Factory.StartNew(() => GenerateSecondPassWithNoise());
        }
    }

    /// <summary>
    /// Pipeline for generating celestial bodies using non-uniform scaling with noise-based deformation.
    /// This is suitable for asteroids, moons, and irregular celestial bodies.
    /// </summary>
    private async Task GenerateScalingWithNoisePipeline()
    {
        if (UseThreadPool && MeshGenerationThreadPool.Instance != null)
        {
            GD.Print($"Using thread pool for ScalingWithNoise mesh generation");
            await MeshGenerationThreadPool.Instance.EnqueueTask(
                () => { try { GenerateFirstPassScalingWithNoise(); return 0; } catch (Exception e) { GD.PrintErr($"Error in GenerateFirstPassScalingWithNoise: {e.Message}\n{e.StackTrace}"); return 1; } },
                $"{Name}_firstpass_scaling",
                TaskPriority.High,
                Name
            );

            StrDb.IncrementMeshState();

            await MeshGenerationThreadPool.Instance.EnqueueTask(
                () => { try { GenerateSecondPassScalingWithNoise(); return 0; } catch (Exception e) { GD.PrintErr($"Error in GenerateSecondPassScalingWithNoise: {e.Message}\n{e.StackTrace}"); return 1; } },
                $"{Name}_secondpass_scaling",
                TaskPriority.High,
                Name
            );
        }
        else
        {
            Task firstPass = Task.Factory.StartNew(() => GenerateFirstPassScalingWithNoise());
            Task.WaitAll(firstPass);
            StrDb.IncrementMeshState();
            Task secondPass = Task.Factory.StartNew(() => GenerateSecondPassScalingWithNoise());
        }
    }

    /// <summary>
    /// Pipeline for generating celestial bodies using only noise-based deformation.
    /// This creates purely procedural terrain features without tectonic processes or scaling.
    /// </summary>
    private async Task GenerateNoiseOnlyPipeline()
    {
        if (UseThreadPool && MeshGenerationThreadPool.Instance != null)
        {
            GD.Print($"Using thread pool for NoiseOnly mesh generation");
            await MeshGenerationThreadPool.Instance.EnqueueTask(
                () => { try { GenerateFirstPassNoiseOnly(); return 0; } catch (Exception e) { GD.PrintErr($"Error in GenerateFirstPassNoiseOnly: {e.Message}\n{e.StackTrace}"); return 1; } },
                $"{Name}_firstpass_noiseonly",
                TaskPriority.High,
                Name
            );

            StrDb.IncrementMeshState();

            await MeshGenerationThreadPool.Instance.EnqueueTask(
                () => { try { GenerateSecondPassNoiseOnly(); return 0; } catch (Exception e) { GD.PrintErr($"Error in GenerateSecondPassNoiseOnly: {e.Message}\n{e.StackTrace}"); return 1; } },
                $"{Name}_secondpass_noiseonly",
                TaskPriority.High,
                Name
            );
        }
        else
        {
            Task firstPass = Task.Factory.StartNew(() => GenerateFirstPassNoiseOnly());
            Task.WaitAll(firstPass);
            StrDb.IncrementMeshState();
            Task secondPass = Task.Factory.StartNew(() => GenerateSecondPassNoiseOnly());
        }
    }

    // Include all the existing methods from CelestialBodyMesh and SatelliteBodyMesh
    // First pass methods
    protected virtual void GenerateFirstPass()
    {
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
                    GD.PrintErr($"\x1b0;0r\x1b[2J\x1b[H\n");
                    GD.PrintErr($"Base Mesh Generation Error:  {e.Message}\n{e.StackTrace}\n");
                }
                return 0;
            }, emptyPercent);

        var OptimalArea = (4.0f * Mathf.Pi * size * size) / StrDb.Base.Triangles.Count;
        float OptimalSideLength = Mathf.Sqrt((OptimalArea * 4.0f) / Mathf.Sqrt(3.0f)) / 3f;

        Task<int> deformationTask = FunctionTimer.TimeFunction<Task<int>>(Name.ToString(), "Deformed Mesh Generation", async () =>
        {
            try
            {
                await baseMesh.InitiateDeformation(NumDeformationCycles, NumAbberations, OptimalSideLength);
            }
            catch (Exception e)
            {
                FunctionTimer.ResetScrollRegionAndClear();
                GD.PrintErr($"x1b0;0r\x1b[2J\x1b[H\nDeform Mesh Error:  {e.Message}\n{e.StackTrace}\n");
            }
            return 0;
        }, emptyPercent);

        deformationTask.Wait();
    }

    protected virtual void GenerateFirstPassWithNoise()
    {
        // Call base implementation first
        GenerateFirstPass();

        // Apply noise-based deformation for additional detail
        foreach (var vertex in StrDb.BaseVertices.Values)
        {
            float noiseValue = noise.GetNoise3D(
                vertex.Position.X * NoiseFrequency,
                vertex.Position.Y * NoiseFrequency,
                vertex.Position.Z * NoiseFrequency
            );

            Vector3 normal = vertex.Position.Normalized();
            Vector3 displacement = normal * noiseValue * NoiseAmplitude;
            vertex.Position += displacement;
            vertex.Height += displacement.Length();
        }
    }

    protected virtual void GenerateFirstPassScalingWithNoise()
    {
        // Generate base mesh first
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
                    GD.PrintErr($"\x1b0;0r\x1b[2J\x1b[H\n");
                    GD.PrintErr($"Base Mesh Generation Error:  {e.Message}\n{e.StackTrace}\n");
                }
                return 0;
            }, emptyPercent);

        // Apply non-uniform scaling to create ellipsoidal base shape
        foreach (var vertex in StrDb.BaseVertices.Values)
        {
            vertex.Position *= ScaleFactors;
        }

        // Apply noise-based deformation for irregularity
        foreach (var vertex in StrDb.BaseVertices.Values)
        {
            float noiseValue = noise.GetNoise3D(
                vertex.Position.X * NoiseFrequency,
                vertex.Position.Y * NoiseFrequency,
                vertex.Position.Z * NoiseFrequency
            );

            Vector3 normal = vertex.Position.Normalized();
            Vector3 displacement = normal * noiseValue * NoiseAmplitude;
            vertex.Position += displacement;
            vertex.Height += displacement.Length();
        }
    }

    protected virtual void GenerateFirstPassNoiseOnly()
    {
        // Generate base mesh first
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
                    GD.PrintErr($"\x1b0;0r\x1b[2J\x1b[H\n");
                    GD.PrintErr($"Base Mesh Generation Error:  {e.Message}\n{e.StackTrace}\n");
                }
                return 0;
            }, emptyPercent);

        // Apply only noise-based deformation
        foreach (var vertex in StrDb.BaseVertices.Values)
        {
            float noiseValue = noise.GetNoise3D(
                vertex.Position.X * NoiseFrequency,
                vertex.Position.Y * NoiseFrequency,
                vertex.Position.Z * NoiseFrequency
            );

            Vector3 normal = vertex.Position.Normalized();
            Vector3 displacement = normal * noiseValue * NoiseAmplitude;
            vertex.Position += displacement;
            vertex.Height += displacement.Length();
        }
    }

    // Second pass methods
    protected virtual async void GenerateSecondPass()
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
                    GD.PrintErr($"\u001b[2J\u001b[H");
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
            catch (Exception e) { GD.PrintErr($"\u001b[2J\u001b[H"); GD.PrintErr($"Flood Filling Error: {e.Message}\n{e.StackTrace}"); }
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
                GD.PrintErr($"\u001b[2J\u001b[H");
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
                GD.PrintErr($"\nHeight Average Error:  {e.Message}\n{e.StackTrace}\n");
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
                GD.PrintErr($"\nBoundary Stress Error:  {boundsError.Message}\n{boundsError.StackTrace}\n");
            }
            return 0;
        }, percent);
        percent.Reset();
        function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Apply Stress to Terrain", () =>
        {
            try
            {
                tectonics.ApplyStressToTerrain(continents, StrDb.VoronoiCells);
                for (int i = 0; i < 5; i++)
                {
                    FinalzeVertexHeights(StrDb.VoronoiCellVertices, continents);
                }
            }
            catch (Exception stressError)
            {
                GD.PrintErr($"\nStress Error:  {stressError.Message}\n{stressError.StackTrace}\n");
            }
            return 0;
        }, percent);
        percent.Reset();
        maxHeight = StrDb.VoronoiCellVertices.Max(p => p.Height);
        try
        {
            await AssignBiomes(continents, StrDb.VoronoiCells);
        }
        catch (Exception biomeError)
        {
            GD.PrintErr($"\nBiome Error:  {biomeError.Message}\n{biomeError.StackTrace}\n");
        }
        percent.Reset();
        percent.PercentTotal = continents.Values.Count;
        percent.PercentCurrent = 0;
        function = FunctionTimer.TimeFunction<int>(Name.ToString(), "Generate From Continents", () =>
        {
            try
            {
                GenerateFromContinents(continents);
            }
            catch (Exception genError)
            {
                GD.PrintErr($"\nGenerate From Continents Error:  {genError.Message}\n{genError.StackTrace}\n");
            }
            return 0;
        }, percent);
    }

    protected virtual async void GenerateSecondPassWithNoise()
    {
        // Call base implementation first
        GenerateSecondPass();

        // Apply additional noise to Voronoi cell vertices
        foreach (Point p in StrDb.VoronoiCellVertices)
        {
            float noiseValue = noise.GetNoise3D(
                p.Position.X * NoiseFrequency,
                p.Position.Y * NoiseFrequency,
                p.Position.Z * NoiseFrequency
            );

            Vector3 normal = p.Position.Normalized();
            Vector3 displacement = normal * noiseValue * NoiseAmplitude * 0.5f; // Reduced amplitude for second pass
            p.Position += displacement;
            p.Height += displacement.Length();
        }
    }

    protected virtual void GenerateSecondPassScalingWithNoise()
    {
        VoronoiCellGeneration voronoiCellGeneration = new VoronoiCellGeneration(StrDb);
        try
        {
            GenericPercent emptyPercent = new GenericPercent();
            GD.Print("Generating Voronoi Cells...");
            voronoiCellGeneration.GenerateVoronoiCells(emptyPercent, this);

            // First pass: apply scaling and noise to all points
            foreach (Point p in StrDb.VoronoiCellVertices)
            {
                p.Position *= ScaleFactors;

                float noiseValue = noise.GetNoise3D(
                    p.Position.X * NoiseFrequency,
                    p.Position.Y * NoiseFrequency,
                    p.Position.Z * NoiseFrequency
                );

                Vector3 normal = p.Position.Normalized();
                Vector3 displacement = normal * noiseValue * NoiseAmplitude;
                p.Position += displacement;
                p.Height += displacement.Length();
            }

            // Second pass: normalize to size while preserving proportions
            float maxDistance = StrDb.VoronoiCellVertices.Max(p => p.Position.Length());
            foreach (Point p in StrDb.VoronoiCellVertices)
            {
                float currentDistance = p.Position.Length();
                float scaleFactor = size * (currentDistance / maxDistance);
                p.Position = p.Position.Normalized() * scaleFactor;
            }
            GenerateSurfaceMesh(StrDb.VoronoiCells);
        }
        catch (Exception e)
        {
            GD.PrintRaw($"\u001b[2J\u001b[H");
            GD.PrintErr("Voronoi Cell Generation Error: " + e.Message + "\n" + e.StackTrace);
            Logger.Error($"Voronoi Cell Generation Error: {e.Message}\n{e.StackTrace}", "ERROR");
        }
    }

    protected virtual void GenerateSecondPassNoiseOnly()
    {
        VoronoiCellGeneration voronoiCellGeneration = new VoronoiCellGeneration(StrDb);
        try
        {
            GenericPercent emptyPercent = new GenericPercent();
            GD.Print("Generating Voronoi Cells...");
            voronoiCellGeneration.GenerateVoronoiCells(emptyPercent, this);

            // Apply noise to all points
            foreach (Point p in StrDb.VoronoiCellVertices)
            {
                float noiseValue = noise.GetNoise3D(
                    p.Position.X * NoiseFrequency,
                    p.Position.Y * NoiseFrequency,
                    p.Position.Z * NoiseFrequency
                );

                Vector3 normal = p.Position.Normalized();
                Vector3 displacement = normal * noiseValue * NoiseAmplitude;
                p.Position += displacement;
                p.Height += displacement.Length();
            }

            GenerateSurfaceMesh(StrDb.VoronoiCells);
        }
        catch (Exception e)
        {
            GD.PrintRaw($"\u001b[2J\u001b[H");
            GD.PrintErr("Voronoi Cell Generation Error: " + e.Message + "\n" + e.StackTrace);
            Logger.Error($"Voronoi Cell Generation Error: {e.Message}\n{e.StackTrace}", "ERROR");
        }
    }

    // Include all the helper methods from the original classes
    private async Task AssignBiomes(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
    {
        if (UseThreadPool && MeshGenerationThreadPool.Instance != null)
        {
            var biomeTasks = new List<Task>();

            foreach (var continent in continents)
            {
                var taskId = $"{Name}_biome_{continent.Key}";
                var task = MeshGenerationThreadPool.Instance.EnqueueTask(
                    () =>
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
                    },
                    taskId,
                    TaskPriority.Low,
                    Name
                );
                biomeTasks.Add(task);
            }

            await Task.WhenAll(biomeTasks);
        }
        else
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
    }

    public void UpdateVertexHeights(HashSet<Point> Vertices, Dictionary<int, Continent> Continents)
    {
        foreach (Point p in Vertices)
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

    public void FinalzeVertexHeights(HashSet<Point> Vertices, Dictionary<int, Continent> Continents)
    {
        foreach (Point p in Vertices)
        {
            var edges = StrDb.GetIncidentHalfEdges(p);
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
            Continent.CRUST_TYPE crustType = rand.RandiRange(0, 100) > 33 ? Continent.CRUST_TYPE.Oceanic : Continent.CRUST_TYPE.Continental;
            float averageHeight = crustType == Continent.CRUST_TYPE.Oceanic ? rand.RandfRange(-5.0f, 0.0f) : rand.RandfRange(1.2f, 7.0f);
            float rotation = Mathf.DegToRad(rand.RandiRange(-360, 360));
            float velocity = rand.RandfRange(0.3f, 1.7f);
            var continent = new Continent(startingCellIndex,
                    new List<VoronoiCell>(),
                    new HashSet<VoronoiCell>(),
                    new HashSet<Point>(),
                    new List<Point>(),
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0f, 0f, 0f),
                    new Vector2(rand.RandfRange(-1f, 1f), rand.RandfRange(-1f, 1f)), velocity, rotation,
                    crustType, averageHeight, rand.RandfRange(1.0f, 5.0f),
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
                Vector3 cellAverageCenter = Vector3.Zero;
                foreach (Point p in vc.Points)
                {
                    cellAverageCenter += p.Position;
                }
                cellAverageCenter /= vc.Points.Length;
                cellAverageCenter = cellAverageCenter.Normalized();

                float k = (1.0f - UnitNorm.X * cellAverageCenter.X - UnitNorm.Y * cellAverageCenter.Y - UnitNorm.Z * cellAverageCenter.Z) / (UnitNorm.X * UnitNorm.X + UnitNorm.Y * UnitNorm.Y + UnitNorm.Z * UnitNorm.Z);
                Vector3 projectedCenter = new Vector3(cellAverageCenter.X + k * UnitNorm.X, cellAverageCenter.Y + k * UnitNorm.Y, cellAverageCenter.Z + k * UnitNorm.Z);
                Vector3 projectedCenter2D = new Vector3(uAxis.Dot(projectedCenter), vAxis.Dot(projectedCenter), 0f);

                float radius = (continent.averagedCenter - cellAverageCenter).Length();
                Vector3 positionFromCenter = continent.averagedCenter - projectedCenter;

                vc.MovementDirection = new Vector2(continent.movementDirection.X * continent.velocity, continent.movementDirection.Y * continent.velocity) + new Vector2(-continent.rotation * projectedCenter2D.Y, continent.rotation * projectedCenter2D.X);
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

                var d = UnitNorm.X * (vc.Points[0].Position.X) + UnitNorm.Y * (vc.Points[0].Position.Y) + UnitNorm.Z * (vc.Points[0].Position.Z);
                var newZ = (d - (UnitNorm.X * vc.Points[0].Position.X) - (UnitNorm.Y * vc.Points[0].Position.Y)) / UnitNorm.Z;
                var newZ2 = (d / UnitNorm.Z);
                var directionPoint = uAxis * vc.MovementDirection.X + vAxis * vc.MovementDirection.Y;
                directionPoint *= vcRadius;
                directionPoint += cellAverageCenter;
                directionPoint = directionPoint.Normalized();
            }
        }

        return continents;
    }

    public void GenerateFromContinents(Dictionary<int, Continent> continents)
    {
        foreach (var keyValuePair in continents)
        {
            GenerateSurfaceMesh(keyValuePair.Value.cells);
            percent.PercentCurrent++;
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

    private Color GetBiomeColor(BiomeType biome, float height)
    {
        switch (biome)
        {
            case BiomeType.Tundra:
                return new Color(0.85f, 0.85f, 0.8f);
            case BiomeType.Icecap:
                return Colors.White;
            case BiomeType.Desert:
                return new Color(0.9f, 0.8f, 0.5f);
            case BiomeType.Grassland:
                return new Color(0.5f, 0.8f, 0.3f);
            case BiomeType.Forest:
                return new Color(0.2f, 0.6f, 0.2f);
            case BiomeType.Rainforest:
                return new Color(0.1f, 0.4f, 0.1f);
            case BiomeType.Taiga:
                return new Color(0.4f, 0.5f, 0.3f);
            case BiomeType.Ocean:
                return new Color(0.1f, 0.3f, 0.7f);
            case BiomeType.Coastal:
                return new Color(0.8f, 0.7f, 0.4f);
            case BiomeType.Mountain:
                return new Color(0.6f, 0.5f, 0.4f);
            default:
                return Colors.Gray;
        }
    }

    public virtual void GenerateSurfaceMesh(List<VoronoiCell> VoronoiList)
    {
        var arrMesh = Mesh as ArrayMesh;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var material = new ShaderMaterial();
        material.Shader = GD.Load<Shader>("res://Shaders/rocky_planet_shader.gdshader");
        material.NextPass = GD.Load<ShaderMaterial>("res://Materials/voronoi_outliner_material.tres");
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
                    st.SetUV2(new Vector2(vor.Index, 0));
                    st.SetNormal(tangent);
                    if (ProjectToSphere)
                    {
                        st.SetColor(GetBiomeColor(((Point)vor.Points[3 * i + j]).Biome, ((Point)vor.Points[3 * i + j]).Height));
                        st.AddVertex(vor.Points[3 * i + j].ToVector3() * (size + vor.Points[3 * i + j].Height / 10f));
                    }
                    else
                    {
                        st.AddVertex(new Vector3((vor.Points[3 * i + j]).Components[0], (vor.Points[3 * i + j]).Components[1], (vor.Points[3 * i + j]).Components[2]) * (size + (vor.Points[3 * i + j]).Components[2] / 10f));
                    }
                }
            }
        }
        st.CallDeferred("commit", arrMesh);
    }
}
