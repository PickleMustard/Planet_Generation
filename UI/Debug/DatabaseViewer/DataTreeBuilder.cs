#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Utility class that converts DebugDataNode hierarchies into Godot TreeItem structures.
/// Handles formatting of various types including primitives, collections, and Godot types.
/// </summary>
public static class DataTreeBuilder
{
    private static readonly Dictionary<string, Color> TypeColors = new()
    {
        { "string", new Color(0.7f, 0.7f, 0.3f) },
        { "int", new Color(0.3f, 0.7f, 0.7f) },
        { "float", new Color(0.3f, 0.7f, 0.7f) },
        { "double", new Color(0.3f, 0.7f, 0.7f) },
        { "bool", new Color(0.7f, 0.3f, 0.7f) },
        { "null", new Color(0.5f, 0.5f, 0.5f) },
        { "vector", new Color(0.3f, 0.9f, 0.3f) },
        { "color", new Color(1f, 0.8f, 0.2f) },
        { "collection", new Color(0.6f, 0.6f, 0.9f) }
    };

    /// <summary>
    /// Builds a TreeItem hierarchy from a DebugDataNode.
    /// </summary>
    /// <param name="tree">The Tree control to build in.</param>
    /// <param name="node">The DebugDataNode to convert.</param>
    /// <param name="parent">Optional parent TreeItem.</param>
    /// <returns>The created TreeItem.</returns>
    public static TreeItem? BuildTree(Tree tree, DebugDataNode node, TreeItem? parent = null)
    {
        if (tree == null || node == null)
        {
            return null;
        }

        var item = parent == null ? tree.CreateItem() : tree.CreateItem(parent);
        item.SetText(0, node.Name);
        item.SetMetadata(0, Variant.From(node));

        if (node.HasValue)
        {
            var formattedValue = FormatValue(node.Value!);
            item.SetText(1, StripBBCode(formattedValue));
            item.SetText(2, node.ValueType);
            item.SetTooltipText(1, GetTooltip(node.Value!));
            ApplyTypeColor(item, node.ValueType!);
        }

        if (!string.IsNullOrEmpty(node.IconName))
        {
            var icon = GetIcon(node.IconName);
            if (icon != null)
            {
                item.SetIcon(0, icon);
            }
        }

        foreach (var property in node.Properties.Values)
        {
            BuildTree(tree, property, item);
        }

        foreach (var child in node.Children)
        {
            BuildTree(tree, child, item);
        }

        if (node.CollapsedByDefault)
        {
            item.Collapsed = true;
        }

        return item;
    }

    /// <summary>
    /// Rebuilds an existing tree item with new data.
    /// </summary>
    /// <param name="item">The TreeItem to rebuild.</param>
    /// <param name="node">The new DebugDataNode data.</param>
    public static void RebuildItem(TreeItem item, DebugDataNode node)
    {
        if (item == null || node == null)
        {
            return;
        }

        item.SetText(0, node.Name);
        item.SetMetadata(0, Variant.From(node));

        if (node.HasValue)
        {
            var formattedValue = FormatValue(node.Value!);
            item.SetText(1, StripBBCode(formattedValue));
            item.SetText(2, node.ValueType);
            item.SetTooltipText(1, GetTooltip(node.Value!));
            ApplyTypeColor(item, node.ValueType!);
        }
        {
            item.SetText(1, "");
            item.SetText(2, "");
        }

        var existingChildren = new List<TreeItem>();
        var existingChild = item.GetFirstChild();
        while (existingChild != null)
        {
            existingChildren.Add(existingChild);
            existingChild = existingChild.GetNext();
        }

        foreach (var toRemove in existingChildren)
        {
            item.RemoveChild(toRemove);
        }

        var tree = item.GetTree();
        foreach (var property in node.Properties.Values)
        {
            BuildTree(tree, property, item);
        }

        foreach (var childNode in node.Children)
        {
            BuildTree(tree, childNode, item);
        }
    }

