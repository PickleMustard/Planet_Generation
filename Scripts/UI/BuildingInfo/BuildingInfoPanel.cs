using Godot;
using Constructables;
using Structures.GameState;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Main panel for displaying building information.
/// Routes to appropriate detail views based on building category.
/// </summary>
public partial class BuildingInfoPanel : PanelContainer
{
    [Export] public BuildingInfoHeader? Header;
    [Export] public VBoxContainer? DetailsContainer;
    [Export] public PackedScene? ExtractionDetailsScene;
    [Export] public PackedScene? ManufacturingDetailsScene;
    [Export] public PackedScene? PowerDetailsScene;
    [Export] public PackedScene? StorageDetailsScene;

    private BuildingConstruction? _currentBuilding;
    private ContinentEconomy? _currentEconomy;
    private BaseBuildingDetails? _currentDetails;

    public override void _Ready()
    {
        // Get node references if not already set via exports
        Header ??= GetNodeOrNull<BuildingInfoHeader>("MarginContainer/MainVBox/BuildingInfoHeader");
        DetailsContainer ??= GetNodeOrNull<VBoxContainer>("MarginContainer/MainVBox/DetailsContainer");
    }

    /// <summary>
    /// Sets the building to display in the panel.
    /// This is the main public API for using this panel.
    /// </summary>
    public void SetBuilding(BuildingConstruction building, ContinentEconomy economy)
    {
        if (building?.Definition == null)
        {
            Clear();
            return;
        }

        _currentBuilding = building;
        _currentEconomy = economy;

        // Get building instance ID from economy
        int buildingInstanceId = GetBuildingInstanceId(building, economy);

        // Update header
        if (Header != null)
        {
            Header.SetBuilding(building.Definition, buildingInstanceId);
        }

        // Show appropriate details based on category
        string category = building.Definition.Category?.ToLowerInvariant() ?? "";
        ShowDetailsForCategory(category);

        GameLogger.Debug($"BuildingInfoPanel: Displaying building '{building.Name}' (category: {category})");
    }

    /// <summary>
    /// Clears the panel to default state.
    /// </summary>
    public void Clear()
    {
        _currentBuilding = null;
        _currentEconomy = null;

        if (Header != null)
        {
            Header.Clear();
        }

        ClearCurrentDetails();
    }

    /// <summary>
    /// Shows the "No Building" state when cell has no building.
    /// </summary>
    public void ShowNoBuilding()
    {
        ClearCurrentDetails();

        if (Header != null)
        {
            Header.Clear();
        }

        // Add a "No Building" label to details container
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

    private void ShowDetailsForCategory(string? category)
    {
        ClearCurrentDetails();

        if (_currentBuilding == null || _currentEconomy == null)
        {
            return;
        }

        PackedScene? detailsScene = category?.ToLowerInvariant() switch
        {
            "extraction" => ExtractionDetailsScene,
            "agriculture" => ExtractionDetailsScene, // Agriculture uses same view as extraction
            "manufacturing" => ManufacturingDetailsScene,
            "power" => PowerDetailsScene,
            "storage" => StorageDetailsScene,
            _ => null
        };

        if (detailsScene == null)
        {
            GameLogger.Warning($"BuildingInfoPanel: No detail view for category '{category}'");
            return;
        }

        // Instantiate the details view
        _currentDetails = detailsScene.Instantiate<BaseBuildingDetails>();
        if (_currentDetails != null)
        {
            _currentDetails.SetBuilding(_currentBuilding, _currentEconomy);
            DetailsContainer?.AddChild(_currentDetails);
        }
    }

    private void ClearCurrentDetails()
    {
        if (_currentDetails != null)
        {
            _currentDetails.Clear();
            _currentDetails.QueueFree();
            _currentDetails = null;
        }

        // Clear any remaining children in details container
        if (DetailsContainer != null)
        {
            foreach (var child in DetailsContainer.GetChildren())
            {
                child.QueueFree();
            }
        }
    }

    private int GetBuildingInstanceId(BuildingConstruction building, ContinentEconomy economy)
    {
        // Find the registration to get the instance ID
        var buildings = economy.GetType()
            .GetField("_activeBuildings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(economy) as System.Collections.Generic.List<ContinentEconomy.BuildingRegistration>;

        if (buildings != null)
        {
            foreach (var reg in buildings)
            {
                if (reg.BuildingNode == building)
                {
                    return reg.BuildingInstanceId;
                }
            }
        }

        return -1;
    }

    private void OnUpgradeRequested()
    {
        if (_currentBuilding != null)
        {
            GameLogger.Info($"BuildingInfoPanel: Upgrade requested for '{_currentBuilding.Name}'");
            // Additional logic can be added here if needed
        }
    }

    private void OnDemolishRequested()
    {
        if (_currentBuilding != null)
        {
            GameLogger.Info($"BuildingInfoPanel: Demolish requested for '{_currentBuilding.Name}'");
            // Additional logic can be added here if needed
        }
    }
}
