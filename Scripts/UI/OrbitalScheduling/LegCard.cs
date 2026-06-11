using System;
using Godot;
using UI.Wireframe;

namespace UI.OrbitalScheduling;

/// <summary>
/// Train-ticket styled card for one leg in the schedule list. Layout lives in
/// <c>LegCard.tscn</c>; this script binds data and applies the validity-driven
/// panel stylebox at runtime. Clicking a control edits / reorders / deletes the
/// leg. Instantiate via <see cref="Create"/>.
/// </summary>
public sealed partial class LegCard : PanelContainer
{
    public event Action<int>? EditRequested;
    public event Action<int>? MoveUpRequested;
    public event Action<int>? MoveDownRequested;
    public event Action<int>? DeleteRequested;

    [Export] private Label _kicker = null!;
    [Export] private Label _num = null!;
    [Export] private Label _route = null!;
    [Export] private Label _cargo = null!;
    [Export] private Label _fuel = null!;
    [Export] private Label _status = null!;
    [Export] private VBoxContainer _buttonRows = null!;
    [Export] private Label _note = null!;
    [Export] private Button _up = null!;
    [Export] private Button _down = null!;
    [Export] private Button _edit = null!;
    [Export] private Button _del = null!;

    private LegCardData _data = new();

    private static PackedScene? _scene;

    public static LegCard Create()
    {
        _scene ??= GD.Load<PackedScene>("res://UI/OrbitalScheduling/LegCard.tscn");
        return _scene.Instantiate<LegCard>();
    }

    public void Bind(LegCardData data)
    {
        _data = data;
        AddThemeStyleboxOverride("panel", BuildStyle());

        _kicker.Text = data.IsClosingLeg ? "RTN" : "LEG";
        _num.Text = data.IsClosingLeg ? "↩" : (data.Index + 1).ToString("D2");
        _route.Text = $"{(data.IsCurrent ? "▶ " : "")}{data.OriginName}  →  {data.DestName}";
        _cargo.Text = $"CARGO · {data.ManifestSummary}";
        _fuel.Text = $"FUEL · {data.FuelSummary}    TIME · {data.TimingSummary}";

        _status.Text = data.IsValid ? $"OK · {data.StateText}" : $"ERROR · {data.InvalidReason}";
        _status.ThemeTypeVariation = data.IsValid ? "LabelOk" : "LabelAlert";

        _buttonRows.Visible = !data.IsClosingLeg;
        _note.Visible = data.IsClosingLeg;
    }

    private void OnUpPressed() => MoveUpRequested?.Invoke(_data.Index);
    private void OnDownPressed() => MoveDownRequested?.Invoke(_data.Index);
    private void OnEditPressed() => EditRequested?.Invoke(_data.Index);
    private void OnDelPressed() => DeleteRequested?.Invoke(_data.Index);

    private StyleBoxFlat BuildStyle()
    {
        Color bg = _data.IsCurrent
            ? new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.10f)
            : new Color(1f, 1f, 1f, 0.40f);
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = _data.IsValid ? WireColors.Ink : WireColors.Red,
            BorderWidthLeft = _data.IsCurrent ? 4 : 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ShadowColor = new Color(WireColors.Ink.R, WireColors.Ink.G, WireColors.Ink.B, 0.18f),
            ShadowOffset = new Vector2(2, 3),
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        };
    }
}
