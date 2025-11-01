using System;
using System.Globalization;
using System.IO;
using Godot;
using Godot.Collections;
using PlanetGeneration;
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
                                bodyTable.HasKey("satellites")
                                && bodyTable["satellites"] is TomlArray satellitesArray
                            )
                            {
                                GD.Print($"Sattlies List: {satellitesArray}");
                                foreach (var satelliteNode in satellitesArray.Children)
                                {
                                    if (satelliteNode is TomlTable satelliteTable)
                                    {
                                        var satelliteDict = new Godot.Collections.Dictionary();
                                        satelliteDict["Type"] = ReadString(
                                            satelliteTable,
                                            "type",
                                            "Moon"
                                        );
                                        satelliteDict["Position"] = ReadVector3(
                                            satelliteTable,
                                            "position",
                                            Vector3.Zero
                                        );
                                        satelliteDict["Velocity"] = ReadVector3(
                                            satelliteTable,
                                            "velocity",
                                            Vector3.Zero
                                        );
                                        satelliteDict["Size"] = ReadFloat(
                                            satelliteTable,
                                            "size",
                                            1f
                                        );

                                        var satelliteMeshDict = new Godot.Collections.Dictionary();
                                        if (
                                            satelliteTable.HasKey("mesh")
                                            && satelliteTable["mesh"]
                                                is TomlTable satelliteMeshTable
                                        )
                                        {
                                            if (
                                                satelliteMeshTable.HasKey("base_mesh")
                                                && satelliteMeshTable["base_mesh"]
                                                    is TomlTable baseMeshTable
                                            )
                                            {
                                                var baseMesh = new Godot.Collections.Dictionary();
                                                baseMesh["subdivisions"] = ReadInt(
                                                    baseMeshTable,
                                                    "subdivisions",
                                                    1
                                                );
                                                baseMesh["num_abberations"] = ReadInt(
                                                    baseMeshTable,
                                                    "num_abberations",
                                                    3
                                                );
                                                baseMesh["num_deformation_cycles"] = ReadInt(
                                                    baseMeshTable,
                                                    "num_deformation_cycles",
                                                    3
                                                );
                                                baseMesh["vertices_per_edge"] = ReadIntArray(
                                                    baseMeshTable,
                                                    "vertices_per_edge",
                                                    new int[] { 2 }
                                                );
                                                satelliteMeshDict["base_mesh"] = baseMesh;
                                            }
                                            if (
                                                satelliteMeshTable.HasKey("scaling")
                                                && satelliteMeshTable["scaling"]
                                                    is TomlTable scalingTable
                                            )
                                            {
                                                var scaling = new Godot.Collections.Dictionary();
                                                var xScaleRange = ReadFloatRange(
                                                    scalingTable,
                                                    "scaling_range_x",
                                                    (1.0f, 1.2f)
                                                );
                                                Godot.Collections.Array<float> xScaleArray =
                                                    new Godot.Collections.Array<float>
                                                    {
                                                        xScaleRange.Item1,
                                                        xScaleRange.Item2,
                                                    };
                                                scaling["scaling_range_x"] = xScaleArray;
                                                var yScaleRange = ReadFloatRange(
                                                    scalingTable,
                                                    "scaling_range_y",
                                                    (1.0f, 1.2f)
                                                );
                                                Godot.Collections.Array<float> yScaleArray =
                                                    new Godot.Collections.Array<float>
                                                    {
                                                        yScaleRange.Item1,
                                                        yScaleRange.Item2,
                                                    };
                                                scaling["scaling_range_y"] = yScaleArray;
                                                var zScaleRange = ReadFloatRange(
                                                    scalingTable,
                                                    "scaling_range_z",
                                                    (1.0f, 1.2f)
                                                );
                                                Godot.Collections.Array<float> zScaleArray =
                                                    new Godot.Collections.Array<float>
                                                    {
                                                        zScaleRange.Item1,
                                                        zScaleRange.Item2,
                                                    };
                                                scaling["scaling_range_z"] = zScaleArray;
                                                satelliteMeshDict["scaling"] = scaling;
                                            }
                                            if (
                                                satelliteMeshTable.HasKey("noise_settings")
                                                && satelliteMeshTable["noise_settings"]
                                                    is TomlTable noiseTable
                                            )
                                            {
                                                var noise = new Godot.Collections.Dictionary();
                                                var amplitudeRange = ReadFloatRange(
                                                    noiseTable,
                                                    "amplitude_range",
                                                    (1.0f, 1.2f)
                                                );
                                                Godot.Collections.Array<float> amplitudeArray =
                                                    new Godot.Collections.Array<float>
                                                    {
                                                        amplitudeRange.Item1,
                                                        amplitudeRange.Item2,
                                                    };
                                                noise["amplitude_range"] = amplitudeArray;
                                                var scalingRange = ReadFloatRange(
                                                    noiseTable,
                                                    "scaling_range",
                                                    (1.0f, 1.2f)
                                                );
                                                Godot.Collections.Array<float> scalingArray =
                                                    new Godot.Collections.Array<float>
                                                    {
                                                        scalingRange.Item1,
                                                        scalingRange.Item2,
                                                    };
                                                noise["scaling_range"] = scalingArray;
                                                var octaveRange = ReadIntRange(
                                                    noiseTable,
                                                    "octave_range",
                                                    (1, 2)
                                                );
                                                Godot.Collections.Array<int> octaveArray =
                                                    new Godot.Collections.Array<int>
                                                    {
                                                        octaveRange.Item1,
                                                        octaveRange.Item2,
                                                    };
                                                noise["octave_range"] = octaveArray;
                                                satelliteMeshDict["noise_settings"] = noise;
                                            }
                                        }
                                        satelliteDict["Mesh"] = satelliteMeshDict;

                                        // Handle satellite group template if present (e.g., for AsteroidBelt)
                                        if (
                                            satelliteTable.HasKey("template")
                                            && satelliteTable["template"] is TomlTable satTemplate
                                        )
                                        {
                                            var templateDict = new Godot.Collections.Dictionary();
                                            var numberAsteroidsRange = ReadIntRange(
                                                satTemplate,
                                                "number_asteroids",
                                                (10, 20)
                                            );
                                            Godot.Collections.Array<int> numberAsteroidsArray =
                                                new Godot.Collections.Array<int>
                                                {
                                                    numberAsteroidsRange.Item1,
                                                    numberAsteroidsRange.Item2,
                                                };
                                            templateDict["number_asteroids"] = numberAsteroidsArray;
                                            templateDict["grouping"] = ReadStringArray(
                                                satTemplate,
                                                "grouping",
                                                new string[] { "balanced" }
                                            );
                                            templateDict["ring_velocity"] = ReadVector3(
                                                satTemplate,
                                                "ring_velocity",
                                                Vector3.Zero
                                            );
                                            var sizeRange = ReadFloatRange(
                                                satTemplate,
                                                "size_range",
                                                (0.5f, 2.0f)
                                            );
                                            Godot.Collections.Array<float> sizeArray =
                                                new Godot.Collections.Array<float>
                                                {
                                                    sizeRange.Item1,
                                                    sizeRange.Item2,
                                                };
                                            templateDict["size_range"] = sizeArray;
                                            templateDict["possible_subtypes"] = ReadStringArray(
                                                satTemplate,
                                                "possible_subtypes",
                                                new string[] { "default" }
                                            );
                                            satelliteDict["Template"] = templateDict;
                                        }

                                        satellites.Add(satelliteDict);
                                    }
                                }
                            }
                            bodyDict["Satellites"] = satellites;

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
                CelestialBodyType.RockyPlanet => "Rocky Planet",
                CelestialBodyType.GasGiant => "Gas Giant",
                CelestialBodyType.IceGiant => "Ice Giant",
                CelestialBodyType.DwarfPlanet => "Dwarf Planet",
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
                SatelliteBodyType.DwarfPlanet => "Dwarf Planet",
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
                SatelliteGroupTypes.AsteroidBelt => "Asteroid Belt",
                SatelliteGroupTypes.Comet => "Comet",
                SatelliteGroupTypes.IceBelt => "Ice Belt",
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
            if (resPath.Contains("Asteroid Belt"))
                return "satellite_group";
            if (
                isSatellite
                && (
                    resPath.Contains("Asteroid")
                    || resPath.Contains("Moon")
                    || resPath.Contains("Comet")
                    || resPath.Contains("Dwarf Planet")
                )
            )
                return "satellite";
            if (
                resPath.Contains("Star")
                || resPath.Contains("Rocky Planet")
                || resPath.Contains("Gas Giant")
                || resPath.Contains("Ice Giant")
                || resPath.Contains("Dwarf Planet")
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
                var ringApogee = ReadFloat(tmpl, "apogee", 0f);
                var ringPerigee = ReadFloat(tmpl, "perigee", 0f);
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
    }
}
