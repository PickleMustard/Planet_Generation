using Godot;

namespace UI.BuildingInfo.Administration;

/// <summary>
/// HQ Operations tab content (placeholder data). Treasury / workforce / contracts /
/// reputation / headlines come from stub fields until the economy + HR systems land.
/// Building name and behavior count come from the live <see cref="Constructables.Building"/>.
/// </summary>
public partial class HQAdminTabContent : BaseBuildingDetails
{
    [Export]
    public Label? BuildingNameValue;

    [Export]
    public Label? TreasuryValue;

    [Export]
    public Label? TreasuryDelta;

    [Export]
    public Label? WorkforceValue;

    [Export]
    public Label? ReputationValue;

    [Export]
    public Label? ReputationScore;

    [Export]
    public VBoxContainer? ContractsList;

    [Export]
    public VBoxContainer? HeadlinesList;

    private static readonly PackedScene ContractRowScene =
        GD.Load<PackedScene>("res://UI/BuildingInfo/Administration/ContractRow.tscn");
    private static readonly PackedScene HeadlineRowScene =
        GD.Load<PackedScene>("res://UI/BuildingInfo/Administration/HeadlineRow.tscn");

    private static readonly (string Id, string Client, string Resource, int Qty, string Due, string State)[]
        StubContracts =
        {
            ("CTR-118", "Foundry Co.", "STL", 240, "02:14", "run"),
            ("CTR-119", "Pacific Rail", "IRN", 600, "04:48", "run"),
            ("CTR-120", "Northshore Mfg.", "CL", 180, "00:42", "block"),
            ("CTR-121", "Atlas Logistics", "STL", 90, "06:10", "idle"),
        };

    private static readonly (string Time, string Text)[] StubHeadlines =
    {
        ("04:23", "Quarterly review filed · stock +2.1%"),
        ("04:11", "Hired 4 new operators (workforce 142/180)"),
        ("03:54", "Investor call scheduled · Series B"),
        ("03:30", "Insurance premium paid · −12k credits"),
    };

    protected override void UpdateDisplay()
    {
        if (BuildingNameValue != null)
            BuildingNameValue.Text = _building?.Name ?? "Company Headquarters";

        if (TreasuryValue != null)
            TreasuryValue.Text = "§ 482,300";
        if (TreasuryDelta != null)
            TreasuryDelta.Text = "+12.4k/min · runway 14h 22m";

        if (WorkforceValue != null)
            WorkforceValue.Text = "142 / 180 · morale fair";

        if (ReputationValue != null)
            ReputationValue.Text = "B+";
        if (ReputationScore != null)
            ReputationScore.Text = "6,420 pts · +38 today";

        PopulateContracts();
        PopulateHeadlines();
    }

    private void PopulateContracts()
    {
        if (ContractsList == null)
            return;
        foreach (var child in ContractsList.GetChildren())
            child.QueueFree();
        foreach (var c in StubContracts)
        {
            var row = ContractRowScene.Instantiate<HBoxContainer>();
            row.GetNode<Label>("Id").Text = c.Id;
            row.GetNode<Label>("Client").Text = c.Client;
            row.GetNode<Label>("Qty").Text = $"×{c.Qty}";
            row.GetNode<Label>("Due").Text = $"due {c.Due}";
            row.GetNode<Label>("State").Text = c.State;
            ContractsList.AddChild(row);
        }
    }

    private void PopulateHeadlines()
    {
        if (HeadlinesList == null)
            return;
        foreach (var child in HeadlinesList.GetChildren())
            child.QueueFree();
        foreach (var h in StubHeadlines)
        {
            var row = HeadlineRowScene.Instantiate<HBoxContainer>();
            row.GetNode<Label>("Time").Text = h.Time;
            row.GetNode<Label>("Text").Text = h.Text;
            HeadlinesList.AddChild(row);
        }
    }

    public override void Clear()
    {
        base.Clear();
        if (BuildingNameValue != null) BuildingNameValue.Text = "";
        if (TreasuryValue != null) TreasuryValue.Text = "";
        if (TreasuryDelta != null) TreasuryDelta.Text = "";
        if (WorkforceValue != null) WorkforceValue.Text = "";
        if (ReputationValue != null) ReputationValue.Text = "";
        if (ReputationScore != null) ReputationScore.Text = "";
        if (ContractsList != null)
            foreach (var child in ContractsList.GetChildren())
                child.QueueFree();
        if (HeadlinesList != null)
            foreach (var child in HeadlinesList.GetChildren())
                child.QueueFree();
    }
}
