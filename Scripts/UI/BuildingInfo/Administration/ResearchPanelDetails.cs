using Godot;

namespace UI.BuildingInfo.Administration;

/// <summary>
/// Stub Research tab content. Renders a single placeholder label until the
/// research subsystem lands. Plugged into <see cref="AdministrationTabbedPanel.ResearchTabScene"/>.
/// </summary>
public partial class ResearchPanelDetails : BaseBuildingDetails
{
    [Export]
    public Label? StatusLabel;

    protected override void UpdateDisplay()
    {
        if (StatusLabel != null)
            StatusLabel.Text = "Research subsystem — pending";
    }

    public override void Clear()
    {
        base.Clear();
        if (StatusLabel != null)
            StatusLabel.Text = "";
    }
}
