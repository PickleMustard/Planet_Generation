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
            return TransformCelestialBodyTemplate(raw);
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
            return TransformSatelliteBodyTemplate(raw);
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
            if (raw.TryGetValue("bodies", out var bodiesVariant) && bodiesVariant.As<Godot.Collections.Array>() is Godot.Collections.Array bodyList)
            {
                foreach (var bodyVariant in bodyList)
                {
                    var bodyRaw = bodyVariant.AsGodotDictionary();
                    var transformed = TransformSystemTemplateBody(bodyRaw);
                    bodies.Add(transformed);
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error loading system template {fileName}: {e.Message}");
        }

        return bodies;
    }

    private static Dictionary TransformCelestialBodyTemplate(Dictionary raw)
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
            result["template"] = TransformCelestialTemplate(templateRaw);
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

            if (mesh.TryGetValue("noise_settings", out var noiseVariant))
            {
                result["noise_settings"] = noiseVariant.AsGodotDictionary();
            }
        }

        if (celestial.TryGetValue("resources", out var resourcesVariant))
        {
            result["resources"] = resourcesVariant.AsGodotDictionary();
        }

        result["possible_names"] = ExtractNameCategories(raw);

        return result;
    }

    private static Dictionary TransformSatelliteBodyTemplate(Dictionary raw)
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

            if (mesh.TryGetValue("noise_settings", out var noiseVariant))
            {
                result["noise_settings"] = noiseVariant.AsGodotDictionary();
            }
        }

        if (satellite.TryGetValue("resources", out var resourcesVariant))
        {
            result["resources"] = resourcesVariant.AsGodotDictionary();
        }

        result["possible_names"] = ExtractNameCategories(raw);

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

    private static Dictionary TransformCelestialTemplate(Dictionary raw)
    {
        var result = new Dictionary();

        result["position"] = ReadVector3(raw, "position", Vector3.Zero);
        result["velocity"] = ReadVector3(raw, "velocity", Vector3.Zero);
        result["mass"] = ReadFloat(raw, "mass", 1f);
        result["size"] = ReadFloat(raw, "size", 1f);

        return result;
    }

    private static Dictionary TransformSatelliteTemplate(Dictionary raw)
    {
        var result = new Dictionary();

        result["position"] = ReadVector3(raw, "position", Vector3.Zero);
        result["velocity"] = ReadVector3(raw, "velocity", Vector3.Zero);

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

        result["type"] = ReadString(raw, "type", "Star");

        var template = new Dictionary();
        template["position"] = ReadVector3(raw, "position", Vector3.Zero);
        template["velocity"] = ReadVector3(raw, "velocity", Vector3.Zero);
        template["mass"] = ReadFloat(raw, "mass", 1f);
        template["size"] = ReadFloat(raw, "size", 1f);
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

        return result;
    }

    private static Dictionary TransformSystemTemplateSatellite(Dictionary raw)
    {
        var result = new Dictionary();

        result["type"] = ReadString(raw, "type", "Moon");

        var template = new Dictionary();
        template["position"] = ReadVector3(raw, "position", Vector3.Zero);
        template["velocity"] = ReadVector3(raw, "velocity", Vector3.Zero);
        template["mass"] = ReadFloat(raw, "mass", 1f);
        template["size"] = ReadFloat(raw, "size", 1f);
        result["template"] = template;

        if (raw.TryGetValue("template", out var templateVariant))
        {
            var groupTemplate = templateVariant.AsGodotDictionary();
            var numRange = ReadIntRange(groupTemplate, "number_asteroids", (1, 4));
            result["lower_range"] = numRange.Item1;
            result["upper_range"] = numRange.Item2;
            result["ring_apogee"] = ReadFloat(groupTemplate, "apogee", 0f);
            result["ring_perigee"] = ReadFloat(groupTemplate, "perigee", 0f);
            result["ring_velocity"] = ReadVector3(groupTemplate, "ring_velocity", Vector3.Zero);
            result["grouping"] = ReadString(groupTemplate, "grouping", "Balanced");

            var sizeRange = ReadFloatRange(groupTemplate, "size_range", (1f, 5f));
            result["size_min"] = sizeRange.Item1;
            result["size_max"] = sizeRange.Item2;

            var massRange = ReadFloatRange(groupTemplate, "mass_range", (1f, 10f));
            result["mass_min"] = massRange.Item1;
            result["mass_max"] = massRange.Item2;
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

            if (mesh.TryGetValue("noise_settings", out var noiseVariant))
            {
                result["noise_settings"] = noiseVariant.AsGodotDictionary();
            }
        }

        return result;
    }

    private static Dictionary ExtractNameCategories(Dictionary raw)
    {
        var result = new Dictionary();

        if (!raw.TryGetValue("categories", out var categoriesVariant))
            return result;

        var categories = categoriesVariant.AsGodotDictionary();
        var potential = ReadStringArray(categories, "potential", System.Array.Empty<string>());

        foreach (var category in potential)
        {
            if (raw.TryGetValue(category, out var sectionVariant))
            {
                var section = sectionVariant.AsGodotDictionary();
                var names = ReadStringArray(section, "names", System.Array.Empty<string>());
                if (names.Length > 0)
                {
                    result[category] = names;
                }
            }
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

    private static (float, float) ReadFloatRange(Dictionary dict, string key, (float, float) fallback)
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
                if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
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
                if (int.TryParse(s, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
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
                ["position"] = Vector3.Zero,
                ["velocity"] = Vector3.Zero,
                ["mass"] = 1f,
                ["size"] = 1f
            },
            ["possible_names"] = new Dictionary()
        };
    }

    private static Dictionary GetFallbackSatellite()
    {
        return new Dictionary
        {
            ["template"] = new Dictionary
            {
                ["position"] = Vector3.Zero,
                ["velocity"] = Vector3.Zero,
                ["mass"] = 1f,
                ["size"] = 1f
            },
            ["possible_names"] = new Dictionary()
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
                ["size_max"] = 5f
            }
        };
    }

    public static string GenerateYamlContent(Array<Dictionary> bodies)
    {
        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
            .Build();

        var data = new System.Collections.Generic.Dictionary<string, object>
        {
            ["bodies"] = ConvertBodiesToYamlStructure(bodies)
        };

        return serializer.Serialize(data);
    }

    private static System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>> ConvertBodiesToYamlStructure(Array<Dictionary> bodies)
    {
        var result = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();

        foreach (var body in bodies)
        {
            var bodyDict = new System.Collections.Generic.Dictionary<string, object>();
            string typeStr = (string)body["type"];

            bodyDict["type"] = typeStr;

            if (body.ContainsKey("template"))
            {
                var template = (Dictionary)body["template"];
                if (template.ContainsKey("position"))
                {
                    var pos = (Vector3)template["position"];
                    bodyDict["position"] = new System.Collections.Generic.List<float> { pos.X, pos.Y, pos.Z };
                }
                if (template.ContainsKey("velocity"))
                {
                    var vel = (Vector3)template["velocity"];
                    bodyDict["velocity"] = new System.Collections.Generic.List<float> { vel.X, vel.Y, vel.Z };
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
                    var vpeList = new System.Collections.Generic.List<System.Collections.Generic.List<int>>();
                    foreach (var row in vpe)
                    {
                        vpeList.Add(new System.Collections.Generic.List<int> { row[0], row[1] });
                    }
                    baseMeshSection["vertices_per_edge"] = vpeList;
                }
                if (baseMesh.ContainsKey("num_abberations"))
                    baseMeshSection["num_abberations"] = (int)baseMesh["num_abberations"];
                if (baseMesh.ContainsKey("num_deformation_cycles"))
                    baseMeshSection["num_deformation_cycles"] = (int)baseMesh["num_deformation_cycles"];

                meshSection["base_mesh"] = baseMeshSection;

                if (body.ContainsKey("tectonics"))
                {
                    var tectonics = (Dictionary)body["tectonics"];
                    var tectonicSection = new System.Collections.Generic.Dictionary<string, object>();

                    if (tectonics.ContainsKey("num_continents"))
                    {
                        var nc = (Array<int>)tectonics["num_continents"];
                        tectonicSection["num_continents"] = new System.Collections.Generic.List<int> { nc[0], nc[1] };
                    }
                    if (tectonics.ContainsKey("stress_scale"))
                    {
                        var ss = (Array<float>)tectonics["stress_scale"];
                        tectonicSection["stress_scale"] = new System.Collections.Generic.List<float> { ss[0], ss[1] };
                    }
                    if (tectonics.ContainsKey("shear_scale"))
                    {
                        var shs = (Array<float>)tectonics["shear_scale"];
                        tectonicSection["shear_scale"] = new System.Collections.Generic.List<float> { shs[0], shs[1] };
                    }
                    if (tectonics.ContainsKey("max_propagation_distance"))
                    {
                        var mpd = (Array<float>)tectonics["max_propagation_distance"];
                        tectonicSection["max_propagation_distance"] = new System.Collections.Generic.List<float> { mpd[0], mpd[1] };
                    }
                    if (tectonics.ContainsKey("propagation_falloff"))
                    {
                        var pf = (Array<float>)tectonics["propagation_falloff"];
                        tectonicSection["propagation_falloff"] = new System.Collections.Generic.List<float> { pf[0], pf[1] };
                    }
                    if (tectonics.ContainsKey("inactive_stress_threshold"))
                    {
                        var ist = (Array<float>)tectonics["inactive_stress_threshold"];
                        tectonicSection["inactive_stress_threshold"] = new System.Collections.Generic.List<float> { ist[0], ist[1] };
                    }
                    if (tectonics.ContainsKey("general_height_scale"))
                    {
                        var ghs = (Array<float>)tectonics["general_height_scale"];
                        tectonicSection["general_height_scale"] = new System.Collections.Generic.List<float> { ghs[0], ghs[1] };
                    }
                    if (tectonics.ContainsKey("general_shear_scale"))
                    {
                        var gss = (Array<float>)tectonics["general_shear_scale"];
                        tectonicSection["general_shear_scale"] = new System.Collections.Generic.List<float> { gss[0], gss[1] };
                    }
                    if (tectonics.ContainsKey("general_compression_scale"))
                    {
                        var gcs = (Array<float>)tectonics["general_compression_scale"];
                        tectonicSection["general_compression_scale"] = new System.Collections.Generic.List<float> { gcs[0], gcs[1] };
                    }
                    if (tectonics.ContainsKey("general_transform_scale"))
                    {
                        var gts = (Array<float>)tectonics["general_transform_scale"];
                        tectonicSection["general_transform_scale"] = new System.Collections.Generic.List<float> { gts[0], gts[1] };
                    }

                    meshSection["tectonic"] = tectonicSection;
                }

                bodyDict["mesh"] = meshSection;
            }

            if (body.ContainsKey("satellites"))
            {
                var satellites = (Godot.Collections.Array)body["satellites"];
                var satellitesList = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();

                foreach (Dictionary satellite in satellites)
                {
                    var satDict = new System.Collections.Generic.Dictionary<string, object>();
                    string satTypeStr = (string)satellite["type"];
                    satDict["type"] = satTypeStr;

                    bool isSatelliteGroup = satTypeStr.Contains("Belt") || satTypeStr.Contains("Comet");

                    if (satellite.ContainsKey("template"))
                    {
                        var satTemplate = (Dictionary)satellite["template"];

                        if (isSatelliteGroup)
                        {
                            var groupTemplateSection = new System.Collections.Generic.Dictionary<string, object>();

                            if (satTemplate.ContainsKey("lower_range"))
                                groupTemplateSection["number_asteroids"] = new System.Collections.Generic.List<int>
                                {
                                    (int)satTemplate["lower_range"],
                                    satTemplate.ContainsKey("upper_range") ? (int)satTemplate["upper_range"] : (int)satTemplate["lower_range"]
                                };
                            if (satTemplate.ContainsKey("grouping"))
                                groupTemplateSection["grouping"] = (string)satTemplate["grouping"];
                            if (satTemplate.ContainsKey("ring_apogee"))
                                groupTemplateSection["apogee"] = (float)satTemplate["ring_apogee"];
                            if (satTemplate.ContainsKey("ring_perigee"))
                                groupTemplateSection["perigee"] = (float)satTemplate["ring_perigee"];
                            if (satTemplate.ContainsKey("ring_velocity"))
                            {
                                var rv = (Vector3)satTemplate["ring_velocity"];
                                groupTemplateSection["ring_velocity"] = new System.Collections.Generic.List<float> { rv.X, rv.Y, rv.Z };
                            }
                            if (satTemplate.ContainsKey("size_min"))
                            {
                                groupTemplateSection["size_range"] = new System.Collections.Generic.List<float>
                                {
                                    (float)satTemplate["size_min"],
                                    satTemplate.ContainsKey("size_max") ? (float)satTemplate["size_max"] : (float)satTemplate["size_min"]
                                };
                            }
                            if (satTemplate.ContainsKey("mass_min"))
                            {
                                groupTemplateSection["mass_range"] = new System.Collections.Generic.List<float>
                                {
                                    (float)satTemplate["mass_min"],
                                    satTemplate.ContainsKey("mass_max") ? (float)satTemplate["mass_max"] : (float)satTemplate["mass_min"]
                                };
                            }

                            satDict["template"] = groupTemplateSection;
                        }
                        else
                        {
                            if (satTemplate.ContainsKey("position"))
                            {
                                var pos = (Vector3)satTemplate["position"];
                                satDict["position"] = new System.Collections.Generic.List<float> { pos.X, pos.Y, pos.Z };
                            }
                            if (satTemplate.ContainsKey("velocity"))
                            {
                                var vel = (Vector3)satTemplate["velocity"];
                                satDict["velocity"] = new System.Collections.Generic.List<float> { vel.X, vel.Y, vel.Z };
                            }
                            if (satTemplate.ContainsKey("mass"))
                                satDict["mass"] = (float)satTemplate["mass"];
                            if (satTemplate.ContainsKey("size"))
                                satDict["size"] = (int)satTemplate["size"];
                        }
                    }

                    var satMeshSection = new System.Collections.Generic.Dictionary<string, object>();
                    bool hasMeshContent = false;

                    if (satellite.ContainsKey("base_mesh"))
                    {
                        var satBaseMesh = (Dictionary)satellite["base_mesh"];
                        var baseMeshSection = new System.Collections.Generic.Dictionary<string, object>();

                        if (satBaseMesh.ContainsKey("subdivisions"))
                            baseMeshSection["subdivisions"] = (int)satBaseMesh["subdivisions"];
                        if (satBaseMesh.ContainsKey("vertices_per_edge"))
                        {
                            var vpe = (Array<Array<int>>)satBaseMesh["vertices_per_edge"];
                            var vpeList = new System.Collections.Generic.List<System.Collections.Generic.List<int>>();
                            foreach (var row in vpe)
                            {
                                vpeList.Add(new System.Collections.Generic.List<int> { row[0], row[1] });
                            }
                            baseMeshSection["vertices_per_edge"] = vpeList;
                        }
                        if (satBaseMesh.ContainsKey("num_abberations"))
                            baseMeshSection["num_abberations"] = (int)satBaseMesh["num_abberations"];
                        if (satBaseMesh.ContainsKey("num_deformation_cycles"))
                            baseMeshSection["num_deformation_cycles"] = (int)satBaseMesh["num_deformation_cycles"];

                        satMeshSection["base_mesh"] = baseMeshSection;
                        hasMeshContent = true;
                    }

                    if (satellite.ContainsKey("tectonics"))
                    {
                        var satTectonics = (Dictionary)satellite["tectonics"];
                        var tectonicSection = new System.Collections.Generic.Dictionary<string, object>();

                        if (satTectonics.ContainsKey("num_continents"))
                        {
                            var nc = (Array<int>)satTectonics["num_continents"];
                            tectonicSection["num_continents"] = new System.Collections.Generic.List<int> { nc[0], nc[1] };
                        }
                        if (satTectonics.ContainsKey("stress_scale"))
                        {
                            var ss = (Array<float>)satTectonics["stress_scale"];
                            tectonicSection["stress_scale"] = new System.Collections.Generic.List<float> { ss[0], ss[1] };
                        }
                        if (satTectonics.ContainsKey("shear_scale"))
                        {
                            var shs = (Array<float>)satTectonics["shear_scale"];
                            tectonicSection["shear_scale"] = new System.Collections.Generic.List<float> { shs[0], shs[1] };
                        }
                        if (satTectonics.ContainsKey("max_propagation_distance"))
                        {
                            var mpd = (Array<float>)satTectonics["max_propagation_distance"];
                            tectonicSection["max_propagation_distance"] = new System.Collections.Generic.List<float> { mpd[0], mpd[1] };
                        }
                        if (satTectonics.ContainsKey("propagation_falloff"))
                        {
                            var pf = (Array<float>)satTectonics["propagation_falloff"];
                            tectonicSection["propagation_falloff"] = new System.Collections.Generic.List<float> { pf[0], pf[1] };
                        }
                        if (satTectonics.ContainsKey("inactive_stress_threshold"))
                        {
                            var ist = (Array<float>)satTectonics["inactive_stress_threshold"];
                            tectonicSection["inactive_stress_threshold"] = new System.Collections.Generic.List<float> { ist[0], ist[1] };
                        }
                        if (satTectonics.ContainsKey("general_height_scale"))
                        {
                            var ghs = (Array<float>)satTectonics["general_height_scale"];
                            tectonicSection["general_height_scale"] = new System.Collections.Generic.List<float> { ghs[0], ghs[1] };
                        }
                        if (satTectonics.ContainsKey("general_shear_scale"))
                        {
                            var gss = (Array<float>)satTectonics["general_shear_scale"];
                            tectonicSection["general_shear_scale"] = new System.Collections.Generic.List<float> { gss[0], gss[1] };
                        }
                        if (satTectonics.ContainsKey("general_compression_scale"))
                        {
                            var gcs = (Array<float>)satTectonics["general_compression_scale"];
                            tectonicSection["general_compression_scale"] = new System.Collections.Generic.List<float> { gcs[0], gcs[1] };
                        }
                        if (satTectonics.ContainsKey("general_transform_scale"))
                        {
                            var gts = (Array<float>)satTectonics["general_transform_scale"];
                            tectonicSection["general_transform_scale"] = new System.Collections.Generic.List<float> { gts[0], gts[1] };
                        }

                        satMeshSection["tectonic"] = tectonicSection;
                        hasMeshContent = true;
                    }

                    if (satellite.ContainsKey("scaling_settings"))
                    {
                        var satScaling = (Dictionary)satellite["scaling_settings"];
                        var scalingSection = new System.Collections.Generic.Dictionary<string, object>();

                        if (satScaling.ContainsKey("scaling_range_x"))
                        {
                            var srx = (Array<float>)satScaling["scaling_range_x"];
                            scalingSection["scaling_range_x"] = new System.Collections.Generic.List<float> { srx[0], srx[1] };
                        }
                        if (satScaling.ContainsKey("scaling_range_y"))
                        {
                            var sry = (Array<float>)satScaling["scaling_range_y"];
                            scalingSection["scaling_range_y"] = new System.Collections.Generic.List<float> { sry[0], sry[1] };
                        }
                        if (satScaling.ContainsKey("scaling_range_z"))
                        {
                            var srz = (Array<float>)satScaling["scaling_range_z"];
                            scalingSection["scaling_range_z"] = new System.Collections.Generic.List<float> { srz[0], srz[1] };
                        }

                        satMeshSection["scaling"] = scalingSection;
                        hasMeshContent = true;
                    }

                    if (satellite.ContainsKey("noise_settings"))
                    {
                        var satNoise = (Dictionary)satellite["noise_settings"];
                        var noiseSection = new System.Collections.Generic.Dictionary<string, object>();

                        if (satNoise.ContainsKey("amplitude_range"))
                        {
                            var ar = (Array<float>)satNoise["amplitude_range"];
                            noiseSection["amplitude_range"] = new System.Collections.Generic.List<float> { ar[0], ar[1] };
                        }
                        if (satNoise.ContainsKey("scaling_range"))
                        {
                            var sr = (Array<float>)satNoise["scaling_range"];
                            noiseSection["scaling_range"] = new System.Collections.Generic.List<float> { sr[0], sr[1] };
                        }
                        if (satNoise.ContainsKey("octave_range"))
                        {
                            var or = (Array<int>)satNoise["octave_range"];
                            noiseSection["octave_range"] = new System.Collections.Generic.List<int> { or[0], or[1] };
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
