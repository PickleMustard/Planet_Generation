using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace UtilityLibrary;

public delegate bool ValidationCallback(
    string filePath,
    string yamlContent,
    out List<string> errors
);

public static class TemplateLoader
{
    public static readonly ValidationCallback SyntaxOnlyValidator;
    public static readonly ValidationCallback CelestialBodyValidator;
    public static readonly ValidationCallback SystemTemplateValidator;
    public static readonly ValidationCallback ResourceDefinitionValidator;

    private static readonly string[] DefaultSearchDirectories =
    {
        "res://Configuration/SystemGen/",
        "res://Configuration/SystemTemplate/",
        "res://Configuration/ResourceDefinition/",
    };

    private static string[] _searchDirectories;
    private static ValidationCallback _defaultValidator;

    static TemplateLoader()
    {
        SyntaxOnlyValidator = ValidateSyntaxOnly;
        CelestialBodyValidator = CreateValidatorWrapper(
            YamlValidator.ValidateCelestialBodyTemplate
        );
        SystemTemplateValidator = CreateValidatorWrapper(YamlValidator.ValidateSystemTemplate);
        ResourceDefinitionValidator = CreateValidatorWrapper(
            YamlValidator.ValidateResourceDefinition
        );
        _defaultValidator = SyntaxOnlyValidator;
    }

    public static Godot.Collections.Dictionary Load(string pathOrName)
    {
        return Load(pathOrName, _defaultValidator);
    }

    public static Godot.Collections.Dictionary Load(string pathOrName, ValidationCallback validator)
    {
        var resolvedPath = ResolvePath(pathOrName);
        if (resolvedPath == null)
        {
            throw new FileNotFoundException($"Could not find YAML file: {pathOrName}");
        }

        string yamlContent = ReadFileContent(resolvedPath);
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            throw new InvalidOperationException($"File is empty or invalid: {resolvedPath}");
        }

        if (validator != null)
        {
            if (!validator(resolvedPath, yamlContent, out var errors))
            {
                var errorMessage = string.Join("\n  - ", errors);
                throw new InvalidOperationException(
                    $"Validation failed for {resolvedPath}:\n  - {errorMessage}"
                );
            }
        }

