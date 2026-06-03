using System;
using System.IO;
using Godot;
using Godot.Collections;
using ProceduralGeneration.Data;
using Structures.Enums;
using Structures.Resources;

namespace UtilityLibrary.DataLoading;

public static class AUProbabilityLoader
{
    private static readonly Dictionary<OrbitalBodyType, AUProbabilityConfig> _configCache = new();
    private static BeltAUProbabilityConfig? _beltConfigCache;

    public static AUProbabilityConfig LoadForType(OrbitalBodyType bodyType)
    {
        if (_configCache.TryGetValue(bodyType, out var cachedConfig))
        {
            return cachedConfig;
        }

        string fileName = $"{bodyType}_AU.yaml";
        string fullPath = $"res://Configuration/AUProbability/{fileName}";

        try
        {
            var raw = TemplateLoader.LoadWithoutValidation(fullPath);
            var config = ParseConfig(raw, bodyType);
            _configCache[bodyType] = config;
            return config;
        }
        catch (FileNotFoundException)
        {
            GameLogger.Info($"AU probability config not found for {bodyType}, using default");
            var defaultConfig = CreateDefaultConfig(bodyType);
            _configCache[bodyType] = defaultConfig;
            return defaultConfig;
        }
        catch (Exception ex)
        {
            GameLogger.Warning($"Error loading AU probability config for {bodyType}: {ex.Message}");
            GD.PrintErr(
                $"Error loading AU Probability Config for {bodyType}: {ex.Message}\n{ex.StackTrace}"
            );
            var defaultConfig = CreateDefaultConfig(bodyType);
            _configCache[bodyType] = defaultConfig;
            return defaultConfig;
        }
    }

    public static BeltAUProbabilityConfig LoadBeltConfig()
    {
        if (_beltConfigCache != null)
        {
            return _beltConfigCache;
        }

        string fullPath = "res://Configuration/AUProbability/Belt_AU.yaml";

        try
        {
            var raw = TemplateLoader.LoadWithoutValidation(fullPath);
            var baseConfig = ParseConfig(raw, OrbitalBodyType.DwarfPlanet);
            _beltConfigCache = new BeltAUProbabilityConfig
            {
                BodyType = baseConfig.BodyType,
                SchemaVersion = baseConfig.SchemaVersion,
                MaxConsideredAU = baseConfig.MaxConsideredAU,
                RangeOverlapPolicy = baseConfig.RangeOverlapPolicy,
                DefaultSubtype = baseConfig.DefaultSubtype,
                AURanges = baseConfig.AURanges,
            };
            return _beltConfigCache;
        }
        catch (Exception ex)
        {
            GameLogger.Warning($"Error loading belt AU probability config: {ex.Message}");
            _beltConfigCache = new BeltAUProbabilityConfig
            {
                AURanges = new Array<AUProbabilityRange>
                {
                    new AUProbabilityRange
                    {
                        MinAU = 0f,
                        MaxAU = 100f,
                        Name = "default",
                        SubtypeDistribution = new Array<SubtypeProbability>
                        {
                            new SubtypeProbability
                            {
                                Subtype = BeltSubtype.AsteroidBelt,
                                Weight = 1.0f,
                            },
                        },
                    },
                },
                DefaultSubtype = BeltSubtype.AsteroidBelt,
            };
            return _beltConfigCache;
        }
    }

    public static void ClearCache()
    {
        _configCache.Clear();
        _beltConfigCache = null;
    }

    private static AUProbabilityConfig ParseConfig(
        Godot.Collections.Dictionary raw,
        OrbitalBodyType bodyType
    )
    {
        var config = new AUProbabilityConfig
        {
            BodyType = bodyType,
            SchemaVersion = raw.ContainsKey("schema_version")
                ? raw["schema_version"].ToString() ?? "1.0"
                : "1.0",
            MaxConsideredAU = raw.ContainsKey("max_considered_au")
                ? (float)raw["max_considered_au"]
                : 100f,
            RangeOverlapPolicy = raw.ContainsKey("range_overlap_policy")
                ? raw["range_overlap_policy"].ToString() ?? "use_first"
                : "use_first",
        };

        if (raw.ContainsKey("default_subtype"))
        {
            config.DefaultSubtype = ParseSubtypeFromString(
                bodyType,
                raw["default_subtype"].ToString()!
            );
        }
        else
        {
            config.DefaultSubtype = GetDefaultSubtype(bodyType);
        }

        if (raw.ContainsKey("au_ranges"))
        {
            var auRanges = raw["au_ranges"].As<Godot.Collections.Array>();
            GD.Print($"AU Ranges: {auRanges}");
            foreach (var rangeVariant in auRanges)
            {
                var rangeDict = rangeVariant.As<Godot.Collections.Dictionary>();
                config.AURanges.Add(ParseRange(rangeDict, bodyType));
            }
        }

        if (raw.ContainsKey("parent_subtype_modifiers"))
        {
            config.ParentSubtypeModifiers = ParseParentSubtypeModifiers(
                raw["parent_subtype_modifiers"].As<Godot.Collections.Dictionary>()
            );
        }

        return config;
    }

