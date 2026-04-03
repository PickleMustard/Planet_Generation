using System;
using System.IO;
using Godot;
using Godot.Collections;
using ProceduralGeneration.Data;
using Structures.Enums;

namespace UtilityLibrary.DataLoading;

public static class AUProbabilityLoader
{
    private static readonly Dictionary<CelestialBodyType, AUProbabilityConfig> _configCache = new();
    private static SatelliteAUProbabilityConfig? _satelliteConfigCache;
    private static BeltAUProbabilityConfig? _beltConfigCache;

    public static AUProbabilityConfig LoadForType(CelestialBodyType bodyType)
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

    public static SatelliteAUProbabilityConfig LoadSatelliteConfig()
    {
        if (_satelliteConfigCache != null)
        {
            return _satelliteConfigCache;
        }

        string fullPath = "res://Configuration/AUProbability/Satellite_AU.yaml";

        try
        {
            var raw = TemplateLoader.LoadWithoutValidation(fullPath);
            _satelliteConfigCache = ParseSatelliteConfig(raw);
            return _satelliteConfigCache;
        }
        catch (Exception ex)
        {
            GameLogger.Warning($"Error loading satellite AU probability config: {ex.Message}");
            _satelliteConfigCache = CreateDefaultSatelliteConfig();
            return _satelliteConfigCache;
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
            var baseConfig = ParseConfig(raw, CelestialBodyType.DwarfPlanet);
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
        _satelliteConfigCache = null;
        _beltConfigCache = null;
    }

    private static AUProbabilityConfig ParseConfig(
        Godot.Collections.Dictionary raw,
        CelestialBodyType bodyType
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

        return config;
    }

    private static SatelliteAUProbabilityConfig ParseSatelliteConfig(
        Godot.Collections.Dictionary raw
    )
    {
        var config = new SatelliteAUProbabilityConfig
        {
            SchemaVersion = raw.ContainsKey("schema_version")
                ? raw["schema_version"].ToString() ?? "1.0"
                : "1.0",
            MaxConsideredAU = raw.ContainsKey("max_considered_au")
                ? (float)raw["max_considered_au"]
                : 100f,
        };

        // Parse parent_body_influence
        if (raw.ContainsKey("parent_body_influence"))
        {
            var influenceDict = raw["parent_body_influence"].As<Godot.Collections.Dictionary>();
            foreach (var key in influenceDict.Keys)
            {
                string parentTypeStr = key.ToString()!;
                if (Enum.TryParse<CelestialBodyType>(parentTypeStr, out var parentType))
                {
                    var parentData = influenceDict[key].As<Godot.Collections.Dictionary>();
                    var parentConfig = new AUProbabilityConfig { BodyType = parentType };

                    if (parentData.ContainsKey("au_ranges"))
                    {
                        var ranges = parentData["au_ranges"].As<Godot.Collections.Array>();
                        foreach (var rangeVariant in ranges)
                        {
                            var rangeDict = rangeVariant.As<Godot.Collections.Dictionary>();
                            parentConfig.AURanges.Add(ParseSatelliteRange(rangeDict));
                        }
                    }

                    config.ParentBodyInfluence[parentType] = parentConfig;
                }
            }
        }

        // Parse default section
        if (raw.ContainsKey("default"))
        {
            var defaultData = raw["default"].As<Godot.Collections.Dictionary>();
            config.DefaultConfig = new AUProbabilityConfig();

            if (defaultData.ContainsKey("au_ranges"))
            {
                var ranges = defaultData["au_ranges"].As<Godot.Collections.Array>();
                foreach (var rangeVariant in ranges)
                {
                    var rangeDict = rangeVariant.As<Godot.Collections.Dictionary>();
                    config.DefaultConfig.AURanges.Add(ParseSatelliteRange(rangeDict));
                }
            }
        }
        else
        {
            config.DefaultConfig = new AUProbabilityConfig
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
                                Subtype = SatelliteSubtype.RockyMoon,
                                Weight = 1.0f,
                            },
                        },
                    },
                },
            };
        }

        return config;
    }

    private static AUProbabilityRange ParseRange(
        Godot.Collections.Dictionary rangeDict,
        CelestialBodyType bodyType
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

    private static AUProbabilityRange ParseSatelliteRange(Godot.Collections.Dictionary rangeDict)
    {
        var range = new AUProbabilityRange();

        if (rangeDict.ContainsKey("range"))
        {
            var rangeBounds = rangeDict["range"].As<Godot.Collections.Dictionary>();
            range.MinAU = rangeBounds.ContainsKey("min_au") ? (float)rangeBounds["min_au"] : 0f;
            range.MaxAU = rangeBounds.ContainsKey("max_au") ? (float)rangeBounds["max_au"] : 100f;
        }

        range.Name = rangeDict.ContainsKey("name") ? rangeDict["name"].ToString() ?? "" : "";

        if (rangeDict.ContainsKey("subtype_distribution"))
        {
            var distribution = rangeDict["subtype_distribution"].As<Godot.Collections.Array>();
            foreach (var distVariant in distribution)
            {
                var distDict = distVariant.As<Godot.Collections.Dictionary>();
                string subtypeStr = distDict["subtype"].ToString()!;

                var subtypeProb = new SubtypeProbability
                {
                    Subtype = Enum.Parse(typeof(SatelliteSubtype), subtypeStr),
                    Weight = distDict.ContainsKey("weight") ? (float)distDict["weight"] : 1.0f,
                };

                range.SubtypeDistribution.Add(subtypeProb);
            }
        }

        return range;
    }

    private static object ParseSubtypeFromString(CelestialBodyType bodyType, string subtypeString)
    {
        return bodyType switch
        {
            CelestialBodyType.Star => Enum.Parse(typeof(StarSubtype), subtypeString),
            CelestialBodyType.RockyPlanet => Enum.Parse(typeof(RockyPlanetSubtype), subtypeString),
            CelestialBodyType.GasGiant => Enum.Parse(typeof(GasGiantSubtype), subtypeString),
            CelestialBodyType.IceGiant => Enum.Parse(typeof(IceGiantSubtype), subtypeString),
            CelestialBodyType.DwarfPlanet => Enum.Parse(typeof(DwarfPlanetSubtype), subtypeString),
            CelestialBodyType.BlackHole => Enum.Parse(typeof(BlackHoleSubtype), subtypeString),
            CelestialBodyType.NeutronStar => Enum.Parse(typeof(NeutronStarSubtype), subtypeString),
            _ => throw new ArgumentException(
                $"Unsupported body type for subtype parsing: {bodyType}"
            ),
        };
    }

    private static AUProbabilityConfig CreateDefaultConfig(CelestialBodyType bodyType)
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

    private static SatelliteAUProbabilityConfig CreateDefaultSatelliteConfig()
    {
        return new SatelliteAUProbabilityConfig
        {
            DefaultConfig = new AUProbabilityConfig
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
                                Subtype = SatelliteSubtype.RockyMoon,
                                Weight = 1.0f,
                            },
                        },
                    },
                },
            },
        };
    }

    public static object GetDefaultSubtype(CelestialBodyType bodyType)
    {
        return bodyType switch
        {
            CelestialBodyType.RockyPlanet => RockyPlanetSubtype.Temperate,
            CelestialBodyType.GasGiant => GasGiantSubtype.StandardJupiter,
            CelestialBodyType.IceGiant => IceGiantSubtype.StandardNeptune,
            CelestialBodyType.DwarfPlanet => DwarfPlanetSubtype.IcyKuiper,
            CelestialBodyType.Star => StarSubtype.MainSequence,
            CelestialBodyType.BlackHole => BlackHoleSubtype.StellarMass,
            CelestialBodyType.NeutronStar => NeutronStarSubtype.Pulsar,
            _ => StarSubtype.MainSequence,
        };
    }
}
