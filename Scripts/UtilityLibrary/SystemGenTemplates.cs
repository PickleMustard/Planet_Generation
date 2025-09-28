using Godot;
using Godot.Collections;
using PlanetGeneration;
using System;
using System.Globalization;
using System.IO;
using Tommy;

namespace UtilityLibrary
{
    public static class SystemGenTemplates
    {
        public struct Template
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Mass;
        }

        private const float Limit = 10000f;

        public static Template GetDefaults(CelestialBodyType type)
        {
            var path = GetTomlPath(type);
            if (TryParseTemplate(path, out var template))
            {
                template.Position = Clamp(template.Position, Limit);
                template.Velocity = Clamp(template.Velocity, Limit);
                template.Mass = Mathf.Clamp(template.Mass, 0f, Limit);
                return template;
            }

            var fb = GetFallback(type);
            fb.Position = Clamp(fb.Position, Limit);
            fb.Velocity = Clamp(fb.Velocity, Limit);
            fb.Mass = Mathf.Clamp(fb.Mass, 0f, Limit);
            return fb;
        }

        public static Dictionary GetMeshParams(CelestialBodyType type, ulong seed = 0)
        {
            var dict = new Dictionary();
            string resPath = GetTomlPath(type);
            if (!Godot.FileAccess.FileExists(resPath))
            {
                return BuildMeshDefaults(dict); // no file, return defaults
            }

            try
            {
                using var f = Godot.FileAccess.Open(resPath, Godot.FileAccess.ModeFlags.Read);
                string text = f.GetAsText();
                using var reader = new StringReader(text);
                var table = TOML.Parse(reader);

                // Random source
                var rng = new RandomNumberGenerator();
                if (seed != 0) rng.Seed = seed; else rng.Randomize();

                // mesh.base_mesh
                int subdivisions = 1;
                int numAbberations = 3;
                int numDeformationCycles = 3;
                (int minVpe, int maxVpe) vpeRange = (1, 2);

                if (table.HasKey("mesh") && table["mesh"] is TomlTable mesh)
                {
                    if (mesh.HasKey("base_mesh") && mesh["base_mesh"] is TomlTable baseMesh)
                    {
                        subdivisions = ReadInt(baseMesh, "subdivisions", 1);
                        numAbberations = ReadInt(baseMesh, "num_abberations", 3);
                        numDeformationCycles = ReadInt(baseMesh, "num_deformation_cycles", 3);
                        vpeRange = ReadIntRange(baseMesh, "vertices_per_edge", (1, 2));
                    }

                    // mesh.tectonic
                    var tect = new Dictionary();
                    if (mesh.HasKey("tectonic") && mesh["tectonic"] is TomlTable tectonic)
                    {
                        tect["num_continents"] = RandomInRangeInt(rng, ReadIntRange(tectonic, "num_continents", (5, 10)));
                        tect["stress_scale"] = RandomInRangeFloat(rng, ReadFloatRange(tectonic, "stress_scale", (4.0f, 4.0f)));
                        tect["shear_scale"] = RandomInRangeFloat(rng, ReadFloatRange(tectonic, "shear_scale", (1.2f, 1.2f)));
                        tect["max_propagation_distance"] = RandomInRangeFloat(rng, ReadFloatRange(tectonic, "max_propagation_distance", (0.1f, 0.1f)));
                        tect["propagation_falloff"] = RandomInRangeFloat(rng, ReadFloatRange(tectonic, "propagation_falloff", (1.5f, 1.5f)));
                        tect["inactive_stress_threshold"] = RandomInRangeFloat(rng, ReadFloatRange(tectonic, "inactive_stress_threshold", (0.1f, 0.1f)));
                        tect["general_height_scale"] = RandomInRangeFloat(rng, ReadFloatRange(tectonic, "general_height_scale", (1.0f, 1.0f)));
                        tect["general_shear_scale"] = RandomInRangeFloat(rng, ReadFloatRange(tectonic, "general_shear_scale", (1.2f, 1.2f)));
                        tect["general_compression_scale"] = RandomInRangeFloat(rng, ReadFloatRange(tectonic, "general_compression_scale", (1.75f, 1.75f)));
                        tect["general_transform_scale"] = RandomInRangeFloat(rng, ReadFloatRange(tectonic, "general_transform_scale", (1.1f, 1.1f)));
                    }
                    dict["tectonic"] = tect;
                }

                // Build vertices_per_edge array from extremes per instruction
                var vpeArray = new int[subdivisions];
                for (int i = 0; i < subdivisions; i++)
                {
                    bool pickMin = rng.RandiRange(0, 1) == 0;
                    vpeArray[i] = pickMin ? vpeRange.minVpe : vpeRange.maxVpe;
                }

                dict["subdivisions"] = subdivisions;
                dict["vertices_per_edge"] = vpeArray;
                dict["num_abberations"] = numAbberations;
                dict["num_deformation_cycles"] = numDeformationCycles;

                return dict;
            }
            catch
            {
                return BuildMeshDefaults(dict);
            }
        }

        private static Dictionary BuildMeshDefaults(Dictionary dict)
        {
            dict["subdivisions"] = 1;
            dict["vertices_per_edge"] = new int[] { 2 };
            dict["num_abberations"] = 3;
            dict["num_deformation_cycles"] = 3;
            var t = new Dictionary
            {
                {"num_continents", 5},
                {"stress_scale", 4.0f},
                {"shear_scale", 1.2f},
                {"max_propagation_distance", 0.1f},
                {"propagation_falloff", 1.5f},
                {"inactive_stress_threshold", 0.1f},
                {"general_height_scale", 1.0f},
                {"general_shear_scale", 1.2f},
                {"general_compression_scale", 1.75f},
                {"general_transform_scale", 1.1f}
            };
            dict["tectonic"] = t;
            return dict;
        }

