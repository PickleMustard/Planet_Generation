using System.Collections.Generic;
using Structures.Enums;

namespace ProceduralGeneration.BiomeSystem;

public enum MoistureMode
{
    Whittaker,
    UniformRandom,
}

public class MoistureParams
{
    public MoistureMode Mode { get; set; } = MoistureMode.Whittaker;
    public float BaseOffset { get; set; } = 0f;
    public float LatitudeFactorDivisor { get; set; } = 9f;
    public float SizeFactorDivisor { get; set; } = 100f;
    public float RandomMin { get; set; } = 0f;
    public float RandomMax { get; set; } = 0f;
    public float MaxMoisture { get; set; } = 2.7f;
    public float ConstantOffset { get; set; } = 0f;
}

public class BiomeRuleConditions
{
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

public class BiomeRule
{
    public Biome.BiomeType Biome { get; set; }
    public BiomeRuleConditions When { get; set; } = new();
}

public class BiomeAssignerEntry
{
    public MoistureParams Moisture { get; set; } = new();
    public List<BiomeRule> Rules { get; set; } = new();
}

public class BiomeAssignerConfig
{
    public Dictionary<RockyPlanetSubtype, BiomeAssignerEntry> Assigners { get; set; } = new();
}
