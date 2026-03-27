using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YamlDotNet.RepresentationModel;

namespace UtilityLibrary.DataLoading;

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

    public static ValidationResult ValidateBuildingDefinition(string filePath)
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

            ValidateBuildingDefinitionStructure(root, result);
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
            result.AddError(
                $"YAML syntax error at line {e.Start.Line}, column {e.Start.Column}: {e.Message}"
            );
        }
        catch (Exception e)
        {
            result.AddError($"Parse error: {e.Message}");
        }

        return result;
    }

    private static void ValidateCelestialBodyStructure(
        YamlMappingNode root,
        ValidationResult result
    )
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
                        result.AddWarning(
                            "'celestial.template' has neither orbital parameters (apogee/perigee) nor position/velocity"
                        );
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

    private static void ValidateSystemTemplateStructure(
        YamlMappingNode root,
        ValidationResult result
    )
    {
        bool hasAnySection =
            root.Children.ContainsKey("dominant")
            || root.Children.ContainsKey("belts")
            || root.Children.ContainsKey("planetary");

        if (!hasAnySection)
        {
            result.AddError(
                "System template must have at least one of: 'dominant', 'belts', 'planetary'"
            );
            return;
        }

        // Validate dominant section
        if (root.Children.ContainsKey("dominant"))
        {
            var dominant = root.Children["dominant"] as YamlSequenceNode;
            if (dominant == null)
            {
                result.AddError("'dominant' must be a sequence");
            }
            else
            {
                int idx = 0;
                foreach (var bodyNode in dominant.Children)
                {
                    var body = bodyNode as YamlMappingNode;
                    if (body == null)
                    {
                        result.AddError($"Dominant body at index {idx} must be a mapping");
                    }
                    else if (!body.Children.ContainsKey("type"))
                    {
                        result.AddWarning($"Dominant body at index {idx} missing 'type' field");
                    }
                    idx++;
                }
            }
        }

        // Validate belts section
        if (root.Children.ContainsKey("belts"))
        {
            var belts = root.Children["belts"] as YamlSequenceNode;
            if (belts == null)
            {
                result.AddError("'belts' must be a sequence");
            }
            else
            {
                int idx = 0;
                foreach (var beltNode in belts.Children)
                {
                    var belt = beltNode as YamlMappingNode;
                    if (belt == null)
                    {
                        result.AddError($"Belt at index {idx} must be a mapping");
                    }
                    else if (!belt.Children.ContainsKey("type"))
                    {
                        result.AddWarning($"Belt at index {idx} missing 'type' field");
                    }
                    idx++;
                }
            }
        }

        // Validate planetary section
        if (root.Children.ContainsKey("planetary"))
        {
            var planetary = root.Children["planetary"] as YamlSequenceNode;
            if (planetary == null)
            {
                result.AddError("'planetary' must be a sequence");
            }
            else
            {
                int idx = 0;
                foreach (var bodyNode in planetary.Children)
                {
                    var body = bodyNode as YamlMappingNode;
                    if (body == null)
                    {
                        result.AddError($"Planetary body at index {idx} must be a mapping");
                    }
                    else
                    {
                        if (!body.Children.ContainsKey("type"))
                            result.AddWarning(
                                $"Planetary body at index {idx} missing 'type' field"
                            );
                        if (!body.Children.ContainsKey("orbital_parameters"))
                            result.AddWarning(
                                $"Planetary body at index {idx} missing 'orbital_parameters'"
                            );
                    }
                    idx++;
                }
            }
        }
    }

    private static void ValidateResourceDefinitionStructure(
        YamlMappingNode root,
        ValidationResult result
    )
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
                    result.AddError(
                        $"Resource at index {resourceIndex} missing required field: '{field}'"
                    );
                }
            }

            resourceIndex++;
        }
    }

    private static void ValidateBuildingDefinitionStructure(
        YamlMappingNode root,
        ValidationResult result
    )
    {
        if (!root.Children.ContainsKey("buildings"))
        {
            result.AddError("Missing required key: 'buildings'");
            return;
        }

        var buildings = root.Children["buildings"] as YamlSequenceNode;
        if (buildings == null)
        {
            result.AddError("'buildings' must be a sequence");
            return;
        }

        int buildingIndex = 0;
        foreach (var buildingNode in buildings.Children)
        {
            var building = buildingNode as YamlMappingNode;
            if (building == null)
            {
                result.AddError($"Building at index {buildingIndex} must be a mapping");
                buildingIndex++;
                continue;
            }

            var requiredFields = new[] { "id_name", "display_name", "category" };
            foreach (var field in requiredFields)
            {
                if (!building.Children.ContainsKey(field))
                {
                    result.AddError(
                        $"Building at index {buildingIndex} missing required field: '{field}'"
                    );
                }
            }

            // Validate placement_requirements structure if present
            if (building.Children.ContainsKey("placement_requirements"))
            {
                var placement = building.Children["placement_requirements"] as YamlMappingNode;
                if (placement == null)
                {
                    result.AddError(
                        $"Building at index {buildingIndex}: 'placement_requirements' must be a mapping"
                    );
                }
                else
                {
                    // Validate elevation ranges if present
                    if (placement.Children.ContainsKey("min_elevation"))
                    {
                        var minElev = placement.Children["min_elevation"];
                        if (!IsNumericNode(minElev))
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'placement_requirements.min_elevation' must be numeric"
                            );
                        }
                    }

                    if (placement.Children.ContainsKey("max_elevation"))
                    {
                        var maxElev = placement.Children["max_elevation"];
                        if (!IsNumericNode(maxElev))
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'placement_requirements.max_elevation' must be numeric"
                            );
                        }
                    }

                    if (placement.Children.ContainsKey("max_slope"))
                    {
                        var maxSlope = placement.Children["max_slope"];
                        if (!IsNumericNode(maxSlope))
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'placement_requirements.max_slope' must be numeric"
                            );
                        }
                    }

                    if (placement.Children.ContainsKey("cell_count"))
                    {
                        var cellCount = placement.Children["cell_count"];
                        if (!IsIntegerNode(cellCount))
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'placement_requirements.cell_count' must be integer"
                            );
                        }
                    }
                }
            }

            // Validate required_resources structure if present
            if (building.Children.ContainsKey("required_resources"))
            {
                var resources = building.Children["required_resources"] as YamlMappingNode;
                if (resources == null)
                {
                    result.AddError(
                        $"Building at index {buildingIndex}: 'required_resources' must be a mapping"
                    );
                }
                else
                {
                    foreach (var resourceEntry in resources.Children)
                    {
                        var resourceValue = resourceEntry.Value;
                        if (!IsIntegerNode(resourceValue))
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: Resource '{resourceEntry.Key}' must have integer quantity"
                            );
                        }
                    }
                }
            }

            buildingIndex++;
        }
    }

    private static bool IsNumericNode(YamlNode node)
    {
        if (node is YamlScalarNode scalar)
        {
            var value = scalar.Value;
            return float.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out _
            );
        }
        return false;
    }

    private static bool IsIntegerNode(YamlNode node)
    {
        if (node is YamlScalarNode scalar)
        {
            var value = scalar.Value;
            return int.TryParse(
                value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out _
            );
        }
        return false;
    }

    private static void ValidateTemplateSection(
        YamlMappingNode parent,
        string parentName,
        ValidationResult result
    )
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

    private static void ValidateMeshSection(
        YamlMappingNode parent,
        string parentName,
        ValidationResult result
    )
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

    private static void ValidateOrbitalParameters(
        YamlMappingNode node,
        string path,
        ValidationResult result
    )
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
            result.AddInfo(
                "Categories section contains 'potential' list - names will be loaded from name files"
            );
        }
        else
        {
            // Potential is missing - this is optional, but log it for clarity
            result.AddInfo(
                "'categories.potential' is missing - names will be loaded from name files"
            );
        }
    }

    public static List<ValidationResult> ValidateAllConfigurations()
    {
        var results = new List<ValidationResult>();

        var systemGenFiles = new[]
        {
            "Star",
            "RockyPlanet",
            "GasGiant",
            "IceGiant",
            "DwarfPlanet",
            "Moon",
            "Asteroid",
            "Comet",
            "AsteroidBelt",
            "BlackHole",
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

        results.Add(
            ValidateResourceDefinition(
                "res://Configuration/ResourceDefinition/ResourceDefinition.yaml"
            )
        );

        // Validate building definitions if directory exists
        var buildingsPath = "res://Configuration/Buildings/";
        if (DirAccess.DirExistsAbsolute(buildingsPath))
        {
            var buildingFiles = GetYamlFilesRecursive(buildingsPath);
            foreach (var file in buildingFiles)
            {
                results.Add(ValidateBuildingDefinition(file));
            }
        }

        return results;
    }

    private static List<string> GetYamlFilesRecursive(string directory)
    {
        var files = new List<string>();

        if (!DirAccess.DirExistsAbsolute(directory))
            return files;

        // Get files in current directory
        var currentFiles = DirAccess.GetFilesAt(directory);
        foreach (var file in currentFiles)
        {
            if (file.EndsWith(".yaml"))
            {
                files.Add(directory + file);
            }
        }

        // Get subdirectories
        var subdirs = DirAccess.GetDirectoriesAt(directory);
        foreach (var subdir in subdirs)
        {
            files.AddRange(GetYamlFilesRecursive(directory + subdir + "/"));
        }

        return files;
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
