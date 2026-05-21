#if DEBUG
using System;
using System.Collections.Generic;
using Godot;

namespace Debug.DatabaseViewer;

/// <summary>
/// Data container class representing hierarchical data for the database viewer.
/// Provides a fluent builder API for constructing data trees.
/// Inherits from RefCounted to be Variant-compatible for Godot TreeItem metadata.
/// </summary>
public partial class DebugDataNode : RefCounted
{
    /// <summary>
    /// The name/label of this node.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The value of this node (for leaf nodes).
    /// </summary>
    public object? Value { get; private set; }

    /// <summary>
    /// Whether this node has a value (is a leaf node).
    /// </summary>
    public bool HasValue { get; private set; }

    /// <summary>
    /// The type of the value for display purposes.
    /// </summary>
    public string? ValueType { get; private set; }

    /// <summary>
    /// Child nodes of this node.
    /// </summary>
    public List<DebugDataNode> Children { get; } = new();

    /// <summary>
    /// Properties (key-value pairs) of this node.
    /// </summary>
    public Dictionary<string, DebugDataNode> Properties { get; } = new();

    /// <summary>
    /// Custom metadata attached to this node.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();

    /// <summary>
    /// Whether this node's children should be collapsed by default.
    /// </summary>
    public bool CollapsedByDefault { get; set; }

    /// <summary>
    /// Custom icon name for this node (if applicable).
    /// </summary>
    public string? IconName { get; set; }

    /// <summary>
    /// Creates a new DebugDataNode with the given name.
    /// </summary>
    /// <param name="name">The name/label for this node.</param>
    public DebugDataNode(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Adds a property (key-value pair) to this node.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <returns>This node for fluent chaining.</returns>
    public DebugDataNode AddProperty(string key, object value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        var node = new DebugDataNode(key)
        {
            Value = value,
            HasValue = true,
            ValueType = GetTypeName(value)
        };

        Properties[key] = node;
        return this;
    }

    /// <summary>
    /// Adds multiple properties from a dictionary.
    /// </summary>
    /// <param name="properties">The properties to add.</param>
    /// <returns>This node for fluent chaining.</returns>
    public DebugDataNode AddProperties(IEnumerable<KeyValuePair<string, object>> properties)
    {
        foreach (var kvp in properties)
        {
            AddProperty(kvp.Key, kvp.Value);
        }
        return this;
    }

    /// <summary>
    /// Adds a child node to this node.
    /// </summary>
    /// <param name="child">The child node to add.</param>
    /// <returns>This node for fluent chaining.</returns>
    public DebugDataNode AddChild(DebugDataNode child)
    {
        if (child == null)
        {
            throw new ArgumentNullException(nameof(child));
        }

        Children.Add(child);
        return this;
    }

    /// <summary>
    /// Adds a child node with the given name.
    /// </summary>
    /// <param name="name">The name of the child node.</param>
    /// <returns>The newly created child node.</returns>
    public DebugDataNode AddChild(string name)
    {
        var child = new DebugDataNode(name);
        Children.Add(child);
        return child;
    }

    /// <summary>
    /// Adds multiple child nodes.
    /// </summary>
    /// <param name="children">The children to add.</param>
    /// <returns>This node for fluent chaining.</returns>
    public DebugDataNode AddChildren(IEnumerable<DebugDataNode> children)
    {
        foreach (var child in children)
        {
            AddChild(child);
        }
        return this;
    }

    /// <summary>
    /// Sets a value for this node (making it a leaf node).
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <returns>This node for fluent chaining.</returns>
    public DebugDataNode SetValue(object? value)
    {
        Value = value;
        HasValue = true;
        ValueType = GetTypeName(value!);
        return this;
    }

    /// <summary>
    /// Sets custom metadata for this node.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>This node for fluent chaining.</returns>
    public DebugDataNode SetMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Sets whether this node should be collapsed by default.
    /// </summary>
    /// <param name="collapsed">True to collapse by default.</param>
    /// <returns>This node for fluent chaining.</returns>
    public DebugDataNode SetCollapsed(bool collapsed = true)
    {
        CollapsedByDefault = collapsed;
        return this;
    }

    /// <summary>
    /// Sets the custom icon name for this node.
    /// </summary>
    /// <param name="iconName">The icon name.</param>
    /// <returns>This node for fluent chaining.</returns>
    public DebugDataNode SetIcon(string iconName)
    {
        IconName = iconName;
        return this;
    }

    /// <summary>
    /// Gets the formatted string representation of this node's value.
    /// </summary>
    /// <param name="format">Optional format string.</param>
    /// <returns>Formatted string representation.</returns>
    public string GetFormattedValue(string? format = null)
    {
        if (!HasValue || Value == null)
        {
            return "null";
        }

        return Value switch
        {
            float f when !string.IsNullOrEmpty(format) => f.ToString(format),
            double d when !string.IsNullOrEmpty(format) => d.ToString(format),
            int i when !string.IsNullOrEmpty(format) => i.ToString(format),
            Vector2 v2 => $"({v2.X:F2}, {v2.Y:F2})",
            Vector3 v3 => $"({v3.X:F2}, {v3.Y:F2}, {v3.Z:F2})",
            Vector2I v2i => $"({v2i.X}, {v2i.Y})",
            Vector3I v3i => $"({v3i.X}, {v3i.Y}, {v3i.Z})",
            Color c => $"RGBA({c.R:F2}, {c.G:F2}, {c.B:F2}, {c.A:F2})",
            Rect2 r => $"({r.Position.X:F2}, {r.Position.Y:F2}) {r.Size.X:F2}x{r.Size.Y:F2}",
            Quaternion q => $"({q.X:F3}, {q.Y:F3}, {q.Z:F3}, {q.W:F3})",
            Transform2D t => $"({t.Origin.X:F2}, {t.Origin.Y:F2})",
            Transform3D t => $"Origin: ({t.Origin.X:F2}, {t.Origin.Y:F2}, {t.Origin.Z:F2})",
            _ => Value.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Gets the total count of all items (properties + children) in this node.
    /// </summary>
    public int TotalCount => Properties.Count + Children.Count;

    /// <summary>
    /// Checks if this node contains the given search text.
    /// </summary>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="recursive">Whether to search recursively.</param>
    /// <returns>True if the text is found.</returns>
    public bool Contains(string searchText, bool recursive = true)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            return true;
        }

        var comparison = StringComparison.OrdinalIgnoreCase;

        if (Name.Contains(searchText, comparison))
        {
            return true;
        }

        if (HasValue && Value?.ToString()?.Contains(searchText, comparison) == true)
        {
            return true;
        }

        foreach (var prop in Properties.Values)
        {
            if (prop.Contains(searchText, false))
            {
                return true;
            }
        }

        if (recursive)
        {
            foreach (var child in Children)
            {
                if (child.Contains(searchText, true))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetTypeName(object value)
    {
        if (value == null)
        {
            return "null";
        }

        var type = value.GetType();

        if (type.IsPrimitive)
        {
            return type.Name.ToLower();
        }

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(decimal))
        {
            return "decimal";
        }

        return type.Name;
    }
}
#endif
