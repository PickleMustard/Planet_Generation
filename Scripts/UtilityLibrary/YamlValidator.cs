using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YamlDotNet.RepresentationModel;

namespace UtilityLibrary
{
    public static class YamlValidator
    {
        public static ValidationResult ValidateCelestialBodyTemplate(string filePath)
        {
            var result = new ValidationResult { FilePath = filePath };

            if (!Godot.FileAccess.FileExists(filePath))
            {
                result.AddError("File does not exist");
                return result;
            }

            try
            {
                using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
                string text = f.GetAsText();

                var parseResult = ValidateYamlSyntax(text);
                if (!parseResult.IsValid)
                {
                    result.Errors.AddRange(parseResult.Errors);
                    return result;
                }

                var yaml = new YamlStream();
                yaml.Load(new StringReader(text));
                var root = (YamlMappingNode)yaml.Documents[0].RootNode;

                ValidateCelestialBodyStructure(root, result);
            }
            catch (Exception e)
            {
                result.AddError($"Validation exception: {e.Message}");
            }

            return result;
        }

        public static ValidationResult ValidateSystemTemplate(string filePath)
        {
            var result = new ValidationResult { FilePath = filePath };

            if (!Godot.FileAccess.FileExists(filePath))
            {
                result.AddError("File does not exist");
                return result;
            }

            try
            {
                using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
                string text = f.GetAsText();

                var parseResult = ValidateYamlSyntax(text);
                if (!parseResult.IsValid)
                {
                    result.Errors.AddRange(parseResult.Errors);
                    return result;
                }

                var yaml = new YamlStream();
                yaml.Load(new StringReader(text));
                var root = (YamlMappingNode)yaml.Documents[0].RootNode;

                ValidateSystemTemplateStructure(root, result);
            }
            catch (Exception e)
            {
                result.AddError($"Validation exception: {e.Message}");
            }

            return result;
        }

        public static ValidationResult ValidateResourceDefinition(string filePath)
        {
            var result = new ValidationResult { FilePath = filePath };

            if (!Godot.FileAccess.FileExists(filePath))
            {
                result.AddError("File does not exist");
                return result;
            }

            try
            {
                using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
                string text = f.GetAsText();

                var parseResult = ValidateYamlSyntax(text);
                if (!parseResult.IsValid)
                {
                    result.Errors.AddRange(parseResult.Errors);
                    return result;
                }

                var yaml = new YamlStream();
                yaml.Load(new StringReader(text));
                var root = (YamlMappingNode)yaml.Documents[0].RootNode;

                ValidateResourceDefinitionStructure(root, result);
            }
            catch (Exception e)
            {
                result.AddError($"Validation exception: {e.Message}");
            }

            return result;
        }

        private static ValidationResult ValidateYamlSyntax(string yamlText)
        {
            var result = new ValidationResult();

            try
            {
                var yaml = new YamlStream();
                yaml.Load(new StringReader(yamlText));
            }
            catch (YamlDotNet.Core.YamlException e)
            {
                result.AddError($"YAML syntax error at line {e.Start.Line}, column {e.Start.Column}: {e.Message}");
            }
            catch (Exception e)
            {
                result.AddError($"Parse error: {e.Message}");
            }

            return result;
        }

