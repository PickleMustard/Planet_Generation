#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using DeveloperTools.BiomeEditor.Popups;

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

    private LineEdit _displayEdit = null!;
    private ColorPickerButton _defaultColorBtn = null!;
    private VBoxContainer _overridesList = null!;
    private SpinBox _hazardSpin = null!;
    private SpinBox _ventSpin = null!;
    private LineEdit _tagsEdit = null!;
    private VBoxContainer _weightsList = null!;

    public void Initialize(BiomeEditorModel model, string biomeId)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _biomeId = biomeId;
        BuildLayout();
        Refresh();
        _model.BiomeDefinitionChanged += OnDefChanged;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _model.BiomeDefinitionChanged -= OnDefChanged;
    }

    private void OnDefChanged(string id)
    {
        if (id != _biomeId) return;
        CallDeferred(nameof(Refresh));
    }

    private void BuildLayout()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.14f, 0.18f),
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        });
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(root);

        var header = new HBoxContainer();
        var title = new Label { Text = _biomeId, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color(0.7f, 1f, 0.85f));
        header.AddChild(title);
        var renameBtn = new Button { Text = "Rename" };
        renameBtn.Pressed += OnRename;
        header.AddChild(renameBtn);
        var delBtn = new Button { Text = "✕ Delete Biome" };
        delBtn.Pressed += OnDelete;
        header.AddChild(delBtn);
        root.AddChild(header);

        var nameRow = new HBoxContainer();
        nameRow.AddChild(new Label { Text = "display_name:", CustomMinimumSize = new Vector2(150, 0) });
        _displayEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _displayEdit.TextSubmitted += s => CommitDisplay(s);
        _displayEdit.FocusExited += () => CommitDisplay(_displayEdit.Text);
        nameRow.AddChild(_displayEdit);
        root.AddChild(nameRow);

        var colorRow = new HBoxContainer();
        colorRow.AddChild(new Label { Text = "default_color:", CustomMinimumSize = new Vector2(150, 0) });
        _defaultColorBtn = new ColorPickerButton { CustomMinimumSize = new Vector2(120, 24), EditAlpha = false };
        _defaultColorBtn.ColorChanged += c => CommitDefaultColor(c);
        colorRow.AddChild(_defaultColorBtn);
        root.AddChild(colorRow);

        var ovHeader = new HBoxContainer();
        ovHeader.AddChild(new Label { Text = "color_overrides:", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var ovAdd = new Button { Text = "+ Add Override" };
        ovAdd.Pressed += OnAddOverride;
        ovHeader.AddChild(ovAdd);
        root.AddChild(ovHeader);
        _overridesList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_overridesList);

        var hazardRow = new HBoxContainer();
        hazardRow.AddChild(new Label { Text = "hazard_weight:", CustomMinimumSize = new Vector2(150, 0) });
        _hazardSpin = NewSpin(0.0, 10.0, 0.1);
        _hazardSpin.ValueChanged += v => CommitHazard((float)v);
        hazardRow.AddChild(_hazardSpin);
        root.AddChild(hazardRow);

        var ventRow = new HBoxContainer();
        ventRow.AddChild(new Label { Text = "vent_probability:", CustomMinimumSize = new Vector2(150, 0) });
        _ventSpin = NewSpin(0.0, 1.0, 0.01);
        _ventSpin.ValueChanged += v => CommitVent((float)v);
        ventRow.AddChild(_ventSpin);
        root.AddChild(ventRow);

        var tagsRow = new HBoxContainer();
        tagsRow.AddChild(new Label { Text = "tags (csv):", CustomMinimumSize = new Vector2(150, 0) });
        _tagsEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tagsEdit.TextSubmitted += s => CommitTags(s);
        _tagsEdit.FocusExited += () => CommitTags(_tagsEdit.Text);
        tagsRow.AddChild(_tagsEdit);
        root.AddChild(tagsRow);

        var wHeader = new HBoxContainer();
        wHeader.AddChild(new Label { Text = "resource_weight_modifiers:", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var wAdd = new Button { Text = "+ Resource" };
        wAdd.Pressed += OnAddWeight;
        wHeader.AddChild(wAdd);
        root.AddChild(wHeader);
        _weightsList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_weightsList);
    }

    private static SpinBox NewSpin(double min, double max, double step) => new()
    {
        MinValue = min,
        MaxValue = max,
        Step = step,
        AllowGreater = false,
        AllowLesser = false,
        CustomMinimumSize = new Vector2(100, 0),
    };

    private void Refresh()
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
        _displayEdit.Text = b.DisplayName;
        _defaultColorBtn.Color = b.DefaultColor;
        _hazardSpin.SetValueNoSignal(b.HazardWeight);
        _ventSpin.SetValueNoSignal(b.GeothermalVentProbability);
        _tagsEdit.Text = string.Join(", ", b.Tags);

        foreach (var c in _overridesList.GetChildren()) c.QueueFree();
        foreach (var kvp in b.ColorOverrides.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            string subId = kvp.Key;
            Color color = kvp.Value;
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = subId, SizeFlagsHorizontal = SizeFlags.ExpandFill });
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

        foreach (var c in _weightsList.GetChildren()) c.QueueFree();
        foreach (var kvp in b.ResourceWeightModifiers.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            string resId = kvp.Key;
            float w = kvp.Value;
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = resId, SizeFlagsHorizontal = SizeFlags.ExpandFill });
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
        if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
        if (b.DisplayName == s) return;
        b.DisplayName = s;
        _model.UpdateBiome(b);
    }

    private void CommitDefaultColor(Color c)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
        b.DefaultColor = c;
        _model.UpdateBiome(b);
    }

    private void CommitOverride(string subId, Color c)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
        b.ColorOverrides[subId] = c;
        _model.UpdateBiome(b);
    }

    private void RemoveOverride(string subId)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
        if (b.ColorOverrides.Remove(subId)) _model.UpdateBiome(b);
    }

    private void CommitHazard(float v)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
        b.HazardWeight = v;
        _model.UpdateBiome(b);
    }

    private void CommitVent(float v)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
        b.GeothermalVentProbability = v;
        _model.UpdateBiome(b);
    }

    private void CommitTags(string s)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
        b.Tags = s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        _model.UpdateBiome(b);
    }

    private void CommitWeight(string resId, string s)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return;
        b.ResourceWeightModifiers[resId] = v;
        _model.UpdateBiome(b);
    }

    private void RemoveWeight(string resId)
    {
        if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
        if (b.ResourceWeightModifiers.Remove(resId)) _model.UpdateBiome(b);
    }

    // ── Add-override picker ─────────────────────────────────────────────

    private void OnAddOverride()
    {
        var lineEdit = new LineEdit
        {
            PlaceholderText = "subtype_<family>_<name>",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var dialog = new ConfirmationDialog { Title = "Add color override", DialogText = "Subtype id:" };
        dialog.AddChild(lineEdit);
        dialog.Confirmed += () =>
        {
            string id = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(id)) return;
            if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
            if (b.ColorOverrides.ContainsKey(id)) return;
            b.ColorOverrides[id] = b.DefaultColor;
            _model.UpdateBiome(b);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    private void OnAddWeight()
    {
        var popup = new ResourceIdPickerPopup();
        popup.ResourcePicked += id =>
        {
            if (!_model.Biomes.TryGetValue(_biomeId, out var b)) return;
            if (b.ResourceWeightModifiers.ContainsKey(id)) return;
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
            DialogText = $"Rename '{_biomeId}'. References in subtype assigner rules will be updated.",
        };
        dialog.AddChild(lineEdit);
        dialog.Confirmed += () =>
        {
            string newId = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(newId) || newId == _biomeId) return;
            if (!_model.RenameBiome(_biomeId, newId)) return;
            _biomeId = newId;
            CallDeferred(nameof(Refresh));
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(440, 180));
    }

    private void OnDelete()
    {
        var refs = _model.FindBiomeReferences(_biomeId);
        string text = refs.Count == 0
            ? $"Delete biome '{_biomeId}'?"
            : $"'{_biomeId}' is referenced by:\n  - " + string.Join("\n  - ", refs)
              + "\n\nCascade delete will also remove these references.";
        var dialog = new ConfirmationDialog { Title = "Delete biome", DialogText = text };
        dialog.GetOkButton().Text = "Cascade delete";
        dialog.Confirmed += () => _model.RemoveBiome(_biomeId, cascade: true);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(480, 220));
    }
}
#endif
