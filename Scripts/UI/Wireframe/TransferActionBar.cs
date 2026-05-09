using Godot;

namespace UI.Wireframe;

/// <summary>
/// Footer action bar with a left and right slot. Children added to LeftSlot or
/// RightSlot are laid out horizontally with paper styling.
/// </summary>
[GlobalClass]
public partial class TransferActionBar : PanelContainer
{
    public HBoxContainer LeftSlot { get; private set; } = null!;
    public HBoxContainer RightSlot { get; private set; } = null!;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", BuildBg());

        var root = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);

        LeftSlot = new HBoxContainer();
        LeftSlot.AddThemeConstantOverride("separation", 10);
        LeftSlot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(LeftSlot);

        RightSlot = new HBoxContainer();
        RightSlot.AddThemeConstantOverride("separation", 10);
        RightSlot.Alignment = BoxContainer.AlignmentMode.End;
        RightSlot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(RightSlot);
    }

    private static StyleBoxFlat BuildBg()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.05f),
            BorderColor = WireColors.Ink,
            BorderWidthTop = 2,
            ContentMarginLeft = 18,
            ContentMarginTop = 10,
            ContentMarginRight = 18,
            ContentMarginBottom = 10,
        };
    }
}
