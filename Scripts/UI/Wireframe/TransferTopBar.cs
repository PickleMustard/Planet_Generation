using System;
using System.Collections.Generic;
using Godot;

namespace UI.Wireframe;

/// <summary>
/// Sub-flow header bar with breadcrumb trail, ✕ HUD / ← Back buttons, and a
/// foreman tag on the right.
/// </summary>
[GlobalClass]
public partial class TransferTopBar : PanelContainer
{
    [Signal] public delegate void HudCloseRequestedEventHandler();
    [Signal] public delegate void BackRequestedEventHandler();

    [Export] public string ForemanTag { get; set; } = "FRM · TRX-04 · LEDGER 1905";

    private HBoxContainer? _trailBox;
    private Label? _foremanLabel;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", BuildBg());
        var root = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 12);
        AddChild(root);

        var leftCluster = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        leftCluster.AddThemeConstantOverride("separation", 10);
        root.AddChild(leftCluster);

        var hud = new Button { Text = "✕ HUD", TooltipText = "Return to HUD" };
        hud.Pressed += () => EmitSignal(SignalName.HudCloseRequested);
        leftCluster.AddChild(hud);

        var back = new Button { Text = "← Back", TooltipText = "Back" };
        back.Pressed += () => EmitSignal(SignalName.BackRequested);
        leftCluster.AddChild(back);

        _trailBox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _trailBox.AddThemeConstantOverride("separation", 8);
        leftCluster.AddChild(_trailBox);

        _foremanLabel = new Label
        {
            Text = ForemanTag,
            ThemeTypeVariation = "LabelMono",
        };
        _foremanLabel.AddThemeFontSizeOverride("font_size", 10);
        _foremanLabel.AddThemeColorOverride("font_color", WireColors.InkFaint);
        root.AddChild(_foremanLabel);
    }

    public void SetTrail(IReadOnlyList<string> trail)
    {
        if (_trailBox == null) return;
        foreach (var c in _trailBox.GetChildren()) c.QueueFree();
        for (int i = 0; i < trail.Count; i++)
        {
            bool isLast = i == trail.Count - 1;
            var lbl = new Label
            {
                Text = trail[i],
                ThemeTypeVariation = isLast ? "LabelHand" : "LabelSub",
            };
            lbl.AddThemeFontSizeOverride("font_size", isLast ? 22 : 17);
            lbl.AddThemeColorOverride("font_color", isLast ? WireColors.Ink : WireColors.InkSoft);
            _trailBox.AddChild(lbl);
            if (!isLast)
            {
                var sep = new Label { Text = "›", ThemeTypeVariation = "LabelMono" };
                sep.AddThemeFontSizeOverride("font_size", 14);
                sep.AddThemeColorOverride("font_color", WireColors.InkFaint);
                _trailBox.AddChild(sep);
            }
        }
    }

    public void SetForemanTag(string tag)
    {
        ForemanTag = tag;
        if (_foremanLabel != null) _foremanLabel.Text = tag;
    }

    private static StyleBoxFlat BuildBg()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(WireColors.Paper.R, WireColors.Paper.G, WireColors.Paper.B, 0f),
            BorderColor = WireColors.Ink,
            BorderWidthBottom = 2,
            ContentMarginLeft = 18,
            ContentMarginTop = 12,
            ContentMarginRight = 18,
            ContentMarginBottom = 12,
        };
    }
}
