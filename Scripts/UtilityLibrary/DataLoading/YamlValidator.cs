using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Structures.Resources;
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
        catch (Exception ex)
        {
            result.AddError($"Validation failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Validates a resource definition category file.
    /// Category files have resource type inferred from filename, so resource_type field should not be present.
    /// </summary>
    public static ValidationResult ValidateResourceDefinitionCategory(string filePath)
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

            ValidateResourceDefinitionCategoryStructure(root, result);
        }
        catch (Exception ex)
        {
            result.AddError($"Validation failed: {ex.Message}");
        }

        return result;
    }

    private static void ValidateResourceDefinitionCategoryStructure(
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

            // Check for required fields
            if (!resource.Children.ContainsKey("id_name"))
            {
                result.AddError(
                    $"Resource at index {resourceIndex} missing required field: 'id_name'"
                );
            }

            if (!resource.Children.ContainsKey("resource_tier"))
            {
                result.AddError(
                    $"Resource at index {resourceIndex} missing required field: 'resource_tier'"
                );
            }

            // resource_type should NOT be present in category files
            if (resource.Children.ContainsKey("resource_type"))
            {
                result.AddError(
                    $"Resource at index {resourceIndex} contains 'resource_type' field. " +
                    "In category-based format, resource type should be inferred from file name."
                );
            }

            // Validate icon section if present
            if (resource.Children.ContainsKey("icon"))
            {
                ValidateIconSection(resource.Children["icon"], $"Resource at index {resourceIndex}", result);
            }

            resourceIndex++;
        }
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
            || root.Children.ContainsKey("planetary")
            || root.Children.ContainsKey("satellites");

        if (!hasAnySection)
        {
            result.AddError(
                "System template must have at least one of: 'dominant', 'belts', 'planetary', 'satellites'"
            );
            return;
        }

        // Collect known body names (dominant + planetary + satellites) so the satellites section
        // can verify each entry's `parent` references a real body (or the system barycenter).
        var knownBodyNames = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal)
        {
            "barycenter",
        };
        foreach (var section in new[] { "dominant", "planetary", "satellites" })
        {
            if (root.Children.TryGetValue(section, out var secNode)
                && secNode is YamlSequenceNode secSeq)
            {
                foreach (var entry in secSeq.Children)
                {
                    if (entry is YamlMappingNode map
                        && map.Children.TryGetValue("name", out var nameNode)
                        && nameNode is YamlScalarNode scalar
                        && !string.IsNullOrEmpty(scalar.Value))
                    {
                        knownBodyNames.Add(scalar.Value);
                    }
                }
            }
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
                    else
                    {
                        if (!body.Children.ContainsKey("type"))
                            result.AddWarning($"Dominant body at index {idx} missing 'type' field");
                        RejectLegacyGenRangeKeys(body, $"dominant[{idx}]", result);
                        ValidateSubtypeSlot(body, $"dominant[{idx}]", result);
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
                    else
                    {
                        if (!belt.Children.ContainsKey("type"))
                            result.AddWarning($"Belt at index {idx} missing 'type' field");
                        RejectLegacyGenRangeKeys(belt, $"belts[{idx}]", result);
                        ValidateSubtypeSlot(belt, $"belts[{idx}]", result);
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
                        RejectLegacyGenRangeKeys(body, $"planetary[{idx}]", result);
                        ValidateSubtypeSlot(body, $"planetary[{idx}]", result);
                    }
                    idx++;
                }
            }
        }

        // Validate flattened satellites section
        if (root.Children.ContainsKey("satellites"))
        {
            var satellites = root.Children["satellites"] as YamlSequenceNode;
            if (satellites == null)
            {
                result.AddError("'satellites' must be a sequence");
            }
            else
            {
                int idx = 0;
                foreach (var satNode in satellites.Children)
                {
                    var sat = satNode as YamlMappingNode;
                    if (sat == null)
                    {
                        result.AddError($"Satellite at index {idx} must be a mapping");
                    }
                    else
                    {
                        if (!sat.Children.ContainsKey("type"))
                            result.AddWarning($"Satellite at index {idx} missing 'type' field");

                        if (!sat.Children.TryGetValue("parent", out var parentNode)
                            || parentNode is not YamlScalarNode parentScalar
                            || string.IsNullOrEmpty(parentScalar.Value))
                        {
                            result.AddError($"Satellite at index {idx} missing required 'parent' field");
                        }
                        else if (!knownBodyNames.Contains(parentScalar.Value))
                        {
                            result.AddError(
                                $"Satellite at index {idx} references unknown parent '{parentScalar.Value}'"
                            );
                        }

                        RejectLegacyGenRangeKeys(sat, $"satellites[{idx}]", result);
                        ValidateSubtypeSlot(sat, $"satellites[{idx}]", result);
                    }
                    idx++;
                }
            }
        }
    }

    /// <summary>
    /// Phase 6: per-body gen-range blocks (<c>base_mesh</c>, <c>tectonics</c>,
    /// <c>spherical_harmonics_settings</c>) have moved into <c>Configuration/Subtypes/*.yaml</c>.
    /// A SystemTemplate file containing them is from before the migration and must be
    /// rewritten by <c>SystemTemplateMigrator</c>.
    /// </summary>
    private static readonly string[] LegacyBodyGenRangeKeys =
    {
        "base_mesh",
        "tectonics",
        "spherical_harmonics_settings",
    };

    private static void RejectLegacyGenRangeKeys(
        YamlMappingNode body,
        string context,
        ValidationResult result
    )
    {
        foreach (var key in LegacyBodyGenRangeKeys)
        {
            if (body.Children.ContainsKey(key))
            {
                result.AddError(
                    $"{context}: legacy key '{key}' is no longer accepted. "
                    + "Per-body gen ranges have moved to `Configuration/Subtypes/*.yaml`. "
                    + "Run `SystemTemplateMigrator`."
                );
            }
        }
    }

    private static void ValidateSubtypeSlot(
        YamlMappingNode body,
        string context,
        ValidationResult result
    )
    {
        bool hasExplicit = body.Children.ContainsKey("subtype");
        bool hasWeights = body.Children.ContainsKey("subtype_weights");

        if (hasExplicit && hasWeights)
        {
            result.AddWarning(
                $"{context}: both 'subtype' and 'subtype_weights' provided — 'subtype' wins, weights ignored."
            );
        }

        if (hasWeights)
        {
            if (body.Children["subtype_weights"] is not YamlMappingNode weightsMap)
            {
                result.AddError($"{context}: 'subtype_weights' must be a mapping of subtype_id → weight.");
                return;
            }

            float total = 0f;
            foreach (var kvp in weightsMap.Children)
            {
                if (!IsNumericNode(kvp.Value))
                {
                    result.AddError(
                        $"{context}: 'subtype_weights[{kvp.Key}]' must be numeric"
                    );
                    continue;
                }

                if (kvp.Value is YamlScalarNode wScalar
                    && float.TryParse(
                        wScalar.Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var w))
                {
                    if (w < 0)
                        result.AddError(
                            $"{context}: 'subtype_weights[{kvp.Key}]' must be non-negative (got {w})"
                        );
                    total += w;
                }
            }

            if (Math.Abs(total - 1.0f) > 0.01f && total > 0f)
            {
                result.AddWarning(
                    $"{context}: subtype_weights sum to {total:0.###}, expected ~1.0"
                );
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

            // Check for required fields
            if (!resource.Children.ContainsKey("id_name"))
            {
                result.AddError(
                    $"Resource at index {resourceIndex} missing required field: 'id_name'"
                );
            }

            if (!resource.Children.ContainsKey("resource_tier"))
            {
                result.AddError(
                    $"Resource at index {resourceIndex} missing required field: 'resource_tier'"
                );
            }

            // resource_type is now inferred from file name, but we check if it's present
            // (it shouldn't be in the new category-based format)
            if (resource.Children.ContainsKey("resource_type"))
            {
                result.AddWarning(
                    $"Resource at index {resourceIndex} contains 'resource_type' field. " +
                    "In category-based format, resource type should be inferred from file name."
                );
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

            // Validate behaviors structure if present
            if (building.Children.ContainsKey("behaviors"))
            {
                var behaviorsNode = building.Children["behaviors"];
                if (behaviorsNode is YamlSequenceNode behaviorsSeq)
                {
                    int behaviorIndex = 0;
                    foreach (var entryNode in behaviorsSeq.Children)
                    {
                        if (entryNode is YamlScalarNode)
                        {
                            // Bare string — allowed but not preferred
                        }
                        else if (entryNode is YamlMappingNode entryMap)
                        {
                            if (!entryMap.Children.ContainsKey("behavior_id"))
                            {
                                result.AddError(
                                    $"Building at index {buildingIndex}: behavior entry at index {behaviorIndex} missing required 'behavior_id' key"
                                );
                            }
                            else
                            {
                                var idNode = entryMap.Children["behavior_id"];
                                if (idNode is YamlScalarNode idScalar)
                                {
                                    string behaviorId = idScalar.Value ?? "";
                                    if (!IsValidBehaviorId(behaviorId))
                                    {
                                        result.AddWarning(
                                            $"Building at index {buildingIndex}: behavior_id '{behaviorId}' may not be a valid behavior class name"
                                        );
                                    }
                                }
                            }
                        }
                        else
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: behavior entry at index {behaviorIndex} must be a string or mapping"
                            );
                        }
                        behaviorIndex++;
                    }
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

                    // Validate biomes field
                    if (placement.Children.ContainsKey("biomes"))
                    {
                        var biomesNode = placement.Children["biomes"];

                        if (biomesNode is YamlSequenceNode biomesSequence)
                        {
                            // Check for empty list - deprecated
                            if (biomesSequence.Children.Count == 0)
                            {
                                result.AddWarning(
                                    $"Building at index {buildingIndex}: Empty 'biomes' list is deprecated. " +
                                    "Use ['*'] to allow construction in any biome."
                                );
                            }
                            else
                            {
                                // Check for wildcard and other biomes/categories
                                bool hasWildcard = false;
                                var otherEntries = new List<string>();
                                var categoryNames = new List<string>();

                                foreach (var biomeNode in biomesSequence.Children)
                                {
                                    if (biomeNode is YamlScalarNode biomeScalar)
                                    {
                                        string biomeName = biomeScalar.Value ?? "";

                                        if (biomeName == "*")
                                        {
                                            hasWildcard = true;
                                        }
                                        else if (biomeName.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // Validate category reference
                                            string categoryName = biomeName.Substring(9).Trim();
                                            if (!IsValidBiomeCategory(categoryName))
                                            {
                                                result.AddError(
                                                    $"Building at index {buildingIndex}: Unknown biome category '{categoryName}'"
                                                );
                                            }
                                            categoryNames.Add(categoryName);
                                            otherEntries.Add(biomeName);
                                        }
                                        else
                                        {
                                            // Validate biome name against known types
                                            if (!IsValidBiomeName(biomeName))
                                            {
                                                result.AddError(
                                                    $"Building at index {buildingIndex}: Unknown biome type '{biomeName}'"
                                                );
                                            }
                                            otherEntries.Add(biomeName);
                                        }
                                    }
                                }

                                // Warn if wildcard is used with other biomes/categories
                                if (hasWildcard && otherEntries.Count > 0)
                                {
                                    result.AddWarning(
                                        $"Building at index {buildingIndex}: Wildcard '*' allows all biomes, " +
                                        $"but additional entries were also specified: {string.Join(", ", otherEntries)}. " +
                                        "These additional entries will be ignored."
                                    );
                                }
                            }
                        }
                        else if (biomesNode is YamlScalarNode biomesScalar)
                        {
                            // Handle null/empty scalar
                            if (
                                biomesScalar.Value == null
                                || biomesScalar.Value == ""
                                || biomesScalar.Value == "~"
                            )
                            {
                                result.AddWarning(
                                    $"Building at index {buildingIndex}: Null 'biomes' is deprecated. " +
                                    "Use ['*'] to allow construction in any biome."
                                );
                            }
                            else
                            {
                                result.AddError(
                                    $"Building at index {buildingIndex}: 'placement_requirements.biomes' must be a sequence"
                                );
                            }
                        }
                        else
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'placement_requirements.biomes' must be a sequence"
                            );
                        }
                    }

                    // Validate configurable_behavior if present
                    if (placement.Children.ContainsKey("configurable_behavior"))
                    {
                        var behaviorNode = placement.Children["configurable_behavior"];
                        if (behaviorNode is YamlScalarNode behaviorScalar)
                        {
                            // Legacy bare string format — deprecated
                            string? behaviorValue = behaviorScalar.Value;
                            if (!string.IsNullOrWhiteSpace(behaviorValue))
                            {
                                result.AddWarning(
                                    $"Building at index {buildingIndex}: " +
                                    "Bare string 'configurable_behavior' is deprecated. " +
                                    "Use mapping with 'behavior_class' key and inline config."
                                );

                                // Check if it's a file path or class name
                                if (behaviorValue.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!behaviorValue.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                                    {
                                        result.AddWarning(
                                            $"Building at index {buildingIndex}: " +
                                            $"'configurable_behavior' file path should end with .cs: '{behaviorValue}'"
                                        );
                                    }
                                }
                                else if (!IsValidBehaviorName(behaviorValue))
                                {
                                    result.AddWarning(
                                        $"Building at index {buildingIndex}: " +
                                        $"'configurable_behavior' value '{behaviorValue}' may not be a valid class name"
                                    );
                                }
                            }
                        }
                        else if (behaviorNode is YamlMappingNode behaviorMap)
                        {
                            // New inline config format
                            if (!behaviorMap.Children.ContainsKey("behavior_class"))
                            {
                                result.AddError(
                                    $"Building at index {buildingIndex}: " +
                                    "'configurable_behavior' mapping missing required 'behavior_class' key"
                                );
                            }
                            else
                            {
                                var classNode = behaviorMap.Children["behavior_class"];
                                if (classNode is YamlScalarNode classScalar)
                                {
                                    string className = classScalar.Value ?? "";
                                    if (!string.IsNullOrWhiteSpace(className) && !IsValidBehaviorName(className))
                                    {
                                        result.AddWarning(
                                            $"Building at index {buildingIndex}: " +
                                            $"'configurable_behavior.behavior_class' value '{className}' may not be a valid class name"
                                        );
                                    }
                                }
                            }
                        }
                        else
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'configurable_behavior' must be a string or mapping"
                            );
                        }
                    }
                }
            }

            // Validate building_limit if present
            if (building.Children.ContainsKey("building_limit"))
            {
                var buildingLimit = building.Children["building_limit"];
                if (!IsIntegerNode(buildingLimit))
                {
                    result.AddError(
                        $"Building at index {buildingIndex}: 'building_limit' must be integer"
                    );
                }
            }

            // Validate required_resources structure if present
            if (building.Children.ContainsKey("required_resources"))
            {
                var resourcesNode = building.Children["required_resources"];

                // Handle empty/null required_resources (YAML null scalar)
                if (
                    resourcesNode is YamlScalarNode scalar
                    && (scalar.Value == null || scalar.Value == "" || scalar.Value == "~")
                )
                {
                    // Valid: empty required_resources
                }
                else if (resourcesNode is YamlMappingNode resources)
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
                else
                {
                    result.AddError(
                        $"Building at index {buildingIndex}: 'required_resources' must be a mapping or empty"
                    );
                }
            }

            // Warn on deprecated top-level sections (config should be inline in behaviors:)
            var deprecatedSections = new[] { "production", "power", "extraction", "transfer_station", "storage_capacity", "slot_filters", "starting_stockpiles" };
            foreach (var section in deprecatedSections)
            {
                if (building.Children.ContainsKey(section))
                {
                    result.AddWarning(
                        $"Building at index {buildingIndex}: Top-level '{section}' section is deprecated. " +
                        $"Move config inline under 'behaviors:' entries."
                    );
                }
            }

            // Validate visual structure if present
            if (building.Children.ContainsKey("visual"))
            {
                var visualNode = building.Children["visual"];

                if (
                    visualNode is YamlScalarNode visScalar
                    && (visScalar.Value == null || visScalar.Value == "" || visScalar.Value == "~")
                )
                {
                    // Valid: empty visual
                }
                else if (visualNode is YamlMappingNode visualMap)
                {
                    if (visualMap.Children.ContainsKey("scale"))
                    {
                        if (!IsNumericNode(visualMap.Children["scale"]))
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'visual.scale' must be numeric"
                            );
                        }
                    }
                    if (visualMap.Children.ContainsKey("rotation_offset"))
                    {
                        if (visualMap.Children["rotation_offset"] is not YamlSequenceNode)
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'visual.rotation_offset' must be a sequence"
                            );
                        }
                    }
                    if (visualMap.Children.ContainsKey("shape_id"))
                    {
                        if (visualMap.Children["shape_id"] is not YamlScalarNode)
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'visual.shape_id' must be a string"
                            );
                        }
                    }
                    if (visualMap.Children.ContainsKey("shape"))
                    {
                        result.AddError(
                            $"Building at index {buildingIndex}: 'visual.shape' is deprecated. " +
                            "Use 'shape_id' referencing a BuildingShape2D in Configuration/Building2D/."
                        );
                    }
                    if (visualMap.Children.ContainsKey("shape_size"))
                    {
                        if (!IsNumericNode(visualMap.Children["shape_size"]))
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'visual.shape_size' must be numeric"
                            );
                        }
                    }
                    if (visualMap.Children.ContainsKey("shape_color"))
                    {
                        if (visualMap.Children["shape_color"] is not YamlSequenceNode)
                        {
                            result.AddError(
                                $"Building at index {buildingIndex}: 'visual.shape_color' must be a sequence"
                            );
                        }
                    }
                }
                else
                {
                    result.AddError(
                        $"Building at index {buildingIndex}: 'visual' must be a mapping or empty"
                    );
                }
            }

            // Validate sound structure if present
            if (building.Children.ContainsKey("sound"))
            {
                var soundNode = building.Children["sound"];

                if (
                    soundNode is YamlScalarNode sndScalar
                    && (sndScalar.Value == null || sndScalar.Value == "" || sndScalar.Value == "~")
                )
                {
                    // Valid: empty sound
                }
                else if (soundNode is YamlMappingNode soundMap)
                {
                    var knownSoundKeys = new HashSet<string>
                    {
                        "building",
                        "finished",
                        "idle",
                        "fabricating",
                    };
                    foreach (var entry in soundMap.Children)
                    {
                        string key = entry.Key.ToString();
                        if (!knownSoundKeys.Contains(key))
                        {
                            result.AddWarning(
                                $"Building at index {buildingIndex}: Unknown sound key '{key}'"
                            );
                        }
                    }
                }
                else
                {
                    result.AddError(
                        $"Building at index {buildingIndex}: 'sound' must be a mapping or empty"
                    );
                }
            }

            // Validate icon structure if present
            if (building.Children.ContainsKey("icon"))
            {
                ValidateIconSection(building.Children["icon"], $"Building at index {buildingIndex}", result);
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

    private static bool IsValidBiomeName(string name)
        => BiomeCategoryConfig.TryNormalizeBiomeId(name, out _);

    // Valid biome category IDs from biome_categories.yaml
    private static readonly HashSet<string> ValidBiomeCategories = new HashSet<string>(
        System.StringComparer.OrdinalIgnoreCase
    )
    {
        "flat",
        "mountain",
        "rocky",
        "arable",
        "temperate",
        "tropical",
        "desert",
        "ocean",
        "deep_ocean",
        "frozen",
        "terrestrial",
        "volcanic",
        "rusted",
        "scoured",
        "coastal",
        "forested",
    };

    private static bool IsValidBiomeCategory(string categoryName)
    {
        return !string.IsNullOrWhiteSpace(categoryName)
            && ValidBiomeCategories.Contains(categoryName.Trim());
    }

    private static bool IsValidBehaviorName(string name)
    {
        // Basic validation: alphanumeric with underscores, must start with letter
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Allow fully qualified names (Namespace.ClassName)
        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                return false;
        }

        return char.IsLetter(name[0]) || name[0] == '_';
    }

    private static readonly HashSet<string> KnownBehaviorIds = new()
    {
        "ManufacturingBehavior",
        "PowerProducerBehavior",
        "PowerConsumerBehavior",
        "BatteryBehavior",
        "ExtractionBehavior",
        "StorageHubBehavior",
        "TransferStationBehavior",
        "TransportHubBehavior",
        "InitialStockpileBehavior",
        "GameStartBehavior",
        "BulkStorageRoutingBehavior",
    };

    private static bool IsValidBehaviorId(string id)
    {
        return KnownBehaviorIds.Contains(id);
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

    public static ValidationResult ValidateAUProbability(string filePath)
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

            ValidateAUProbabilityStructure(root, result);
        }
        catch (Exception e)
        {
            result.AddError($"Validation exception: {e.Message}");
        }

        return result;
    }

    private static void ValidateAUProbabilityStructure(
        YamlMappingNode root,
        ValidationResult result
    )
    {
        // Validate required fields
        var requiredFields = new[] { "schema_version", "body_type" };
        foreach (var field in requiredFields)
        {
            if (!root.Children.ContainsKey(field))
            {
                result.AddError($"Missing required field: {field}");
            }
        }

        // Validate au_ranges if present (not required for satellite configs with parent_body_influence)
        bool hasAURanges = root.Children.ContainsKey("au_ranges");
        bool hasParentInfluence = root.Children.ContainsKey("parent_body_influence");

        if (!hasAURanges && !hasParentInfluence)
        {
            result.AddError("Must have either 'au_ranges' or 'parent_body_influence'");
            return;
        }

        if (hasAURanges)
        {
            var auRanges = root.Children[new YamlScalarNode("au_ranges")] as YamlSequenceNode;
            if (auRanges != null)
            {
                ValidateAURanges(auRanges, result);
            }
        }
    }

    private static void ValidateAURanges(YamlSequenceNode ranges, ValidationResult result)
    {
        float lastMax = 0;

        for (int i = 0; i < ranges.Children.Count; i++)
        {
            var range = ranges.Children[i] as YamlMappingNode;
            if (range == null)
            {
                result.AddError($"Range {i} is not a mapping node");
                continue;
            }

            // Validate range bounds
            if (range.Children.ContainsKey("range"))
            {
                var rangeBounds = range.Children[new YamlScalarNode("range")] as YamlMappingNode;
                if (rangeBounds != null)
                {
                    float minAu = GetFloatFromYaml(rangeBounds, "min_au", result, $"range[{i}].min_au");
                    float maxAu = GetFloatFromYaml(rangeBounds, "max_au", result, $"range[{i}].max_au");

                    if (minAu >= maxAu)
                    {
                        result.AddError($"Range {i}: min_au ({minAu}) must be less than max_au ({maxAu})");
                    }

                    if (i > 0 && minAu < lastMax)
                    {
                        result.AddWarning($"Range {i}: min_au ({minAu}) overlaps with previous range max ({lastMax})");
                    }

                    lastMax = maxAu;
                }
            }

            // Validate subtype distribution weights sum to ~1.0
            if (range.Children.ContainsKey("subtype_distribution"))
            {
                var distribution = range.Children[new YamlScalarNode("subtype_distribution")] as YamlSequenceNode;
                if (distribution != null)
                {
                    float totalWeight = 0;
                    foreach (var item in distribution)
                    {
                        var distEntry = item as YamlMappingNode;
                        if (distEntry != null && distEntry.Children.ContainsKey("weight"))
                        {
                            totalWeight += GetFloatFromYaml(distEntry, "weight", result, "weight");
                        }
                    }

                    if (Math.Abs(totalWeight - 1.0f) > 0.01f)
                    {
                        result.AddWarning($"Range {i}: subtype weights sum to {totalWeight}, expected ~1.0");
                    }
                }
            }
        }
    }

    private static float GetFloatFromYaml(
        YamlMappingNode node,
        string key,
        ValidationResult result,
        string context
    )
    {
        if (!node.Children.ContainsKey(key))
        {
            result.AddError($"Missing value for: {context}");
            return 0f;
        }

        try
        {
            return Convert.ToSingle(node.Children[new YamlScalarNode(key)].ToString());
        }
        catch
        {
            result.AddError($"Invalid float value for {context}: {node.Children[new YamlScalarNode(key)]}");
            return 0f;
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

        // Validate AU probability configurations
        var auProbabilityPath = "res://Configuration/AUProbability/";
        if (DirAccess.DirExistsAbsolute(auProbabilityPath))
        {
            var auFiles = DirAccess.GetFilesAt(auProbabilityPath);
            foreach (var file in auFiles)
            {
                if (file.EndsWith(".yaml"))
                {
                    results.Add(ValidateAUProbability(auProbabilityPath + file));
                }
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

    public static ValidationResult ValidateBuildingShape2D(string filePath)
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

            ValidateBuildingShape2DStructure(root, result);
        }
        catch (Exception e)
        {
            result.AddError($"Validation exception: {e.Message}");
        }

        return result;
    }

    private static void ValidateBuildingShape2DStructure(YamlMappingNode root, ValidationResult result)
    {
        if (!root.Children.ContainsKey("id"))
        {
            result.AddError("Missing required field: 'id'");
        }

        if (!root.Children.ContainsKey("vertices"))
        {
            result.AddError("Missing required field: 'vertices'");
            return;
        }

        if (root.Children["vertices"] is not YamlSequenceNode vertices)
        {
            result.AddError("'vertices' must be a sequence");
            return;
        }

        if (vertices.Children.Count < 3)
        {
            result.AddError($"'vertices' must contain at least 3 entries; got {vertices.Children.Count}");
            return;
        }

        int vIdx = 0;
        foreach (var vNode in vertices.Children)
        {
            if (vNode is not YamlSequenceNode pair || pair.Children.Count < 2)
            {
                result.AddError($"vertices[{vIdx}] must be [x, y]");
            }
            else
            {
                if (!IsNumericNode(pair.Children[0]) || !IsNumericNode(pair.Children[1]))
                    result.AddError($"vertices[{vIdx}] must contain numeric x and y");
            }
            vIdx++;
        }

        if (root.Children.ContainsKey("sides"))
        {
            if (root.Children["sides"] is not YamlSequenceNode sides)
            {
                result.AddError("'sides' must be a sequence");
                return;
            }

            if (sides.Children.Count != vertices.Children.Count)
            {
                result.AddError(
                    $"'sides' count ({sides.Children.Count}) must equal 'vertices' count ({vertices.Children.Count})"
                );
            }

            int sIdx = 0;
            foreach (var sideNode in sides.Children)
            {
                if (sideNode is YamlMappingNode sideMap && sideMap.Children.ContainsKey("slots"))
                {
                    if (sideMap.Children["slots"] is YamlSequenceNode slots)
                    {
                        int slotIdx = 0;
                        foreach (var slotNode in slots.Children)
                        {
                            if (slotNode is not YamlMappingNode slotMap)
                            {
                                result.AddError(
                                    $"sides[{sIdx}].slots[{slotIdx}] must be a mapping with 'kind' and 'state'"
                                );
                                slotIdx++;
                                continue;
                            }

                            if (!slotMap.Children.TryGetValue("kind", out var kindNode)
                                || kindNode is not YamlScalarNode kindScalar
                                || string.IsNullOrWhiteSpace(kindScalar.Value)
                                || !IsValidResourceNodeKind(kindScalar.Value))
                            {
                                result.AddError(
                                    $"sides[{sIdx}].slots[{slotIdx}].kind must be one of: import, export, flex"
                                );
                            }

                            if (!slotMap.Children.TryGetValue("state", out var stateNode)
                                || stateNode is not YamlScalarNode stateScalar
                                || string.IsNullOrWhiteSpace(stateScalar.Value)
                                || !IsValidStateOfMatter(stateScalar.Value))
                            {
                                result.AddError(
                                    $"sides[{sIdx}].slots[{slotIdx}].state must be one of: solid, fluid, liquid, gas"
                                );
                            }
                            slotIdx++;
                        }
                    }
                }
                sIdx++;
            }
        }
    }

    private static bool IsValidResourceNodeKind(string value)
    {
        return string.Equals(value, "import", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "export", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "flex", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidStateOfMatter(string value)
    {
        return string.Equals(value, "solid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "fluid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "liquid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "gas", StringComparison.OrdinalIgnoreCase);
    }

    public static ValidationResult ValidateRecipeDefinition(string filePath)
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
            string rawText = f.GetAsText();

            // Empty-key slot entries (e.g. `- : 5`) crash YamlDotNet's representation
            // model. Pre-substitute them so the rest of the file can still parse, and
            // surface each empty key as a validation error with its line number.
            var pre = RecipeYamlPreprocessor.Preprocess(rawText);
            foreach (int lineNumber in pre.EmptyKeyLineNumbers)
            {
                result.AddError($"Empty resource key at line {lineNumber}");
            }

            string text = pre.Text;

            var parseResult = ValidateYamlSyntax(text);
            if (!parseResult.IsValid)
            {
                result.Errors.AddRange(parseResult.Errors);
                return result;
            }

            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;

            ValidateRecipeDefinitionStructure(root, result);
        }
        catch (Exception e)
        {
            result.AddError($"Validation exception: {e.Message}");
        }

        return result;
    }

    private static void ValidateRecipeDefinitionStructure(
        YamlMappingNode root,
        ValidationResult result)
    {
        if (!root.Children.ContainsKey("recipes"))
        {
            result.AddError("Missing required key: 'recipes'");
            return;
        }

        var recipes = root.Children["recipes"] as YamlSequenceNode;
        if (recipes == null)
        {
            result.AddError("'recipes' must be a sequence");
            return;
        }

        int recipeIndex = 0;
        foreach (var recipeNode in recipes.Children)
        {
            var recipe = recipeNode as YamlMappingNode;
            if (recipe == null)
            {
                result.AddError($"Recipe at index {recipeIndex} must be a mapping");
                recipeIndex++;
                continue;
            }

            var requiredFields = new[] { "recipe_id", "work_required", "output_resources" };
            foreach (var field in requiredFields)
            {
                if (!recipe.Children.ContainsKey(field))
                {
                    result.AddError(
                        $"Recipe at index {recipeIndex} missing required field: '{field}'"
                    );
                }
            }

            if (recipe.Children.ContainsKey("work_required"))
            {
                var workReq = recipe.Children["work_required"];
                if (!IsNumericNode(workReq))
                {
                    result.AddError(
                        $"Recipe at index {recipeIndex}: 'work_required' must be numeric"
                    );
                }
            }

            // Validate icon section if present
            if (recipe.Children.ContainsKey("icon"))
            {
                ValidateIconSection(recipe.Children["icon"], $"Recipe at index {recipeIndex}", result);
            }

            // Validate optional tags section
            if (recipe.Children.ContainsKey("tags"))
            {
                if (recipe.Children["tags"] is not YamlSequenceNode tagsSeq)
                {
                    result.AddError($"Recipe at index {recipeIndex}: 'tags' must be a sequence");
                }
                else
                {
                    int tagIndex = 0;
                    foreach (var tagNode in tagsSeq.Children)
                    {
                        if (tagNode is not YamlScalarNode tagScalar || string.IsNullOrWhiteSpace(tagScalar.Value))
                        {
                            result.AddError(
                                $"Recipe at index {recipeIndex}: 'tags[{tagIndex}]' must be a non-empty string"
                            );
                        }
                        tagIndex++;
                    }
                }
            }

            // Validate input/output resources keys (allow optional tag: prefix on both)
            ValidateRecipeSlotKeys(recipe, "input_resources", recipeIndex, allowTagPrefix: true, result);
            ValidateRecipeSlotKeys(recipe, "output_resources", recipeIndex, allowTagPrefix: true, result);

            // Validate conditional_outputs: list of { condition, resource, amount }.
            ValidateConditionalOutputs(recipe, recipeIndex, result);

            recipeIndex++;
        }
    }

    private static void ValidateConditionalOutputs(
        YamlMappingNode recipe,
        int recipeIndex,
        ValidationResult result)
    {
        if (!recipe.Children.ContainsKey("conditional_outputs")) return;
        var node = recipe.Children["conditional_outputs"];

        if (node is YamlScalarNode s && (s.Value == null || s.Value == "" || s.Value == "~"))
            return;

        if (node is not YamlSequenceNode list)
        {
            result.AddError(
                $"Recipe at index {recipeIndex}: 'conditional_outputs' must be a sequence"
            );
            return;
        }

        int idx = 0;
        foreach (var entry in list.Children)
        {
            if (entry is not YamlMappingNode m)
            {
                result.AddError(
                    $"Recipe at index {recipeIndex}: 'conditional_outputs[{idx}]' must be a mapping"
                );
                idx++;
                continue;
            }

            foreach (var key in new[] { "condition", "resource", "amount" })
            {
                if (!m.Children.ContainsKey(key))
                {
                    result.AddError(
                        $"Recipe at index {recipeIndex}: 'conditional_outputs[{idx}]' missing required key '{key}'"
                    );
                }
            }

            if (m.Children.TryGetValue("condition", out var condNode))
            {
                if (condNode is YamlScalarNode condScalar)
                {
                    string? conditionText = condScalar.Value;
                    if (string.IsNullOrWhiteSpace(conditionText))
                    {
                        result.AddError(
                            $"Recipe at index {recipeIndex}: 'conditional_outputs[{idx}].condition' must be non-empty"
                        );
                    }
                    else
                    {
                        try
                        {
                            Structures.Resources.RecipeExpressionEvaluator.Compile(conditionText);
                        }
                        catch (Structures.Resources.RecipeExpressionException ex)
                        {
                            result.AddError(
                                $"Recipe at index {recipeIndex}: 'conditional_outputs[{idx}].condition' parse error: {ex.Message}"
                            );
                        }
                    }
                }
                else if (condNode is YamlSequenceNode ruleList)
                {
                    ValidateConditionRuleList(ruleList, recipeIndex, idx, result);
                }
                else
                {
                    result.AddError(
                        $"Recipe at index {recipeIndex}: 'conditional_outputs[{idx}].condition' must be a string or a sequence of rule mappings"
                    );
                }
            }

            if (m.Children.TryGetValue("resource", out var resNode))
            {
                if (resNode is not YamlScalarNode resScalar || string.IsNullOrWhiteSpace(resScalar.Value))
                {
                    result.AddError(
                        $"Recipe at index {recipeIndex}: 'conditional_outputs[{idx}].resource' must be a non-empty string"
                    );
                }
            }

            if (m.Children.TryGetValue("amount", out var amtNode) && !IsNumericNode(amtNode))
            {
                result.AddError(
                    $"Recipe at index {recipeIndex}: 'conditional_outputs[{idx}].amount' must be numeric"
                );
            }

            idx++;
        }
    }

    private static void ValidateConditionRuleList(
        YamlSequenceNode ruleList,
        int recipeIndex,
        int outputIndex,
        ValidationResult result)
    {
        if (ruleList.Children.Count == 0)
        {
            result.AddError(
                $"Recipe at index {recipeIndex}: 'conditional_outputs[{outputIndex}].condition' must contain at least one rule"
            );
            return;
        }

        int j = 0;
        foreach (var ruleNode in ruleList.Children)
        {
            if (ruleNode is not YamlMappingNode ruleMap)
            {
                result.AddError(
                    $"Recipe at index {recipeIndex}: 'conditional_outputs[{outputIndex}].condition[{j}]' must be a mapping"
                );
                j++;
                continue;
            }

            // var: required, must be one of AllowedVariables
            if (!ruleMap.Children.TryGetValue("var", out var varNode) || varNode is not YamlScalarNode varScalar
                || string.IsNullOrWhiteSpace(varScalar.Value))
            {
                result.AddError(
                    $"Recipe at index {recipeIndex}: 'conditional_outputs[{outputIndex}].condition[{j}].var' missing or empty"
                );
            }
            else
            {
                bool known = false;
                foreach (var v in Structures.Resources.RecipeExpressionEvaluator.AllowedVariables)
                {
                    if (v == varScalar.Value) { known = true; break; }
                }
                if (!known)
                {
                    result.AddError(
                        $"Recipe at index {recipeIndex}: 'conditional_outputs[{outputIndex}].condition[{j}].var' unknown variable '{varScalar.Value}'. " +
                        $"Allowed: {string.Join(", ", Structures.Resources.RecipeExpressionEvaluator.AllowedVariables)}."
                    );
                }
            }

            // op: required, must be one of ==,!=,<,<=,>,>=
            if (!ruleMap.Children.TryGetValue("op", out var opNode) || opNode is not YamlScalarNode opScalar
                || string.IsNullOrWhiteSpace(opScalar.Value))
            {
                result.AddError(
                    $"Recipe at index {recipeIndex}: 'conditional_outputs[{outputIndex}].condition[{j}].op' missing or empty"
                );
            }
            else if (!Structures.Resources.ConditionOperatorExtensions.TryParseSymbol(opScalar.Value, out _))
            {
                result.AddError(
                    $"Recipe at index {recipeIndex}: 'conditional_outputs[{outputIndex}].condition[{j}].op' invalid '{opScalar.Value}'. Allowed: ==, !=, <, <=, >, >="
                );
            }

            // value: required, numeric
            if (!ruleMap.Children.TryGetValue("value", out var valueNode))
            {
                result.AddError(
                    $"Recipe at index {recipeIndex}: 'conditional_outputs[{outputIndex}].condition[{j}].value' missing"
                );
            }
            else if (!IsNumericNode(valueNode))
            {
                result.AddError(
                    $"Recipe at index {recipeIndex}: 'conditional_outputs[{outputIndex}].condition[{j}].value' must be numeric"
                );
            }

            // join: optional on first rule, AND/OR thereafter
            if (ruleMap.Children.TryGetValue("join", out var joinNode))
            {
                if (joinNode is not YamlScalarNode joinScalar
                    || !Structures.Resources.ConditionJoinExtensions.TryParseYaml(joinScalar.Value ?? "", out _))
                {
                    result.AddError(
                        $"Recipe at index {recipeIndex}: 'conditional_outputs[{outputIndex}].condition[{j}].join' must be AND or OR"
                    );
                }
            }

            j++;
        }
    }

    private static readonly System.Text.RegularExpressions.Regex _recipeSlotKeyRegex =
        new("^[a-z_][a-z0-9_]*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static void ValidateRecipeSlotKeys(
        YamlMappingNode recipe,
        string field,
        int recipeIndex,
        bool allowTagPrefix,
        ValidationResult result)
    {
        if (!recipe.Children.ContainsKey(field)) return;
        if (recipe.Children[field] is not YamlSequenceNode list) return;

        int slotIndex = 0;
        foreach (var entry in list.Children)
        {
            if (entry is not YamlMappingNode slotMap)
            {
                slotIndex++;
                continue;
            }

            foreach (var kvp in slotMap.Children)
            {
                if (kvp.Key is not YamlScalarNode keyScalar || string.IsNullOrEmpty(keyScalar.Value))
                {
                    result.AddError(
                        $"Recipe at index {recipeIndex}: '{field}[{slotIndex}]' has empty key"
                    );
                    continue;
                }

                string key = keyScalar.Value!;
                string baseKey = key;
                if (allowTagPrefix && key.StartsWith("tag:"))
                {
                    baseKey = key.Substring("tag:".Length);
                }
                else if (!allowTagPrefix && key.StartsWith("tag:"))
                {
                    result.AddError(
                        $"Recipe at index {recipeIndex}: '{field}[{slotIndex}]' may not use 'tag:' prefix"
                    );
                    continue;
                }

                if (!_recipeSlotKeyRegex.IsMatch(baseKey))
                {
                    result.AddError(
                        $"Recipe at index {recipeIndex}: '{field}[{slotIndex}]' key '{key}' must be lower_snake_case"
                    );
                }
            }
            slotIndex++;
        }
    }

    /// <summary>
    /// Validates an icon section from YAML configuration.
    /// Checks that all three icon sizes exist (64, 128, 512).
    /// </summary>
    /// <param name="iconNode">The icon YAML node</param>
    /// <param name="context">Context for error messages (e.g., "Resource at index 0")</param>
    /// <param name="result">Validation result to add errors/warnings to</param>
    private static void ValidateIconSection(YamlNode iconNode, string context, ValidationResult result)
    {
        if (iconNode is YamlScalarNode scalar)
        {
            // Handle empty/null icon (valid - will use fallback)
            if (scalar.Value == null || scalar.Value == "" || scalar.Value == "~")
            {
                return;
            }
        }

        if (iconNode is not YamlMappingNode icon)
        {
            result.AddError($"{context}: 'icon' must be a mapping");
            return;
        }

        // Check for base_path
        if (!icon.Children.ContainsKey("base_path"))
        {
            result.AddWarning($"{context}: 'icon' section missing 'base_path' - will use fallback icons");
            return;
        }

        var basePathNode = icon.Children["base_path"];
        if (basePathNode is not YamlScalarNode basePathScalar || string.IsNullOrEmpty(basePathScalar.Value))
        {
            result.AddWarning($"{context}: 'icon.base_path' is empty - will use fallback icons");
            return;
        }

        string basePath = basePathScalar.Value;

        // Single texture per icon — Godot mipmaps handle runtime scaling.
        string svgPath = $"{basePath}.svg";
        string pngPath = $"{basePath}.png";
        if (!Godot.FileAccess.FileExists(svgPath) && !Godot.FileAccess.FileExists(pngPath))
        {
            result.AddWarning($"{context}: Icon not found: {svgPath} (or .png)");
        }

        // Validate optional scale field
        if (icon.Children.ContainsKey("scale"))
        {
            if (!IsNumericNode(icon.Children["scale"]))
            {
                result.AddError($"{context}: 'icon.scale' must be numeric");
            }
        }

        // Validate optional tint field (can be color array or hex string)
        if (icon.Children.ContainsKey("tint"))
        {
            var tintNode = icon.Children["tint"];
            if (tintNode is YamlSequenceNode)
            {
                // Color array [r, g, b] - valid
            }
            else if (tintNode is YamlScalarNode tintScalar)
            {
                // Hex color string - valid
                if (string.IsNullOrEmpty(tintScalar.Value))
                {
                    result.AddWarning($"{context}: 'icon.tint' is empty");
                }
            }
            else
            {
                result.AddError($"{context}: 'icon.tint' must be a color array [r, g, b] or hex string");
            }
        }
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
