using System;
using System.Globalization;
using System.IO;
using Godot;
using Godot.Collections;
using Structures.Enums;
using Tommy;

namespace UtilityLibrary
{
    public static class SystemGenTemplates
    {
        private const float Limit = 10000f;

        public static Godot.Collections.Dictionary GetCelestialBodyDefaults(CelestialBodyType type)
        {
            GD.Print("Getting defaults for " + type);
            var path = GetTomlPath(type);
            GD.Print("Trying to parse template: " + path);
            Godot.Collections.Dictionary template;
            try
            {
                template = TryParseTemplate(path);
                return template;
            }
            catch (Exception e)
            {
                GD.PrintErr($"Could not parse Celestial Body: {e.Message}\n{e.StackTrace}\n");
            }

            template = GetFallback(type);
            template["position"] = Clamp((Vector3)template["position"], Limit);
            template["velocity"] = Clamp((Vector3)template["velocity"], Limit);
            template["mass"] = Mathf.Clamp((float)template["mass"], 0f, Limit);
            return template;
        }

        public static Godot.Collections.Dictionary GetSatelliteGroupDefaults(
            SatelliteGroupTypes type
        )
        {
            var path = GetTomlPath(type);
            Godot.Collections.Dictionary template;
            try
            {
                template = TryParseTemplate(path, true);
                return template;
            }
            catch (Exception e)
            {
                GD.PrintErr(
                    $"Issues creating Satellite Group defaults: {e.Message}\n{e.StackTrace}\n"
                );
            }

            var fb = GetFallback(type);
            fb["position"] = Clamp((Vector3)fb["position"], Limit);
            fb["velocity"] = Clamp((Vector3)fb["velocity"], Limit);
            fb["mass"] = Mathf.Clamp((float)fb["mass"], 0f, Limit);
            if (fb.ContainsKey("size"))
            {
                fb["size"] = Mathf.Clamp((float)fb["size"], 0f, Limit);
            }
            return fb;
        }

        public static Godot.Collections.Dictionary GetSatelliteBodyDefaults(SatelliteBodyType type)
        {
            var path = GetTomlPath(type);
            Godot.Collections.Dictionary template;
            try
            {
                template = TryParseTemplate(path, true);
                var subtemplate = (Godot.Collections.Dictionary)template["template"];
                subtemplate["position"] = Clamp((Vector3)subtemplate["position"], Limit);
                subtemplate["velocity"] = Clamp((Vector3)subtemplate["velocity"], Limit);
                subtemplate["mass"] = Mathf.Clamp((float)subtemplate["mass"], 0f, Limit);
                if (subtemplate.ContainsKey("size"))
                {
                    subtemplate["size"] = Mathf.Clamp((float)subtemplate["size"], 0f, Limit);
                }
                template["template"] = subtemplate;
                return template;
            }
            catch (Exception e)
            {
                GD.PrintErr(
                    $"Issue creating Satellite Body Defaults: {e.Message}\n{e.StackTrace}\n"
                );
            }

            var fb = GetFallback(type);
            fb["position"] = Clamp((Vector3)fb["position"], Limit);
            fb["velocity"] = Clamp((Vector3)fb["velocity"], Limit);
            fb["mass"] = Mathf.Clamp((float)fb["mass"], 0f, Limit);
            if (fb.ContainsKey("size"))
            {
                fb["size"] = Mathf.Clamp((float)fb["size"], 0f, Limit);
            }
            return fb;
        }