    /// <summary>
    /// Parses the <c>parent_subtype_modifiers:</c> section: immediate-parent-subtype-id →
    /// (target-subtype-id → multiplier). Keys are stable subtype id strings (see
    /// <c>BiomeIdMapper</c>); the manager keys candidate weights by the same ids.
    /// </summary>
    private static System.Collections.Generic.Dictionary<
        string,
        System.Collections.Generic.Dictionary<string, float>
    > ParseParentSubtypeModifiers(Godot.Collections.Dictionary raw)
    {
        var modifiers = new System.Collections.Generic.Dictionary<
            string,
            System.Collections.Generic.Dictionary<string, float>
        >();

        foreach (var parentKey in raw.Keys)
        {
            string parentId = parentKey.ToString() ?? "";
            if (string.IsNullOrEmpty(parentId))
            {
                continue;
            }

            var targetMap = new System.Collections.Generic.Dictionary<string, float>();
            var targetDict = raw[parentKey].As<Godot.Collections.Dictionary>();
            foreach (var targetKey in targetDict.Keys)
            {
                string targetId = targetKey.ToString() ?? "";
                if (!string.IsNullOrEmpty(targetId))
                {
                    targetMap[targetId] = (float)targetDict[targetKey];
                }
            }

            modifiers[parentId] = targetMap;
        }

        return modifiers;
    }

    private static AUProbabilityRange ParseRange(
        Godot.Collections.Dictionary rangeDict,
        OrbitalBodyType bodyType
    )
    {
        var range = new AUProbabilityRange();

        if (rangeDict.ContainsKey("range"))
        {
            var rangeBounds = rangeDict["range"].As<Godot.Collections.Dictionary>();
            range.MinAU = rangeBounds.ContainsKey("min_au") ? (float)rangeBounds["min_au"] : 0f;
            range.MaxAU = rangeBounds.ContainsKey("max_au") ? (float)rangeBounds["max_au"] : 100f;
            range.ExclusiveMax =
                rangeBounds.ContainsKey("exclusive_max") && (bool)rangeBounds["exclusive_max"];
        }

        range.Name = rangeDict.ContainsKey("name") ? rangeDict["name"].ToString() ?? "" : "";

        if (rangeDict.ContainsKey("subtype_distribution"))
        {
            var distribution = rangeDict["subtype_distribution"].As<Godot.Collections.Array>();
            foreach (var distVariant in distribution)
            {
                var distDict = distVariant.As<Godot.Collections.Dictionary>();
                var subtypeProb = new SubtypeProbability
                {
                    Subtype = ParseSubtypeFromString(bodyType, distDict["subtype"].ToString()!),
                    Weight = distDict.ContainsKey("weight") ? (float)distDict["weight"] : 1.0f,
                };

                if (distDict.ContainsKey("required_biomes"))
                {
                    var biomes = distDict["required_biomes"].As<Godot.Collections.Array>();
                    foreach (var biome in biomes)
                    {
                        subtypeProb.RequiredBiomes.Add(biome.ToString()!);
                    }
                }

                range.SubtypeDistribution.Add(subtypeProb);
            }
        }

        return range;
    }

    private static object ParseSubtypeFromString(OrbitalBodyType bodyType, string subtypeString)
    {
        // Moon/Asteroid/Comet all share the SatelliteSubtype enum via the BodyFamily map.
        if (bodyType.ToFamily() == BodyFamily.Satellite)
        {
            return Enum.Parse(typeof(SatelliteSubtype), subtypeString);
        }

        return bodyType switch
        {
            OrbitalBodyType.Star => Enum.Parse(typeof(StarSubtype), subtypeString),
            OrbitalBodyType.RockyPlanet => Enum.Parse(typeof(RockyPlanetSubtype), subtypeString),
            OrbitalBodyType.GasGiant => Enum.Parse(typeof(GasGiantSubtype), subtypeString),
            OrbitalBodyType.IceGiant => Enum.Parse(typeof(IceGiantSubtype), subtypeString),
            OrbitalBodyType.DwarfPlanet => Enum.Parse(typeof(DwarfPlanetSubtype), subtypeString),
            OrbitalBodyType.BlackHole => Enum.Parse(typeof(BlackHoleSubtype), subtypeString),
            OrbitalBodyType.NeutronStar => Enum.Parse(typeof(NeutronStarSubtype), subtypeString),
            _ => throw new ArgumentException(
                $"Unsupported body type for subtype parsing: {bodyType}"
            ),
        };
    }

    private static AUProbabilityConfig CreateDefaultConfig(OrbitalBodyType bodyType)
    {
        return new AUProbabilityConfig
        {
            BodyType = bodyType,
            SchemaVersion = "1.0",
            MaxConsideredAU = 100f,
            RangeOverlapPolicy = "use_first",
            AURanges = new Array<AUProbabilityRange>
            {
                new AUProbabilityRange
                {
                    MinAU = 0f,
                    MaxAU = 100f,
                    ExclusiveMax = false,
                    Name = "default_range",
                    SubtypeDistribution = new Array<SubtypeProbability>
                    {
                        new SubtypeProbability
                        {
                            Subtype = GetDefaultSubtype(bodyType),
                            Weight = 1.0f,
                        },
                    },
                },
            },
            DefaultSubtype = GetDefaultSubtype(bodyType),
        };
    }

    public static object GetDefaultSubtype(OrbitalBodyType bodyType)
    {
        return bodyType switch
        {
            OrbitalBodyType.RockyPlanet => RockyPlanetSubtype.Temperate,
            OrbitalBodyType.GasGiant => GasGiantSubtype.StandardJupiter,
            OrbitalBodyType.IceGiant => IceGiantSubtype.StandardNeptune,
            OrbitalBodyType.DwarfPlanet => DwarfPlanetSubtype.IcyKuiper,
            OrbitalBodyType.Star => StarSubtype.MainSequence,
            OrbitalBodyType.BlackHole => BlackHoleSubtype.StellarMass,
            OrbitalBodyType.NeutronStar => NeutronStarSubtype.Pulsar,
            OrbitalBodyType.Moon => SatelliteSubtype.RockyMoon,
            OrbitalBodyType.Asteroid => SatelliteSubtype.Carbonaceous,
            OrbitalBodyType.Comet => SatelliteSubtype.ShortPeriod,
            _ => StarSubtype.MainSequence,
        };
    }
}