        return ParseYamlToJson(yamlContent);
    }

    public static Godot.Collections.Dictionary LoadWithoutValidation(string pathOrName)
    {
        var resolvedPath = ResolvePath(pathOrName);
        if (resolvedPath == null)
        {
            throw new FileNotFoundException($"Could not find YAML file: {pathOrName}");
        }

        string yamlContent = ReadFileContent(resolvedPath);
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            throw new InvalidOperationException($"File is empty or invalid: {resolvedPath}");
        }

        return ParseYamlToJson(yamlContent);
    }

    public static bool TryLoad(string pathOrName, out Godot.Collections.Dictionary result)
    {
        return TryLoad(pathOrName, _defaultValidator, out result);
    }

    public static bool TryLoad(
        string pathOrName,
        ValidationCallback validator,
        out Godot.Collections.Dictionary result
    )
    {
        result = null;
        try
        {
            result = Load(pathOrName, validator);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SetSearchDirectories(params string[] directories)
    {
        _searchDirectories = directories ?? Array.Empty<string>();
    }

    public static void SetDefaultValidator(ValidationCallback validator)
    {
        _defaultValidator = validator;
    }

    public static string ResolvePath(string pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
        {
            return null;
        }

        if (IsFullPath(pathOrName))
        {
            string path = NormalizePath(pathOrName);
            if (Godot.FileAccess.FileExists(path))
            {
                return path;
            }
            return TryWithExtensions(path);
        }

        foreach (var directory in GetSearchDirectories())
        {
            string candidatePath = pathOrName;
            if (!pathOrName.StartsWith("res://"))
            {
                candidatePath = directory.TrimEnd('/') + "/" + pathOrName;
            }

            candidatePath = TryWithExtensions(candidatePath);
            if (candidatePath != null && Godot.FileAccess.FileExists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }

    public static bool IsFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.Contains("/")
            || path.Contains("\\")
            || path.StartsWith("res://")
            || path.StartsWith("user://");
    }

    private static string[] GetSearchDirectories()
    {
        return _searchDirectories ?? DefaultSearchDirectories;
    }

    private static string NormalizePath(string path)
    {
        if (!path.StartsWith("res://") && !path.StartsWith("user://") && !Path.IsPathRooted(path))
        {
            path = "res://" + path;
        }
        return path;
    }

    private static string TryWithExtensions(string path)
    {
        if (Godot.FileAccess.FileExists(path))
        {
            return path;
        }

        string yamlPath = path + ".yaml";
        if (Godot.FileAccess.FileExists(yamlPath))
        {
            return yamlPath;
        }

        string ymlPath = path + ".yml";
        if (Godot.FileAccess.FileExists(ymlPath))
        {
            return ymlPath;
        }

        int lastDot = path.LastIndexOf('.');
        if (lastDot > 0)
        {
            string withoutExtension = path.Substring(0, lastDot);
            yamlPath = withoutExtension + ".yaml";
            if (Godot.FileAccess.FileExists(yamlPath))
            {
                return yamlPath;
            }
            ymlPath = withoutExtension + ".yml";
            if (Godot.FileAccess.FileExists(ymlPath))
            {
                return ymlPath;
            }
        }

        return null;
    }

    private static string ReadFileContent(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new FileNotFoundException($"Could not open file: {path}");
        }
        return file.GetAsText();
    }

    private static Godot.Collections.Dictionary ParseYamlToJson(string yamlContent)
    {
        try
        {
            var deserializer = new DeserializerBuilder().Build();

            var yamlData = deserializer.Deserialize<object>(yamlContent);

            if (yamlData == null)
            {
                return new Godot.Collections.Dictionary();
            }

            var converted = (Godot.Collections.Dictionary)ConvertYamlValue(yamlData);
            return converted;
        }
        catch (YamlException e)
        {
            throw new YamlException(
                $"YAML parsing error at line {e.Start.Line}, column {e.Start.Column}: {e.Message}"
            );
        }
    }

    private static Variant ConvertYamlValue(object obj)
    {
        if (obj == null)
        {
            return default(Variant);
        }

        if (obj is Dictionary<object, object> dict)
        {
            var godotDict = new Godot.Collections.Dictionary();
            foreach (var kvp in dict)
            {
                try
                {
                    Variant key = ConvertYamlValue(kvp.Key);
                    Variant value = ConvertYamlValue(kvp.Value);
                    godotDict[key] = value;
                }
                catch (Exception e)
                {
                    GD.PrintErr(
                        $"{e.TargetSite}: Error converting key {kvp.Key} to Variant: {e.Message}"
                    );
                }
            }
            return godotDict;
        }

        if (obj is List<object> list)
        {
            var godotArray = new Godot.Collections.Array();
            foreach (var item in list)
            {
                try
                {
                    godotArray.Add(ConvertYamlValue(item));
                }
                catch (Exception e)
                {
                    GD.PrintErr(
                        $"{e.TargetSite}: Error converting item {item} to Variant: {e.Message}"
                    );
                    godotArray.Add(default(Variant));
                }
            }
            return godotArray;
        }

        if (obj is IList<object> iList)
        {
            var godotArray = new Godot.Collections.Array();
            foreach (var item in iList)
            {
                try
                {
                    godotArray.Add(ConvertYamlValue(item));
                }
                catch (Exception e)
                {
                    GD.PrintErr(
                        $"{e.TargetSite}: Error converting item {item} to Variant: {e.Message}"
                    );
                    godotArray.Add(default(Variant));
                }
            }
            return godotArray;
        }

        if (obj is string str)
            return (String)str;
        if (obj is int i)
            return (int)i;
        if (obj is float f)
            return (float)f;
        if (obj is bool b)
            return (bool)b;
        if (obj is long l)
            return (int)l;
        if (obj is double d)
            return (float)d;

        return (Variant)obj;
    }

    private static bool ValidateSyntaxOnly(
        string filePath,
        string yamlContent,
        out List<string> errors
    )
    {
        errors = new List<string>();
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(yamlContent));
            return true;
        }
        catch (YamlException e)
        {
            errors.Add(
                $"YAML syntax error at line {e.Start.Line}, column {e.Start.Column}: {e.Message}"
            );
            return false;
        }
        catch (Exception e)
        {
            errors.Add($"Parse error: {e.Message}");
            return false;
        }
    }

    private static ValidationCallback CreateValidatorWrapper(
        Func<string, ValidationResult> validatorFunc
    )
    {
        return (string filePath, string yamlContent, out List<string> errors) =>
        {
            var result = validatorFunc(filePath);
            errors = result.Errors;
            return result.IsValid;
        };
    }
}