    /// <summary>
    /// Formats a value for display based on its type.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="format">Optional format string.</param>
    /// <returns>Formatted string representation.</returns>
    public static string FormatValue(object? value, string? format = null)
    {
        if (value == null)
        {
            return "[color=gray][i]null[/i][/color]";
        }

        return value switch
        {
            bool b => b ? "[color=green]true[/color]" : "[color=red]false[/color]",
            string s => $"\"[color=yellow]{EscapeBBCode(s)}[/color]\"",
            float f when !string.IsNullOrEmpty(format) => $"[color=cyan]{f.ToString(format)}[/color]",
            float f => $"[color=cyan]{f:F4}[/color]",
            double d when !string.IsNullOrEmpty(format) => $"[color=cyan]{d.ToString(format)}[/color]",
            double d => $"[color=cyan]{d:F4}[/color]",
            int i => $"[color=cyan]{i}[/color]",
            long l => $"[color=cyan]{l}[/color]",
            Vector2 v2 => $"[color=lime]({v2.X:F2}, {v2.Y:F2})[/color]",
            Vector3 v3 => $"[color=lime]({v3.X:F2}, {v3.Y:F2}, {v3.Z:F2})[/color]",
            Vector2I v2i => $"[color=lime]({v2i.X}, {v2i.Y})[/color]",
            Vector3I v3i => $"[color=lime]({v3i.X}, {v3i.Y}, {v3i.Z})[/color]",
            Vector4 v4 => $"[color=lime]({v4.X:F2}, {v4.Y:F2}, {v4.Z:F2}, {v4.W:F2})[/color]",
            Vector4I v4i => $"[color=lime]({v4i.X}, {v4i.Y}, {v4i.Z}, {v4i.W})[/color]",
            Color c => $"[color=#{c.ToHtml(false)}]■[/color] RGBA({c.R:F2}, {c.G:F2}, {c.B:F2}, {c.A:F2})",
            Rect2 r => $"[color=lime]Pos: ({r.Position.X:F2}, {r.Position.Y:F2}) Size: {r.Size.X:F2}x{r.Size.Y:F2}[/color]",
            Rect2I r => $"[color=lime]Pos: ({r.Position.X}, {r.Position.Y}) Size: {r.Size.X}x{r.Size.Y}[/color]",
            Quaternion q => $"[color=lime]({q.X:F3}, {q.Y:F3}, {q.Z:F3}, {q.W:F3})[/color]",
            Transform2D t => $"[color=lime]Origin: ({t.Origin.X:F2}, {t.Origin.Y:F2})[/color]",
            Transform3D t => $"[color=lime]Origin: ({t.Origin.X:F2}, {t.Origin.Y:F2}, {t.Origin.Z:F2})[/color]",
            Basis b => $"[color=lime]Basis[/color]",
            Array array => FormatCollection(array),
            IList list => FormatCollection(list),
            IDictionary dict => FormatDictionary(dict),
            IDictionary<string, object> dict => FormatDictionary(dict),
            Enum e => $"[color=orange]{e}[/color]",
            _ => FormatObject(value)
        };
    }

    /// <summary>
    /// Applies DebugDataPropertyAttribute formatting to a value.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="attribute">The attribute containing formatting options.</param>
    /// <returns>Formatted string representation.</returns>
    public static string? FormatWithAttribute(object value, DebugDataPropertyAttribute attribute)
    {
        if (value == null)
        {
            return attribute.HideIfNull ? null : "[color=gray][i]null[/i][/color]";
        }

        return FormatValue(value, attribute.Format);
    }

    private static string FormatCollection(IEnumerable collection)
    {
        var count = 0;
        foreach (var _ in collection)
        {
            count++;
        }

        return $"[color=#9999ff][{count} items][/color]";
    }

    private static string FormatDictionary(IDictionary dict)
    {
        return $"[color=#9999ff][{dict.Count} entries][/color]";
    }

    private static string FormatDictionary(IDictionary<string, object> dict)
    {
        return $"[color=#9999ff][{dict.Count} entries][/color]";
    }

