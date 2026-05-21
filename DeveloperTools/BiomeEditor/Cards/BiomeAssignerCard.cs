#if DEBUG
using System;
using System.Globalization;
using System.Linq;
using Godot;
using ProceduralGeneration.BiomeSystem;
using Structures.Enums;

namespace DeveloperTools.BiomeEditor.Cards;

/// <summary>
/// Per-subtype assigner editor: moisture parameters, ordered rule list, and displayed hazard score.
/// </summary>
public partial class BiomeAssignerCard : PanelContainer
{
    private BiomeEditorModel _model = null!;
    private RockyPlanetSubtype _subtype;

    private VBoxContainer _ruleList = null!;
    private VBoxContainer _moistureFields = null!;
    private Label _hazardLabel = null!;

    public void Initialize(BiomeEditorModel model, RockyPlanetSubtype subtype)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _subtype = subtype;
        BuildLayout();
        Refresh();
    }

    private void BuildLayout()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.14f, 0.14f, 0.18f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6 });
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(root);

        var header = new HBoxContainer();
        var title = new Label { Text = _subtype.ToString(), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.5f));
        header.AddChild(title);

        _hazardLabel = new Label { Text = "Hazard: --" };
        _hazardLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.5f, 0.5f));
        header.AddChild(_hazardLabel);
        root.AddChild(header);

        root.AddChild(new Label { Text = "Moisture" });
        _moistureFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_moistureFields);

        root.AddChild(new HSeparator());
        root.AddChild(new Label { Text = "Rules (first match wins)" });
        _ruleList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_ruleList);

        var addBtn = new Button { Text = "+ Add Rule" };
        addBtn.Pressed += OnAddRule;
        root.AddChild(addBtn);
    }

    private void Refresh()
    {
        BuildMoistureFields();
        BuildRuleRows();
        UpdateHazardLabel();
    }

    private void BuildMoistureFields()
    {
        foreach (var c in _moistureFields.GetChildren()) c.QueueFree();
        if (!_model.Assigners.TryGetValue(_subtype, out var entry)) return;
        var m = entry.Moisture;

        var modeRow = new HBoxContainer();
        modeRow.AddChild(new Label { Text = "Mode:" });
        var modeOpt = new OptionButton();
        modeOpt.AddItem("Whittaker");
        modeOpt.AddItem("UniformRandom");
        modeOpt.Selected = (int)m.Mode;
        modeOpt.ItemSelected += idx =>
        {
            var clone = CloneMoisture(m);
            clone.Mode = (MoistureMode)idx;
            _model.UpdateAssignerMoisture(_subtype, clone);
            Refresh();
        };
        modeRow.AddChild(modeOpt);
        _moistureFields.AddChild(modeRow);

        if (m.Mode == MoistureMode.Whittaker)
        {
            AddFloatRow("base_offset", m.BaseOffset, v => MutateMoisture(c => c.BaseOffset = v));
            AddFloatRow("latitude_factor_divisor", m.LatitudeFactorDivisor, v => MutateMoisture(c => c.LatitudeFactorDivisor = v));
            AddFloatRow("size_factor_divisor", m.SizeFactorDivisor, v => MutateMoisture(c => c.SizeFactorDivisor = v));
            AddFloatRow("random_min", m.RandomMin, v => MutateMoisture(c => c.RandomMin = v));
            AddFloatRow("random_max", m.RandomMax, v => MutateMoisture(c => c.RandomMax = v));
            AddFloatRow("max_moisture", m.MaxMoisture, v => MutateMoisture(c => c.MaxMoisture = v));
            AddFloatRow("constant_offset", m.ConstantOffset, v => MutateMoisture(c => c.ConstantOffset = v));
        }
        else
        {
            AddFloatRow("random_min", m.RandomMin, v => MutateMoisture(c => c.RandomMin = v));
            AddFloatRow("random_max", m.RandomMax, v => MutateMoisture(c => c.RandomMax = v));
        }
    }

    private void AddFloatRow(string name, float value, Action<float> onChange)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(180, 0) });
        var edit = new LineEdit
        {
            Text = value.ToString("0.######", CultureInfo.InvariantCulture),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        edit.TextSubmitted += s => { if (TryParseFloat(s, out var v)) onChange(v); };
        edit.FocusExited += () => { if (TryParseFloat(edit.Text, out var v)) onChange(v); };
        row.AddChild(edit);
        _moistureFields.AddChild(row);
    }

    private void MutateMoisture(Action<MoistureParams> apply)
    {
        if (!_model.Assigners.TryGetValue(_subtype, out var entry)) return;
        var clone = CloneMoisture(entry.Moisture);
        apply(clone);
        _model.UpdateAssignerMoisture(_subtype, clone);
        UpdateHazardLabel();
    }

    private void BuildRuleRows()
    {
        foreach (var c in _ruleList.GetChildren()) c.QueueFree();
        if (!_model.Assigners.TryGetValue(_subtype, out var entry)) return;

        for (int i = 0; i < entry.Rules.Count; i++)
        {
            int capturedIndex = i;
            var rule = entry.Rules[i];
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            var biomeOpt = new OptionButton();
            foreach (var biome in Enum.GetNames<Biome.BiomeType>())
                biomeOpt.AddItem(biome);
            biomeOpt.Selected = (int)rule.Biome;
            biomeOpt.ItemSelected += idx => MutateRule(capturedIndex, r => r.Biome = (Biome.BiomeType)idx);
            row.AddChild(biomeOpt);

            AddCondFloatEdit(row, "h≥", rule.When.HeightAbove, v => MutateRule(capturedIndex, r => r.When.HeightAbove = v));
            AddCondFloatEdit(row, "h≤", rule.When.HeightBelow, v => MutateRule(capturedIndex, r => r.When.HeightBelow = v));
            AddCondFloatEdit(row, "m≥", rule.When.MoistureAbove, v => MutateRule(capturedIndex, r => r.When.MoistureAbove = v));
            AddCondFloatEdit(row, "m≤", rule.When.MoistureBelow, v => MutateRule(capturedIndex, r => r.When.MoistureBelow = v));
            AddCondFloatEdit(row, "|lat|≥", rule.When.AbsLatitudeAbove, v => MutateRule(capturedIndex, r => r.When.AbsLatitudeAbove = v));
            AddCondFloatEdit(row, "|lat|≤", rule.When.AbsLatitudeBelow, v => MutateRule(capturedIndex, r => r.When.AbsLatitudeBelow = v));

            var hazard = new Label { Text = $"hz {HazardCalculator.BiomeHazardWeight(rule.Biome):0.#}" };
            hazard.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.6f));
            row.AddChild(hazard);

            var upBtn = new Button { Text = "↑" };
            upBtn.Pressed += () => _model.MoveAssignerRule(_subtype, capturedIndex, Math.Max(0, capturedIndex - 1));
            row.AddChild(upBtn);
            var downBtn = new Button { Text = "↓" };
            downBtn.Pressed += () => _model.MoveAssignerRule(_subtype, capturedIndex, capturedIndex + 1);
            row.AddChild(downBtn);
            var delBtn = new Button { Text = "✕" };
            delBtn.Pressed += () => _model.RemoveAssignerRule(_subtype, capturedIndex);
            row.AddChild(delBtn);

            _ruleList.AddChild(row);
        }

        // Refresh after model mutation completes since events are synchronous.
        _model.AssignerChanged -= OnAssignerChanged;
        _model.AssignerChanged += OnAssignerChanged;
    }

    private void OnAssignerChanged(RockyPlanetSubtype subtype)
    {
        if (subtype != _subtype) return;
        CallDeferred(nameof(DeferredRefresh));
    }

    private void DeferredRefresh() => Refresh();

    public override void _ExitTree()
    {
        base._ExitTree();
        _model.AssignerChanged -= OnAssignerChanged;
    }

    private static void AddCondFloatEdit(HBoxContainer row, string label, float? value, Action<float?> onChange)
    {
        row.AddChild(new Label { Text = label });
        var edit = new LineEdit
        {
            Text = value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "",
            CustomMinimumSize = new Vector2(56, 0),
            PlaceholderText = "—",
        };
        edit.TextSubmitted += s => Apply(s);
        edit.FocusExited += () => Apply(edit.Text);
        row.AddChild(edit);

        void Apply(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) { onChange(null); return; }
            if (TryParseFloat(s, out var v)) onChange(v);
        }
    }

    private void MutateRule(int index, Action<BiomeRule> apply)
    {
        if (!_model.Assigners.TryGetValue(_subtype, out var entry)) return;
        if (index < 0 || index >= entry.Rules.Count) return;
        var rule = entry.Rules[index];
        var clone = new BiomeRule
        {
            Biome = rule.Biome,
            When = new BiomeRuleConditions
            {
                HeightAbove = rule.When.HeightAbove,
                HeightBelow = rule.When.HeightBelow,
                MoistureAbove = rule.When.MoistureAbove,
                MoistureBelow = rule.When.MoistureBelow,
                AbsLatitudeAbove = rule.When.AbsLatitudeAbove,
                AbsLatitudeBelow = rule.When.AbsLatitudeBelow,
            },
        };
        apply(clone);
        _model.UpdateAssignerRule(_subtype, index, clone);
    }

    private void OnAddRule()
    {
        _model.AddAssignerRule(_subtype, new BiomeRule { Biome = Biome.BiomeType.Grassland });
    }

    private void UpdateHazardLabel()
    {
        var atm = _model.AtmosphereTemplates.TryGetValue("RockyPlanet", out var template)
            ? (template.AtmosphereMin, template.AtmosphereMax) : (0f, 1f);
        if (!_model.Assigners.TryGetValue(_subtype, out var entry) || entry.Rules.Count == 0)
        {
            _hazardLabel.Text = "Hazard: --";
            return;
        }
        // Use first rule's biome as representative.
        float h = HazardCalculator.Compute(_subtype, atm.Item1, atm.Item2, entry.Rules[0].Biome);
        _hazardLabel.Text = $"Hazard: {h:0.0}";
    }

    private static MoistureParams CloneMoisture(MoistureParams m) => new()
    {
        Mode = m.Mode,
        BaseOffset = m.BaseOffset,
        LatitudeFactorDivisor = m.LatitudeFactorDivisor,
        SizeFactorDivisor = m.SizeFactorDivisor,
        RandomMin = m.RandomMin,
        RandomMax = m.RandomMax,
        MaxMoisture = m.MaxMoisture,
        ConstantOffset = m.ConstantOffset,
    };

    private static bool TryParseFloat(string s, out float v) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}
#endif