        private static string GetTomlPath(CelestialBodyType type)
        {
            string name = type switch
            {
                CelestialBodyType.RockyPlanet => "Rocky Planet",
                CelestialBodyType.GasGiant => "Gas Giant",
                CelestialBodyType.IceGiant => "Ice Giant",
                CelestialBodyType.DwarfPlanet => "Dwarf Planet",
                CelestialBodyType.Star => "Star",
                _ => type.ToString()
            };
            return $"res://Configuration/SystemGen/{name}.toml";
        }

        private static bool TryParseTemplate(string resPath, out Template template)
        {
            template = default;
            if (!Godot.FileAccess.FileExists(resPath)) return false;

            try
            {
                using var f = Godot.FileAccess.Open(resPath, Godot.FileAccess.ModeFlags.Read);
                string text = f.GetAsText();
                using var reader = new StringReader(text);

                var table = TOML.Parse(reader);
                if (!table.HasKey("template")) return false;
                if (table["template"] is not TomlTable tmpl) return false;

                var pos = ReadVector3(tmpl, "position", Vector3.Zero);
                var vel = ReadVector3(tmpl, "velocity", Vector3.Zero);
                var mass = ReadFloat(tmpl, "mass", 1f);

                template = new Template { Position = pos, Velocity = vel, Mass = mass };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Vector3 ReadVector3(TomlTable table, string key, Vector3 fallback)
        {
            if (!table.HasKey(key)) return fallback;
            if (table[key] is not TomlArray arr) return fallback;
            if (arr.ChildrenCount < 3) return fallback;

            float x = NodeToFloat(arr[0], 0f);
            float y = NodeToFloat(arr[1], 0f);
            float z = NodeToFloat(arr[2], 0f);
            return new Vector3(x, y, z);
        }

        private static float ReadFloat(TomlTable table, string key, float fallback)
        {
            if (!table.HasKey(key)) return fallback;
            return NodeToFloat(table[key], fallback);
        }

        private static int ReadInt(TomlTable table, string key, int fallback)
        {
            if (!table.HasKey(key)) return fallback;
            var node = table[key];
            if (node is Tommy.TomlInteger ti) return (int)ti.Value;
            if (node is Tommy.TomlFloat tf) return (int)tf.Value;
            var s = node.ToString();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var vf)) return (int)vf;
            return fallback;
        }

        private static (int, int) ReadIntRange(TomlTable table, string key, (int, int) fallback)
        {
            if (!table.HasKey(key)) return fallback;
            if (table[key] is TomlArray arr && arr.ChildrenCount >= 2)
            {
                int a = (int)NodeToFloat(arr[0], fallback.Item1);
                int b = (int)NodeToFloat(arr[1], fallback.Item2);
                return (Mathf.Min(a, b), Mathf.Max(a, b));
            }
            // if single value, treat as fixed range
            var single = ReadInt(table, key, fallback.Item1);
            return (single, single);
        }

        private static (float, float) ReadFloatRange(TomlTable table, string key, (float, float) fallback)
        {
            if (!table.HasKey(key)) return fallback;
            if (table[key] is TomlArray arr && arr.ChildrenCount >= 2)
            {
                float a = NodeToFloat(arr[0], fallback.Item1);
                float b = NodeToFloat(arr[1], fallback.Item2);
                return (Mathf.Min(a, b), Mathf.Max(a, b));
            }
            var single = ReadFloat(table, key, fallback.Item1);
            return (single, single);
        }

        private static float NodeToFloat(TomlNode node, float fallback)
        {
            try
            {
                if (node is Tommy.TomlInteger ti) return (float)ti.Value;
                if (node is Tommy.TomlFloat tf) return (float)tf.Value;

                var s = node.ToString();
                if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
                    s = s.Substring(1, s.Length - 2);
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            catch
            {
            }
            return fallback;
        }

        private static int RandomInRangeInt(RandomNumberGenerator rng, (int, int) range)
        {
            return rng.RandiRange(range.Item1, range.Item2);
        }

        private static float RandomInRangeFloat(RandomNumberGenerator rng, (float, float) range)
        {
            if (Mathf.IsEqualApprox(range.Item1, range.Item2)) return range.Item1;
            return rng.RandfRange(range.Item1, range.Item2);
        }

        private static Vector3 Clamp(Vector3 v, float limit)
        {
            return new Vector3(
                Mathf.Clamp(v.X, -limit, limit),
                Mathf.Clamp(v.Y, -limit, limit),
                Mathf.Clamp(v.Z, -limit, limit)
            );
        }

        private static Template GetFallback(CelestialBodyType type)
        {
            switch (type)
            {
                case CelestialBodyType.Star:
                    return new Template { Position = Vector3.Zero, Velocity = Vector3.Zero, Mass = 5000f };
                case CelestialBodyType.RockyPlanet:
                    return new Template { Position = new Vector3(100, 0, 0), Velocity = new Vector3(0, 10, 0), Mass = 10f };
                case CelestialBodyType.GasGiant:
                    return new Template { Position = new Vector3(500, 0, 0), Velocity = new Vector3(0, 7, 0), Mass = 100f };
                case CelestialBodyType.IceGiant:
                    return new Template { Position = new Vector3(800, 0, 0), Velocity = new Vector3(0, 5, 0), Mass = 50f };
                case CelestialBodyType.DwarfPlanet:
                    return new Template { Position = new Vector3(1200, 0, 0), Velocity = new Vector3(0, 12, 0), Mass = 1f };
                default:
                    return new Template { Position = Vector3.Zero, Velocity = Vector3.Zero, Mass = 1f };
            }
        }
    }
}
