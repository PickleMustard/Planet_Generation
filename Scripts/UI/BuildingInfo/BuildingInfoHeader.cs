using Constructables;
using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Header for the Building Info Panel displaying building icon, name,
/// and an edit-name button.
/// </summary>
public partial class BuildingInfoHeader : HBoxContainer
{
    private TextureRect? _buildingIcon;
    private Label? _buildingNameLabel;
    private LineEdit? _buildingNameEdit;
    private Button? _editNameButton;

    private Building? _building;
    private int _buildingInstanceId = -1;

    [Signal]
    public delegate void NameChangedEventHandler(string newName);

    public override void _Ready()
    {
        _buildingIcon = GetNodeOrNull<TextureRect>("BuildingIcon");
        _buildingNameLabel = GetNodeOrNull<Label>("BuildingNameLabel");
        _buildingNameEdit = GetNodeOrNull<LineEdit>("BuildingNameEdit");
        _editNameButton = GetNodeOrNull<Button>("EditNameButton");

        if (_editNameButton != null)
            _editNameButton.Pressed += BeginEdit;

        if (_buildingNameEdit != null)
        {
            _buildingNameEdit.Hide();
            _buildingNameEdit.TextSubmitted += OnNameSubmitted;
            _buildingNameEdit.FocusExited += CommitOrCancelEdit;
        }
    }

    /// <summary>
    /// Sets the building to display in the header.
    /// </summary>
    public void SetBuilding(BuildingDefinition? definition, int buildingInstanceId)
    {
        _buildingInstanceId = buildingInstanceId;

        if (_buildingNameLabel != null)
        {
            string name = definition?.DisplayName ?? definition?.IdName ?? "Unknown Building";
            if (definition != null)
                name += $"  [Tier {definition.MaxResourceTier}]";
            _buildingNameLabel.Text = name;
        }

        if (_buildingIcon != null)
        {
            _buildingIcon.Texture = LoadBuildingIcon(definition);
        }

        if (_editNameButton != null)
            _editNameButton.Disabled = _building == null;
    }

    /// <summary>
    /// Sets the runtime building for live name editing. Header still works without it
    /// (read-only display) when only the definition is provided.
    /// </summary>
    public void SetBuilding(Building? building, int buildingInstanceId)
    {
        _building = building;
        SetBuilding(building?.Definition, buildingInstanceId);
        if (_buildingNameLabel != null && building != null && !string.IsNullOrEmpty(building.Name))
        {
            _buildingNameLabel.Text = building.Name;
        }
    }

    /// <summary>
    /// Clears the header to default state.
    /// </summary>
    public void Clear()
    {
        _building = null;
        _buildingInstanceId = -1;

        if (_buildingNameLabel != null)
            _buildingNameLabel.Text = "No Building Selected";

        if (_buildingIcon != null)
            _buildingIcon.Texture = null;

        if (_editNameButton != null)
            _editNameButton.Disabled = true;

        if (_buildingNameEdit != null)
            _buildingNameEdit.Hide();
    }

    private void BeginEdit()
    {
        if (_building == null || _buildingNameEdit == null || _buildingNameLabel == null)
            return;
        _buildingNameEdit.Text = _building.Name;
        _buildingNameLabel.Hide();
        _buildingNameEdit.Show();
        _buildingNameEdit.GrabFocus();
        _buildingNameEdit.SelectAll();
    }

    private void OnNameSubmitted(string newName) => Commit(newName);

    private void CommitOrCancelEdit()
    {
        if (_buildingNameEdit == null) return;
        Commit(_buildingNameEdit.Text);
    }

    private void Commit(string newName)
    {
        if (_buildingNameEdit == null || _buildingNameLabel == null)
            return;

        newName = newName?.Trim() ?? "";
        if (_building != null && !string.IsNullOrEmpty(newName))
        {
            _building.Name = newName;
            _buildingNameLabel.Text = newName;
            EmitSignal(SignalName.NameChanged, newName);
        }
        _buildingNameEdit.Hide();
        _buildingNameLabel.Show();
    }

    private Texture2D? LoadBuildingIcon(BuildingDefinition? definition)
    {
        if (definition?.Icon?.IsValid == true)
        {
            return definition.Icon.LargeTexture ?? definition.Icon.MediumTexture ?? definition.Icon.SmallTexture;
        }

        if (!string.IsNullOrEmpty(definition?.Icon?.BasePath))
        {
            try
            {
                string iconPath = definition.Icon.BasePath + ".png";
                return ResourceLoader.Load<Texture2D>(iconPath);
            }
            catch
            {
                GameLogger.Debug($"BuildingInfoHeader: Failed to load icon for building '{definition.IdName}'");
            }
        }

        return null;
    }
}
