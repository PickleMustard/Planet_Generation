using Godot;
using Godot.Collections;
using Structures.Enums;

namespace ProceduralGeneration.Data;

public partial class AUProbabilityConfig : Resource
{
    public string SchemaVersion { get; set; } = "1.0";
    public CelestialBodyType BodyType { get; set; }
    public Array<AUProbabilityRange> AURanges { get; set; } = new();
    public object? DefaultSubtype { get; set; }
    public float MaxConsideredAU { get; set; } = 100f;
    public string RangeOverlapPolicy { get; set; } = "use_first";
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

public partial class SatelliteAUProbabilityConfig : AUProbabilityConfig
{
    public Dictionary<CelestialBodyType, AUProbabilityConfig> ParentBodyInfluence { get; set; } =
        new();
    public AUProbabilityConfig? DefaultConfig { get; set; }
}

public partial class BeltAUProbabilityConfig : AUProbabilityConfig { }
