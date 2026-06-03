using Godot;
using Godot.Collections;
using Structures.Enums;

namespace ProceduralGeneration.Data;

public partial class AUProbabilityConfig : Resource
{
    public string SchemaVersion { get; set; } = "1.0";
    public OrbitalBodyType BodyType { get; set; }
    public Array<AUProbabilityRange> AURanges { get; set; } = new();
    public object? DefaultSubtype { get; set; }
    public float MaxConsideredAU { get; set; } = 100f;
    public string RangeOverlapPolicy { get; set; } = "use_first";

    /// <summary>
    /// Optional per-parent-subtype weight multipliers, keyed by the immediate parent's
    /// subtype id (e.g. <c>subtype_ice_giant_standard_neptune</c>) → (target subtype id →
    /// multiplier). Applied on top of the matched AU range's base weights when this body is a
    /// satellite of a parent whose subtype id appears here; unlisted entries default to 1.0.
    /// </summary>
    public System.Collections.Generic.Dictionary<
        string,
        System.Collections.Generic.Dictionary<string, float>
    > ParentSubtypeModifiers
    { get; set; } = new();
}

public partial class AUProbabilityRange : Resource
{
    public float MinAU { get; set; }
    public float MaxAU { get; set; }
    public bool ExclusiveMax { get; set; }
    public string Name { get; set; } = "";
    public Array<SubtypeProbability> SubtypeDistribution { get; set; } = new();
}

public partial class SubtypeProbability : Resource
{
    public object Subtype { get; set; } = null!;
    public float Weight { get; set; }
    public Array<string> RequiredBiomes { get; set; } = new();
}

public partial class BeltAUProbabilityConfig : AUProbabilityConfig { }
