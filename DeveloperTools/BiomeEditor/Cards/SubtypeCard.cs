#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Structures.Resources;

namespace DeveloperTools.BiomeEditor.Cards;

/// <summary>
/// Per-subtype editor card. Lets developer edit display name, atmosphere range,
/// base hazard, base resource weight, moisture mode/params, assigner rules (biome id +
/// optional conditions) and resource group / add / remove lists. Family is read-only
/// after creation. Includes rename + delete (with cascade reference scan).
/// </summary>
public partial class SubtypeCard : PanelContainer
{
    private BiomeEditorModel _model = null!;
    private string _subtypeId = "";

    [Export] private Label _titleLabel = null!;
    [Export] private LineEdit _displayEdit = null!;
    [Export] private Label _familyLabel = null!;
    [Export] private SpinBox _atmMinSpin = null!;
    [Export] private SpinBox _atmMaxSpin = null!;
    [Export] private SpinBox _hazardSpin = null!;
    [Export] private SpinBox _baseWeightSpin = null!;
    [Export] private OptionButton _moistureModeOpt = null!;
    [Export] private VBoxContainer _rulesList = null!;
    [Export] private LineEdit _groupsEdit = null!;
    [Export] private LineEdit _addResEdit = null!;
    [Export] private LineEdit _remResEdit = null!;

    private static PackedScene? _scene;

