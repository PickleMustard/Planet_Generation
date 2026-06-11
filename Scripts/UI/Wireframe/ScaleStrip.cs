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

    [Export] private ScaleBalance? _balance;
    [Export] private Label? _readout;
    [Export] private ProgressBar? _bar;
    [Export] private Label? _overLabel;

    private static PackedScene? _scene;

    public static ScaleStrip Create()
    {
        _scene ??= GD.Load<PackedScene>("res://UI/Wireframe/ScaleStrip.tscn");
        return _scene.Instantiate<ScaleStrip>();
    }

    public override void _Ready()
    {
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
