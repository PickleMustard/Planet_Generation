#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ProceduralGeneration.BiomeSystem;
using ProceduralGeneration.ColorSystem;
using Structures.Enums;
using Structures.Resources;

namespace DeveloperTools.BiomeEditor.Tabs;

/// <summary>
/// Whittaker-style preview: 256x256 grid where (x, y) = (height, moisture); pixel colour
/// = biome produced by the selected subtype's rule list with current latitude slider value.
/// </summary>
public partial class WhittakerTab : Control
{
    private const int GridSize = 256;

    private BiomeEditorModel? _model;
    private OptionButton _subtypeOpt = null!;
    private HSlider _latSlider = null!;
    private Label _latLabel = null!;
    private TextureRect _texture = null!;
    private VBoxContainer _legend = null!;

    public override void _Ready()
    {
        base._Ready();
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLayout();
    }

    public void Initialize(BiomeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _model.AssignerChanged += OnAssignerChanged;
        RegenerateImage();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_model != null) _model.AssignerChanged -= OnAssignerChanged;
    }

    private void BuildLayout()
    {
        var root = new HBoxContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(root);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(left);

        var ctrlRow = new HBoxContainer();
        ctrlRow.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Subtype:" });
        _subtypeOpt = new OptionButton();
        foreach (var id in GetRockySubtypeIds())
            _subtypeOpt.AddItem(id);
        _subtypeOpt.ItemSelected += _ => RegenerateImage();
        ctrlRow.AddChild(_subtypeOpt);

        ctrlRow.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Latitude:" });
        _latSlider = new HSlider
        {
            MinValue = -1.0,
            MaxValue = 1.0,
            Step = 0.05,
            Value = 0.0,
            CustomMinimumSize = new Vector2(200, 0),
        };
        _latSlider.ValueChanged += _ => RegenerateImage();
        ctrlRow.AddChild(_latSlider);
        _latLabel = new Label { ThemeTypeVariation = "LabelHighContrast", Text = "0.00" };
        ctrlRow.AddChild(_latLabel);
        left.AddChild(ctrlRow);

        _texture = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(GridSize, GridSize),
        };
        left.AddChild(_texture);

        var axisRow = new HBoxContainer();
        axisRow.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "X = height (0→1)    Y = moisture (0→max)" });
        left.AddChild(axisRow);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(220, 0),
        };
        root.AddChild(scroll);
        _legend = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_legend);
    }

    private List<string> GetRockySubtypeIds()
    {
        if (_model != null)
            return _model.Subtypes.Values
                .Where(s => s.Family == BodyFamily.RockyPlanet)
                .Select(s => s.Id)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
        // Fallback when called pre-Initialize: derive from enum so dropdown isn't empty.
        return Enum.GetValues<RockyPlanetSubtype>()
            .Select(BiomeIdMapper.RockyPlanetSubtypeToId)
            .ToList();
    }

    private string SelectedSubtypeId =>
        _subtypeOpt.Selected >= 0 && _subtypeOpt.Selected < _subtypeOpt.ItemCount
            ? _subtypeOpt.GetItemText(_subtypeOpt.Selected)
            : "";

    private void OnAssignerChanged(RockyPlanetSubtype subtype)
    {
        if (BiomeIdMapper.RockyPlanetSubtypeToId(subtype) != SelectedSubtypeId) return;
        CallDeferred(nameof(RegenerateImage));
    }

    private void RegenerateImage()
    {
        if (_model == null) return;
        _latLabel.Text = ((float)_latSlider.Value).ToString("0.00");

        string subId = SelectedSubtypeId;
        var subtypeEnum = BiomeIdMapper.IdToRockyPlanetSubtype(subId);
        if (subtypeEnum == null || !_model.Assigners.TryGetValue(subtypeEnum.Value, out var entry)) return;
        var subtype = subtypeEnum.Value;

        float lat = (float)_latSlider.Value;
        float maxM = entry.Moisture.MaxMoisture > 0.01f ? entry.Moisture.MaxMoisture : 1f;

        var image = Image.CreateEmpty(GridSize, GridSize, false, Image.Format.Rgb8);
        var biomesSeen = new HashSet<string>();

        for (int x = 0; x < GridSize; x++)
        {
            float h = x / (float)(GridSize - 1);
            for (int y = 0; y < GridSize; y++)
            {
                float m = (y / (float)(GridSize - 1)) * maxM;
                var biome = EvaluateRules(entry.Rules, h, m, lat);
                biomesSeen.Add(biome);
                image.SetPixel(x, GridSize - 1 - y, BiomeColor(biome));
            }
        }
        _texture.Texture = ImageTexture.CreateFromImage(image);

        RebuildLegend(subtype, biomesSeen);
    }

    private void RebuildLegend(RockyPlanetSubtype subtype, HashSet<string> biomes)
    {
        foreach (var c in _legend.GetChildren()) c.QueueFree();
        var header = new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Legend" };
        header.AddThemeFontSizeOverride("font_size", 14);
        _legend.AddChild(header);

        var atm = _model!.AtmosphereTemplates.TryGetValue("RockyPlanet", out var t)
            ? (t.AtmosphereMin, t.AtmosphereMax) : (0f, 1f);

        foreach (var biomeId in biomes.OrderBy(b => b, StringComparer.Ordinal))
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var swatch = new ColorRect { Color = BiomeColor(biomeId), CustomMinimumSize = new Vector2(16, 16) };
            row.AddChild(swatch);
            row.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = biomeId, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            float hz = HazardCalculator.Compute(subtype, atm.Item1, atm.Item2, biomeId);
            var hzLabel = new Label { Text = $"hz {hz:0.0}" };
            hzLabel.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.6f));
            row.AddChild(hzLabel);
            _legend.AddChild(row);
        }
    }

    public static string EvaluateRules(List<BiomeRule> rules, float h, float m, float lat)
    {
        float absLat = Mathf.Abs(lat);
        foreach (var rule in rules)
        {
            var w = rule.When;
            if (w.HeightAbove.HasValue && h < w.HeightAbove.Value) continue;
            if (w.HeightBelow.HasValue && h > w.HeightBelow.Value) continue;
            if (w.MoistureAbove.HasValue && m < w.MoistureAbove.Value) continue;
            if (w.MoistureBelow.HasValue && m > w.MoistureBelow.Value) continue;
            if (w.AbsLatitudeAbove.HasValue && absLat < w.AbsLatitudeAbove.Value) continue;
            if (w.AbsLatitudeBelow.HasValue && absLat > w.AbsLatitudeBelow.Value) continue;
            return rule.Biome;
        }
        return rules.Count > 0 ? rules[^1].Biome : "biome_grassland";
    }

    private static Color BiomeColor(string biomeId)
    {
        // Stable hashed palette from biome ID string.
        int hash = biomeId.GetHashCode();
        float hue = (Mathf.Abs(hash) % 360) / 360f;
        return Color.FromHsv(hue, 0.65f, 0.85f);
    }
}
#endif
