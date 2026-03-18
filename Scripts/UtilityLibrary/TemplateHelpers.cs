using System;
using Godot;
using Godot.Collections;
using Structures.Enums;

namespace UtilityLibrary;

public static class TemplateHelpers
{
    private const float Limit = 10000f;

    public static Dictionary GetCelestialBodyDefaults(CelestialBodyType type)
    {
        var path = GetYamlPath(type);
        GD.Print($"Getting defaults for {type} from {path}");

        try
        {
            var raw = TemplateLoader.Load(path, TemplateLoader.CelestialBodyValidator);
            return TransformCelestialBodyTemplate(raw, type);
        }
        catch (Exception e)
        {
            GD.PrintErr($"Could not load Celestial Body template: {e.Message}\n{e.StackTrace}");
            return GetFallbackCelestial();
        }
    }

    public static Dictionary GetSatelliteBodyDefaults(SatelliteBodyType type)
    {
        var path = GetYamlPath(type);

        try
        {
            var raw = TemplateLoader.Load(path, TemplateLoader.CelestialBodyValidator);
            return TransformSatelliteBodyTemplate(raw, type);
        }
        catch (Exception e)
        {
            GD.PrintErr($"Could not load Satellite Body template: {e.Message}\n{e.StackTrace}");
            return GetFallbackSatellite();
        }
    }

    public static Dictionary GetSatelliteGroupDefaults(SatelliteGroupTypes type)
    {
        var path = GetYamlPath(type);

        try
        {
            var raw = TemplateLoader.Load(path, TemplateLoader.CelestialBodyValidator);
            return TransformSatelliteGroupTemplate(raw);
        }
        catch (Exception e)
        {
            GD.PrintErr($"Could not load Satellite Group template: {e.Message}\n{e.StackTrace}");
            return GetFallbackSatelliteGroup();
        }
    }