    private static string FormatObject(object obj)
    {
        var type = obj.GetType();

        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
        {
            return obj.ToString() ?? string.Empty;
        }

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (props.Length == 0)
        {
            return obj.ToString() ?? string.Empty;
        }

        return $"[color=#9999ff][{type.Name}][/color]";
    }

    private static void ApplyTypeColor(TreeItem item, string valueType)
    {
        if (string.IsNullOrEmpty(valueType))
        {
            return;
        }

        var key = valueType.ToLower();
        if (TypeColors.TryGetValue(key, out var color))
        {
            item.SetCustomColor(1, color);
        }
        else if (valueType.Contains("vector", StringComparison.OrdinalIgnoreCase))
        {
            item.SetCustomColor(1, TypeColors["vector"]);
        }
    }

    private static string GetTooltip(object value)
    {
        if (value == null)
        {
            return "null";
        }

        var type = value.GetType();
        var tooltip = $"Type: {type.FullName}\nValue: {value}";

        if (value is Array array)
        {
            tooltip += $"\nLength: {array.Length}\nRank: {array.Rank}";
        }
        else if (value is ICollection collection)
        {
            tooltip += $"\nCount: {collection.Count}";
        }

        return tooltip;
    }

    private static string EscapeBBCode(string text)
    {
        return text.Replace("[", "\\[")
                   .Replace("]", "\\]")
                   .Replace("\n", "\\n")
                   .Replace("\t", "\\t");
    }

    private static string StripBBCode(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }
        return System.Text.RegularExpressions.Regex.Replace(text, @"\[/?[^\]]*\]", "");
    }

    private static Texture2D? GetIcon(string iconName)
    {
        return null;
    }

    /// <summary>
    /// Creates a DebugDataNode from an object using reflection.
    /// </summary>
    /// <param name="obj">The object to convert.</param>
    /// <param name="name">The name for the node.</param>
    /// <param name="maxDepth">Maximum recursion depth.</param>
    /// <returns>A DebugDataNode representing the object.</returns>
    public static DebugDataNode FromObject(object obj, string name, int maxDepth = 3)
    {
        return FromObjectInternal(obj, name, maxDepth, 0, new HashSet<object>());
    }

    private static DebugDataNode FromObjectInternal(object obj, string name, int maxDepth, int currentDepth, HashSet<object> visited)
    {
        var node = new DebugDataNode(name);

        if (obj == null)
        {
            node.SetValue(null);
            return node;
        }

        if (currentDepth >= maxDepth)
        {
            node.SetValue(obj);
            return node;
        }

        var type = obj.GetType();

        if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
        {
            node.SetValue(obj);
            return node;
        }

        if (obj is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                var keyStr = entry.Key?.ToString() ?? "null";
                var childNode = FromObjectInternal(entry.Value!, keyStr, maxDepth, currentDepth + 1, visited);
                node.AddChild(childNode);
            }
            return node;
        }

        if (obj is IEnumerable enumerable and not string)
        {
            var index = 0;
            foreach (var item in enumerable)
            {
                var childNode = FromObjectInternal(item!, $"[{index}]", maxDepth, currentDepth + 1, visited);
                node.AddChild(childNode);
                index++;
            }
            return node;
        }

        if (visited.Contains(obj))
        {
            node.SetValue("[circular reference]");
            return node;
        }

        visited.Add(obj);

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            try
            {
                if (prop.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var value = prop.GetValue(obj);
                var attr = prop.GetCustomAttribute<DebugDataPropertyAttribute>();
                var propName = attr?.DisplayName ?? prop.Name;

                if (value == null && attr?.HideIfNull == true)
                {
                    continue;
                }

                var childNode = FromObjectInternal(value!, propName, maxDepth, currentDepth + 1, visited);
                node.AddChild(childNode);
            }
            catch (Exception ex)
            {
                node.AddProperty(prop.Name, $"[error: {ex.Message}]");
            }
        }

        return node;
    }
}
#endif
