#if DEBUG
using System.Collections.Generic;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Interface for database viewer data providers.
/// Provides data for display in the database viewer with refresh and search capabilities.
/// </summary>
public interface IDataProvider
{
    /// <summary>
    /// The display name of this data provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The category this provider belongs to (used for sidebar organization).
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Whether this provider needs to refresh its data.
    /// Used to determine if Refresh() should be called.
    /// </summary>
    bool NeedsRefresh { get; }

    /// <summary>
    /// Gets the current data from this provider.
    /// </summary>
    /// <returns>A DebugDataNode containing the hierarchical data.</returns>
    DebugDataNode GetData();

    /// <summary>
    /// Refreshes the data in this provider.
    /// Called at physics update rate when the viewer is visible and NeedsRefresh is true.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Searches the data for items matching the given pattern.
    /// </summary>
    /// <param name="pattern">The search pattern (supports wildcards).</param>
    /// <returns>An enumerable of matching item paths.</returns>
    IEnumerable<string> Search(string pattern);
}
#endif