        public static Godot.Collections.Array<Godot.Collections.Dictionary> LoadSolarSystemTemplate(
            string fileName
        )
        {
            var bodies = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            var filePath = $"res://Configuration/SystemTemplate/{fileName}";
            if (!Godot.FileAccess.FileExists(filePath))
                return bodies;

            try
            {
                using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
                string text = f.GetAsText();
                using var reader = new StringReader(text);
                var table = TOML.Parse(reader);

                if (table.HasKey("bodies") && table["bodies"] is TomlArray bodiesArray)
                {
                    foreach (var bodyNode in bodiesArray.Children)
                    {
                        if (bodyNode is TomlTable bodyTable)
                        {
                            var bodyDict = new Godot.Collections.Dictionary();
                            var typeStr = ReadString(bodyTable, "type", "Star");
                            string templateType = GetTemplateType(typeStr, false);
                            bodyDict["type"] = typeStr;
                            if (!bodyTable.HasKey(templateType))
                            {
                                GD.PrintErr($"Sub-table is not TOML table");
                                throw new ArgumentException(
                                    $"TOML template does not have key '{templateType}'"
                                );
                            }
                            if (bodyTable[templateType] is not TomlTable tabl)
                            {
                                GD.PrintErr($"Sub-table is not TOML table");
                                throw new Exception("Sub-table is not TOML table");
                            }

                            if (tabl.HasKey("template") && tabl["template"] is TomlArray templateArr)
                            {
                                TomlTable templateTable = (TomlTable)templateArr[0];
                                bodyDict.Add("template", ParseTemplateSection(templateTable, templateType));
                            }

                            if (tabl.HasKey("mesh"))
                            {
                                if (tabl["mesh"] is not TomlArray mshArr)
                                    throw new Exception("Mesh is not a TOML table");
                                TomlTable msh = (TomlTable)mshArr[0];
                                bodyDict.Add("base_mesh", ParseBaseMeshSection(msh));
                                if (msh.HasKey("tectonic"))
                                    bodyDict.Add(
                                        "tectonics",
                                        ParseTectonicSection(msh["tectonic"] as TomlTable)
                                    );
                                if (msh.HasKey("scaling"))
                                    bodyDict.Add("scaling", ParseScalingSection(msh["scaling"] as TomlTable));
                                if (msh.HasKey("noise_settings"))
                                    bodyDict.Add(
                                        "noise_settings",
                                        ParseNoiseSettingsSection(msh["noise_settings"] as TomlTable)
                                    );
                            }
                            // Parse satellites if present
                            var satellites =
                                new Godot.Collections.Array<Godot.Collections.Dictionary>();
                            if (
                                tabl.HasKey("satellites")
                                && tabl["satellites"] is TomlArray satellitesArray
                            )
                            {
                                foreach (var satelliteNode in satellitesArray.Children)
                                {
                                    if (satelliteNode is TomlTable satelliteTable)
                                    {
                                        var satelliteDict = new Godot.Collections.Dictionary();
                                        var satTypeStr = ReadString(
                                            satelliteTable,
                                            "type",
                                            "Moon"
                                        );
                                        GD.Print($"Satellite Node: {satelliteTable}");
                                        string satTemplateType = GetTemplateType(satTypeStr, true);
                                        GD.Print($"Satellite Type: {satTypeStr}");
                                        GD.Print($"Satellite Template Type: {satTemplateType}");
                                        satelliteDict["type"] = satTypeStr;
                                        if (satTemplateType == "satellite_group")
                                        {
                                            GD.Print($"Satellite Type: {satTypeStr}");
                                            GD.Print($"Satellite Template Type: {satTemplateType}");
                                            if (satelliteTable.HasKey("template") && satelliteTable["template"] is TomlArray satTemplateArr)
                                            {
                                                TomlTable templateTable = (TomlTable)satTemplateArr[0];
                                                GD.Print($"Parsing Satellite Template: {templateTable}");
                                                satelliteDict.Add("template", ParseTemplateSection(templateTable, satTemplateType));
                                            }
                                        }
                                        else
                                        {
                                            if (satelliteTable.HasKey("template") && satelliteTable["template"] is TomlArray satTemplateArr)
                                            {
                                                TomlTable templateTable = (TomlTable)satTemplateArr[0];
                                                GD.Print($"Parsing Satellite Template: {templateTable}");
                                                satelliteDict.Add("template", ParseTemplateSection(templateTable, satTemplateType));
                                            }

                                            var satelliteMeshDict = new Godot.Collections.Dictionary();
                                            if (satelliteTable.HasKey("mesh"))
                                            {
                                                if (satelliteTable["mesh"] is not TomlArray satelliteMeshArr)
                                                    throw new Exception("Mesh is not a TOML table");
                                                TomlTable msh = (TomlTable)satelliteMeshArr[0];
                                                GD.Print($"Parsing Satellite Mesh: {msh}");
                                                satelliteDict.Add("base_mesh", ParseBaseMeshSection(msh));
                                                if (msh.HasKey("tectonic"))
                                                    satelliteDict.Add(
                                                        "tectonics",
                                                        ParseTectonicSection(msh["tectonic"] as TomlTable)
                                                    );
                                                if (msh.HasKey("scaling"))
                                                    satelliteDict.Add("scaling_settings", ParseScalingSection(msh["scaling"] as TomlTable));
                                                if (msh.HasKey("noise_settings"))
                                                    satelliteDict.Add(
                                                        "noise_settings",
                                                        ParseNoiseSettingsSection(msh["noise_settings"] as TomlTable)
                                                    );
                                            }
                                        }
                                        satellites.Add(satelliteDict);
                                    }
                                }
                            }
                            bodyDict["satellites"] = satellites;
                            bodies.Add(bodyDict);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error loading template {fileName}: {e.Message}");
            }
            return bodies;
        }

        private static string GetTomlPath(CelestialBodyType type)
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
            return $"res://Configuration/SystemGen/{name}.toml";
        }

        private static string GetTomlPath(SatelliteBodyType type)
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
            return $"res://Configuration/SystemGen/{name}.toml";
        }

        private static string GetTomlPath(SatelliteGroupTypes type)
        {
            string name = type switch
            {
                SatelliteGroupTypes.AsteroidBelt => "AsteroidBelt",
                SatelliteGroupTypes.Comet => "Comet",
                SatelliteGroupTypes.IceBelt => "IceBelt",
                _ => type.ToString(),
            };
            return $"res://Configuration/SystemGen/{name}.toml";
        }

        private static Godot.Collections.Dictionary TryParseTemplate(
            string resPath,
            bool isSatellite = false
        )
        {
            var template = new Godot.Collections.Dictionary();
            if (!Godot.FileAccess.FileExists(resPath))
                throw new ArgumentException("File path does not exist");

            try
            {
                using var f = Godot.FileAccess.Open(resPath, Godot.FileAccess.ModeFlags.Read);
                string text = f.GetAsText();
                using var reader = new StringReader(text);
                string templateType = GetTemplateType(resPath, isSatellite);

                var table = TOML.Parse(reader);
                if (!table.HasKey(templateType))
                {
                    GD.PrintErr($"Sub-table is not TOML table");
                    throw new ArgumentException(
                        $"TOML template does not have key '{templateType}'"
                    );
                }
                if (table[templateType] is not TomlTable tabl)
                {
                    GD.PrintErr($"Sub-table is not TOML table");
                    throw new Exception("Sub-table is not TOML table");
                }
                if (!tabl.HasKey("template"))
                    throw new ArgumentException("TOML template does not have key 'template'");
                if (tabl["template"] is not TomlTable tmpl)
                    throw new Exception("Template is not a TOML table");

                template.Add("template", ParseTemplateSection(tmpl, templateType));

                if (table.HasKey("categories"))
                    template.Add("possible_names", ParseNameSection(table));

                if (tabl.HasKey("mesh"))
                {
                    if (tabl["mesh"] is not TomlTable msh)
                        throw new Exception("Mesh is not a TOML table");
                    template.Add("base_mesh", ParseBaseMeshSection(msh));
                    if (msh.HasKey("tectonic"))
                        template.Add(
                            "tectonics",
                            ParseTectonicSection(msh["tectonic"] as TomlTable)
                        );
                    if (msh.HasKey("scaling"))
                        template.Add("scaling_settings", ParseScalingSection(msh["scaling"] as TomlTable));
                    if (msh.HasKey("noise_settings"))
                        template.Add(
                            "noise_settings",
                            ParseNoiseSettingsSection(msh["noise_settings"] as TomlTable)
                        );
                }
            }
            catch (Exception e)
            {
                GD.Print($"Error parsing template: {e.Message}\n{e.StackTrace}\n");
            }
            return template;
        }

        private static string GetTemplateType(string resPath, bool isSatellite)
        {
            if (resPath.Contains("AsteroidBelt"))
                return "satellite_group";
            if (
                isSatellite
                && (
                    resPath.Contains("Asteroid")
                    || resPath.Contains("Moon")
                    || resPath.Contains("Comet")
                    || resPath.Contains("DwarfPlanet")
                )
            )
                return "satellite";
            if (
                resPath.Contains("Star")
                || resPath.Contains("RockyPlanet")
                || resPath.Contains("GasGiant")
                || resPath.Contains("IceGiant")
                || resPath.Contains("DwarfPlanet")
                || resPath.Contains("BlackHole")
            )
                return "celestial";
            return "celestial"; // fallback
        }

        private static Godot.Collections.Dictionary ParseTemplateSection(
            TomlTable tmpl,
            string type
        )
        {
            var templateDict = new Godot.Collections.Dictionary();
            if (type == "celestial")
            {
                var pos = ReadVector3(tmpl, "position", Vector3.Zero);
                var vel = ReadVector3(tmpl, "velocity", Vector3.Zero);
                var mass = ReadFloat(tmpl, "mass", 1f);
                var size = ReadFloat(tmpl, "size", 1f);
                templateDict.Add("position", pos);
                templateDict.Add("velocity", vel);
                templateDict.Add("mass", mass);
                templateDict.Add("size", size);
            }
            else if (type == "satellite")
            {
                var pos = ReadVector3(tmpl, "position", Vector3.Zero);
                var vel = ReadVector3(tmpl, "velocity", Vector3.Zero);
                var sizeRange = ReadFloatRange(tmpl, "size_range", (1f, 4f));
                var massRange = ReadFloatRange(tmpl, "mass_range", (1f, 10f));
                templateDict.Add("position", pos);
                templateDict.Add("velocity", vel);
                templateDict.Add("mass", massRange.Item1);
                templateDict.Add("size", sizeRange.Item1);
            }
            else if (type == "satellite_group")
            {
                var numAsteroidRange = ReadIntRange(tmpl, "number_asteroids", (1, 4));
                var ringApogee = ReadFloat(tmpl, "ring_apogee", 0f);
                var ringPerigee = ReadFloat(tmpl, "ring_perigee", 0f);
                var ringVelocity = ReadVector3(tmpl, "ring_velocity", Vector3.Zero);
                var sizeRange = ReadFloatRange(tmpl, "size_range", (1f, 5f));
                var massRange = ReadFloatRange(tmpl, "mass_range", (1f, 10f));
                var grouping = ReadString(tmpl, "grouping", "Balanced");
                templateDict.Add("ring_apogee", ringApogee);
                templateDict.Add("ring_perigee", ringPerigee);
                templateDict.Add("ring_velocity", ringVelocity);
                templateDict.Add("lower_range", numAsteroidRange.Item1);
                templateDict.Add("upper_range", numAsteroidRange.Item2);
                templateDict.Add("grouping", grouping);
                templateDict.Add("mass_min", massRange.Item1);
                templateDict.Add("mass_max", massRange.Item2);
                templateDict.Add("size_min", sizeRange.Item1);
                templateDict.Add("size_max", sizeRange.Item2);
            }
            return templateDict;
        }

        private static Godot.Collections.Dictionary ParseBaseMeshSection(TomlTable msh)
        {
            if (msh["base_mesh"] is not TomlTable mshSet)
                throw new Exception("Base Mesh is not a TOML table");

            var baseMeshDict = new Godot.Collections.Dictionary();
            var subdivisions = ReadInt(mshSet, "subdivisions", 1);
            (int minVpe, int maxVpe)[] vpeRange = new (int, int)[] { (1, 2) };
            vpeRange = ReadIntRangeArray(mshSet, "vertices_per_edge", new (int, int)[] { (1, 2) });
            Godot.Collections.Array<Godot.Collections.Array<int>> vpeArray = new Godot.Collections.Array<Godot.Collections.Array<int>>();
            for (int i = 0; i < subdivisions; i++)
            {
                Godot.Collections.Array<int> row = new Godot.Collections.Array<int>();
                if (vpeRange.Length > i)
                {
                    row.Add(vpeRange[i].minVpe);
                    row.Add(vpeRange[i].maxVpe);
                }
                vpeArray.Add(row);
            }
            var num_abberations = ReadInt(mshSet, "num_abberations", 3);
            var num_deformation_cycles = ReadInt(mshSet, "num_deformation_cycles", 3);
            baseMeshDict.Add("subdivisions", subdivisions);
            baseMeshDict.Add("vertices_per_edge", vpeArray);
            baseMeshDict.Add("num_abberations", num_abberations);
            baseMeshDict.Add("num_deformation_cycles", num_deformation_cycles);
            return baseMeshDict;
        }

        private static Godot.Collections.Dictionary ParseTectonicSection(TomlTable tecSet)
        {
            var tectDict = new Godot.Collections.Dictionary();
            var num_continents = ReadIntRange(tecSet, "num_continents", (3, 5));
            Godot.Collections.Array<int> numContRange = new Godot.Collections.Array<int>
            {
                num_continents.Item1,
                num_continents.Item2,
            };
            var stress_scale = ReadFloatRange(tecSet, "stress_scale", (1.0f, 1.2f));
            Godot.Collections.Array<float> stressScaleRange = new Godot.Collections.Array<float>
            {
                stress_scale.Item1,
                stress_scale.Item2,
            };
            var shear_scale = ReadFloatRange(tecSet, "shear_scale", (1.0f, 1.2f));
            Godot.Collections.Array<float> shearStressScaleRange =
                new Godot.Collections.Array<float> { shear_scale.Item1, shear_scale.Item2 };
            var max_propagation_distance = ReadFloatRange(
                tecSet,
                "max_propagation_distance",
                (1.0f, 1.2f)
            );
            Godot.Collections.Array<float> propDistRange = new Godot.Collections.Array<float>
            {
                max_propagation_distance.Item1,
                max_propagation_distance.Item2,
            };
            var propagation_falloff = ReadFloatRange(tecSet, "propagation_falloff", (1.0f, 1.2f));
            Godot.Collections.Array<float> propFalloffRange = new Godot.Collections.Array<float>
            {
                propagation_falloff.Item1,
                propagation_falloff.Item2,
            };
            var inactive_stress_threshold = ReadFloatRange(
                tecSet,
                "inactive_stress_threshold",
                (1.0f, 1.2f)
            );
            Godot.Collections.Array<float> inactiveStressRange = new Godot.Collections.Array<float>
            {
                inactive_stress_threshold.Item1,
                inactive_stress_threshold.Item2,
            };
            var general_height_scale = ReadFloatRange(tecSet, "general_height_scale", (1.0f, 1.2f));
            Godot.Collections.Array<float> heightScaleRange = new Godot.Collections.Array<float>
            {
                general_height_scale.Item1,
                general_height_scale.Item2,
            };
            var general_transform_scale = ReadFloatRange(
                tecSet,
                "general_transform_scale",
                (1.0f, 1.2f)
            );
            Godot.Collections.Array<float> transformScaleRange = new Godot.Collections.Array<float>
            {
                general_transform_scale.Item1,
                general_transform_scale.Item2,
            };
            var general_compression_scale = ReadFloatRange(
                tecSet,
                "general_compression_scale",
                (1.0f, 1.2f)
            );
            Godot.Collections.Array<float> compressionScaleRange =
                new Godot.Collections.Array<float>
                {
                    general_compression_scale.Item1,
                    general_compression_scale.Item2,
                };
            var general_shear_scale = ReadFloatRange(tecSet, "general_shear_scale", (1.0f, 1.2f));
            Godot.Collections.Array<float> shearScaleRange = new Godot.Collections.Array<float>
            {
                general_shear_scale.Item1,
                general_shear_scale.Item2,
            };
            tectDict.Add("num_continents", numContRange);
            tectDict.Add("stress_scale", stressScaleRange);
            tectDict.Add("shear_scale", shearStressScaleRange);
            tectDict.Add("max_propagation_distance", propDistRange);
            tectDict.Add("propagation_falloff", propFalloffRange);
            tectDict.Add("inactive_stress_threshold", inactiveStressRange);
            tectDict.Add("general_height_scale", heightScaleRange);
            tectDict.Add("general_transform_scale", transformScaleRange);
            tectDict.Add("general_compression_scale", compressionScaleRange);
            tectDict.Add("general_shear_scale", shearScaleRange);
            return tectDict;
        }

        private static Godot.Collections.Dictionary ParseScalingSection(TomlTable scl)
        {
            var scalingDict = new Godot.Collections.Dictionary();
            var x_scale_range = ReadFloatRange(scl, "scaling_range_x", (1.0f, 1.2f));
            Godot.Collections.Array<float> xScaleRange = new Godot.Collections.Array<float>
            {
                x_scale_range.Item1,
                x_scale_range.Item2,
            };
            var y_scale_range = ReadFloatRange(scl, "scaling_range_y", (1.0f, 1.2f));
            Godot.Collections.Array<float> yScaleRange = new Godot.Collections.Array<float>
            {
                y_scale_range.Item1,
                y_scale_range.Item2,
            };
            var z_scale_range = ReadFloatRange(scl, "scaling_range_z", (1.0f, 1.2f));
            Godot.Collections.Array<float> zScaleRange = new Godot.Collections.Array<float>
            {
                z_scale_range.Item1,
                z_scale_range.Item2,
            };

            scalingDict.Add("x_scale_range", xScaleRange);
            scalingDict.Add("y_scale_range", yScaleRange);
            scalingDict.Add("z_scale_range", zScaleRange);
            return scalingDict;
        }

        private static Godot.Collections.Dictionary ParseNoiseSettingsSection(TomlTable noiseSet)
        {
            var noiseDict = new Godot.Collections.Dictionary();
            var amplitude_range = ReadFloatRange(noiseSet, "amplitude_range", (1.0f, 1.2f));
            Godot.Collections.Array<float> amplitudeRange = new Godot.Collections.Array<float>
            {
                amplitude_range.Item1,
                amplitude_range.Item2,
            };
            var scaling_range = ReadFloatRange(noiseSet, "scaling_range", (1.0f, 1.2f));
            Godot.Collections.Array<float> scalingRange = new Godot.Collections.Array<float>
            {
                scaling_range.Item1,
                scaling_range.Item2,
            };
            var octave_range = ReadIntRange(noiseSet, "octave_range", (1, 2));
            Godot.Collections.Array<int> octaveRange = new Godot.Collections.Array<int>
            {
                octave_range.Item1,
                octave_range.Item2,
            };
            noiseDict.Add("amplitude_range", amplitudeRange);
            noiseDict.Add("scaling_range", scalingRange);
            noiseDict.Add("octave_range", octaveRange);
            return noiseDict;
        }

        private static Godot.Collections.Dictionary ParseNameSection(TomlTable table)
        {
            var nameDict = new Godot.Collections.Dictionary();

            if (table.HasKey("categories") && table["categories"] is TomlTable categories)
            {
                var potentialCategories = ReadStringArray(categories, "potential", new String[] { "mythology" });

                foreach (var category in potentialCategories)
                {
                    if (table.HasKey(category) && table[category] is TomlTable nameArray)
                    {
                        var names = ReadStringArray(nameArray, "names", new String[] { });
                        if (names.Length > 0)
                        {
                            nameDict[category] = names;
                        }
                    }
                }
            }

            return nameDict;
        }

        private static Vector3 ReadVector3(TomlTable table, string key, Vector3 fallback)
        {
            if (!table.HasKey(key))
                return fallback;
            if (table[key] is not TomlArray arr)
                return fallback;
            if (arr.ChildrenCount < 3)
                return fallback;

            float x = NodeToFloat(arr[0], 0f);
            float y = NodeToFloat(arr[1], 0f);
            float z = NodeToFloat(arr[2], 0f);
            return new Vector3(x, y, z);
        }

        private static float ReadFloat(TomlTable table, string key, float fallback)
        {
            if (!table.HasKey(key))
                return fallback;
            return NodeToFloat(table[key], fallback);
        }

        private static int ReadInt(TomlTable table, string key, int fallback)
        {
            if (!table.HasKey(key))
                return fallback;
            var node = table[key];
            if (node is Tommy.TomlInteger ti)
                return (int)ti.Value;
            if (node is Tommy.TomlFloat tf)
                return (int)tf.Value;
            var s = node.ToString();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var vf))
                return (int)vf;
            return fallback;
        }

