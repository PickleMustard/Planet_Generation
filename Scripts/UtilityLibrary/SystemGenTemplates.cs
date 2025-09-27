using Godot;
using System;
using System.Globalization;
using System.IO;
using Tommy;

namespace PlanetGeneration
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
            if (arr < 3) return fallback;

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

        private static float NodeToFloat(TomlNode node, float fallback)
        {
            try
            {
                // Handle integer and float nodes explicitly when possible
                if (node is Tommy.TomlInteger ti) return (float)ti.Value;
                if (node is Tommy.TomlFloat tf) return (float)tf.Value;

                // Fallback: attempt to parse the serialized representation
                var s = node.ToString();
                // Strip quotes if any
                if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
                    s = s.Substring(1, s.Length - 2);
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            catch
            {
                // ignore and return fallback
            }
            return fallback;
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
