using Godot;
using Constructables;
using Constructables.Buildings.Behaviors;
using Structures.GameState;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Main panel for displaying building information.
/// Routes to bespoke per-type detail views by building category.
/// </summary>
public partial class BuildingInfoPanel : PanelContainer
{
    [Export] public BuildingInfoHeader? Header;
    [Export] public VBoxContainer? DetailsContainer;
    [Export] public PackedScene? BuildingPanelDetailsScene;
    [Export] public PackedScene? PowerPanelDetailsScene;
    [Export] public PackedScene? HubPanelDetailsScene;
    [Export] public Godot.Collections.Dictionary<string, PackedScene> AdminBuildingScenes = new();
    [Export] public PackedScene? AdminFallbackDetailsScene;

    private Building? _currentBuilding;
    private BaseBuildingDetails? _currentDetails;

    public override void _Ready()
    {
        Header ??= GetNodeOrNull<BuildingInfoHeader>("MarginContainer/MainVBox/BuildingInfoHeader");
        DetailsContainer ??= GetNodeOrNull<VBoxContainer>("MarginContainer/MainVBox/DetailsContainer");
    }

    /// <summary>
    /// Sets the building to display in the panel.
    /// </summary>
    public void SetBuilding(Building building)
    {
        if (building?.Definition == null)
        {
            Clear();
            return;
        }

        _currentBuilding = building;

        int buildingInstanceId = GetBuildingInstanceId(building);

        if (Header != null)
        {
            Header.SetBuilding(building, buildingInstanceId);
        }

        string category = building.Definition.Category?.ToLowerInvariant() ?? "";
        ShowDetailsFor(building, category);

        GameLogger.Debug($"BuildingInfoPanel: Displaying building '{building.Name}' (category: {category})");
    }

    /// <summary>
    /// Clears the panel to default state.
    /// </summary>
    public void Clear()
    {
        _currentBuilding = null;
        Header?.Clear();
        ClearCurrentDetails();
    }

    /// <summary>
    /// Shows the "No Building" state when cell has no building.
    /// </summary>
    public void ShowNoBuilding()
    {
        ClearCurrentDetails();
        Header?.Clear();

        var label = new Label
        {
            Text = "No Building",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        DetailsContainer?.AddChild(label);
    }

    private void ShowDetailsFor(Building building, string category)
    {
        ClearCurrentDetails();

        PackedScene? detailsScene = ResolveDetailsScene(building, category);

        if (detailsScene == null)
        {
            var id = building.Definition?.IdName ?? "<unknown>";
            GameLogger.Warning($"BuildingInfoPanel: no detail view for '{id}' (category='{category}')");
            return;
        }

        _currentDetails = detailsScene.Instantiate<BaseBuildingDetails>();
        if (_currentDetails != null)
        {
            DetailsContainer?.AddChild(_currentDetails);
            _currentDetails.SetBuilding(building);

            // Bubble ManageRoutesRequested from HubPanelDetails up through the window
            if (_currentDetails is HubPanelDetails hubDetails)
                hubDetails.ManageRoutesRequested += OnManageRoutesRequested;
        }
    }

    private PackedScene? ResolveDetailsScene(Building b, string category)
    {
        if (category == "administration") return ResolveAdminScene(b);

        if (b.GetBehavior<ManufacturingBehavior>() != null
         || b.GetBehavior<ExtractionBehavior>() != null)
            return BuildingPanelDetailsScene;

        if (b.GetBehavior<StorageHubBehavior>() != null
         || b.GetBehavior<TransferStationBehavior>() != null
         || b.GetBehavior<TransportHubBehavior>() != null
         || b.GetBehavior<BulkStorageRoutingBehavior>() != null)
            return HubPanelDetailsScene;

        if (b.GetBehavior<PowerProducerBehavior>() != null
         || b.GetBehavior<BatteryBehavior>() != null
         || b.GetBehavior<PowerConsumerBehavior>() != null)
            return PowerPanelDetailsScene;

        return null;
    }

    private void ClearCurrentDetails()
    {
        if (_currentDetails != null)
        {
            // Disconnect signal before freeing
            if (_currentDetails is HubPanelDetails hubDetails)
                hubDetails.ManageRoutesRequested -= OnManageRoutesRequested;

            _currentDetails.Clear();
            _currentDetails.QueueFree();
            _currentDetails = null;
        }

        if (DetailsContainer != null)
        {
            foreach (var child in DetailsContainer.GetChildren())
            {
                child.QueueFree();
            }
        }
    }

    private int GetBuildingInstanceId(Building building)
    {
        // TODO: Implement when building instance registry lands.
        return 0;
    }

    private PackedScene? ResolveAdminScene(Building building)
    {
        var id = building.Definition?.IdName ?? "";
        if (!string.IsNullOrEmpty(id) && AdminBuildingScenes.TryGetValue(id, out var scene))
            return scene;
        GameLogger.Warning(
            $"BuildingInfoPanel: no admin scene registered for '{id}', using fallback"
        );
        return AdminFallbackDetailsScene;
    }

    private void OnManageRoutesRequested()
    {
        BuildingInfoWindow.Instance?.EmitSignal(BuildingInfoWindow.SignalName.ManageRoutesRequested);
    }
}
