#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using DeveloperTools.Common;

namespace DeveloperTools.BiomeEditor.Cards;

/// <summary>
/// Per-biome editor card. Lets developer set display name, default color,
/// per-subtype color overrides, hazard weight, geothermal vent probability,
/// tags, and resource weight modifiers — plus delete the biome (with cascade
/// confirmation if references exist).
/// </summary>
public partial class BiomeCard : PanelContainer
{
    private BiomeEditorModel _model = null!;
    private string _biomeId = "";

    [Export] private Label _titleLabel = null!;
    [Export] private LineEdit _displayEdit = null!;
    [Export] private ColorPickerButton _defaultColorBtn = null!;
    [Export] private VBoxContainer _overridesList = null!;
    [Export] private SpinBox _hazardSpin = null!;
    [Export] private SpinBox _ventSpin = null!;
    [Export] private LineEdit _tagsEdit = null!;
    [Export] private VBoxContainer _weightsList = null!;

    private static PackedScene? _scene;

    public static BiomeCard Create(BiomeEditorModel model, string biomeId)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/BiomeEditor/Cards/BiomeCard.tscn");
        var card = _scene.Instantiate<BiomeCard>();
        card.Initialize(model, biomeId);
        return card;
    }

    public void Initialize(BiomeEditorModel model, string biomeId)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _biomeId = biomeId;
    }

    public override void _Ready()
    {
        base._Ready();
        _displayEdit.TextSubmitted += s => CommitDisplay(s);
        _displayEdit.FocusExited += () => CommitDisplay(_displayEdit.Text);
        _defaultColorBtn.ColorChanged += c => CommitDefaultColor(c);
        _hazardSpin.ValueChanged += v => CommitHazard((float)v);
        _ventSpin.ValueChanged += v => CommitVent((float)v);
        _tagsEdit.TextSubmitted += s => CommitTags(s);
        _tagsEdit.FocusExited += () => CommitTags(_tagsEdit.Text);

        _model.BiomeDefinitionChanged += OnDefChanged;
        Refresh();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _model.BiomeDefinitionChanged -= OnDefChanged;
    }

    private void OnDefChanged(string id)
    {
        if (id != _biomeId)
            return;
        CallDeferred(nameof(Refresh));
    }

    private void Refresh()
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b))
            return;
        _titleLabel.Text = _biomeId;
        _displayEdit.Text = b.DisplayName;
        _defaultColorBtn.Color = b.DefaultColor;
        _hazardSpin.SetValueNoSignal(b.HazardWeight);
        _ventSpin.SetValueNoSignal(b.GeothermalVentProbability);
        _tagsEdit.Text = string.Join(", ", b.Tags);

        foreach (var c in _overridesList.GetChildren())
            c.QueueFree();
        foreach (var kvp in b.ColorOverrides.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            string subId = kvp.Key;
            Color color = kvp.Value;
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(
                new Label
                {
                    ThemeTypeVariation = "LabelHighContrast",
                    Text = subId,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                }
            );
            var pick = new ColorPickerButton
            {
                Color = color,
                CustomMinimumSize = new Vector2(100, 22),
                EditAlpha = false,
            };
            pick.ColorChanged += c2 => CommitOverride(subId, c2);
            row.AddChild(pick);
            var del = new Button { Text = "✕" };
            del.Pressed += () => RemoveOverride(subId);
            row.AddChild(del);
            _overridesList.AddChild(row);
        }

        foreach (var c in _weightsList.GetChildren())
            c.QueueFree();
        foreach (var kvp in b.ResourceWeightModifiers.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            string resId = kvp.Key;
            float w = kvp.Value;
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(
                new Label
                {
                    ThemeTypeVariation = "LabelHighContrast",
                    Text = resId,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                }
            );
            var edit = new LineEdit
            {
                Text = w.ToString("0.##", CultureInfo.InvariantCulture),
                CustomMinimumSize = new Vector2(72, 0),
            };
            edit.TextSubmitted += s => CommitWeight(resId, s);
            edit.FocusExited += () => CommitWeight(resId, edit.Text);
            row.AddChild(edit);
            var del = new Button { Text = "✕" };
            del.Pressed += () => RemoveWeight(resId);
            row.AddChild(del);
            _weightsList.AddChild(row);
        }
    }

    // ── Commit helpers ──────────────────────────────────────────────────

    private void CommitDisplay(string s)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b))
            return;
        if (b.DisplayName == s)
            return;
        b.DisplayName = s;
        _model.UpdateBiome(b);
    }

    private void CommitDefaultColor(Color c)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b))
            return;
        b.DefaultColor = c;
        _model.UpdateBiome(b);
    }

    private void CommitOverride(string subId, Color c)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b))
            return;
        b.ColorOverrides[subId] = c;
        _model.UpdateBiome(b);
    }

    private void RemoveOverride(string subId)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b))
            return;
        if (b.ColorOverrides.Remove(subId))
            _model.UpdateBiome(b);
    }

    private void CommitHazard(float v)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b))
            return;
        b.HazardWeight = v;
        _model.UpdateBiome(b);
    }

    private void CommitVent(float v)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b))
            return;
        b.GeothermalVentProbability = v;
        _model.UpdateBiome(b);
    }

    private void CommitTags(string s)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b))
            return;
        b.Tags = s.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();
        _model.UpdateBiome(b);
    }

    private void CommitWeight(string resId, string s)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b))
            return;
        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return;
        b.ResourceWeightModifiers[resId] = v;
        _model.UpdateBiome(b);
    }

    private void RemoveWeight(string resId)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b))
            return;
        if (b.ResourceWeightModifiers.Remove(resId))
            _model.UpdateBiome(b);
    }

    // ── Add-override picker ─────────────────────────────────────────────

    private void OnAddOverride()
    {
        var lineEdit = new LineEdit
        {
            PlaceholderText = "subtype_<family>_<name>",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var dialog = new ConfirmationDialog
        {
            Title = "Add color override",
            DialogText = "Subtype id:",
        };
        dialog.AddChild(lineEdit);
        dialog.Confirmed += () =>
        {
            string id = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(id))
                return;
            if (!_model.Biomes.TryGetValue(_biomeId, out var b))
                return;
            if (b.ColorOverrides.ContainsKey(id))
                return;
            b.ColorOverrides[id] = b.DefaultColor;
            _model.UpdateBiome(b);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    private void OnAddWeight()
    {
        var popup = ResourcePickerPopup.Create();
        popup.ResourcePicked += id =>
        {
            if (!_model.Biomes.TryGetValue(_biomeId, out var b))
                return;
            if (b.ResourceWeightModifiers.ContainsKey(id))
                return;
            b.ResourceWeightModifiers[id] = 1f;
            _model.UpdateBiome(b);
        };
        GetTree().Root.AddChild(popup);
        popup.PopupCentered();
    }

    private void OnRename()
    {
        var lineEdit = new LineEdit
        {
            Text = _biomeId,
            PlaceholderText = "new biome id",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var dialog = new ConfirmationDialog
        {
            Title = "Rename biome",
            DialogText =
                $"Rename '{_biomeId}'. References in subtype assigner rules will be updated.",
        };
        dialog.AddChild(lineEdit);
        dialog.Confirmed += () =>
        {
            string newId = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(newId) || newId == _biomeId)
                return;
            if (!_model.RenameBiome(_biomeId, newId))
                return;
            _biomeId = newId;
            CallDeferred(nameof(Refresh));
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(440, 180));
    }

    private void OnDelete()
    {
        var refs = _model.FindBiomeReferences(_biomeId);
        string text =
            refs.Count == 0
                ? $"Delete biome '{_biomeId}'?"
                : $"'{_biomeId}' is referenced by:\n  - "
                    + string.Join("\n  - ", refs)
                    + "\n\nCascade delete will also remove these references.";
        var dialog = new ConfirmationDialog { Title = "Delete biome", DialogText = text };
        dialog.GetOkButton().Text = "Cascade delete";
        dialog.Confirmed += () => _model.RemoveBiome(_biomeId, cascade: true);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(480, 220));
    }
}
#endif
