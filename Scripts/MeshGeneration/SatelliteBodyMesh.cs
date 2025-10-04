using Godot;
using Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UtilityLibrary;

/// <summary>
/// Child class of CelestialBodyMesh for generating irregular asteroid-like meshes.
/// Extends the base spherical mesh generation with non-uniform scaling and noise-based deformation.
/// </summary>
/// <remarks>
/// This class adds parameters for ellipsoidal scaling and procedural noise to create exaggerated,
/// irregular shapes suitable for asteroids or other non-spherical celestial bodies.
/// The generation process inherits the full pipeline from CelestialBodyMesh but applies
/// additional deformations in the first pass.
/// </remarks>
namespace MeshGeneration;
public partial class SatelliteBodyMesh : CelestialBodyMesh
{
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

    /// <summary>
    /// Initializes the SatelliteBodyMesh with noise generator setup.
    /// Called when the node is ready in Godot's lifecycle.
    /// </summary>
    public override void _Ready()
    {
        base._Ready();
        noise = new FastNoiseLite();
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        noise.FractalOctaves = NoiseOctaves;
        noise.FractalLacunarity = 2.0f;
        noise.FractalGain = 0.5f;
        GenerateMesh();
    }

    public override void GenerateMesh()
    {
        this.Mesh = new ArrayMesh();
        ShouldDrawArrows = ShouldDrawArrowsInterface;
        StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
        GD.Print($"Rand Seed: {rand.Seed}\n");
        List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();
        GenerateFirstPass();
        GenerateSecondPass();
        GenerateSurfaceMesh(StrDb.VoronoiCells);

    }

    /// <summary>
    /// Overrides the first pass of mesh generation to include non-uniform scaling and noise deformation.
    /// This method extends the base implementation with asteroid-specific features.
    /// </summary>
    /// <remarks>
    /// The process:
    /// 1. Calls the base GenerateFirstPass() to create the initial mesh.
    /// 2. Applies non-uniform scaling to vertices for ellipsoidal shape.
    /// 3. Applies noise-based displacement for irregular surface features.
    /// 4. Ensures the mesh remains valid for further processing.
    /// </remarks>
    protected override void GenerateFirstPass()
    {
        // Call base implementation to generate the initial spherical mesh
        base.GenerateFirstPass();

        // Apply non-uniform scaling to create ellipsoidal base shape
        foreach (var vertex in StrDb.BaseVertices.Values)
        {
            vertex.Position *= ScaleFactors;
            // Optional: Re-normalize if extreme scaling breaks spherical assumption
            if (ProjectToSphere)
            {
                //vertex.Position = vertex.Position.Normalized() * size;
            }
        }

        // Apply noise-based deformation for irregularity
        foreach (var vertex in StrDb.BaseVertices.Values)
        {
            // Compute noise value based on vertex position and frequency
            float noiseValue = noise.GetNoise3D(
                vertex.Position.X * NoiseFrequency,
                vertex.Position.Y * NoiseFrequency,
                vertex.Position.Z * NoiseFrequency
            );

            // Displace vertex along its normal for surface irregularity
            Vector3 normal = vertex.Position.Normalized();
            Vector3 displacement = normal * noiseValue * NoiseAmplitude;
            vertex.Position += displacement;

            // Update height for consistency with base class
            vertex.Height += displacement.Length();
        }

        GD.Print("Applied non-uniform scaling and noise deformation to SatelliteBodyMesh.");
    }

    protected override void GenerateSecondPass()
    {

        VoronoiCellGeneration voronoiCellGeneration = new VoronoiCellGeneration(StrDb);
        try
        {
            GenericPercent emptyPercent = new GenericPercent();
            GD.Print("Generating Voronoi Cells...");
            voronoiCellGeneration.GenerateVoronoiCells(emptyPercent, this);
            GD.Print($"Voronoi Cell Generation Completed, {StrDb.VoronoiCells.Count} Voronoi Cells Generated.");
            foreach (Point p in StrDb.VoronoiCellVertices)
            {
                p.Position *= ScaleFactors;
                // Compute noise value based on vertex position and frequency
                float noiseValue = noise.GetNoise3D(
                    p.Position.X * NoiseFrequency,
                    p.Position.Y * NoiseFrequency,
                    p.Position.Z * NoiseFrequency
                );

                // Displace vertex along its normal for surface irregularity
                Vector3 normal = p.Position.Normalized();
                Vector3 displacement = normal * noiseValue * NoiseAmplitude;
                p.Position += displacement;

                // Update height for consistency with base class
                p.Height += displacement.Length();

            }
        }
        catch (Exception e)
        {
            GD.PrintRaw($"\u001b[2J\u001b[H");
            Logger.Error($"Voronoi Cell Generation Error: {e.Message}\n{e.StackTrace}", "ERROR");
        }
    }
}
