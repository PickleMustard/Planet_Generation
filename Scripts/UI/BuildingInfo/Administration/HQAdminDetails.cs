namespace UI.BuildingInfo.Administration;

/// <summary>
/// Concrete admin panel for the Company Headquarters. Supplies HQ-specific labels;
/// the bespoke tab 0 content scene is bound through <c>AdminTabScene</c> in
/// <c>HQAdminDetails.tscn</c>.
/// </summary>
public partial class HQAdminDetails : AdministrationTabbedPanel
{
    protected override string AdminTabLabel => "HQ Operations";

    protected override string BuildingTag => "ADMIN · CORP #01";

    protected override string RenderText => "3D · HQ Tower";
}