        private static void ValidateCelestialBodyStructure(YamlMappingNode root, ValidationResult result)
        {
            if (root.Children.ContainsKey("celestial"))
            {
                var celestial = root.Children["celestial"] as YamlMappingNode;
                if (celestial == null)
                {
                    result.AddError("'celestial' must be a mapping");
                    return;
                }

                ValidateTemplateSection(celestial, "celestial", result);
                ValidateMeshSection(celestial, "celestial", result);

                // Validate orbital parameters or position/velocity in template
                if (celestial.Children.ContainsKey("template"))
                {
                    var template = celestial.Children["template"] as YamlMappingNode;
                    if (template != null)
                    {
                        bool hasOrbitalParams = template.Children.ContainsKey("apogee");
                        bool hasPosition = template.Children.ContainsKey("position");

                        if (hasOrbitalParams)
                        {
                            ValidateOrbitalParameters(template, "celestial.template", result);
                        }
                        else if (!hasPosition)
                        {
                            result.AddWarning("'celestial.template' has neither orbital parameters (apogee/perigee) nor position/velocity");
                        }
                    }
                }
            }
            else if (root.Children.ContainsKey("satellite"))
            {
                var satellite = root.Children["satellite"] as YamlMappingNode;
                if (satellite == null)
                {
                    result.AddError("'satellite' must be a mapping");
                    return;
                }

                ValidateTemplateSection(satellite, "satellite", result);
                ValidateMeshSection(satellite, "satellite", result);

                // Validate orbital parameters in satellite template
                if (satellite.Children.ContainsKey("template"))
                {
                    var template = satellite.Children["template"] as YamlMappingNode;
                    if (template != null && template.Children.ContainsKey("apogee"))
                    {
                        ValidateOrbitalParameters(template, "satellite.template", result);
                    }
                }
            }
            else if (root.Children.ContainsKey("satellite_group"))
            {
                var satGroup = root.Children["satellite_group"] as YamlMappingNode;
                if (satGroup == null)
                {
                    result.AddError("'satellite_group' must be a mapping");
                    return;
                }

                ValidateTemplateSection(satGroup, "satellite_group", result);
            }

            if (root.Children.ContainsKey("categories"))
            {
                ValidateCategoriesSection(root, result);
            }
        }

        private static void ValidateSystemTemplateStructure(YamlMappingNode root, ValidationResult result)
        {
            if (!root.Children.ContainsKey("bodies"))
            {
                result.AddError("Missing required key: 'bodies'");
                return;
            }

            var bodies = root.Children["bodies"] as YamlSequenceNode;
            if (bodies == null)
            {
                result.AddError("'bodies' must be a sequence");
                return;
            }

            int bodyIndex = 0;
            foreach (var bodyNode in bodies.Children)
            {
                var body = bodyNode as YamlMappingNode;
                if (body == null)
                {
                    result.AddError($"Body at index {bodyIndex} must be a mapping");
                    bodyIndex++;
                    continue;
                }

                if (!body.Children.ContainsKey("type"))
                {
                    result.AddWarning($"Body at index {bodyIndex} missing 'type' field");
                }
                else
                {
                    var typeNode = body.Children["type"] as YamlScalarNode;
                    string typeStr = typeNode?.Value ?? "";
                    bool isDominant = typeStr.Equals("Star", StringComparison.OrdinalIgnoreCase)
                        || typeStr.Equals("BlackHole", StringComparison.OrdinalIgnoreCase);

                    if (isDominant)
                    {
                        if (!body.Children.ContainsKey("position"))
                        {
                            result.AddWarning($"Dominant body at index {bodyIndex} ({typeStr}) missing 'position'");
                        }
                    }
                    else
                    {
                        if (!body.Children.ContainsKey("apogee"))
                        {
                            result.AddWarning($"Non-dominant body at index {bodyIndex} ({typeStr}) missing orbital parameters (apogee/perigee)");
                        }
                        else
                        {
                            ValidateOrbitalParameters(body, $"bodies[{bodyIndex}]", result);
                        }
                    }
                }

                bodyIndex++;
            }
        }

        private static void ValidateResourceDefinitionStructure(YamlMappingNode root, ValidationResult result)
        {
            if (!root.Children.ContainsKey("resources"))
            {
                result.AddError("Missing required key: 'resources'");
                return;
            }

            var resources = root.Children["resources"] as YamlSequenceNode;
            if (resources == null)
            {
                result.AddError("'resources' must be a sequence");
                return;
            }

            int resourceIndex = 0;
            foreach (var resourceNode in resources.Children)
            {
                var resource = resourceNode as YamlMappingNode;
                if (resource == null)
                {
                    result.AddError($"Resource at index {resourceIndex} must be a mapping");
                    resourceIndex++;
                    continue;
                }

                var requiredFields = new[] { "id_name", "resource_tier", "resource_type" };
                foreach (var field in requiredFields)
                {
                    if (!resource.Children.ContainsKey(field))
                    {
                        result.AddError($"Resource at index {resourceIndex} missing required field: '{field}'");
                    }
                }

                resourceIndex++;
            }
        }

        private static void ValidateTemplateSection(YamlMappingNode parent, string parentName, ValidationResult result)
        {
            if (!parent.Children.ContainsKey("template"))
            {
                result.AddError($"Missing required section: '{parentName}.template'");
                return;
            }

            var template = parent.Children["template"] as YamlMappingNode;
            if (template == null)
            {
                result.AddError($"'{parentName}.template' must be a mapping");
            }
        }

