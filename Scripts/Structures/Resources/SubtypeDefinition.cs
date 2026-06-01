using System.Collections.Generic;

namespace Structures.Resources;

/// <summary>
/// Data-driven planetary / celestial body subtype loaded from
/// <c>Configuration/Subtypes/&lt;family&gt;_subtypes.yaml</c>.
/// Replaces the per-family subtype enums (<c>RockyPlanetSubtype</c>, <c>GasGiantSubtype</c>, …).
/// </summary>
public sealed class SubtypeDefinition
{
    /// <summary>Stable string ID. e.g. <c>subtype_rocky_temperate</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable name. e.g. "Temperate".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Which body-generation pipeline this subtype belongs to.</summary>
    public BodyFamily Family { get; set; }

    /// <summary>Atmosphere min/max range used by templated body generation.</summary>
    public float AtmosphereMin { get; set; }
    public float AtmosphereMax { get; set; }

    /// <summary>
    /// Base hazard score for the subtype (independent of per-biome hazards).
    /// </summary>
    public float BaseHazard { get; set; }

    /// <summary>
    /// Moisture-distribution parameters used by the biome assigner for surface biome
    /// placement. Stored opaquely as a dictionary so the registry has no compile-time
    /// dependency on the procedural-generation layer. The biome assigner config loader
    /// projects this onto its <c>MoistureParams</c> type.
    /// </summary>
    public Dictionary<string, float> MoistureParams { get; set; } = new();

    /// <summary>Moisture mode: <c>whittaker</c> or <c>uniform_random</c>.</summary>
    public string MoistureMode { get; set; } = "whittaker";

    /// <summary>
    /// Ordered biome-assignment rules for surface generation. Each rule references a
    /// biome by <see cref="BiomeDefinition.Id"/> and has optional conditions on
    /// height / moisture / latitude.
    /// </summary>
    public List<BiomeRuleDefinition> AssignerRules { get; set; } = new();

    /// <summary>
    /// Resource configuration: groups to include, plus explicit add/remove lists and
    /// base weight multiplier.
    /// </summary>
    public List<string> ResourceGroups { get; set; } = new();
    public List<string> AddResources { get; set; } = new();
    public List<string> RemoveResources { get; set; } = new();
    public float BaseResourceWeight { get; set; } = 1.0f;

    /// <summary>
    /// Per-knob ranges for the base mesh stage (e.g. <c>subdivisions</c>,
    /// <c>num_abberations</c>, <c>num_deformation_cycles</c>). At body generation time
    /// the SubtypeGenParamResolver rolls a value inside each range. A missing key
    /// means "fall through to the SystemGen type-level default."
    /// </summary>
    public Dictionary<string, FloatRange> MeshRanges { get; set; } = new();

    /// <summary>
    /// Per-subdivision-level vertex-count ranges. Each entry's Min/Max are integer counts
    /// (rolled to int by the resolver). Index 0 is the first subdivision pass, index 1 the
    /// second, etc. If the rolled <c>subdivisions</c> count exceeds the list length, extra
    /// levels reuse the last entry — matches the legacy
    /// <c>CalculateBaseMeshFromParams</c> clamp behavior.
    /// </summary>
    public List<FloatRange> VerticesPerEdgeRanges { get; set; } = new();

    /// <summary>
    /// Per-knob ranges for the tectonic stage (stress_scale, shear_scale, num_continents,
    /// propagation params, general scales). Empty for non-tectonic families (gas/ice giants,
    /// stars, belts).
    /// </summary>
    public Dictionary<string, FloatRange> TectonicRanges { get; set; } = new();

    /// <summary>
    /// When true, the tectonic stage samples plate velocity at the shared edge midpoint to
    /// eliminate the ω×r discontinuity across boundaries. Not a range; a single per-subtype flag.
    /// </summary>
    public bool SampleVelocityAtEdgeMidpoint { get; set; } = true;

    /// <summary>
    /// Per-knob ranges for the spherical-harmonics deformer: <c>amplitude</c> plus the four
    /// per-band scales <c>band_scale_l0</c>..<c>band_scale_l3</c>.
    /// </summary>
    public Dictionary<string, FloatRange> SphericalHarmonicsRanges { get; set; } = new();
}

/// <summary>
/// Single ordered biome-assignment rule. Mirrors the runtime <c>BiomeRule</c> but
/// references its biome by stable string ID instead of the legacy <c>Biome.BiomeType</c> enum.
/// </summary>
public sealed class BiomeRuleDefinition
{
    /// <summary><see cref="BiomeDefinition.Id"/> of the biome this rule assigns.</summary>
    public string BiomeId { get; set; } = string.Empty;

    public float? HeightAbove { get; set; }
    public float? HeightBelow { get; set; }
    public float? MoistureAbove { get; set; }
    public float? MoistureBelow { get; set; }
    public float? AbsLatitudeAbove { get; set; }
    public float? AbsLatitudeBelow { get; set; }

    public bool IsEmpty =>
        HeightAbove == null && HeightBelow == null
        && MoistureAbove == null && MoistureBelow == null
        && AbsLatitudeAbove == null && AbsLatitudeBelow == null;
}
