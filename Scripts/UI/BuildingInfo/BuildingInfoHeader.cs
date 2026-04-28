using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Header for the Building Info Panel displaying building icon, name,
/// and action buttons (upgrade, demolish).
/// </summary>
public partial class BuildingInfoHeader : HBoxContainer
{
    private TextureRect? _buildingIcon;
    private Label? _buildingNameLabel;
    private Button? _upgradeButton;
    private Button? _demolishButton;

    private int _buildingInstanceId = -1;

    [Signal]
    public delegate void UpgradeRequestedEventHandler();

    [Signal]
    public delegate void DemolishRequestedEventHandler();

    public override void _Ready()
    {
        _buildingIcon = GetNodeOrNull<TextureRect>("BuildingIcon");
        _buildingNameLabel = GetNodeOrNull<Label>("BuildingNameLabel");
        _upgradeButton = GetNodeOrNull<Button>("UpgradeButton");
        _demolishButton = GetNodeOrNull<Button>("DemolishButton");

        // Connect button signals
        if (_upgradeButton != null)
        {
            _upgradeButton.Pressed += OnUpgradePressed;
        }

        if (_demolishButton != null)
        {
            _demolishButton.Pressed += OnDemolishPressed;
        }
    }

    /// <summary>
    /// Sets the building to display in the header.
    /// </summary>
    public void SetBuilding(BuildingDefinition? definition, int buildingInstanceId)
    {
        _buildingInstanceId = buildingInstanceId;

        // Set building name
        if (_buildingNameLabel != null)
        {
            _buildingNameLabel.Text = definition?.DisplayName ?? definition?.IdName ?? "Unknown Building";
        }

        // Load and set icon
        if (_buildingIcon != null)
        {
            Texture2D? icon = LoadBuildingIcon(definition);
            _buildingIcon.Texture = icon;
        }

        // Update button states based on building
        UpdateButtonStates(definition);
    }

    /// <summary>
    /// Clears the header to default state.
    /// </summary>
    public void Clear()
    {
        _buildingInstanceId = -1;

        if (_buildingNameLabel != null)
        {
            _buildingNameLabel.Text = "No Building Selected";
        }

        if (_buildingIcon != null)
        {
            _buildingIcon.Texture = null;
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.Disabled = true;
        }

        if (_demolishButton != null)
        {
            _demolishButton.Disabled = true;
        }
    }

    private void OnUpgradePressed()
    {
        if (_buildingInstanceId >= 0)
        {
            SignalBus.Instance?.EmitBuildingUpgradeRequested(_buildingInstanceId);
            EmitSignal(SignalName.UpgradeRequested);
        }
    }

    private void OnDemolishPressed()
    {
        if (_buildingInstanceId >= 0)
        {
            SignalBus.Instance?.EmitBuildingDemolishRequested(_buildingInstanceId);
            EmitSignal(SignalName.DemolishRequested);
        }
    }

    private Texture2D? LoadBuildingIcon(BuildingDefinition? definition)
    {
        if (definition?.Icon?.IsValid == true)
        {
            return definition.Icon.MediumTexture ?? definition.Icon.SmallTexture;
        }

        // Fallback: try to load from base path if defined
        if (!string.IsNullOrEmpty(definition?.Icon?.BasePath))
        {
            try
            {
                string mediumPath = definition.Icon.BasePath + "_medium.png";
                return ResourceLoader.Load<Texture2D>(mediumPath);
            }
            catch
            {
                GameLogger.Debug($"BuildingInfoHeader: Failed to load icon for building '{definition.IdName}'");
            }
        }

        return null;
    }

    private void UpdateButtonStates(BuildingDefinition? definition)
    {
        // Enable/disable buttons based on building capabilities
        // TODO: Check if building can be upgraded
        bool canUpgrade = definition != null && _buildingInstanceId >= 0;
        bool canDemolish = definition != null && _buildingInstanceId >= 0;

        if (_upgradeButton != null)
        {
            _upgradeButton.Disabled = !canUpgrade;
        }

        if (_demolishButton != null)
        {
            _demolishButton.Disabled = !canDemolish;
        }
    }
}
