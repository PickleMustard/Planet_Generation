#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Enums;
using Structures.Resources;

namespace DeveloperTools.BiomeEditor.Tabs;

/// <summary>
/// Subtype + biome selectors driving a horizontal bar chart of resource weights, sorted descending.
/// Pulls from in-memory model edits so unsaved changes preview live.
/// </summary>
public partial class ResourceHeatmapTab : Control
{
    private BiomeEditorModel? _model;
    private OptionButton _subtypeOpt = null!;
    private OptionButton _biomeOpt = null!;
    private BarChart _chart = null!;

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
        _model.BiomeWeightsChanged += _ => CallDeferred(nameof(Refresh));
        _model.SubtypeWeightsChanged += _ => CallDeferred(nameof(Refresh));
        Refresh();
    }

    private void BuildLayout()
    {
        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(root);

        var ctrlRow = new HBoxContainer();
        ctrlRow.AddChild(new Label { Text = "Subtype:" });
        _subtypeOpt = new OptionButton();
        foreach (var s in Enum.GetNames<RockyPlanetSubtype>()) _subtypeOpt.AddItem(s);
        _subtypeOpt.ItemSelected += _ => Refresh();
        ctrlRow.AddChild(_subtypeOpt);

        ctrlRow.AddChild(new Label { Text = "Biome:" });
        _biomeOpt = new OptionButton();
        foreach (var b in Enum.GetNames<Biome.BiomeType>()) _biomeOpt.AddItem(b);
        _biomeOpt.ItemSelected += _ => Refresh();
        ctrlRow.AddChild(_biomeOpt);
        root.AddChild(ctrlRow);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);

        _chart = new BarChart { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 400) };
        scroll.AddChild(_chart);
    }

    public void Refresh()
    {
        if (_model == null) return;

        var subtype = (RockyPlanetSubtype)_subtypeOpt.Selected;
        var biome = (Biome.BiomeType)_biomeOpt.Selected;

        var subtypeCfg = BuildSubtypeConfig(_model, subtype);
        var biomeEntry = BuildBiomeEntry(_model, biome);

        var weights = ResourceWeightSampler.ForBiome(subtypeCfg, biomeEntry);
        _chart.SetData(weights);
    }

    private static SubtypeResourceConfig BuildSubtypeConfig(BiomeEditorModel model, RockyPlanetSubtype subtype)
    {
        var cfg = new SubtypeResourceConfig { Subtype = subtype.ToString() };
        if (model.SubtypeResources.TryGetValue(subtype, out var e))
        {
            cfg.BaseResourceWeight = e.BaseResourceWeight;
            cfg.ResourceGroups = new List<string>(e.ResourceGroups);
            cfg.AddResources = new List<string>(e.AddResources);
            cfg.RemoveResources = new List<string>(e.RemoveResources);
        }
        var groupDict = new Dictionary<string, List<string>>();
        foreach (var g in model.ResourceGroups)
            groupDict[g.GroupName] = new List<string>(g.ResourceIds);
        cfg.ResolveResources(groupDict);
        return cfg;
    }

    private static BiomeResourceEntry BuildBiomeEntry(BiomeEditorModel model, Biome.BiomeType biome)
    {
        var entry = new BiomeResourceEntry { Biome = biome };
        if (model.BiomeResources.TryGetValue(biome, out var e))
            entry.ResourceWeightModifiers = new Dictionary<string, float>(e.Weights);
        return entry;
    }

    public partial class BarChart : Control
    {
        private Dictionary<string, float> _data = new();

        public void SetData(Dictionary<string, float> data)
        {
            _data = data;
            QueueRedraw();
            CustomMinimumSize = new Vector2(CustomMinimumSize.X, Math.Max(400, data.Count * 22 + 20));
        }

        public override void _Draw()
        {
            if (_data.Count == 0)
            {
                DrawString(ThemeDB.FallbackFont, new Vector2(10, 20), "No resources in pool", HorizontalAlignment.Left, -1, 14, Colors.Gray);
                return;
            }

            var ordered = _data.OrderByDescending(kv => kv.Value).ToList();
            float maxValue = ordered[0].Value;
            if (maxValue <= 0) maxValue = 1f;

            float barHeight = 18f;
            float rowSpacing = 4f;
            float labelWidth = 140f;
            float chartLeft = labelWidth;
            float chartWidth = Mathf.Max(50f, Size.X - chartLeft - 60f);

            for (int i = 0; i < ordered.Count; i++)
            {
                var kvp = ordered[i];
                float y = i * (barHeight + rowSpacing) + 4f;
                DrawString(ThemeDB.FallbackFont, new Vector2(4, y + 14), kvp.Key, HorizontalAlignment.Left, labelWidth - 4, 12, Colors.White);
                float w = (kvp.Value / maxValue) * chartWidth;
                DrawRect(new Rect2(chartLeft, y, w, barHeight), new Color(0.35f, 0.7f, 0.95f, 0.85f));
                DrawString(ThemeDB.FallbackFont, new Vector2(chartLeft + w + 4, y + 14), kvp.Value.ToString("0.##"), HorizontalAlignment.Left, -1, 12, Colors.White);
            }
        }
    }
}
#endif