        private static (int, int)[] ReadIntRangeArray(TomlTable table, string key, (int, int)[] fallback)
        {
            System.Collections.Generic.List<(int, int)> result = new System.Collections.Generic.List<(int, int)>();
            if (!table.HasKey(key))
                return fallback;
            if (table[key] is TomlArray arr && arr.ChildrenCount > 0)
            {
                foreach (var child in arr.Children)
                {
                    if (child is TomlArray childArr && childArr.ChildrenCount >= 2)
                    {
                        int a = (int)NodeToFloat(childArr[0], fallback[0].Item1);
                        int b = (int)NodeToFloat(childArr[1], fallback[0].Item2);
                        result.Add((a, b));
                    }
                    else if (child is TomlArray childNode)
                    {
                        int single = (int)NodeToFloat(childNode[0], fallback[0].Item1);
                    }
                }
            }
            return result.ToArray();

        }
        private static (int, int) ReadIntRange(TomlTable table, string key, (int, int) fallback)
        {
            if (!table.HasKey(key))
                return fallback;
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

        private static (float, float) ReadFloatRange(
            TomlTable table,
            string key,
            (float, float) fallback
        )
        {
            if (!table.HasKey(key))
                return fallback;
            if (table[key] is TomlArray arr && arr.ChildrenCount >= 2)
            {
                float a = NodeToFloat(arr[0], fallback.Item1);
                float b = NodeToFloat(arr[1], fallback.Item2);
                return (Mathf.Min(a, b), Mathf.Max(a, b));
            }
            var single = ReadFloat(table, key, fallback.Item1);
            return (single, single);
        }

        private static String[] ReadStringArray(TomlTable table, String key, String[] fallback)
        {
            if (!table.HasKey(key))
                return fallback;
            if (table[key] is TomlArray arr && arr.ChildrenCount > 0)
            {
                String[] result = new String[arr.ChildrenCount];
                for (int i = 0; i < arr.ChildrenCount; i++)
                {
                    result[i] = arr[i].ToString();
                }
                return result;
            }
            return fallback;
        }

        private static float NodeToFloat(TomlNode node, float fallback)
        {
            try
            {
                if (node is Tommy.TomlInteger ti)
                {
                    return (float)ti.Value;
                }
                if (node is Tommy.TomlFloat tf)
                    return (float)tf.Value;

                var s = node.ToString();
                if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
                    s = s.Substring(1, s.Length - 2);
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            catch (Exception e) { GD.PrintErr($"Error reading Node into float: {e.Message}\n{e.StackTrace}\n"); }
            return fallback;
        }

        private static int RandomInRangeInt(RandomNumberGenerator rng, (int, int) range)
        {
            return rng.RandiRange(range.Item1, range.Item2);
        }

        private static float RandomInRangeFloat(RandomNumberGenerator rng, (float, float) range)
        {
            if (Mathf.IsEqualApprox(range.Item1, range.Item2))
                return range.Item1;
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

        private static Godot.Collections.Dictionary GetFallback(CelestialBodyType type)
        {
            return new Godot.Collections.Dictionary();
        }

        private static Godot.Collections.Dictionary GetFallback(SatelliteGroupTypes type)
        {
            return new Godot.Collections.Dictionary();
        }

        private static Godot.Collections.Dictionary GetFallback(SatelliteBodyType type)
        {
            return new Godot.Collections.Dictionary();
        }


        private static string ReadString(TomlTable table, string key, string fallback)
        {
            if (table.HasKey(key) && table[key] is TomlNode node)
            {
                return node.ToString().Trim('"');
            }
            return fallback;
        }

        private static int[] ReadIntArray(TomlTable table, string key, int[] fallback)
        {
            if (!table.HasKey(key))
                return fallback;
            if (table[key] is TomlArray arr && arr.ChildrenCount > 0)
            {
                int[] result = new int[arr.ChildrenCount];
                for (int i = 0; i < arr.ChildrenCount; i++)
                {
                    result[i] = (int)NodeToFloat(arr[i], fallback[i % fallback.Length]);
                }
                return result;
            }
            return fallback;
        }

        public static string GenerateTOMLContent(Array<Dictionary> bodies)
        {
            var toml = new System.Text.StringBuilder();
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            for (int bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
            {
                var body = bodies[bodyIndex];
                string templateType = GetTemplateType((string)body["type"], false);

                // Add body section
                toml.AppendLine("[[bodies]]");
                toml.AppendLine($"type = \"{body["type"]}\"");
                toml.AppendLine();

                // Add template section
                var template = (Godot.Collections.Dictionary)body["template"];
                toml.AppendLine("[[bodies.celestial.template]]");

                var position = (Vector3)template["position"];
                toml.AppendLine($"position = [{position.X.ToString("F1", culture)}, {position.Y.ToString("F1", culture)}, {position.Z.ToString("F1", culture)}]");

                var velocity = (Vector3)template["velocity"];
                toml.AppendLine($"velocity = [{velocity.X.ToString("F1", culture)}, {velocity.Y.ToString("F1", culture)}, {velocity.Z.ToString("F1", culture)}]");

                toml.AppendLine($"mass = {((float)template["mass"]).ToString("F1", culture)}");
                toml.AppendLine($"size = {(int)template["size"]}");
                toml.AppendLine();

                // Add mesh section
                toml.AppendLine("[[bodies.celestial.mesh]]");

                var baseMesh = (Godot.Collections.Dictionary)body["base_mesh"];
                toml.Append("base_mesh = { ");
                toml.Append($"subdivisions = {(int)baseMesh["subdivisions"]}, ");

                var verticesPerEdge = (Godot.Collections.Array<Godot.Collections.Array<int>>)baseMesh["vertices_per_edge"];
                toml.Append("vertices_per_edge = [");
                for (int i = 0; i < verticesPerEdge.Count; i++)
                {
                    var vpe = verticesPerEdge[i];
                    toml.Append($"[{vpe[0]}, {vpe[1]}]");
                    if (i < verticesPerEdge.Count - 1) toml.Append(", ");
                }
                toml.Append("], ");

                toml.Append($"num_abberations = {(int)baseMesh["num_abberations"]}, ");
                toml.AppendLine("num_deformation_cycles = " + ((int)baseMesh["num_deformation_cycles"]) + " }");

                // Add tectonics section
                var tectonics = (Godot.Collections.Dictionary)body["tectonics"];
                toml.Append("tectonic = { ");

                toml.Append($"num_continents = {ArrayToTOML((int[])tectonics["num_continents"])}, ");
                toml.Append($"stress_scale = {ArrayToTOML((float[])tectonics["stress_scale"], culture)}, ");
                toml.Append($"shear_scale = {ArrayToTOML((float[])tectonics["shear_scale"], culture)}, ");
                toml.Append($"max_propagation_distance = {ArrayToTOML((float[])tectonics["max_propagation_distance"], culture)}, ");
                toml.Append($"propagation_falloff = {ArrayToTOML((float[])tectonics["propagation_falloff"], culture)}, ");
                toml.Append($"inactive_stress_threshold = {ArrayToTOML((float[])tectonics["inactive_stress_threshold"], culture)}, ");
                toml.Append($"general_height_scale = {ArrayToTOML((float[])tectonics["general_height_scale"], culture)}, ");
                toml.Append($"general_shear_scale = {ArrayToTOML((float[])tectonics["general_shear_scale"], culture)}, ");
                toml.Append($"general_compression_scale = {ArrayToTOML((float[])tectonics["general_compression_scale"], culture)}, ");
                toml.AppendLine("general_transform_scale = " + ArrayToTOML((float[])tectonics["general_transform_scale"], culture) + " }");

                // Add satellites if any
                if (body.ContainsKey("satellites"))
                {
                    var satellites = (Godot.Collections.Array)body["satellites"];
                    foreach (Godot.Collections.Dictionary satellite in satellites)
                    {
                        GD.Print($"Satellite: {satellite}");
                        toml.AppendLine();
                        toml.AppendLine("[[bodies.celestial.satellites]]");
                        toml.AppendLine($"type = \"{satellite["type"]}\"");
                        string satTypeStr = (string)satellite["type"];
                        string satTemplateType = GetTemplateType(satTypeStr, true);

                        if (satTemplateType == "satellite")
                        {
                            var satTemplate = (Godot.Collections.Dictionary)satellite["template"];
                            toml.AppendLine("[[bodies.celestial.satellites.template]]");

                            var satPosition = (Vector3)satTemplate["base_position"];
                            toml.AppendLine($"position = [{satPosition.X.ToString("F1", culture)}, {satPosition.Y.ToString("F1", culture)}, {satPosition.Z.ToString("F1", culture)}]");

                            var satVelocity = (Vector3)satTemplate["satellite_velocity"];
                            toml.AppendLine($"velocity = [{satVelocity.X.ToString("F1", culture)}, {satVelocity.Y.ToString("F1", culture)}, {satVelocity.Z.ToString("F1", culture)}]");

                            toml.AppendLine($"mass = {((float)satTemplate["mass"]).ToString("F1", culture)}");

                            toml.AppendLine($"size = {(int)satTemplate["size"]}");

                            // Add satellite mesh section
                            if (satellite.ContainsKey("base_mesh"))
                            {
                                var satMesh = (Godot.Collections.Dictionary)satellite["base_mesh"];
                                toml.AppendLine("[[bodies.celestial.satellites.mesh]]");

                                toml.Append("base_mesh = { ");
                                toml.Append($"subdivisions = {(int)satMesh["subdivisions"]}, ");

                                var satVerticesPerEdge = (Godot.Collections.Array<Godot.Collections.Array<int>>)satMesh["vertices_per_edge"];
                                toml.Append("vertices_per_edge = [");
                                for (int i = 0; i < satVerticesPerEdge.Count; i++)
                                {
                                    var vpe = satVerticesPerEdge[i];
                                    toml.Append($"[{vpe[0]}, {vpe[1]}]");
                                    if (i < satVerticesPerEdge.Count - 1) toml.Append(", ");
                                }
                                toml.Append("], ");

                                toml.Append($"num_abberations = {(int)satMesh["num_abberations"]}, ");
                                toml.AppendLine("num_deformation_cycles = " + ((int)satMesh["num_deformation_cycles"]) + " }");
                            }
                            // Add satellite tectonics
                            if (satellite.ContainsKey("tectonic"))
                            {
                                var satTectonics = (Godot.Collections.Dictionary)satellite["tectonic"];
                                toml.Append("tectonic = { ");

                                toml.Append($"num_continents = {ArrayToTOML((int[])satTectonics["num_continents"])}, ");
                                toml.Append($"stress_scale = {ArrayToTOML((float[])satTectonics["stress_scale"], culture)}, ");
                                toml.Append($"shear_scale = {ArrayToTOML((float[])satTectonics["shear_scale"], culture)}, ");
                                toml.Append($"max_propagation_distance = {ArrayToTOML((float[])satTectonics["max_propagation_distance"], culture)}, ");
                                toml.Append($"propagation_falloff = {ArrayToTOML((float[])satTectonics["propagation_falloff"], culture)}, ");
                                toml.Append($"inactive_stress_threshold = {ArrayToTOML((float[])satTectonics["inactive_stress_threshold"], culture)}, ");
                                toml.Append($"general_height_scale = {ArrayToTOML((float[])satTectonics["general_height_scale"], culture)}, ");
                                toml.Append($"general_shear_scale = {ArrayToTOML((float[])satTectonics["general_shear_scale"], culture)}, ");
                                toml.Append($"general_compression_scale = {ArrayToTOML((float[])satTectonics["general_compression_scale"], culture)}, ");
                                toml.AppendLine("general_transform_scale = " + ArrayToTOML((float[])satTectonics["general_transform_scale"], culture) + " }");
                            }

                            // Add scaling section if present
                            if (satellite.ContainsKey("scaling_settings"))
                            {
                                var scaling = (Godot.Collections.Dictionary)satellite["scaling_settings"];
                                toml.Append("scaling = { ");

                                var scalingRangeX = (float[])scaling["x_scale_range"];
                                var scalingRangeY = (float[])scaling["y_scale_range"];
                                var scalingRangeZ = (float[])scaling["z_scale_range"];

                                toml.Append($"scaling_range_x = {ArrayToTOML(scalingRangeX, culture)}, ");
                                toml.Append($"scaling_range_y = {ArrayToTOML(scalingRangeY, culture)}, ");
                                toml.AppendLine("scaling_range_z = " + ArrayToTOML(scalingRangeZ, culture) + " }");
                            }

                            // Add noise settings if present
                            if (satellite.ContainsKey("noise_settings"))
                            {
                                var noise = (Godot.Collections.Dictionary)satellite["noise_settings"];
                                toml.Append("noise_settings = { ");

                                var amplitudeRange = (float[])noise["amplitude_range"];
                                var scalingRange = (float[])noise["scaling_range"];
                                var octaveRange = (int[])noise["octave_range"];

                                toml.Append($"amplitude_range = {ArrayToTOML(amplitudeRange, culture)}, ");
                                toml.Append($"scaling_range = {ArrayToTOML(scalingRange, culture)}, ");
                                toml.AppendLine("octave_range = " + ArrayToTOML(octaveRange) + " }");
                            }
                        }
                        else if (satTemplateType == "satellite_group")
                        {
                            toml.AppendLine("[[bodies.celestial.satellites.template]]");
                            toml.AppendLine($"ring_apogee = {((float)satellite["ring_apogee"]).ToString("F1", culture)}");
                            toml.AppendLine($"ring_perigee = {((float)satellite["ring_perigee"]).ToString("F1", culture)}");
                            Vector3 ringVelocity = (Vector3)satellite["ring_velocity"];
                            toml.AppendLine($"ring_velocity = [{ringVelocity.X.ToString("F1", culture)}, {ringVelocity.Y.ToString("F1", culture)}, {ringVelocity.Z.ToString("F1", culture)}]");
                            toml.AppendLine($"lower_range = {((int)satellite["lower_range"])}");
                            toml.AppendLine($"upper_range = {((int)satellite["upper_range"])}");
                            toml.AppendLine($"grouping = \"{(String)satellite["grouping"]}\"");
                            toml.AppendLine($"mass_min = {((float)satellite["mass_min"]).ToString("F1", culture)}");
                            toml.AppendLine($"mass_max = {((float)satellite["mass_max"]).ToString("F1", culture)}");
                            toml.AppendLine($"size_min = {((float)satellite["size_min"]).ToString("F1", culture)}");
                            toml.AppendLine($"size_max = {((float)satellite["size_max"]).ToString("F1", culture)}");
                        }
                    }
                }

                if (bodyIndex < bodies.Count - 1)
                {
                    toml.AppendLine();
                }
            }

            return toml.ToString();
        }

        private static string ArrayToTOML<T>(T[] array, System.Globalization.CultureInfo culture = null)
        {
            if (culture == null)
                culture = System.Globalization.CultureInfo.InvariantCulture;

            if (array == null || array.Length == 0)
                return "[]";

            var result = "[";
            for (int i = 0; i < array.Length; i++)
            {
                if (typeof(T) == typeof(float))
                    result += ((float)(object)array[i]).ToString("F3", culture);
                else if (typeof(T) == typeof(int))
                    result += array[i];
                else
                    result += $"\"{array[i]}\"";

                if (i < array.Length - 1)
                    result += ", ";
            }
            result += "]";
            return result;
        }
    }
}
