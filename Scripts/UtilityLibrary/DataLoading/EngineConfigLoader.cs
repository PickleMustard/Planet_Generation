using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Logistics;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Loads and parses engine YAML configurations from the Configuration/engines/ directory.
/// Supports recursive directory scanning, category inference, and duplicate detection.
/// </summary>
public static class EngineConfigLoader
{
    private const string EnginesDirectory = "res://Configuration/engines/";
    private const string EngineTypesPath = "res://Configuration/engines/EngineTypes.yaml";

    private static List<EngineConfigDefinition>? _allEngines;
    private static HashSet<string>? _inferredCategories;
    private static Dictionary<string, string>? _engineSourceFiles;
    private static List<EngineTypeCategory>? _templateCategories;

    /// <summary>
    /// Loads all engine definitions from the Configuration/engines/ directory.
    /// Scans recursively for all .yaml and .yml files, skipping EngineTypes.yaml.
    /// Results are cached for subsequent calls.
    /// </summary>
    /// <returns>List of all engine definitions found</returns>
    public static List<EngineConfigDefinition> LoadAllEngines()
    {
        if (_allEngines != null)
            return _allEngines;

        _allEngines = new List<EngineConfigDefinition>();
        _engineSourceFiles = new Dictionary<string, string>();

        var files = BaseConfigLoader.GetYamlFilesRecursive(EnginesDirectory);

        GameLogger.Info($"EngineConfigLoader: Scanning {files.Count} files in {EnginesDirectory}");

        foreach (var filePath in files)
        {
            // Skip template definitions file (used only for validation)
            if (filePath.EndsWith("EngineTypes.yaml"))
                continue;

            var fileEngines = LoadEnginesFromFile(filePath);

            foreach (var engine in fileEngines)
            {
                if (_engineSourceFiles.ContainsKey(engine.Name))
                {
                    GD.PrintErr($"EngineConfigLoader: Duplicate engine '{engine.Name}' found in {filePath} (first defined in {_engineSourceFiles[engine.Name]})");
                    continue;
                }

                _allEngines.Add(engine);
                _engineSourceFiles[engine.Name] = filePath;
            }
        }

        InferCategories(_allEngines);
        ValidateEngineTypes(_allEngines);

        GameLogger.Info($"EngineConfigLoader: Loaded {_allEngines.Count} engines across {_inferredCategories?.Count ?? 0} categories");

        return _allEngines;
    }

    /// <summary>
    /// Loads engine definitions from a specific YAML file.
    /// </summary>
    /// <param name="filePath">Path to the YAML file (e.g., "res://Configuration/engines/Chemical.yaml")</param>
    /// <returns>List of engine definitions from that file</returns>
    public static List<EngineConfigDefinition> LoadEnginesFromFile(string filePath)
    {
        var engines = new List<EngineConfigDefinition>();

        string? yamlContent = BaseConfigLoader.ReadAllText(filePath);
        if (yamlContent == null)
        {
            GD.PrintErr($"EngineConfigLoader: File not found: {filePath}");
            return engines;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

            if (yamlData != null && yamlData.ContainsKey("engines"))
            {
                var enginesList = yamlData["engines"] as List<object>;
                if (enginesList != null)
                {
                    foreach (var engineObj in enginesList)
                    {
                        if (engineObj is Dictionary<object, object> engineDict)
                        {
                            var engine = ParseEngineDefinition(engineDict);
                            engines.Add(engine);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"EngineConfigLoader: Error loading from {filePath}: {e.Message}");
        }

        return engines;
    }

    /// <summary>
    /// Loads engine type categories from EngineTypes.yaml.
    /// These categories are used for validation and organization.
    /// </summary>
    /// <returns>List of engine type categories</returns>
    public static List<EngineTypeCategory> LoadEngineTypes()
    {
        if (_templateCategories != null)
            return _templateCategories;

        _templateCategories = new List<EngineTypeCategory>();

        string? yamlContent = BaseConfigLoader.ReadAllText(EngineTypesPath);
        if (yamlContent == null)
        {
            GameLogger.Warning($"EngineConfigLoader: Types file not found: {EngineTypesPath}");
            return _templateCategories;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

            if (yamlData != null && yamlData.ContainsKey("categories"))
            {
                var categoriesList = yamlData["categories"] as List<object>;
                if (categoriesList != null)
                {
                    foreach (var categoryObj in categoriesList)
                    {
                        if (categoryObj is Dictionary<object, object> categoryDict)
                        {
                            var category = new EngineTypeCategory
                            {
                                Name = BaseConfigLoader.ReadString(categoryDict, "name", ""),
                                Description = BaseConfigLoader.ReadString(categoryDict, "description", ""),
                            };
                            _templateCategories.Add(category);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"EngineConfigLoader: Error loading types from {EngineTypesPath}: {e.Message}");
        }

        return _templateCategories;
    }

    /// <summary>
    /// Gets all inferred engine categories from loaded engines.
    /// Categories are inferred from the source filename.
    /// </summary>
    /// <returns>List of unique category names</returns>
    public static List<string> GetEngineCategories()
    {
        LoadAllEngines(); // Ensure engines are loaded
        return _inferredCategories?.ToList() ?? new List<string>();
    }

    /// <summary>
    /// Gets an engine definition by name (case-insensitive).
    /// </summary>
    /// <param name="name">The engine name to look up</param>
    /// <returns>The engine definition, or null if not found</returns>
    public static EngineConfigDefinition? GetEngineByName(string name)
    {
        var engines = LoadAllEngines();
        return engines.FirstOrDefault(e =>
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears the cached engine data to force a reload on next access.
    /// </summary>
    public static void ClearCache()
    {
        _allEngines = null;
        _inferredCategories = null;
        _engineSourceFiles = null;
        _templateCategories = null;
        GameLogger.Debug("EngineConfigLoader: Cache cleared");
    }

    private static void InferCategories(List<EngineConfigDefinition> engines)
    {
        _inferredCategories = new HashSet<string>();

        // Infer categories from source filenames
        if (_engineSourceFiles != null)
        {
            foreach (var kvp in _engineSourceFiles)
            {
                // Extract category from filename (e.g., "Chemical.yaml" -> "Chemical")
                string fileName = System.IO.Path.GetFileNameWithoutExtension(kvp.Value);
                if (!string.IsNullOrEmpty(fileName))
                {
                    _inferredCategories.Add(fileName);
                }
            }
        }
    }

    private static void ValidateEngineTypes(List<EngineConfigDefinition> engines)
    {
        var validTypes = LoadEngineTypes().Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var engine in engines)
        {
            // Get the category from source file
            if (_engineSourceFiles != null && _engineSourceFiles.TryGetValue(engine.Name, out string? sourceFile))
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(sourceFile);
                if (!validTypes.Contains(fileName))
                {
                    GD.PushWarning($"EngineConfigLoader: Unknown engine category '{fileName}' for engine '{engine.Name}'");
                }
            }
        }
    }

    private static EngineConfigDefinition ParseEngineDefinition(Dictionary<object, object> dict)
    {
        return new EngineConfigDefinition
        {
            Name = BaseConfigLoader.ReadString(dict, "name", ""),
            SpecificImpulse = BaseConfigLoader.ReadFloat(dict, "specific_impulse", 0f),
            Thrust = BaseConfigLoader.ReadFloat(dict, "thrust", 0f),
            Description = BaseConfigLoader.ReadString(dict, "description", ""),
        };
    }
}