    public static SubtypeCard Create(BiomeEditorModel model, string subtypeId)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/BiomeEditor/Cards/SubtypeCard.tscn");
        var card = _scene.Instantiate<SubtypeCard>();
        card.Initialize(model, subtypeId);
        return card;
    }

    public void Initialize(BiomeEditorModel model, string subtypeId)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _subtypeId = subtypeId;
    }

    public override void _Ready()
    {
        base._Ready();
        _displayEdit.TextSubmitted += s => CommitDisplay(s);
        _displayEdit.FocusExited += () => CommitDisplay(_displayEdit.Text);
        _atmMinSpin.ValueChanged += v => CommitAtmosphere();
        _atmMaxSpin.ValueChanged += v => CommitAtmosphere();
        _hazardSpin.ValueChanged += v => CommitHazard((float)v);
        _baseWeightSpin.ValueChanged += v => CommitBaseWeight((float)v);
        _moistureModeOpt.AddItem("whittaker");
        _moistureModeOpt.AddItem("uniform_random");
        _moistureModeOpt.ItemSelected += _ => CommitMoistureMode();
        _groupsEdit.TextSubmitted += _ => CommitCsvLists();
        _groupsEdit.FocusExited += () => CommitCsvLists();
        _addResEdit.TextSubmitted += _ => CommitCsvLists();
        _addResEdit.FocusExited += () => CommitCsvLists();
        _remResEdit.TextSubmitted += _ => CommitCsvLists();
        _remResEdit.FocusExited += () => CommitCsvLists();

        _model.SubtypeDefinitionChanged += OnDefChanged;
        Refresh();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _model.SubtypeDefinitionChanged -= OnDefChanged;
    }

    private void OnDefChanged(string id)
    {
        if (id != _subtypeId) return;
        CallDeferred(nameof(Refresh));
    }

    private void Refresh()
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var s)) return;
        _titleLabel.Text = _subtypeId;
        _displayEdit.Text = s.DisplayName;
        _familyLabel.Text = s.Family.ToString();
        _atmMinSpin.SetValueNoSignal(s.AtmosphereMin);
        _atmMaxSpin.SetValueNoSignal(s.AtmosphereMax);
        _hazardSpin.SetValueNoSignal(s.BaseHazard);
        _baseWeightSpin.SetValueNoSignal(s.BaseResourceWeight);
        _moistureModeOpt.Selected = s.MoistureMode == "uniform_random" ? 1 : 0;
        _groupsEdit.Text = string.Join(", ", s.ResourceGroups);
        _addResEdit.Text = string.Join(", ", s.AddResources);
        _remResEdit.Text = string.Join(", ", s.RemoveResources);

        foreach (var c in _rulesList.GetChildren()) c.QueueFree();
        for (int i = 0; i < s.AssignerRules.Count; i++)
        {
            int idx = i;
            var rule = s.AssignerRules[i];
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = $"{idx}:", CustomMinimumSize = new Vector2(28, 0) });
            var biomeEdit = new LineEdit { Text = rule.BiomeId, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            biomeEdit.FocusExited += () => CommitRuleBiome(idx, biomeEdit.Text);
            biomeEdit.TextSubmitted += t => CommitRuleBiome(idx, t);
            row.AddChild(biomeEdit);
            var condEdit = new LineEdit
            {
                Text = SerializeWhen(rule),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                PlaceholderText = "height_above=0.4,moisture_below=0.2,...",
            };
            condEdit.FocusExited += () => CommitRuleCond(idx, condEdit.Text);
            condEdit.TextSubmitted += t => CommitRuleCond(idx, t);
            row.AddChild(condEdit);
            var del = new Button { Text = "✕" };
            del.Pressed += () => RemoveRule(idx);
            row.AddChild(del);
            _rulesList.AddChild(row);
        }
    }

    // ── Commit helpers ──────────────────────────────────────────────────

    private void CommitDisplay(string s)
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var sub)) return;
        if (sub.DisplayName == s) return;
        sub.DisplayName = s;
        _model.UpdateSubtype(sub);
    }

    private void CommitAtmosphere()
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var sub)) return;
        sub.AtmosphereMin = (float)_atmMinSpin.Value;
        sub.AtmosphereMax = (float)_atmMaxSpin.Value;
        _model.UpdateSubtype(sub);
    }

    private void CommitHazard(float v)
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var sub)) return;
        sub.BaseHazard = v;
        _model.UpdateSubtype(sub);
    }

    private void CommitBaseWeight(float v)
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var sub)) return;
        sub.BaseResourceWeight = v;
        _model.UpdateSubtype(sub);
    }

    private void CommitMoistureMode()
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var sub)) return;
        sub.MoistureMode = _moistureModeOpt.Selected == 1 ? "uniform_random" : "whittaker";
        _model.UpdateSubtype(sub);
    }

    private void CommitCsvLists()
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var sub)) return;
        sub.ResourceGroups = ParseCsv(_groupsEdit.Text);
        sub.AddResources = ParseCsv(_addResEdit.Text);
        sub.RemoveResources = ParseCsv(_remResEdit.Text);
        _model.UpdateSubtype(sub);
    }

    private static List<string> ParseCsv(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries)
         .Select(t => t.Trim())
         .Where(t => t.Length > 0)
         .ToList();

    private void OnAddRule()
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var sub)) return;
        sub.AssignerRules.Add(new BiomeRuleDefinition { BiomeId = "" });
        _model.UpdateSubtype(sub);
    }

    private void RemoveRule(int idx)
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var sub)) return;
        if (idx < 0 || idx >= sub.AssignerRules.Count) return;
        sub.AssignerRules.RemoveAt(idx);
        _model.UpdateSubtype(sub);
    }

    private void CommitRuleBiome(int idx, string biomeId)
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var sub)) return;
        if (idx < 0 || idx >= sub.AssignerRules.Count) return;
        sub.AssignerRules[idx].BiomeId = biomeId.Trim();
        _model.UpdateSubtype(sub);
    }

    private void CommitRuleCond(int idx, string text)
    {
        if (!_model.Subtypes.TryGetValue(_subtypeId, out var sub)) return;
        if (idx < 0 || idx >= sub.AssignerRules.Count) return;
        var rule = sub.AssignerRules[idx];
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
        _model.UpdateSubtype(sub);
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

    private void OnRename()
    {
        var lineEdit = new LineEdit
        {
            Text = _subtypeId,
            PlaceholderText = "new subtype id",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var dialog = new ConfirmationDialog
        {
            Title = "Rename subtype",
            DialogText = $"Rename '{_subtypeId}'. References in biome color overrides will be updated.",
        };
        dialog.AddChild(lineEdit);
        dialog.Confirmed += () =>
        {
            string newId = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(newId) || newId == _subtypeId) return;
            if (!_model.RenameSubtype(_subtypeId, newId)) return;
            _subtypeId = newId;
            CallDeferred(nameof(Refresh));
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(440, 180));
    }

    private void OnDelete()
    {
        var refs = _model.FindSubtypeReferences(_subtypeId);
        string text = refs.Count == 0
            ? $"Delete subtype '{_subtypeId}'?"
            : $"'{_subtypeId}' is referenced by:\n  - " + string.Join("\n  - ", refs)
              + "\n\nCascade delete will also remove these references.";
        var dialog = new ConfirmationDialog { Title = "Delete subtype", DialogText = text };
        dialog.GetOkButton().Text = "Cascade delete";
        dialog.Confirmed += () => _model.RemoveSubtype(_subtypeId, cascade: true);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(480, 220));
    }
}
#endif
