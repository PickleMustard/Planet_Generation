using Godot;

namespace UI.Wireframe;

/// <summary>
/// Footer action bar with a left and right slot. Children added to LeftSlot or
/// RightSlot are laid out horizontally with paper styling. The slot nodes and
/// panel styling are defined in <c>TransferActionBar.tscn</c>.
/// </summary>
[GlobalClass]
public partial class TransferActionBar : PanelContainer
{
    [Export]
    public HBoxContainer LeftSlot { get; private set; } = null!;

    [Export]
    public HBoxContainer RightSlot { get; private set; } = null!;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", BuildBg());

        // Fallback: when constructed via new (not from .tscn), create slots programmatically
        if (LeftSlot == null || RightSlot == null)
        {
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
