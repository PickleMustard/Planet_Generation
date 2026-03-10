#if DEBUG
using System;

namespace UI.Debug;

/// <summary>
/// Attribute used to control how individual properties are displayed in the database viewer.
/// Apply to properties to customize their display behavior.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class DebugDataPropertyAttribute : Attribute
{
    /// <summary>
    /// Custom display name. If null, the property name is used.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Whether this property is read-only in the viewer.
    /// </summary>
    public bool IsReadOnly { get; set; } = true;

    /// <summary>
    /// Format string for displaying the value (e.g., "F2" for floats).
    /// </summary>
    public string Format { get; set; } = "";

    /// <summary>
    /// Whether to hide this property if its value is null.
    /// </summary>
    public bool HideIfNull { get; set; } = true;

    /// <summary>
    /// Creates a new DebugDataPropertyAttribute.
    /// </summary>
    public DebugDataPropertyAttribute()
    {
    }

    /// <summary>
    /// Creates a new DebugDataPropertyAttribute with a custom display name.
    /// </summary>
    /// <param name="displayName">Custom display name for the property.</param>
    public DebugDataPropertyAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}
#endif
