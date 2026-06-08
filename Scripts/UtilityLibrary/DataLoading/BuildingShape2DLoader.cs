using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

public static class BuildingShape2DLoader
{
    public static BuildingShape2D? LoadShape(string filePath)
    {
        var validation = YamlValidator.ValidateBuildingShape2D(filePath);
        if (!validation.IsValid)
        {
            GD.PrintErr($"BuildingShape2DLoader: validation failed for {filePath}");
            foreach (var error in validation.Errors)
                GD.PrintErr($"  - {error}");
            return null;
        }

        string? text = BaseConfigLoader.ReadAllText(filePath);
        if (text == null)
        {
            GD.PrintErr($"BuildingShape2DLoader: file not found: {filePath}");
            return null;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(text);
            return ParseShape(yamlData, filePath);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"BuildingShape2DLoader: error loading {filePath}: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    private static BuildingShape2D? ParseShape(SysDict dict, string filePath)
    {
        var shape = new BuildingShape2D
        {
            Id = ReadString(dict, "id", ""),
            DisplayName = ReadString(dict, "display_name", ""),
        };

        if (string.IsNullOrEmpty(shape.Id))
        {
            GD.PrintErr($"BuildingShape2DLoader: {filePath} missing 'id'");
            return null;
        }

        if (!dict.TryGetValue("vertices", out var vertsObj) || vertsObj is not List<object> vertsList)
        {
            GD.PrintErr($"BuildingShape2DLoader: {filePath} missing 'vertices' sequence");
            return null;
        }

        var verts = new List<Vector2>(vertsList.Count);
        for (int i = 0; i < vertsList.Count; i++)
        {
            if (vertsList[i] is not List<object> pair || pair.Count < 2)
            {
                GD.PrintErr($"BuildingShape2DLoader: {filePath} vertex[{i}] must be [x, y]");
                return null;
            }
            float x = NodeToFloat(pair[0], 0);
            float y = NodeToFloat(pair[1], 0);
            verts.Add(new Vector2(x, y));
        }
        shape.Vertices = verts.ToArray();

        if (shape.Vertices.Length < 3)
        {
            GD.PrintErr($"BuildingShape2DLoader: {filePath} needs >= 3 vertices");
            return null;
        }

        if (dict.TryGetValue("sides", out var sidesObj) && sidesObj is List<object> sidesList)
        {
            foreach (var sideObj in sidesList)
            {
                var side = new BuildingShape2D.SideSpec();
                if (sideObj is Dictionary<object, object> sideDict && sideDict.TryGetValue("slots", out var slotsObj))
                {
                    if (slotsObj is List<object> slotsList)
                    {
                        foreach (var slotObj in slotsList)
                        {
                            if (slotObj is not Dictionary<object, object> slotMap)
                            {
                                GD.PrintErr($"BuildingShape2DLoader: {filePath} slot entry must be a mapping with 'kind' and 'state' (got {slotObj?.GetType().Name ?? "null"}).");
                                return null;
                            }
                            string kindStr = ReadSlotField(slotMap, "kind");
                            string stateStr = ReadSlotField(slotMap, "state");
                            if (string.IsNullOrWhiteSpace(kindStr) || string.IsNullOrWhiteSpace(stateStr))
                            {
                                GD.PrintErr($"BuildingShape2DLoader: {filePath} slot entry missing required 'kind' and/or 'state'.");
                                return null;
                            }
                            side.Slots.Add(new BuildingShape2D.SlotSpec
                            {
                                Kind = ParseResourceNodeKind(kindStr),
                                StateOfMatter = StateOfMatterExtensions.Parse(stateStr),
                            });
                        }
                    }
                }
                shape.Sides.Add(side);
            }
        }

        while (shape.Sides.Count < shape.Vertices.Length)
            shape.Sides.Add(new BuildingShape2D.SideSpec());

        if (shape.Sides.Count > shape.Vertices.Length)
        {
            GameLogger.Warning($"BuildingShape2DLoader: {filePath} has more sides than vertices; trimming");
            shape.Sides.RemoveRange(shape.Vertices.Length, shape.Sides.Count - shape.Vertices.Length);
        }

        return shape;
    }

    private static ResourceNodeKind ParseResourceNodeKind(string value)
    {
        if (Enum.TryParse<ResourceNodeKind>(value, ignoreCase: true, out var kind))
            return kind;
        return ResourceNodeKind.Flex;
    }

    private static string ReadSlotField(Dictionary<object, object> slotMap, string key)
    {
        if (slotMap.TryGetValue(key, out var v)) return v?.ToString() ?? "";
        return "";
    }

    private static string ReadString(SysDict dict, string key, string fallback)
    {
        if (!dict.TryGetValue(key, out var value)) return fallback;
        return value?.ToString() ?? fallback;
    }

    private static float NodeToFloat(object? node, float fallback)
    {
        switch (node)
        {
            case null: return fallback;
            case float f: return f;
            case double d: return (float)d;
            case int i: return i;
            case long l: return l;
            default:
                if (float.TryParse(node.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return v;
                return fallback;
        }
    }
}
