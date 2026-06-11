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

    [Export] private HBoxContainer? _trailBox;
    [Export] private Label? _foremanLabel;

    private static PackedScene? _scene;

    public static TransferTopBar Create()
    {
        _scene ??= GD.Load<PackedScene>("res://UI/Wireframe/TransferTopBar.tscn");
        return _scene.Instantiate<TransferTopBar>();
    }

    public override void _Ready()
    {
        if (_foremanLabel != null) _foremanLabel.Text = ForemanTag;
    }

    private void OnHudPressed() => EmitSignal(SignalName.HudCloseRequested);
    private void OnBackPressed() => EmitSignal(SignalName.BackRequested);

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
            _trailBox.AddChild(lbl);
            if (!isLast)
            {
                var sep = new Label { Text = "›", ThemeTypeVariation = "LabelFaint" };
                sep.AddThemeFontSizeOverride("font_size", 14);
                _trailBox.AddChild(sep);
            }
        }
    }

    public void SetForemanTag(string tag)
    {
        ForemanTag = tag;
        if (_foremanLabel != null) _foremanLabel.Text = tag;
    }
}