        private static void ValidateMeshSection(YamlMappingNode parent, string parentName, ValidationResult result)
        {
            if (!parent.Children.ContainsKey("mesh"))
                return;

            var mesh = parent.Children["mesh"] as YamlMappingNode;
            if (mesh == null)
            {
                result.AddError($"'{parentName}.mesh' must be a mapping");
                return;
            }

            if (mesh.Children.ContainsKey("base_mesh"))
            {
                var baseMesh = mesh.Children["base_mesh"] as YamlMappingNode;
                if (baseMesh == null)
                {
                    result.AddError($"'{parentName}.mesh.base_mesh' must be a mapping");
                }
            }

            if (mesh.Children.ContainsKey("tectonic"))
            {
                var tectonic = mesh.Children["tectonic"] as YamlMappingNode;
                if (tectonic == null)
                {
                    result.AddError($"'{parentName}.mesh.tectonic' must be a mapping");
                }
            }
        }

        private static void ValidateOrbitalParameters(YamlMappingNode node, string path, ValidationResult result)
        {
            var optionalFields = new[] { "apogee", "perigee", "starting_angle", "vertical_offset" };
            foreach (var field in optionalFields)
            {
                if (!node.Children.ContainsKey(field))
                {
                    result.AddWarning($"'{path}' missing orbital parameter '{field}'");
                }
            }
        }

        private static void ValidateCategoriesSection(YamlMappingNode root, ValidationResult result)
        {
            if (!root.Children.ContainsKey("categories"))
            {
                return;
            }

            var categories = root.Children["categories"] as YamlMappingNode;
            if (categories == null)
            {
                result.AddError("'categories' must be a mapping");
                return;
            }

            // Check if potential key exists, but don't require it
            if (categories.Children.TryGetValue(new YamlScalarNode("potential"), out var potentialNode))
            {
                // Potential is present - this is optional, names will be loaded from separate files
                result.AddInfo("Categories section contains 'potential' list - names will be loaded from name files");
            }
            else
            {
                // Potential is missing - this is optional, but log it for clarity
                result.AddInfo("'categories.potential' is missing - names will be loaded from name files");
            }
        }

        public static List<ValidationResult> ValidateAllConfigurations()
        {
            var results = new List<ValidationResult>();

            var systemGenFiles = new[]
            {
                "Star", "RockyPlanet", "GasGiant", "IceGiant", "DwarfPlanet",
                "Moon", "Asteroid", "Comet", "AsteroidBelt", "BlackHole"
            };

            foreach (var name in systemGenFiles)
            {
                var path = $"res://Configuration/SystemGen/{name}.yaml";
                results.Add(ValidateCelestialBodyTemplate(path));
            }

            var systemTemplatePath = "res://Configuration/SystemTemplate/";
            if (DirAccess.DirExistsAbsolute(systemTemplatePath))
            {
                var files = DirAccess.GetFilesAt(systemTemplatePath);
                foreach (var file in files)
                {
                    if (file.EndsWith(".yaml"))
                    {
                        results.Add(ValidateSystemTemplate(systemTemplatePath + file));
                    }
                }
            }

            results.Add(ValidateResourceDefinition(
                "res://Configuration/ResourceDefinition/ResourceDefinition.yaml"));

            return results;
        }
    }

    public class ValidationResult
    {
        public string? FilePath { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        public bool IsValid => Errors.Count == 0;

        public void AddError(string message)
        {
            Errors.Add(message);
        }

        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }

        public void AddInfo(string message)
        {
            Warnings.Add($"[INFO] {message}");
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Validation: {FilePath}");
            sb.AppendLine($"Status: {(IsValid ? "VALID" : "INVALID")}");

            if (Errors.Count > 0)
            {
                sb.AppendLine("Errors:");
                foreach (var error in Errors)
                    sb.AppendLine($"  - {error}");
            }

            if (Warnings.Count > 0)
            {
                sb.AppendLine("Warnings:");
                foreach (var warning in Warnings)
                    sb.AppendLine($"  - {warning}");
            }

            return sb.ToString();
        }
    }
}
