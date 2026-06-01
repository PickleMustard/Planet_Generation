using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Structures.Resources;

namespace UI.SubtypeEditor;

/// <summary>
/// Biome assignment rules editor. Each rule has a biome_id and optional condition keys.
/// Layout defined in BiomeAssignmentTab.tscn; rule rows added dynamically.
/// </summary>
public partial class BiomeAssignmentTab : ScrollContainer
{
    private SubtypeEditorModel _model = null!;
    private string? _subtypeId;

    [Export] private Button? _addRuleButton;
    [Export] private VBoxContainer? _rulesList;

    public void Initialize(SubtypeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    public override void _Ready()
    {
        if (_addRuleButton != null)
            _addRuleButton.Pressed += OnAddRule;
    }

    public void SetSubtype(string? subtypeId)
    {
        _subtypeId = subtypeId;
        Refresh();
    }

    private void Refresh()
    {
        if (_rulesList == null) return;
        foreach (var c in _rulesList.GetChildren()) c.QueueFree();
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;

        for (int i = 0; i < def.AssignerRules.Count; i++)
        {
            int idx = i;
            var rule = def.AssignerRules[i];
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = $"{idx}:", CustomMinimumSize = new Vector2(28, 0) });

            var biomeEdit = new LineEdit { Text = rule.BiomeId, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            biomeEdit.FocusExited += () => CommitBiome(idx, biomeEdit.Text);
            biomeEdit.TextSubmitted += t => CommitBiome(idx, t);
            row.AddChild(biomeEdit);

            var condEdit = new LineEdit
            {
                Text = SerializeWhen(rule),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                PlaceholderText = "height_above=0.4,moisture_below=0.2,...",
            };
            condEdit.FocusExited += () => CommitCondition(idx, condEdit.Text);
            condEdit.TextSubmitted += t => CommitCondition(idx, t);
            row.AddChild(condEdit);

            var del = new Button { Text = "✕" };
            del.Pressed += () => RemoveRule(idx);
            row.AddChild(del);

            _rulesList.AddChild(row);
        }
    }

    private void OnAddRule()
    {
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        def.AssignerRules.Add(new BiomeRuleDefinition { BiomeId = "" });
        _model.MarkDirty(_subtypeId);
        Refresh();
    }

    private void RemoveRule(int idx)
    {
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        if (idx < 0 || idx >= def.AssignerRules.Count) return;
        def.AssignerRules.RemoveAt(idx);
        _model.MarkDirty(_subtypeId);
        Refresh();
    }

    private void CommitBiome(int idx, string text)
    {
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        if (idx < 0 || idx >= def.AssignerRules.Count) return;
        def.AssignerRules[idx].BiomeId = text.Trim();
        _model.MarkDirty(_subtypeId);
    }

    private void CommitCondition(int idx, string text)
    {
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        if (idx < 0 || idx >= def.AssignerRules.Count) return;
        var rule = def.AssignerRules[idx];
        rule.HeightAbove = rule.HeightBelow = rule.MoistureAbove = rule.MoistureBelow = null;
        rule.AbsLatitudeAbove = rule.AbsLatitudeBelow = null;
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            string key = kv[0].Trim();
            if (!float.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) continue;
            switch (key)
            {
                case "height_above": rule.HeightAbove = v; break;
                case "height_below": rule.HeightBelow = v; break;
                case "moisture_above": rule.MoistureAbove = v; break;
                case "moisture_below": rule.MoistureBelow = v; break;
                case "abs_latitude_above": rule.AbsLatitudeAbove = v; break;
                case "abs_latitude_below": rule.AbsLatitudeBelow = v; break;
            }
        }
        _model.MarkDirty(_subtypeId);
    }

    private static string SerializeWhen(BiomeRuleDefinition r)
    {
        var parts = new List<string>();
        if (r.HeightAbove.HasValue) parts.Add($"height_above={r.HeightAbove.Value.ToString(CultureInfo.InvariantCulture)}");
        if (r.HeightBelow.HasValue) parts.Add($"height_below={r.HeightBelow.Value.ToString(CultureInfo.InvariantCulture)}");
        if (r.MoistureAbove.HasValue) parts.Add($"moisture_above={r.MoistureAbove.Value.ToString(CultureInfo.InvariantCulture)}");
        if (r.MoistureBelow.HasValue) parts.Add($"moisture_below={r.MoistureBelow.Value.ToString(CultureInfo.InvariantCulture)}");
        if (r.AbsLatitudeAbove.HasValue) parts.Add($"abs_latitude_above={r.AbsLatitudeAbove.Value.ToString(CultureInfo.InvariantCulture)}");
        if (r.AbsLatitudeBelow.HasValue) parts.Add($"abs_latitude_below={r.AbsLatitudeBelow.Value.ToString(CultureInfo.InvariantCulture)}");
        return string.Join(", ", parts);
    }
}