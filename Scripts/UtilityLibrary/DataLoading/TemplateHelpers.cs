using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using Structures;
using Structures.Enums;
// Alias to disambiguate from Godot.Collections.Dictionary when using generic Dictionary<K,V>
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

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

    /// <summary>
    /// Loads a system template YAML file in the new 3-section format (dominant/belts/planetary).
    /// Each section's entries are returned in a format directly compatible with the corresponding
    /// GUI item's SetConfiguration()/SetTemplate() methods, matching the ToParams() output structure.
    /// </summary>
    public static SystemTemplateData LoadSystemTemplate(string fileName)
    {
        var dominant = new Array<Dictionary>();
        var belts = new Array<Dictionary>();
        var planetary = new Array<Dictionary>();

        try
        {
            var raw = TemplateLoader.Load(fileName, TemplateLoader.SystemTemplateValidator);

            // Parse "dominant" section
            if (raw.TryGetValue("dominant", out var dominantVariant))
            {
                var dominantList = dominantVariant.As<Godot.Collections.Array>();
                if (dominantList != null)
                {
                    foreach (var bodyVariant in dominantList)
                    {
                        var bodyRaw = bodyVariant.AsGodotDictionary();
                        dominant.Add(LoadDominantBody(bodyRaw));
                    }
                }
            }

            // Parse "belts" section
            if (raw.TryGetValue("belts", out var beltsVariant))
            {
                var beltsList = beltsVariant.As<Godot.Collections.Array>();
                if (beltsList != null)
                {
                    foreach (var beltVariant in beltsList)
                    {
                        var beltRaw = beltVariant.AsGodotDictionary();
                        belts.Add(LoadBeltEntry(beltRaw));
                    }
                }
            }

            // Parse "planetary" section
            if (raw.TryGetValue("planetary", out var planetaryVariant))
            {
                var planetaryList = planetaryVariant.As<Godot.Collections.Array>();
                if (planetaryList != null)
                {
                    foreach (var bodyVariant in planetaryList)
                    {
                        var bodyRaw = bodyVariant.AsGodotDictionary();
                        planetary.Add(LoadPlanetaryBody(bodyRaw));
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error loading system template {fileName}: {e.Message}\n{e.StackTrace}");
        }

        return new SystemTemplateData(dominant, belts, planetary);
    }

    /// <summary>
    /// Loads a dominant body entry from YAML into a dict matching DominantBodyItem.ToParams() format.
    /// </summary>
    private static Dictionary LoadDominantBody(Dictionary raw)
    {
        var result = new Dictionary();
        result["type"] = ReadString(raw, "type", "Star");
        result["name"] = ReadString(raw, "name", "");

        // template: { mass, size, position, velocity }
        var template = new Dictionary();
        if (raw.TryGetValue("template", out var templateVariant))
        {
            var templateRaw = templateVariant.AsGodotDictionary();
            template["mass"] = ReadFloat(templateRaw, "mass", 500000f);
            template["size"] = ReadFloat(templateRaw, "size", 500f);
            template["position"] = ReadVector3(templateRaw, "position", Vector3.Zero);
            template["velocity"] = ReadVector3(templateRaw, "velocity", Vector3.Zero);
        }
        result["template"] = template;

        // central_parameters: { inclination, starting_angle }
        var centralParams = new Dictionary();
        if (raw.TryGetValue("central_parameters", out var cpVariant))
        {
            var cpRaw = cpVariant.AsGodotDictionary();
            centralParams["inclination"] = ReadFloat(cpRaw, "inclination", 0f);
            centralParams["starting_angle"] = ReadFloat(cpRaw, "starting_angle", 0f);
        }
        result["central_parameters"] = centralParams;

        // base_mesh: { subdivisions, vertices_per_edge, num_abberations, num_deformation_cycles }
        if (raw.TryGetValue("base_mesh", out var bmVariant))
            result["base_mesh"] = LoadBaseMesh(bmVariant.AsGodotDictionary());

        // spherical_harmonics_settings (optional)
        if (raw.TryGetValue("spherical_harmonics_settings", out var shVariant))
            result["spherical_harmonics_settings"] = LoadFloatRangeDict(
                shVariant.AsGodotDictionary(),
                "amplitude_range"
            );

        return result;
    }

    /// <summary>
    /// Loads a belt entry from YAML into a dict matching SatelliteBeltItem.ToParams() flat format.
    /// </summary>
    private static Dictionary LoadBeltEntry(Dictionary raw)
    {
        var result = new Dictionary();
        result["type"] = ReadString(raw, "type", "AsteroidBelt");
        result["ring_apogee"] = ReadFloat(raw, "ring_apogee", 0f);
        result["ring_perigee"] = ReadFloat(raw, "ring_perigee", 0f);
        result["ring_velocity"] = ReadVector3(raw, "ring_velocity", Vector3.Zero);
        result["size_min"] = ReadFloat(raw, "size_min", 1f);
        result["size_max"] = ReadFloat(raw, "size_max", 5f);
        result["mass_min"] = ReadFloat(raw, "mass_min", 1f);
        result["mass_max"] = ReadFloat(raw, "mass_max", 10f);
        result["lower_range"] = (int)ReadFloat(raw, "lower_range", 1f);
        result["upper_range"] = (int)ReadFloat(raw, "upper_range", 4f);
        result["grouping"] = ReadString(raw, "grouping", "Balanced");
        result["orbital_center_index"] = (int)ReadFloat(raw, "orbital_center_index", -1f);
        return result;
    }

    /// <summary>
    /// Loads a planetary body entry from YAML into a dict matching PlanetaryBodyItem.ToParams() format.
    /// </summary>
    private static Dictionary LoadPlanetaryBody(Dictionary raw)
    {
        var result = new Dictionary();
        result["type"] = ReadString(raw, "type", "RockyPlanet");
        result["name"] = ReadString(raw, "name", "");

        // template: { mass, size }
        var template = new Dictionary();
        if (raw.TryGetValue("template", out var templateVariant))
        {
            var templateRaw = templateVariant.AsGodotDictionary();
            template["mass"] = ReadFloat(templateRaw, "mass", 1000f);
            template["size"] = ReadFloat(templateRaw, "size", 150f);
        }
        result["template"] = template;

        // orbital_parameters: { apogee, perigee, starting_angle, vertical_offset, orbital_center_index, parent_body }
        var orbitalParams = new Dictionary();
        if (raw.TryGetValue("orbital_parameters", out var opVariant))
        {
            var opRaw = opVariant.AsGodotDictionary();
            orbitalParams["apogee"] = ReadFloat(opRaw, "apogee", 1000f);
            orbitalParams["perigee"] = ReadFloat(opRaw, "perigee", 500f);
            orbitalParams["starting_angle"] = ReadFloat(opRaw, "starting_angle", 0f);
            orbitalParams["vertical_offset"] = ReadFloat(opRaw, "vertical_offset", 0f);
            orbitalParams["orbital_center_index"] = (int)ReadFloat(
                opRaw,
                "orbital_center_index",
                -1f
            );
            orbitalParams["parent_body"] = ReadString(opRaw, "parent_body", "barycenter");
        }
        result["orbital_parameters"] = orbitalParams;

        // base_mesh
        if (raw.TryGetValue("base_mesh", out var bmVariant))
            result["base_mesh"] = LoadBaseMesh(bmVariant.AsGodotDictionary());

        // tectonics
        if (raw.TryGetValue("tectonics", out var tVariant))
            result["tectonics"] = LoadTectonics(tVariant.AsGodotDictionary());

        // spherical_harmonics_settings (optional)
        if (raw.TryGetValue("spherical_harmonics_settings", out var shVariant))
            result["spherical_harmonics_settings"] = LoadFloatRangeDict(
                shVariant.AsGodotDictionary(),
                "amplitude_range"
            );

        // satellites (optional, nested under planetary body)
        if (raw.TryGetValue("satellites", out var satVariant))
        {
            var satList = satVariant.As<Godot.Collections.Array>();
            if (satList != null && satList.Count > 0)
            {
                var satellites = new Array<Dictionary>();
                foreach (var sv in satList)
                {
                    var satRaw = sv.AsGodotDictionary();
                    satellites.Add(LoadSatelliteEntry(satRaw));
                }
                result["satellites"] = satellites;
            }
        }

        return result;
    }

    /// <summary>
    /// Loads a satellite entry from YAML into a dict matching SatelliteItem.ToParams() format.
    /// </summary>
    private static Dictionary LoadSatelliteEntry(Dictionary raw)
    {
        var result = new Dictionary();
        result["type"] = ReadString(raw, "type", "Moon");
        result["name"] = ReadString(raw, "name", "");

        // template: { apogee, perigee, starting_angle, vertical_offset, mass, size }
        var template = new Dictionary();
        if (raw.TryGetValue("template", out var templateVariant))
        {
            var templateRaw = templateVariant.AsGodotDictionary();
            template["apogee"] = ReadFloat(templateRaw, "apogee", 500f);
            template["perigee"] = ReadFloat(templateRaw, "perigee", 300f);
            template["starting_angle"] = ReadFloat(templateRaw, "starting_angle", 0f);
            template["vertical_offset"] = ReadFloat(templateRaw, "vertical_offset", 0f);
            template["mass"] = ReadFloat(templateRaw, "mass", 1f);
            template["size"] = ReadFloat(templateRaw, "size", 1f);
        }
        result["template"] = template;

        // base_mesh
        if (raw.TryGetValue("base_mesh", out var bmVariant))
            result["base_mesh"] = LoadBaseMesh(bmVariant.AsGodotDictionary());

        // tectonics
        if (raw.TryGetValue("tectonics", out var tVariant))
            result["tectonics"] = LoadTectonics(tVariant.AsGodotDictionary());

        // spherical_harmonics_settings
        if (raw.TryGetValue("spherical_harmonics_settings", out var shVariant))
            result["spherical_harmonics_settings"] = LoadFloatRangeDict(
                shVariant.AsGodotDictionary(),
                "amplitude_range"
            );

        // scaling_settings
        if (raw.TryGetValue("scaling_settings", out var scaleVariant))
            result["scaling_settings"] = scaleVariant.AsGodotDictionary();

        // noise_settings
        if (raw.TryGetValue("noise_settings", out var noiseVariant))
            result["noise_settings"] = noiseVariant.AsGodotDictionary();

        return result;
    }

    /// <summary>
    /// Loads a base_mesh section into a Dictionary with properly typed vertices_per_edge.
    /// </summary>
    private static Dictionary LoadBaseMesh(Dictionary raw)
    {
        var result = new Dictionary();
        result["subdivisions"] = (int)ReadFloat(raw, "subdivisions", 1f);
        result["num_abberations"] = (int)ReadFloat(raw, "num_abberations", 0f);
        result["num_deformation_cycles"] = (int)ReadFloat(raw, "num_deformation_cycles", 0f);

        // vertices_per_edge: convert from YAML array to Array<Array<int>>
        if (raw.TryGetValue("vertices_per_edge", out var vpeVariant))
        {
            var vpeArray = new Array<Array<int>>();
            var rawArray = vpeVariant.As<Godot.Collections.Array>();
            if (rawArray != null)
            {
                foreach (var rowVariant in rawArray)
                {
                    var rowArray = rowVariant.As<Godot.Collections.Array>();
                    if (rowArray != null && rowArray.Count >= 2)
                    {
                        var row = new Array<int>();
                        row.Add(NodeToInt(rowArray[0], 2));
                        row.Add(NodeToInt(rowArray[1], 3));
                        vpeArray.Add(row);
                    }
                }
            }
            result["vertices_per_edge"] = vpeArray;
        }

        return result;
    }

    /// <summary>
    /// Loads a tectonics section from raw YAML, preserving array types.
    /// </summary>
    private static Dictionary LoadTectonics(Dictionary raw)
    {
        var result = new Dictionary();
        string[] intRangeKeys = { "num_continents" };
        string[] floatRangeKeys =
        {
            "stress_scale",
            "shear_scale",
            "max_propagation_distance",
            "propagation_falloff",
            "inactive_stress_threshold",
            "general_height_scale",
            "general_shear_scale",
            "general_compression_scale",
            "general_transform_scale",
        };

        foreach (var key in intRangeKeys)
        {
            if (raw.TryGetValue(key, out var v))
            {
                var range = ReadIntRange(raw, key, (0, 1));
                result[key] = new int[] { range.Item1, range.Item2 };
            }
        }

        foreach (var key in floatRangeKeys)
        {
            if (raw.TryGetValue(key, out var v))
            {
                var range = ReadFloatRange(raw, key, (0f, 1f));
                result[key] = new float[] { range.Item1, range.Item2 };
            }
        }

        return result;
    }

    /// <summary>
    /// Loads a settings dict containing a single float range key (e.g. amplitude_range).
    /// </summary>
    private static Dictionary LoadFloatRangeDict(Dictionary raw, string key)
    {
        var result = new Dictionary();
        if (raw.TryGetValue(key, out var v))
        {
            var range = ReadFloatRange(raw, key, (0f, 1f));
            result[key] = new float[] { range.Item1, range.Item2 };
        }
        return result;
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
            || typeStr.Equals("NeutronStar", StringComparison.OrdinalIgnoreCase)
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

    // TransformSystemTemplateBody and TransformSystemTemplateSatellite removed —
    // The new YAML format matches ToParams() directly, so no transformation is needed.
    // Loading is handled by LoadDominantBody(), LoadPlanetaryBody(), LoadBeltEntry(),
    // and LoadSatelliteEntry() above.

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
            CelestialBodyType.NeutronStar => "NeutronStar",
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
            CelestialBodyType.NeutronStar => "centralbodies",
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
            "neutronstar" => "centralbodies",
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

    /// <summary>
    /// Generates YAML content from three separate arrays of body dictionaries,
    /// using the new 3-section format (dominant/belts/planetary).
    /// Each array contains dictionaries in the ToParams() output format.
    /// </summary>
    public static string GenerateYamlContent(
        Array<Dictionary> dominant,
        Array<Dictionary> belts,
        Array<Dictionary> planetary
    )
    {
        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(
                YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance
            )
            .Build();

        var data = new SysDict();

        if (dominant.Count > 0)
            data["dominant"] = ConvertDominantToYaml(dominant);
        if (belts.Count > 0)
            data["belts"] = ConvertBeltsToYaml(belts);
        if (planetary.Count > 0)
            data["planetary"] = ConvertPlanetaryToYaml(planetary);

        return serializer.Serialize(data);
    }

    private static List<SysDict> ConvertDominantToYaml(Array<Dictionary> bodies)
    {
        var result = new List<SysDict>();
        foreach (var body in bodies)
        {
            var dict = new SysDict();
            dict["type"] = (string)body["type"];
            if (body.ContainsKey("name"))
                dict["name"] = (string)body["name"];

            // template: { mass, size, position, velocity }
            if (body.ContainsKey("template"))
            {
                var template = (Dictionary)body["template"];
                var tDict = new SysDict();
                if (template.ContainsKey("mass"))
                    tDict["mass"] = (float)template["mass"];
                if (template.ContainsKey("size"))
                    tDict["size"] = (float)template["size"];
                if (template.ContainsKey("position"))
                {
                    var p = (Vector3)template["position"];
                    tDict["position"] = new List<float> { p.X, p.Y, p.Z };
                }
                if (template.ContainsKey("velocity"))
                {
                    var v = (Vector3)template["velocity"];
                    tDict["velocity"] = new List<float> { v.X, v.Y, v.Z };
                }
                dict["template"] = tDict;
            }

            // central_parameters
            if (body.ContainsKey("central_parameters"))
            {
                var cp = (Dictionary)body["central_parameters"];
                var cpDict = new SysDict();
                if (cp.ContainsKey("inclination"))
                    cpDict["inclination"] = (float)cp["inclination"];
                if (cp.ContainsKey("starting_angle"))
                    cpDict["starting_angle"] = (float)cp["starting_angle"];
                dict["central_parameters"] = cpDict;
            }

            // base_mesh
            if (body.ContainsKey("base_mesh"))
                dict["base_mesh"] = ConvertBaseMeshToYaml((Dictionary)body["base_mesh"]);

            // spherical_harmonics_settings
            if (body.ContainsKey("spherical_harmonics_settings"))
                dict["spherical_harmonics_settings"] = ConvertFloatRangeDictToYaml(
                    (Dictionary)body["spherical_harmonics_settings"]
                );

            result.Add(dict);
        }
        return result;
    }

    private static List<SysDict> ConvertBeltsToYaml(Array<Dictionary> belts)
    {
        var result = new List<SysDict>();
        foreach (var belt in belts)
        {
            var dict = new SysDict();
            dict["type"] = (string)belt["type"];
            if (belt.ContainsKey("ring_apogee"))
                dict["ring_apogee"] = (float)belt["ring_apogee"];
            if (belt.ContainsKey("ring_perigee"))
                dict["ring_perigee"] = (float)belt["ring_perigee"];
            if (belt.ContainsKey("ring_velocity"))
            {
                var rv = (Vector3)belt["ring_velocity"];
                dict["ring_velocity"] = new List<float> { rv.X, rv.Y, rv.Z };
            }
            if (belt.ContainsKey("size_min"))
                dict["size_min"] = (float)belt["size_min"];
            if (belt.ContainsKey("size_max"))
                dict["size_max"] = (float)belt["size_max"];
            if (belt.ContainsKey("mass_min"))
                dict["mass_min"] = (float)belt["mass_min"];
            if (belt.ContainsKey("mass_max"))
                dict["mass_max"] = (float)belt["mass_max"];
            if (belt.ContainsKey("lower_range"))
                dict["lower_range"] = (int)belt["lower_range"];
            if (belt.ContainsKey("upper_range"))
                dict["upper_range"] = (int)belt["upper_range"];
            if (belt.ContainsKey("grouping"))
                dict["grouping"] = (string)belt["grouping"];
            if (belt.ContainsKey("orbital_center_index"))
                dict["orbital_center_index"] = (int)belt["orbital_center_index"];
            result.Add(dict);
        }
        return result;
    }

    private static List<SysDict> ConvertPlanetaryToYaml(Array<Dictionary> bodies)
    {
        var result = new List<SysDict>();
        foreach (var body in bodies)
        {
            var dict = new SysDict();
            dict["type"] = (string)body["type"];
            if (body.ContainsKey("name"))
                dict["name"] = (string)body["name"];

            // template: { mass, size }
            if (body.ContainsKey("template"))
            {
                var template = (Dictionary)body["template"];
                var tDict = new SysDict();
                if (template.ContainsKey("mass"))
                    tDict["mass"] = (float)template["mass"];
                if (template.ContainsKey("size"))
                    tDict["size"] = (float)template["size"];
                dict["template"] = tDict;
            }

            // orbital_parameters
            if (body.ContainsKey("orbital_parameters"))
            {
                var op = (Dictionary)body["orbital_parameters"];
                var opDict = new SysDict();
                if (op.ContainsKey("apogee"))
                    opDict["apogee"] = (float)op["apogee"];
                if (op.ContainsKey("perigee"))
                    opDict["perigee"] = (float)op["perigee"];
                if (op.ContainsKey("starting_angle"))
                    opDict["starting_angle"] = (float)op["starting_angle"];
                if (op.ContainsKey("vertical_offset"))
                    opDict["vertical_offset"] = (float)op["vertical_offset"];
                if (op.ContainsKey("orbital_center_index"))
                    opDict["orbital_center_index"] = (int)op["orbital_center_index"];
                if (op.ContainsKey("parent_body"))
                    opDict["parent_body"] = (string)op["parent_body"];
                dict["orbital_parameters"] = opDict;
            }

            // base_mesh
            if (body.ContainsKey("base_mesh"))
                dict["base_mesh"] = ConvertBaseMeshToYaml((Dictionary)body["base_mesh"]);

            // tectonics
            if (body.ContainsKey("tectonics"))
                dict["tectonics"] = ConvertTectonicsToYaml((Dictionary)body["tectonics"]);

            // spherical_harmonics_settings
            if (body.ContainsKey("spherical_harmonics_settings"))
                dict["spherical_harmonics_settings"] = ConvertFloatRangeDictToYaml(
                    (Dictionary)body["spherical_harmonics_settings"]
                );

            // satellites
            if (body.ContainsKey("satellites"))
            {
                var satellites = (Godot.Collections.Array)body["satellites"];
                var satList = new List<SysDict>();
                foreach (Dictionary sat in satellites)
                {
                    satList.Add(ConvertSatelliteToYaml(sat));
                }
                dict["satellites"] = satList;
            }

            result.Add(dict);
        }
        return result;
    }

    private static SysDict ConvertSatelliteToYaml(Dictionary sat)
    {
        var dict = new SysDict();
        dict["type"] = (string)sat["type"];
        if (sat.ContainsKey("name"))
            dict["name"] = (string)sat["name"];

        // template: { apogee, perigee, starting_angle, vertical_offset, mass, size }
        if (sat.ContainsKey("template"))
        {
            var template = (Dictionary)sat["template"];
            var tDict = new SysDict();
            foreach (
                string key in new[] { "apogee", "perigee", "starting_angle", "vertical_offset" }
            )
            {
                if (template.ContainsKey(key))
                    tDict[key] = (float)template[key];
            }
            if (template.ContainsKey("mass"))
                tDict["mass"] = (float)template["mass"];
            if (template.ContainsKey("size"))
                tDict["size"] = (float)template["size"];
            dict["template"] = tDict;
        }

        // base_mesh
        if (sat.ContainsKey("base_mesh"))
            dict["base_mesh"] = ConvertBaseMeshToYaml((Dictionary)sat["base_mesh"]);

        // tectonics
        if (sat.ContainsKey("tectonics"))
            dict["tectonics"] = ConvertTectonicsToYaml((Dictionary)sat["tectonics"]);

        // spherical_harmonics_settings
        if (sat.ContainsKey("spherical_harmonics_settings"))
            dict["spherical_harmonics_settings"] = ConvertFloatRangeDictToYaml(
                (Dictionary)sat["spherical_harmonics_settings"]
            );

        // scaling_settings
        if (sat.ContainsKey("scaling_settings"))
            dict["scaling_settings"] = ConvertFloatRangeDictToYaml(
                (Dictionary)sat["scaling_settings"]
            );

        // noise_settings
        if (sat.ContainsKey("noise_settings"))
            dict["noise_settings"] = ConvertFloatRangeDictToYaml((Dictionary)sat["noise_settings"]);

        return dict;
    }

    private static SysDict ConvertBaseMeshToYaml(Dictionary baseMesh)
    {
        var dict = new SysDict();
        if (baseMesh.ContainsKey("subdivisions"))
            dict["subdivisions"] = (int)baseMesh["subdivisions"];
        if (baseMesh.ContainsKey("vertices_per_edge"))
        {
            var vpe = (Array<Array<int>>)baseMesh["vertices_per_edge"];
            var vpeList = new List<List<int>>();
            foreach (var row in vpe)
                vpeList.Add(new List<int> { row[0], row[1] });
            dict["vertices_per_edge"] = vpeList;
        }
        if (baseMesh.ContainsKey("num_abberations"))
            dict["num_abberations"] = (int)baseMesh["num_abberations"];
        if (baseMesh.ContainsKey("num_deformation_cycles"))
            dict["num_deformation_cycles"] = (int)baseMesh["num_deformation_cycles"];
        return dict;
    }

    private static SysDict ConvertTectonicsToYaml(Dictionary tectonics)
    {
        var dict = new SysDict();
        foreach (var key in tectonics.Keys)
        {
            string keyStr = key.AsString();
            var value = tectonics[key];

            // Try int[] first (for num_continents), then float[]
            if (value.VariantType == Variant.Type.PackedInt32Array || value.Obj is int[])
            {
                var arr = (int[])value;
                dict[keyStr] = new List<int> { arr[0], arr[1] };
            }
            else if (value.VariantType == Variant.Type.PackedFloat32Array || value.Obj is float[])
            {
                var arr = (float[])value;
                dict[keyStr] = new List<float> { arr[0], arr[1] };
            }
            else
            {
                // Fallback: try to read as Godot Array
                var arr = value.As<Godot.Collections.Array>();
                if (arr != null && arr.Count >= 2)
                {
                    dict[keyStr] = new List<float>
                    {
                        NodeToFloat(arr[0], 0f),
                        NodeToFloat(arr[1], 0f),
                    };
                }
            }
        }
        return dict;
    }

    private static SysDict ConvertFloatRangeDictToYaml(Dictionary settings)
    {
        var dict = new SysDict();
        foreach (var key in settings.Keys)
        {
            string keyStr = key.AsString();
            var value = settings[key];

            if (value.VariantType == Variant.Type.PackedFloat32Array || value.Obj is float[])
            {
                var arr = (float[])value;
                dict[keyStr] = new List<float>(arr);
            }
            else if (value.VariantType == Variant.Type.PackedInt32Array || value.Obj is int[])
            {
                var arr = (int[])value;
                dict[keyStr] = new List<int>(arr);
            }
            else
            {
                var arr = value.As<Godot.Collections.Array>();
                if (arr != null && arr.Count >= 2)
                {
                    dict[keyStr] = new List<float>
                    {
                        NodeToFloat(arr[0], 0f),
                        NodeToFloat(arr[1], 0f),
                    };
                }
            }
        }
        return dict;
    }

    // Old ConvertBodiesToYamlStructure removed — replaced by section-specific converters above.
}
