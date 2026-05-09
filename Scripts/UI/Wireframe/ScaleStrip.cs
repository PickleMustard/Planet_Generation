using Godot;

namespace UI.Wireframe;

/// <summary>
/// Compact horizontal weight readout: <see cref="ScaleBalance"/> on the left,
/// numeric / progress bar on the right. Designed for the Manifest Editor footer.
/// </summary>
[GlobalClass]
public partial class ScaleStrip : HBoxContainer
{
    private float _load;
    private float _limit = 1200f;

    [Export]
    public float Load
    {
        get => _load;
        set { _load = value; Refresh(); }
    }

    [Export]
    public float Limit
    {
        get => _limit;
        set { _limit = Mathf.Max(1f, value); Refresh(); }
    }

    private ScaleBalance? _balance;
    private Label? _readout;
    private ProgressBar? _bar;
    private Label? _overLabel;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 14);
        CustomMinimumSize = new Vector2(0f, 86f);

        _balance = new ScaleBalance
        {
            Load = _load,
            Limit = _limit,
            CustomMinimumSize = new Vector2(170f, 86f),
        };
        AddChild(_balance);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 4);
        AddChild(right);

        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 8);
        right.AddChild(topRow);

        var caption = new Label
        {
            Text = "TOTAL WEIGHT",
            ThemeTypeVariation = "LabelMono",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        caption.AddThemeFontSizeOverride("font_size", 11);
        caption.AddThemeColorOverride("font_color", WireColors.InkFaint);
        topRow.AddChild(caption);

        _readout = new Label { ThemeTypeVariation = "LabelMono" };
        _readout.AddThemeFontSizeOverride("font_size", 13);
        topRow.AddChild(_readout);

        _bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.001,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0f, 10f),
        };
        right.AddChild(_bar);

        _overLabel = new Label
        {
            Text = "OVER LIMIT",
            ThemeTypeVariation = "LabelMono",
            Visible = false,
        };
        _overLabel.AddThemeFontSizeOverride("font_size", 10);
        _overLabel.AddThemeColorOverride("font_color", WireColors.Red);
        right.AddChild(_overLabel);

        Refresh();
    }

    private void Refresh()
    {
        if (_balance != null)
        {
            _balance.Load = _load;
            _balance.Limit = _limit;
        }
        if (_readout != null)
        {
            bool over = _load > _limit;
            _readout.Text = $"{_load:0.#} / {_limit:0} t";
            _readout.AddThemeColorOverride("font_color", over ? WireColors.Red : WireColors.Ink);
        }
        if (_bar != null)
        {
            _bar.Value = Mathf.Clamp(_load / _limit, 0f, 1f);
            var fill = new StyleBoxFlat
            {
                BgColor = _load > _limit ? WireColors.Red : WireColors.Orange,
                BorderColor = WireColors.Ink,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
            };
            var bg = new StyleBoxFlat
            {
                BgColor = new Color(WireColors.Paper.R, WireColors.Paper.G, WireColors.Paper.B, 0.6f),
                BorderColor = WireColors.Ink,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
            };
            _bar.AddThemeStyleboxOverride("fill", fill);
            _bar.AddThemeStyleboxOverride("background", bg);
        }
        if (_overLabel != null)
            _overLabel.Visible = _load > _limit;
    }
}
