using System;
using Godot;
using UI.Wireframe;

namespace UI.OrbitalScheduling;

/// <summary>
/// Train-ticket styled card for one leg in the schedule list. Composed of paper
/// styling, an origin→destination header, manifest/fuel/timing sub-lines, a
/// validation badge, and reorder / delete / edit controls. Clicking the body of
/// the card edits the leg.
/// </summary>
public sealed partial class LegCard : PanelContainer
{
    public event Action<int>? EditRequested;
    public event Action<int>? MoveUpRequested;
    public event Action<int>? MoveDownRequested;
    public event Action<int>? DeleteRequested;

    private LegCardData _data = new();

    public void Bind(LegCardData data)
    {
        _data = data;
        foreach (var c in GetChildren())
            c.QueueFree();
        Build();
    }

    private void Build()
    {
        AddThemeStyleboxOverride("panel", BuildStyle());
        MouseFilter = MouseFilterEnum.Stop;

        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);
        AddChild(row);

        // Left: leg index / number stub (the ticket "stub").
        var stub = new VBoxContainer { CustomMinimumSize = new Vector2(54, 0) };
        stub.AddThemeConstantOverride("separation", 0);
        row.AddChild(stub);
        var kicker = new Label
        {
            Text = _data.IsClosingLeg ? "RTN" : $"LEG",
            ThemeTypeVariation = "LabelMono",
        };
        kicker.AddThemeFontSizeOverride("font_size", 9);
        kicker.AddThemeColorOverride("font_color", WireColors.InkFaint);
        stub.AddChild(kicker);
        var num = new Label
        {
            Text = _data.IsClosingLeg ? "↩" : (_data.Index + 1).ToString("D2"),
            ThemeTypeVariation = "LabelHand",
        };
        num.AddThemeFontSizeOverride("font_size", 28);
        stub.AddChild(num);

        // Middle: route + details.
        var mid = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mid.AddThemeConstantOverride("separation", 2);
        row.AddChild(mid);

        var routeLabel = new Label
        {
            Text = $"{(_data.IsCurrent ? "▶ " : "")}{_data.OriginName}  →  {_data.DestName}",
            ThemeTypeVariation = "LabelHand",
        };
        routeLabel.AddThemeFontSizeOverride("font_size", 20);
        mid.AddChild(routeLabel);

        mid.AddChild(SubLine($"CARGO · {_data.ManifestSummary}"));
        mid.AddChild(SubLine($"FUEL · {_data.FuelSummary}    TIME · {_data.TimingSummary}"));

        var status = new Label
        {
            Text = _data.IsValid ? $"OK · {_data.StateText}" : $"ERROR · {_data.InvalidReason}",
            ThemeTypeVariation = "LabelMono",
        };
        status.AddThemeFontSizeOverride("font_size", 10);
        status.AddThemeColorOverride("font_color", _data.IsValid ? WireColors.Green : WireColors.Red);
        mid.AddChild(status);

        // Right: controls (closing leg has none — it is auto-managed).
        var controls = new VBoxContainer();
        controls.AddThemeConstantOverride("separation", 4);
        controls.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddChild(controls);

        if (!_data.IsClosingLeg)
        {
            var topRow = new HBoxContainer();
            topRow.AddThemeConstantOverride("separation", 4);
            controls.AddChild(topRow);

            var up = new Button { Text = "↑", TooltipText = "Move earlier" };
            up.Pressed += () => MoveUpRequested?.Invoke(_data.Index);
            topRow.AddChild(up);

            var down = new Button { Text = "↓", TooltipText = "Move later" };
            down.Pressed += () => MoveDownRequested?.Invoke(_data.Index);
            topRow.AddChild(down);

            var bottomRow = new HBoxContainer();
            bottomRow.AddThemeConstantOverride("separation", 4);
            controls.AddChild(bottomRow);

            var edit = new Button { Text = "Edit", ThemeTypeVariation = "ButtonPrimary" };
            edit.Pressed += () => EditRequested?.Invoke(_data.Index);
            bottomRow.AddChild(edit);

            var del = new Button { Text = "✕", ThemeTypeVariation = "ButtonDanger" };
            del.Pressed += () => DeleteRequested?.Invoke(_data.Index);
            bottomRow.AddChild(del);
        }
        else
        {
            var note = new Label
            {
                Text = "auto return",
                ThemeTypeVariation = "LabelMono",
            };
            note.AddThemeFontSizeOverride("font_size", 9);
            note.AddThemeColorOverride("font_color", WireColors.InkFaint);
            controls.AddChild(note);
        }
    }

    private static Label SubLine(string text)
    {
        var lbl = new Label { Text = text, ThemeTypeVariation = "LabelMono" };
        lbl.AddThemeFontSizeOverride("font_size", 11);
        lbl.AddThemeColorOverride("font_color", WireColors.InkSoft);
        return lbl;
    }

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
