using Godot;
using Structures.GameState;
using Structures.Resources;
using System.Collections.Generic;
using System.Linq;
using UtilityLibrary;

namespace UI.CellInfo;

/// <summary>
/// A reusable UI sub-panel that displays the natural resources available in a selected Voronoi cell.
/// Shows a list of resources with their names, visual color indicators, and abundance levels.
/// This panel is designed as a sub-component of a larger GUI element.
/// </summary>
public partial class CellResourcePanel : Control
{
    [Export]
    public PackedScene? ResourceListItemScene { get; set; }

    [Export]
    private NodePath? _resourcesListPath;

    [Export]
    private NodePath? _noResourcesLabelPath;

    private VBoxContainer? _resourcesList;
    private Label? _noResourcesLabel;
    private List<ResourceListItem> _resourceItems = new();

    public override void _Ready()
    {
        // Cache node references - use exported paths if available, otherwise use default names
        if (_resourcesListPath != null)
        {
            _resourcesList = GetNodeOrNull<VBoxContainer>(_resourcesListPath);
        }
        else
        {
            _resourcesList = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/ResourcesList");
        }

        if (_noResourcesLabelPath != null)
        {
            _noResourcesLabel = GetNodeOrNull<Label>(_noResourcesLabelPath);
        }
        else
        {
            _noResourcesLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/NoResourcesLabel");
        }

        // Initialize with cleared display
        ClearDisplay();
    }

    /// <summary>
    /// Updates the panel display with resources from the selected Voronoi cell.
    /// Called automatically when CellSelectionManager emits CellSelected signal.
    /// </summary>
    /// <param name="cell">The VoronoiCell to display resources for</param>
    public void UpdateFromCell(VoronoiCell cell)
    {
        ClearDisplay();

        if (cell.Resources == null || cell.Resources.Count == 0)
        {
            if (_noResourcesLabel != null)
            {
                _noResourcesLabel.Visible = true;
            }
            GameLogger.Debug("CellResourcePanel: Cell has no resources");
            return;
        }

        if (_noResourcesLabel != null)
        {
            _noResourcesLabel.Visible = false;
        }

        // Sort resources by abundance (descending)
        var sortedResources = cell.Resources
            .OrderByDescending(kvp => kvp.Value)
            .ToList();

        foreach (var kvp in sortedResources)
        {
            if (ResourceListItemScene == null)
            {
                GameLogger.Warning("CellResourcePanel: ResourceListItemScene is not set");
                continue;
            }

            if (_resourcesList == null)
            {
                GameLogger.Warning("CellResourcePanel: ResourcesList is null");
                continue;
            }

            var item = ResourceListItemScene.Instantiate<ResourceListItem>();

            // Look up resource definition for display info
            ResourceDefinition? definition = null;
            if (ResourceDatabase.Instance != null && ResourceDatabase.Instance.IsLoaded)
            {
                ResourceDatabase.Instance.TryGetResource(kvp.Key, out definition);
            }

            _resourcesList.AddChild(item);
            item.SetResource(kvp.Key, kvp.Value, definition);
            _resourceItems.Add(item);
        }

        GameLogger.Debug($"CellResourcePanel updated: {sortedResources.Count} resources displayed");
    }

    /// <summary>
    /// Clears all displayed resources.
    /// Called automatically when selection is cleared.
    /// </summary>
    public void ClearDisplay()
    {
        // Remove all existing resource items
        foreach (var item in _resourceItems)
        {
            item.QueueFree();
        }
        _resourceItems.Clear();

        if (_noResourcesLabel != null)
        {
            _noResourcesLabel.Visible = false;
        }

        GameLogger.Debug("CellResourcePanel display cleared");
    }

}