    public static Array<Dictionary> LoadSystemTemplate(string fileName)
    {
        var bodies = new Array<Dictionary>();

        try
        {
            var raw = TemplateLoader.Load(fileName, TemplateLoader.SystemTemplateValidator);
            if (
                raw.TryGetValue("bodies", out var bodiesVariant)
                && bodiesVariant.As<Godot.Collections.Array>() is Godot.Collections.Array bodyList
            )
            {
                int dominantBodyIndex = 0;

                foreach (var bodyVariant in bodyList)
                {
                    var bodyRaw = bodyVariant.AsGodotDictionary();
                    var typeStr = ReadString(bodyRaw, "type", "Star").Replace(" ", "");

                    // Check if this top-level entry is a satellite belt type (new flat format)
                    if (Enum.TryParse<SatelliteGroupTypes>(typeStr, out _))
                    {
                        // Top-level belt entry — transform as satellite belt
                        var beltResult = TransformSystemTemplateSatellite(bodyRaw);
                        beltResult["is_top_level_belt"] = true;
                        // Preserve orbital_center_index if present
                        if (!beltResult.ContainsKey("orbital_center_index"))
                            beltResult["orbital_center_index"] = (int)ReadFloat(
                                bodyRaw,
                                "orbital_center_index",
                                -1f
                            );
                        bodies.Add(beltResult);
                        continue;
                    }

                    var transformed = TransformSystemTemplateBody(bodyRaw);
                    bool isDominant = IsDominantBodyTypeString(typeStr);

                    // For the old YAML format: extract belt-type satellites from the body's
                    // satellites array and promote them to top-level entries so the UI can
                    // place them in the dedicated Satellite Belts section.
                    if (isDominant && transformed.ContainsKey("satellites"))
                    {
                        var satellites = (Array<Dictionary>)transformed["satellites"];
                        var individualSatellites = new Array<Dictionary>();

                        foreach (var sat in satellites)
                        {
                            var satTypeStr = ReadString(sat, "type", "Moon");
                            if (Enum.TryParse<SatelliteGroupTypes>(satTypeStr, out _))
                            {
                                // Belt-type satellite: promote to top-level entry
                                sat["is_top_level_belt"] = true;
                                sat["orbital_center_index"] = dominantBodyIndex;
                                bodies.Add(sat);
                            }
                            else
                            {
                                // Individual satellite: keep as child of parent body
                                individualSatellites.Add(sat);
                            }
                        }

                        // Replace satellites array with only individual satellites
                        if (individualSatellites.Count > 0)
                            transformed["satellites"] = individualSatellites;
                        else
                            transformed.Remove("satellites");
                    }

                    bodies.Add(transformed);
                    if (isDominant)
                        dominantBodyIndex++;
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error loading system template {fileName}: {e.Message}\n{e.StackTrace}");
        }

        return bodies;
    }

    private static Dictionary TransformCelestialBodyTemplate(Dictionary raw, CelestialBodyType type)
    {
        var result = new Dictionary();

        if (!raw.TryGetValue("celestial", out var celestialVariant))
        {
            GD.PrintErr("Template missing 'celestial' section");
            return result;
        }

        var celestial = celestialVariant.AsGodotDictionary();

        if (celestial.TryGetValue("template", out var templateVariant))
        {
            var templateRaw = templateVariant.AsGodotDictionary();
            result["template"] = TransformCelestialTemplate(templateRaw, type);
        }

        if (celestial.TryGetValue("mesh", out var meshVariant))
        {
            var mesh = meshVariant.AsGodotDictionary();

            if (mesh.TryGetValue("base_mesh", out var baseMeshVariant))
            {
                result["base_mesh"] = baseMeshVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("tectonic", out var tectonicVariant))
            {
                result["tectonics"] = tectonicVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("scaling", out var scalingVariant))
            {
                result["scaling_settings"] = scalingVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("spherical_harmonics", out var shVariant))
            {
                result["spherical_harmonics_settings"] = shVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("noise_settings", out var noiseVariant))
            {
                result["noise_settings"] = noiseVariant.AsGodotDictionary();
            }
        }

        if (celestial.TryGetValue("resources", out var resourcesVariant))
        {
            result["resources"] = resourcesVariant.AsGodotDictionary();
        }

        var nameFileName = GetNameFileForCelestialBodyType(type);
        result["possible_names"] = ExtractNameCategories(raw, nameFileName);

        return result;
    }

    private static Dictionary TransformSatelliteBodyTemplate(Dictionary raw, SatelliteBodyType type)
    {
        var result = new Dictionary();

        if (!raw.TryGetValue("satellite", out var satelliteVariant))
        {
            GD.PrintErr("Template missing 'satellite' section");
            return result;
        }

        var satellite = satelliteVariant.AsGodotDictionary();

        if (satellite.TryGetValue("template", out var templateVariant))
        {
            var templateRaw = templateVariant.AsGodotDictionary();
            result["template"] = TransformSatelliteTemplate(templateRaw);
        }

        if (satellite.TryGetValue("mesh", out var meshVariant))
        {
            var mesh = meshVariant.AsGodotDictionary();

            if (mesh.TryGetValue("base_mesh", out var baseMeshVariant))
            {
                result["base_mesh"] = baseMeshVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("tectonic", out var tectonicVariant))
            {
                result["tectonics"] = tectonicVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("scaling", out var scalingVariant))
            {
                result["scaling_settings"] = scalingVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("spherical_harmonics", out var shVariant))
            {
                result["spherical_harmonics_settings"] = shVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("noise_settings", out var noiseVariant))
            {
                result["noise_settings"] = noiseVariant.AsGodotDictionary();
            }
        }

        if (satellite.TryGetValue("resources", out var resourcesVariant))
        {
            result["resources"] = resourcesVariant.AsGodotDictionary();
        }

        var nameFileName = GetNameFileForSatelliteType(type);
        result["possible_names"] = ExtractNameCategories(raw, nameFileName);

        return result;
    }

    private static Dictionary TransformSatelliteGroupTemplate(Dictionary raw)
    {
        var result = new Dictionary();

        if (!raw.TryGetValue("satellite_group", out var groupVariant))
        {
            GD.PrintErr("Template missing 'satellite_group' section");
            return result;
        }

        var group = groupVariant.AsGodotDictionary();

        if (group.TryGetValue("template", out var templateVariant))
        {
            var templateRaw = templateVariant.AsGodotDictionary();
            result["template"] = TransformSatelliteGroupTemplateSection(templateRaw);
        }

        return result;
    }

    private static bool IsDominantBodyType(CelestialBodyType type)
    {
        return type == CelestialBodyType.Star || type == CelestialBodyType.BlackHole;
    }

    private static bool IsDominantBodyTypeString(string typeStr)
    {
        return typeStr.Equals("Star", StringComparison.OrdinalIgnoreCase)
            || typeStr.Equals("BlackHole", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary TransformCelestialTemplate(Dictionary raw, CelestialBodyType type)
    {
        var result = new Dictionary();

        if (IsDominantBodyType(type))
        {
            result["distance"] = ReadFloat(raw, "distance", 500f);
            result["eccentricity"] = ReadFloat(raw, "eccentricity", 0.1f);
            result["starting_angle"] = ReadFloat(raw, "starting_angle", 0f);
            result["vertical_offset"] = ReadFloat(raw, "vertical_offset", 0f);
        }
        else
        {
            // Planetary bodies use orbital parameters
            result["apogee"] = ReadFloat(raw, "apogee", 500f);
            result["perigee"] = ReadFloat(raw, "perigee", 300f);
            result["starting_angle"] = ReadFloat(raw, "starting_angle", 0f);
            result["vertical_offset"] = ReadFloat(raw, "vertical_offset", 0f);
        }

        result["mass"] = ReadFloat(raw, "mass", 1f);
        result["size"] = ReadFloat(raw, "size", 1f);

        return result;
    }

    private static Dictionary TransformSatelliteTemplate(Dictionary raw)
    {
        var result = new Dictionary();

        // Replace position/velocity with orbital parameters
        result["apogee"] = ReadFloat(raw, "apogee", 500f);
        result["perigee"] = ReadFloat(raw, "perigee", 300f);
        result["starting_angle"] = ReadFloat(raw, "starting_angle", 0f);
        result["vertical_offset"] = ReadFloat(raw, "vertical_offset", 0f);

        var sizeRange = ReadFloatRange(raw, "size_range", (1f, 4f));
        var massRange = ReadFloatRange(raw, "mass_range", (1f, 10f));

        result["mass"] = massRange.Item1;
        result["size"] = sizeRange.Item1;

        return result;
    }

    private static Dictionary TransformSatelliteGroupTemplateSection(Dictionary raw)
    {
        var result = new Dictionary();

        var numRange = ReadIntRange(raw, "number_asteroids", (1, 4));
        result["lower_range"] = numRange.Item1;
        result["upper_range"] = numRange.Item2;

        result["ring_apogee"] = ReadFloat(raw, "apogee", 0f);
        result["ring_perigee"] = ReadFloat(raw, "perigee", 0f);
        result["ring_velocity"] = ReadVector3(raw, "ring_velocity", Vector3.Zero);
        result["grouping"] = ReadString(raw, "grouping", "Balanced");

        var sizeRange = ReadFloatRange(raw, "size_range", (1f, 5f));
        result["size_min"] = sizeRange.Item1;
        result["size_max"] = sizeRange.Item2;

        var massRange = ReadFloatRange(raw, "mass_range", (1f, 10f));
        result["mass_min"] = massRange.Item1;
        result["mass_max"] = massRange.Item2;

        return result;
    }

    private static Dictionary TransformSystemTemplateBody(Dictionary raw)
    {
        var result = new Dictionary();

        string typeStr = ReadString(raw, "type", "Star");
        result["type"] = typeStr;

        var template = new Dictionary();
        template["mass"] = ReadFloat(raw, "mass", 1f);
        template["size"] = ReadFloat(raw, "size", 1f);

        if (IsDominantBodyTypeString(typeStr))
        {
            // Dominant bodies use position/velocity
            template["position"] = ReadVector3(raw, "position", Vector3.Zero);
            template["velocity"] = ReadVector3(raw, "velocity", Vector3.Zero);
        }
        else
        {
            // Planetary bodies use orbital parameters (at top level of result, not inside template)
            var orbitalParams = new Dictionary();
            orbitalParams["apogee"] = ReadFloat(raw, "apogee", 1000f);
            orbitalParams["perigee"] = ReadFloat(raw, "perigee", 500f);
            orbitalParams["starting_angle"] = ReadFloat(raw, "starting_angle", 0f);
            orbitalParams["vertical_offset"] = ReadFloat(raw, "vertical_offset", 0f);
            orbitalParams["orbital_center_index"] = (int)ReadFloat(
                raw,
                "orbital_center_index",
                -1f
            );
            result["orbital_parameters"] = orbitalParams;
        }

        result["template"] = template;

        if (raw.TryGetValue("mesh", out var meshVariant))
        {
            var mesh = meshVariant.AsGodotDictionary();

            if (mesh.TryGetValue("base_mesh", out var baseMeshVariant))
            {
                result["base_mesh"] = baseMeshVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("tectonic", out var tectonicVariant))
            {
                result["tectonics"] = tectonicVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("scaling", out var scalingVariant))
            {
                result["scaling_settings"] = scalingVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("spherical_harmonics", out var shVariant))
            {
                result["spherical_harmonics_settings"] = shVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("noise_settings", out var noiseVariant))
            {
                result["noise_settings"] = noiseVariant.AsGodotDictionary();
            }
        }

        if (raw.TryGetValue("satellites", out var satellitesVariant))
        {
            var satellites = new Array<Dictionary>();
            var satelliteList = satellitesVariant.As<Godot.Collections.Array>();

            foreach (var satVariant in satelliteList)
            {
                var satRaw = satVariant.AsGodotDictionary();
                var satResult = TransformSystemTemplateSatellite(satRaw);
                satellites.Add(satResult);
            }

            result["satellites"] = satellites;
        }

        // Load names based on the type string
        var nameFileName = GetNameFileFromTypeString(typeStr);
        result["possible_names"] = ExtractNameCategories(raw, nameFileName);

        return result;
    }

    private static Dictionary TransformSystemTemplateSatellite(Dictionary raw)
    {
        var result = new Dictionary();

        // Normalize type string: remove spaces so "Asteroid Belt" becomes "AsteroidBelt"
        // to match enum names in SatelliteGroupTypes and SatelliteBodyType
        result["type"] = ReadString(raw, "type", "Moon").Replace(" ", "");

        // Check if this is a satellite group (has a "template" sub-key with group config)
        bool isSatelliteGroup = raw.ContainsKey("template");

        if (isSatelliteGroup)
        {
            // Satellite group (Asteroid Belt, etc.) — nest all belt params inside "template"
            // so SatelliteBeltItem.SetTemplate() can find them at t["template"][key]
            var template = new Dictionary();
            template["mass"] = ReadFloat(raw, "mass", 1f);
            template["size"] = ReadFloat(raw, "size", 1f);

            var groupTemplate = raw["template"].AsGodotDictionary();
            var numRange = ReadIntRange(groupTemplate, "number_asteroids", (1, 4));
            template["lower_range"] = numRange.Item1;
            template["upper_range"] = numRange.Item2;
            template["ring_apogee"] = ReadFloat(
                groupTemplate,
                "apogee",
                ReadFloat(groupTemplate, "ring_apogee", 0f)
            );
            template["ring_perigee"] = ReadFloat(
                groupTemplate,
                "perigee",
                ReadFloat(groupTemplate, "ring_perigee", 0f)
            );
            template["ring_velocity"] = ReadVector3(groupTemplate, "ring_velocity", Vector3.Zero);
            template["grouping"] = ReadString(groupTemplate, "grouping", "Balanced");

            var sizeRange = ReadFloatRange(groupTemplate, "size_range", (1f, 5f));
            template["size_min"] = sizeRange.Item1;
            template["size_max"] = sizeRange.Item2;

            var massRange = ReadFloatRange(groupTemplate, "mass_range", (1f, 10f));
            template["mass_min"] = massRange.Item1;
            template["mass_max"] = massRange.Item2;

            result["template"] = template;
        }
        else
        {
            // Individual satellite (Moon, Asteroid, Comet) — use orbital parameters
            var template = new Dictionary();
            template["apogee"] = ReadFloat(raw, "apogee", 500f);
            template["perigee"] = ReadFloat(raw, "perigee", 300f);
            template["starting_angle"] = ReadFloat(raw, "starting_angle", 0f);
            template["vertical_offset"] = ReadFloat(raw, "vertical_offset", 0f);
            template["mass"] = ReadFloat(raw, "mass", 1f);
            template["size"] = ReadFloat(raw, "size", 1f);
            result["template"] = template;
        }

        if (raw.TryGetValue("mesh", out var meshVariant))
        {
            var mesh = meshVariant.AsGodotDictionary();

            if (mesh.TryGetValue("base_mesh", out var baseMeshVariant))
            {
                result["base_mesh"] = baseMeshVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("tectonic", out var tectonicVariant))
            {
                result["tectonics"] = tectonicVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("scaling", out var scalingVariant))
            {
                result["scaling_settings"] = scalingVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("spherical_harmonics", out var shVariant))
            {
                result["spherical_harmonics_settings"] = shVariant.AsGodotDictionary();
            }

            if (mesh.TryGetValue("noise_settings", out var noiseVariant))
            {
                result["noise_settings"] = noiseVariant.AsGodotDictionary();
            }
        }

        return result;
    }

    private static Dictionary ExtractNameCategories(Dictionary raw, string nameFileName)
    {
        var result = new Dictionary();

        if (!raw.TryGetValue("categories", out var categoriesVariant))
            return result;

        var categories = categoriesVariant.AsGodotDictionary();
        var potential = ReadStringArray(categories, "potential", System.Array.Empty<string>());

        // Load names from the external name file
        try
        {
            var nameFile = TemplateLoader.LoadNamesFile(nameFileName);

            // Get the list of available categories from the name file
            var nameFileCategoriesList = new string[0];
            if (nameFile.TryGetValue("categories", out var categoriesListVariant))
            {
                nameFileCategoriesList = ReadStringArray(
                    nameFile,
                    "categories",
                    System.Array.Empty<string>()
                );
            }
            else
            {
                // If no categories list in name file, use all top-level keys except 'categories' itself
                var keys = new System.Collections.Generic.List<string>();
                foreach (var key in nameFile.Keys)
                {
                    string keyStr = key.AsString();
                    if (keyStr != "categories" && !string.IsNullOrWhiteSpace(keyStr))
                    {
                        keys.Add(keyStr);
                    }
                }
                nameFileCategoriesList = keys.ToArray();
            }

            // Extract only the categories specified in the potential list
            foreach (var category in potential)
            {
                // Check if this category exists in the name file
                if (System.Array.IndexOf(nameFileCategoriesList, category) >= 0)
                {
                    // In the new name file structure, names are direct arrays (not nested under 'names')
                    if (nameFile.TryGetValue(category, out var namesVariant))
                    {
                        var namesArray = namesVariant.As<Godot.Collections.Array>();
                        if (namesArray != null && namesArray.Count > 0)
                        {
                            var names = new string[namesArray.Count];
                            for (int i = 0; i < namesArray.Count; i++)
                            {
                                names[i] = namesArray[i].AsString() ?? "";
                            }
                            result[category] = names;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to load name file '{nameFileName}': {e.Message}\n{e.StackTrace}");
        }

        return result;
    }

    private static string GetYamlPath(CelestialBodyType type)
    {
        string name = type switch
        {
            CelestialBodyType.RockyPlanet => "RockyPlanet",
            CelestialBodyType.GasGiant => "GasGiant",
            CelestialBodyType.IceGiant => "IceGiant",
            CelestialBodyType.DwarfPlanet => "DwarfPlanet",
            CelestialBodyType.Star => "Star",
            CelestialBodyType.BlackHole => "BlackHole",
            _ => type.ToString(),
        };
        return $"res://Configuration/SystemGen/{name}.yaml";
    }

    private static string GetYamlPath(SatelliteBodyType type)
    {
        string name = type switch
        {
            SatelliteBodyType.Asteroid => "Asteroid",
            SatelliteBodyType.Moon => "Moon",
            SatelliteBodyType.DwarfPlanet => "DwarfPlanet",
            SatelliteBodyType.Rings => "Rings",
            SatelliteBodyType.Satellite => "Satellite",
            _ => type.ToString(),
        };
        return $"res://Configuration/SystemGen/{name}.yaml";
    }

    private static string GetYamlPath(SatelliteGroupTypes type)
    {
        string name = type switch
        {
            SatelliteGroupTypes.AsteroidBelt => "AsteroidBelt",
            SatelliteGroupTypes.Comet => "Comet",
            SatelliteGroupTypes.IceBelt => "IceBelt",
            _ => type.ToString(),
        };
        return $"res://Configuration/SystemGen/{name}.yaml";
    }

    private static string GetNameFileForCelestialBodyType(CelestialBodyType type)
    {
        return type switch
        {
            CelestialBodyType.RockyPlanet => "rockyplanets",
            CelestialBodyType.DwarfPlanet => "rockyplanets",
            CelestialBodyType.GasGiant => "nonrocky",
            CelestialBodyType.IceGiant => "nonrocky",
            CelestialBodyType.Star => "centralbodies",
            CelestialBodyType.BlackHole => "centralbodies",
            _ => "rockyplanets",
        };
    }

    private static string GetNameFileForSatelliteType(SatelliteBodyType type)
    {
        return type switch
        {
            SatelliteBodyType.Moon => "satellites",
            SatelliteBodyType.Asteroid => "satellites",
            SatelliteBodyType.DwarfPlanet => "rockyplanets",
            _ => "satellites",
        };
    }

    private static string GetNameFileForSatelliteGroupType(SatelliteGroupTypes type)
    {
        return type switch
        {
            SatelliteGroupTypes.AsteroidBelt => "satellites",
            SatelliteGroupTypes.Comet => "satellites",
            SatelliteGroupTypes.IceBelt => "satellites",
            _ => "satellites",
        };
    }

    private static string GetNameFileFromTypeString(string typeStr)
    {
        // Map type string to name file
        return typeStr.ToLower() switch
        {
            "star" => "centralbodies",
            "blackhole" => "centralbodies",
            "rockyplanet" => "rockyplanets",
            "dwarfplanet" => "rockyplanets",
            "gasgiant" => "nonrocky",
            "icegiant" => "nonrocky",
            "moon" => "satellites",
            "asteroid" => "satellites",
            "comet" => "satellites",
            "asteroidbelt" => "satellites",
            _ => "rockyplanets", // default fallback
        };
    }

    private static Vector3 ReadVector3(Dictionary dict, string key, Vector3 fallback)
    {
        if (!dict.TryGetValue(key, out var variant))
            return fallback;

        var arr = variant.As<Godot.Collections.Array>();
        if (arr == null || arr.Count < 3)
            return fallback;

        float x = NodeToFloat(arr[0], 0f);
        float y = NodeToFloat(arr[1], 0f);
        float z = NodeToFloat(arr[2], 0f);
        return new Vector3(x, y, z);
    }

    private static float ReadFloat(Dictionary dict, string key, float fallback)
    {
        if (!dict.TryGetValue(key, out var variant))
            return fallback;
        return NodeToFloat(variant, fallback);
    }

    private static string ReadString(Dictionary dict, string key, string fallback)
    {
        if (!dict.TryGetValue(key, out var variant))
            return fallback;
        return variant.AsString() ?? fallback;
    }

    private static string[] ReadStringArray(Dictionary dict, string key, string[] fallback)
    {
        if (!dict.TryGetValue(key, out var variant))
            return fallback;

        var arr = variant.As<Godot.Collections.Array>();
        if (arr == null || arr.Count == 0)
            return fallback;

        var result = new string[arr.Count];
        for (int i = 0; i < arr.Count; i++)
        {
            result[i] = arr[i].AsString() ?? "";
        }
        return result;
    }

    private static (int, int) ReadIntRange(Dictionary dict, string key, (int, int) fallback)
    {
        if (!dict.TryGetValue(key, out var variant))
            return fallback;

        var arr = variant.As<Godot.Collections.Array>();
        if (arr != null && arr.Count >= 2)
        {
            int a = NodeToInt(arr[0], fallback.Item1);
            int b = NodeToInt(arr[1], fallback.Item2);
            return (Mathf.Min(a, b), Mathf.Max(a, b));
        }

        return fallback;
    }

    private static (float, float) ReadFloatRange(
        Dictionary dict,
        string key,
        (float, float) fallback
    )
    {
        if (!dict.TryGetValue(key, out var variant))
            return fallback;

        var arr = variant.As<Godot.Collections.Array>();
        if (arr != null && arr.Count >= 2)
        {
            float a = NodeToFloat(arr[0], fallback.Item1);
            float b = NodeToFloat(arr[1], fallback.Item2);
            return (Mathf.Min(a, b), Mathf.Max(a, b));
        }

        return fallback;
    }

    private static float NodeToFloat(Variant variant, float fallback)
    {
        try
        {
            if (variant.VariantType == Variant.Type.Int)
                return (float)variant.AsInt64();
            if (variant.VariantType == Variant.Type.Float)
                return (float)variant;
            if (variant.VariantType == Variant.Type.String)
            {
                var s = variant.AsString();
                if (
                    float.TryParse(
                        s,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var v
                    )
                )
                    return v;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error reading Variant as float: {e.Message}");
        }
        return fallback;
    }

    private static int NodeToInt(Variant variant, int fallback)
    {
        try
        {
            if (variant.VariantType == Variant.Type.Int)
                return (int)variant.AsInt64();
            if (variant.VariantType == Variant.Type.Float)
                return (int)(float)variant;
            if (variant.VariantType == Variant.Type.String)
            {
                var s = variant.AsString();
                if (
                    int.TryParse(
                        s,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var v
                    )
                )
                    return v;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error reading Variant as int: {e.Message}");
        }
        return fallback;
    }

    private static Dictionary GetFallbackCelestial()
    {
        return new Dictionary
        {
            ["template"] = new Dictionary
            {
                ["apogee"] = 500f,
                ["perigee"] = 300f,
                ["starting_angle"] = 0f,
                ["vertical_offset"] = 0f,
                ["mass"] = 1f,
                ["size"] = 1f,
            },
            ["possible_names"] = new Dictionary(),
        };
    }

    private static Dictionary GetFallbackSatellite()
    {
        return new Dictionary
        {
            ["template"] = new Dictionary
            {
                ["apogee"] = 500f,
                ["perigee"] = 300f,
                ["starting_angle"] = 0f,
                ["vertical_offset"] = 0f,
                ["mass"] = 1f,
                ["size"] = 1f,
            },
            ["possible_names"] = new Dictionary(),
        };
    }

    private static Dictionary GetFallbackSatelliteGroup()
    {
        return new Dictionary
        {
            ["template"] = new Dictionary
            {
                ["ring_apogee"] = 0f,
                ["ring_perigee"] = 0f,
                ["ring_velocity"] = Vector3.Zero,
                ["lower_range"] = 1,
                ["upper_range"] = 4,
                ["grouping"] = "Balanced",
                ["mass_min"] = 1f,
                ["mass_max"] = 10f,
                ["size_min"] = 1f,
                ["size_max"] = 5f,
            },
        };
    }

    public static string GenerateYamlContent(Array<Dictionary> bodies)
    {
        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(
                YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance
            )
            .Build();

        var data = new System.Collections.Generic.Dictionary<string, object>
        {
            ["bodies"] = ConvertBodiesToYamlStructure(bodies),
        };

        return serializer.Serialize(data);
    }

    private static void ConvertBeltToYamlStructure(
        Dictionary body,
        System.Collections.Generic.Dictionary<string, object> bodyDict
    )
    {
        // Satellite belt entries from SatelliteBeltItem.ToParams() have a flat structure
        // with keys like ring_apogee, ring_perigee, etc. at the top level.
        // Serialize into the YAML template format.
        var templateSection = new System.Collections.Generic.Dictionary<string, object>();

        if (body.ContainsKey("lower_range") || body.ContainsKey("upper_range"))
        {
            int lower = body.ContainsKey("lower_range") ? (int)body["lower_range"] : 1;
            int upper = body.ContainsKey("upper_range") ? (int)body["upper_range"] : lower;
            templateSection["number_asteroids"] = new System.Collections.Generic.List<int>
            {
                lower,
                upper,
            };
        }
        if (body.ContainsKey("grouping"))
            templateSection["grouping"] = (string)body["grouping"];
        if (body.ContainsKey("ring_apogee"))
            templateSection["apogee"] = (float)body["ring_apogee"];
        if (body.ContainsKey("ring_perigee"))
            templateSection["perigee"] = (float)body["ring_perigee"];
        if (body.ContainsKey("ring_velocity"))
        {
            var rv = (Vector3)body["ring_velocity"];
            templateSection["ring_velocity"] = new System.Collections.Generic.List<float>
            {
                rv.X,
                rv.Y,
                rv.Z,
            };
        }
        if (body.ContainsKey("size_min"))
        {
            float sizeMin = (float)body["size_min"];
            float sizeMax = body.ContainsKey("size_max") ? (float)body["size_max"] : sizeMin;
            templateSection["size_range"] = new System.Collections.Generic.List<float>
            {
                sizeMin,
                sizeMax,
            };
        }
        if (body.ContainsKey("mass_min"))
        {
            float massMin = (float)body["mass_min"];
            float massMax = body.ContainsKey("mass_max") ? (float)body["mass_max"] : massMin;
            templateSection["mass_range"] = new System.Collections.Generic.List<float>
            {
                massMin,
                massMax,
            };
        }

        bodyDict["template"] = templateSection;

        if (body.ContainsKey("orbital_center_index"))
        {
            int centerIdx = (int)body["orbital_center_index"];
            if (centerIdx >= 0)
                bodyDict["orbital_center_index"] = centerIdx;
        }
    }

    private static System.Collections.Generic.List<System.Collections.Generic.Dictionary<
        string,
        object
    >> ConvertBodiesToYamlStructure(Array<Dictionary> bodies)
    {
        var result = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<
            string,
            object
        >>();

        foreach (var body in bodies)
        {
            var bodyDict = new System.Collections.Generic.Dictionary<string, object>();
            string typeStr = (string)body["type"];

            bodyDict["type"] = typeStr;

            // Check if this is a top-level satellite belt entry (from the separate belt section)
            if (Enum.TryParse<SatelliteGroupTypes>(typeStr, out _))
            {
                ConvertBeltToYamlStructure(body, bodyDict);
                result.Add(bodyDict);
                continue;
            }

            // Serialize orbital parameters for non-dominant bodies, position/velocity for dominant
            if (body.ContainsKey("orbital_parameters"))
            {
                var orbitalParams = (Dictionary)body["orbital_parameters"];
                if (orbitalParams.ContainsKey("apogee"))
                    bodyDict["apogee"] = (float)orbitalParams["apogee"];
                if (orbitalParams.ContainsKey("perigee"))
                    bodyDict["perigee"] = (float)orbitalParams["perigee"];
                if (orbitalParams.ContainsKey("starting_angle"))
                    bodyDict["starting_angle"] = (float)orbitalParams["starting_angle"];
                if (orbitalParams.ContainsKey("vertical_offset"))
                    bodyDict["vertical_offset"] = (float)orbitalParams["vertical_offset"];
                if (orbitalParams.ContainsKey("orbital_center_index"))
                {
                    int centerIdx = (int)orbitalParams["orbital_center_index"];
                    if (centerIdx >= 0)
                        bodyDict["orbital_center_index"] = centerIdx;
                }
            }

            if (body.ContainsKey("template"))
            {
                var template = (Dictionary)body["template"];
                if (template.ContainsKey("position"))
                {
                    var pos = (Vector3)template["position"];
                    bodyDict["position"] = new System.Collections.Generic.List<float>
                    {
                        pos.X,
                        pos.Y,
                        pos.Z,
                    };
                }
                if (template.ContainsKey("velocity"))
                {
                    var vel = (Vector3)template["velocity"];
                    bodyDict["velocity"] = new System.Collections.Generic.List<float>
                    {
                        vel.X,
                        vel.Y,
                        vel.Z,
                    };
                }
                if (template.ContainsKey("mass"))
                    bodyDict["mass"] = (float)template["mass"];
                if (template.ContainsKey("size"))
                    bodyDict["size"] = (int)template["size"];
            }

            if (body.ContainsKey("base_mesh"))
            {
                var baseMesh = (Dictionary)body["base_mesh"];
                var meshSection = new System.Collections.Generic.Dictionary<string, object>();

                var baseMeshSection = new System.Collections.Generic.Dictionary<string, object>();
                if (baseMesh.ContainsKey("subdivisions"))
                    baseMeshSection["subdivisions"] = (int)baseMesh["subdivisions"];
                if (baseMesh.ContainsKey("vertices_per_edge"))
                {
                    var vpe = (Array<Array<int>>)baseMesh["vertices_per_edge"];
                    var vpeList =
                        new System.Collections.Generic.List<System.Collections.Generic.List<int>>();
                    foreach (var row in vpe)
                    {
                        vpeList.Add(new System.Collections.Generic.List<int> { row[0], row[1] });
                    }
                    baseMeshSection["vertices_per_edge"] = vpeList;
                }
                if (baseMesh.ContainsKey("num_abberations"))
                    baseMeshSection["num_abberations"] = (int)baseMesh["num_abberations"];
                if (baseMesh.ContainsKey("num_deformation_cycles"))
                    baseMeshSection["num_deformation_cycles"] = (int)
                        baseMesh["num_deformation_cycles"];

                meshSection["base_mesh"] = baseMeshSection;

                if (body.ContainsKey("tectonics"))
                {
                    var tectonics = (Dictionary)body["tectonics"];
                    var tectonicSection = new System.Collections.Generic.Dictionary<
                        string,
                        object
                    >();

                    if (tectonics.ContainsKey("num_continents"))
                    {
                        var nc = (Array<int>)tectonics["num_continents"];
                        tectonicSection["num_continents"] = new System.Collections.Generic.List<int>
                        {
                            nc[0],
                            nc[1],
                        };
                    }
                    if (tectonics.ContainsKey("stress_scale"))
                    {
                        var ss = (Array<float>)tectonics["stress_scale"];
                        tectonicSection["stress_scale"] = new System.Collections.Generic.List<float>
                        {
                            ss[0],
                            ss[1],
                        };
                    }
                    if (tectonics.ContainsKey("shear_scale"))
                    {
                        var shs = (Array<float>)tectonics["shear_scale"];
                        tectonicSection["shear_scale"] = new System.Collections.Generic.List<float>
                        {
                            shs[0],
                            shs[1],
                        };
                    }
                    if (tectonics.ContainsKey("max_propagation_distance"))
                    {
                        var mpd = (Array<float>)tectonics["max_propagation_distance"];
                        tectonicSection["max_propagation_distance"] =
                            new System.Collections.Generic.List<float> { mpd[0], mpd[1] };
                    }
                    if (tectonics.ContainsKey("propagation_falloff"))
                    {
                        var pf = (Array<float>)tectonics["propagation_falloff"];
                        tectonicSection["propagation_falloff"] =
                            new System.Collections.Generic.List<float> { pf[0], pf[1] };
                    }
                    if (tectonics.ContainsKey("inactive_stress_threshold"))
                    {
                        var ist = (Array<float>)tectonics["inactive_stress_threshold"];
                        tectonicSection["inactive_stress_threshold"] =
                            new System.Collections.Generic.List<float> { ist[0], ist[1] };
                    }
                    if (tectonics.ContainsKey("general_height_scale"))
                    {
                        var ghs = (Array<float>)tectonics["general_height_scale"];
                        tectonicSection["general_height_scale"] =
                            new System.Collections.Generic.List<float> { ghs[0], ghs[1] };
                    }
                    if (tectonics.ContainsKey("general_shear_scale"))
                    {
                        var gss = (Array<float>)tectonics["general_shear_scale"];
                        tectonicSection["general_shear_scale"] =
                            new System.Collections.Generic.List<float> { gss[0], gss[1] };
                    }
                    if (tectonics.ContainsKey("general_compression_scale"))
                    {
                        var gcs = (Array<float>)tectonics["general_compression_scale"];
                        tectonicSection["general_compression_scale"] =
                            new System.Collections.Generic.List<float> { gcs[0], gcs[1] };
                    }
                    if (tectonics.ContainsKey("general_transform_scale"))
                    {
                        var gts = (Array<float>)tectonics["general_transform_scale"];
                        tectonicSection["general_transform_scale"] =
                            new System.Collections.Generic.List<float> { gts[0], gts[1] };
                    }

                    meshSection["tectonic"] = tectonicSection;
                }

                bodyDict["mesh"] = meshSection;
            }

            if (body.ContainsKey("satellites"))
            {
                var satellites = (Godot.Collections.Array)body["satellites"];
                var satellitesList =
                    new System.Collections.Generic.List<System.Collections.Generic.Dictionary<
                        string,
                        object
                    >>();

                foreach (Dictionary satellite in satellites)
                {
                    var satDict = new System.Collections.Generic.Dictionary<string, object>();
                    string satTypeStr = (string)satellite["type"];
                    satDict["type"] = satTypeStr;

                    bool isSatelliteGroup =
                        satTypeStr.Contains("Belt") || satTypeStr.Contains("Comet");

                    if (satellite.ContainsKey("template"))
                    {
                        var satTemplate = (Dictionary)satellite["template"];

                        if (isSatelliteGroup)
                        {
                            var groupTemplateSection = new System.Collections.Generic.Dictionary<
                                string,
                                object
                            >();

                            if (satTemplate.ContainsKey("lower_range"))
                                groupTemplateSection["number_asteroids"] =
                                    new System.Collections.Generic.List<int>
                                    {
                                        (int)satTemplate["lower_range"],
                                        satTemplate.ContainsKey("upper_range")
                                            ? (int)satTemplate["upper_range"]
                                            : (int)satTemplate["lower_range"],
                                    };
                            if (satTemplate.ContainsKey("grouping"))
                                groupTemplateSection["grouping"] = (string)satTemplate["grouping"];
                            if (satTemplate.ContainsKey("ring_apogee"))
                                groupTemplateSection["apogee"] = (float)satTemplate["ring_apogee"];
                            if (satTemplate.ContainsKey("ring_perigee"))
                                groupTemplateSection["perigee"] = (float)
                                    satTemplate["ring_perigee"];
                            if (satTemplate.ContainsKey("ring_velocity"))
                            {
                                var rv = (Vector3)satTemplate["ring_velocity"];
                                groupTemplateSection["ring_velocity"] =
                                    new System.Collections.Generic.List<float> { rv.X, rv.Y, rv.Z };
                            }
                            if (satTemplate.ContainsKey("size_min"))
                            {
                                groupTemplateSection["size_range"] =
                                    new System.Collections.Generic.List<float>
                                    {
                                        (float)satTemplate["size_min"],
                                        satTemplate.ContainsKey("size_max")
                                            ? (float)satTemplate["size_max"]
                                            : (float)satTemplate["size_min"],
                                    };
                            }
                            if (satTemplate.ContainsKey("mass_min"))
                            {
                                groupTemplateSection["mass_range"] =
                                    new System.Collections.Generic.List<float>
                                    {
                                        (float)satTemplate["mass_min"],
                                        satTemplate.ContainsKey("mass_max")
                                            ? (float)satTemplate["mass_max"]
                                            : (float)satTemplate["mass_min"],
                                    };
                            }

                            satDict["template"] = groupTemplateSection;
                        }
                        else
                        {
                            // Individual satellite — serialize orbital parameters
                            if (satTemplate.ContainsKey("apogee"))
                                satDict["apogee"] = (float)satTemplate["apogee"];
                            if (satTemplate.ContainsKey("perigee"))
                                satDict["perigee"] = (float)satTemplate["perigee"];
                            if (satTemplate.ContainsKey("starting_angle"))
                                satDict["starting_angle"] = (float)satTemplate["starting_angle"];
                            if (satTemplate.ContainsKey("vertical_offset"))
                                satDict["vertical_offset"] = (float)satTemplate["vertical_offset"];
                            if (satTemplate.ContainsKey("mass"))
                                satDict["mass"] = (float)satTemplate["mass"];
                            if (satTemplate.ContainsKey("size"))
                                satDict["size"] = (int)satTemplate["size"];
                        }
                    }

                    var satMeshSection = new System.Collections.Generic.Dictionary<
                        string,
                        object
                    >();
                    bool hasMeshContent = false;

                    if (satellite.ContainsKey("base_mesh"))
                    {
                        var satBaseMesh = (Dictionary)satellite["base_mesh"];
                        var baseMeshSection = new System.Collections.Generic.Dictionary<
                            string,
                            object
                        >();

                        if (satBaseMesh.ContainsKey("subdivisions"))
                            baseMeshSection["subdivisions"] = (int)satBaseMesh["subdivisions"];
                        if (satBaseMesh.ContainsKey("vertices_per_edge"))
                        {
                            var vpe = (Array<Array<int>>)satBaseMesh["vertices_per_edge"];
                            var vpeList =
                                new System.Collections.Generic.List<System.Collections.Generic.List<int>>();
                            foreach (var row in vpe)
                            {
                                vpeList.Add(
                                    new System.Collections.Generic.List<int> { row[0], row[1] }
                                );
                            }
                            baseMeshSection["vertices_per_edge"] = vpeList;
                        }
                        if (satBaseMesh.ContainsKey("num_abberations"))
                            baseMeshSection["num_abberations"] = (int)
                                satBaseMesh["num_abberations"];
                        if (satBaseMesh.ContainsKey("num_deformation_cycles"))
                            baseMeshSection["num_deformation_cycles"] = (int)
                                satBaseMesh["num_deformation_cycles"];

                        satMeshSection["base_mesh"] = baseMeshSection;
                        hasMeshContent = true;
                    }

                    if (satellite.ContainsKey("tectonics"))
                    {
                        var satTectonics = (Dictionary)satellite["tectonics"];
                        var tectonicSection = new System.Collections.Generic.Dictionary<
                            string,
                            object
                        >();

                        if (satTectonics.ContainsKey("num_continents"))
                        {
                            var nc = (Array<int>)satTectonics["num_continents"];
                            tectonicSection["num_continents"] =
                                new System.Collections.Generic.List<int> { nc[0], nc[1] };
                        }
                        if (satTectonics.ContainsKey("stress_scale"))
                        {
                            var ss = (Array<float>)satTectonics["stress_scale"];
                            tectonicSection["stress_scale"] =
                                new System.Collections.Generic.List<float> { ss[0], ss[1] };
                        }
                        if (satTectonics.ContainsKey("shear_scale"))
                        {
                            var shs = (Array<float>)satTectonics["shear_scale"];
                            tectonicSection["shear_scale"] =
                                new System.Collections.Generic.List<float> { shs[0], shs[1] };
                        }
                        if (satTectonics.ContainsKey("max_propagation_distance"))
                        {
                            var mpd = (Array<float>)satTectonics["max_propagation_distance"];
                            tectonicSection["max_propagation_distance"] =
                                new System.Collections.Generic.List<float> { mpd[0], mpd[1] };
                        }
                        if (satTectonics.ContainsKey("propagation_falloff"))
                        {
                            var pf = (Array<float>)satTectonics["propagation_falloff"];
                            tectonicSection["propagation_falloff"] =
                                new System.Collections.Generic.List<float> { pf[0], pf[1] };
                        }
                        if (satTectonics.ContainsKey("inactive_stress_threshold"))
                        {
                            var ist = (Array<float>)satTectonics["inactive_stress_threshold"];
                            tectonicSection["inactive_stress_threshold"] =
                                new System.Collections.Generic.List<float> { ist[0], ist[1] };
                        }
                        if (satTectonics.ContainsKey("general_height_scale"))
                        {
                            var ghs = (Array<float>)satTectonics["general_height_scale"];
                            tectonicSection["general_height_scale"] =
                                new System.Collections.Generic.List<float> { ghs[0], ghs[1] };
                        }
                        if (satTectonics.ContainsKey("general_shear_scale"))
                        {
                            var gss = (Array<float>)satTectonics["general_shear_scale"];
                            tectonicSection["general_shear_scale"] =
                                new System.Collections.Generic.List<float> { gss[0], gss[1] };
                        }
                        if (satTectonics.ContainsKey("general_compression_scale"))
                        {
                            var gcs = (Array<float>)satTectonics["general_compression_scale"];
                            tectonicSection["general_compression_scale"] =
                                new System.Collections.Generic.List<float> { gcs[0], gcs[1] };
                        }
                        if (satTectonics.ContainsKey("general_transform_scale"))
                        {
                            var gts = (Array<float>)satTectonics["general_transform_scale"];
                            tectonicSection["general_transform_scale"] =
                                new System.Collections.Generic.List<float> { gts[0], gts[1] };
                        }

                        satMeshSection["tectonic"] = tectonicSection;
                        hasMeshContent = true;
                    }

                    if (satellite.ContainsKey("scaling_settings"))
                    {
                        var satScaling = (Dictionary)satellite["scaling_settings"];
                        var scalingSection = new System.Collections.Generic.Dictionary<
                            string,
                            object
                        >();

                        if (satScaling.ContainsKey("scaling_range_x"))
                        {
                            var srx = (Array<float>)satScaling["scaling_range_x"];
                            scalingSection["scaling_range_x"] =
                                new System.Collections.Generic.List<float> { srx[0], srx[1] };
                        }
                        if (satScaling.ContainsKey("scaling_range_y"))
                        {
                            var sry = (Array<float>)satScaling["scaling_range_y"];
                            scalingSection["scaling_range_y"] =
                                new System.Collections.Generic.List<float> { sry[0], sry[1] };
                        }
                        if (satScaling.ContainsKey("scaling_range_z"))
                        {
                            var srz = (Array<float>)satScaling["scaling_range_z"];
                            scalingSection["scaling_range_z"] =
                                new System.Collections.Generic.List<float> { srz[0], srz[1] };
                        }

                        satMeshSection["scaling"] = scalingSection;
                        hasMeshContent = true;
                    }

                    if (satellite.ContainsKey("noise_settings"))
                    {
                        var satNoise = (Dictionary)satellite["noise_settings"];
                        var noiseSection = new System.Collections.Generic.Dictionary<
                            string,
                            object
                        >();

                        if (satNoise.ContainsKey("amplitude_range"))
                        {
                            var ar = (Array<float>)satNoise["amplitude_range"];
                            noiseSection["amplitude_range"] =
                                new System.Collections.Generic.List<float> { ar[0], ar[1] };
                        }
                        if (satNoise.ContainsKey("scaling_range"))
                        {
                            var sr = (Array<float>)satNoise["scaling_range"];
                            noiseSection["scaling_range"] =
                                new System.Collections.Generic.List<float> { sr[0], sr[1] };
                        }
                        if (satNoise.ContainsKey("octave_range"))
                        {
                            var or = (Array<int>)satNoise["octave_range"];
                            noiseSection["octave_range"] = new System.Collections.Generic.List<int>
                            {
                                or[0],
                                or[1],
                            };
                        }

                        satMeshSection["noise_settings"] = noiseSection;
                        hasMeshContent = true;
                    }

                    if (hasMeshContent)
                    {
                        satDict["mesh"] = satMeshSection;
                    }

                    satellitesList.Add(satDict);
                }

                bodyDict["satellites"] = satellitesList;
            }

            result.Add(bodyDict);
        }

        return result;
    }
}
