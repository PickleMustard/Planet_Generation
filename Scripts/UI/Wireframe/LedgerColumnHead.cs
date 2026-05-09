using Godot;

namespace UI.Wireframe;

/// <summary>
/// Top-and-bottom-ruled header strip with column dividers. Use as the header of
/// a ledger-style table; widths are configured per column with stretch ratios.
/// </summary>
[GlobalClass]
public partial class LedgerColumnHead : PanelContainer
{
    public sealed class Col
    {
        public string Title = string.Empty;
        public float WidthPx;
        public float StretchRatio;
        public HorizontalAlignment Align = HorizontalAlignment.Left;
    }

    private Col[] _cols = System.Array.Empty<Col>();
    [Export] public bool Tall { get; set; }

    public void SetColumns(Col[] cols)
    {
        _cols = cols;
        Rebuild();
    }

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", BuildBg());
    }

    private void Rebuild()
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 0);
        AddChild(row);

        for (int i = 0; i < _cols.Length; i++)
        {
            var col = _cols[i];
            var cell = new MarginContainer();
            cell.AddThemeConstantOverride("margin_left", 8);
            cell.AddThemeConstantOverride("margin_right", 8);
            cell.AddThemeConstantOverride("margin_top", Tall ? 8 : 5);
            cell.AddThemeConstantOverride("margin_bottom", Tall ? 8 : 5);
            if (col.WidthPx > 0f)
            {
                cell.CustomMinimumSize = new Vector2(col.WidthPx, 0f);
                cell.SizeFlagsHorizontal = SizeFlags.Fill;
            }
            else
            {
                cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                cell.SizeFlagsStretchRatio = col.StretchRatio > 0f ? col.StretchRatio : 1f;
            }
            row.AddChild(cell);

            var label = new Label
            {
                Text = col.Title.ToUpperInvariant(),
                HorizontalAlignment = col.Align,
                ThemeTypeVariation = "LabelMono",
            };
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", WireColors.InkFaint);
            cell.AddChild(label);

            if (i < _cols.Length - 1)
            {
                var sep = new VSeparator
                {
                    CustomMinimumSize = new Vector2(1f, 0f),
                };
                row.AddChild(sep);
            }
        }
    }

    private static StyleBoxFlat BuildBg()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.04f),
            BorderColor = WireColors.Ink,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
        };
    }
}
