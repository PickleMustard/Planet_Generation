#if DEBUG
using System;

namespace UI.Debug;

/// <summary>
/// Attribute used to tag classes or properties as database objects for the viewer.
/// Classes tagged with this attribute will be discoverable by the DataProviderRegistry.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public class DebugDataAttribute : Attribute
{
    /// <summary>
    /// The display name shown in the database viewer.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Category for organizing data in the viewer sidebar.
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Display order priority. Lower values appear first.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Whether to poll for changes at physics update rate.
    /// </summary>
    public bool AutoRefresh { get; set; } = true;

    /// <summary>
    /// Creates a new DebugDataAttribute.
    /// </summary>
    /// <param name="displayName">The display name for this data object.</param>
    public DebugDataAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}
#endif
