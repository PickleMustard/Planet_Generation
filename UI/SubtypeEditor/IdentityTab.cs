using System;
using Godot;
using Structures.Resources;

namespace UI.SubtypeEditor;

/// <summary>Identity tab: id (read-only), display_name, family (read-only), atmosphere range, base hazard, base resource weight.
/// Layout defined in IdentityTab.tscn.</summary>
public partial class IdentityTab : ScrollContainer
{
    private SubtypeEditorModel _model = null!;
    private string? _subtypeId;

    [Export] private Label? _idLabel;
    [Export] private LineEdit? _displayEdit;
    [Export] private Label? _familyLabel;
    [Export] private SpinBox? _atmMin;
    [Export] private SpinBox? _atmMax;
    [Export] private SpinBox? _baseHazard;
    [Export] private SpinBox? _baseWeight;
    [Export] private OptionButton? _moistureMode;
    private bool _suppress;

    public void Initialize(SubtypeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    public override void _Ready()
    {
        if (_displayEdit != null)
        {
            _displayEdit.TextSubmitted += s => CommitDisplay(s);
            _displayEdit.FocusExited += () => CommitDisplay(_displayEdit?.Text ?? "");
        }
        if (_atmMin != null)
            _atmMin.ValueChanged += _ => CommitAtmosphere();
        if (_atmMax != null)
            _atmMax.ValueChanged += _ => CommitAtmosphere();
        if (_baseHazard != null)
            _baseHazard.ValueChanged += v => Commit(d => d.BaseHazard = (float)v);
        if (_baseWeight != null)
            _baseWeight.ValueChanged += v => Commit(d => d.BaseResourceWeight = (float)v);
        if (_moistureMode != null)
        {
            _moistureMode.AddItem("whittaker");
            _moistureMode.AddItem("uniform_random");
            _moistureMode.ItemSelected += _ => Commit(d => d.MoistureMode = _moistureMode.Selected == 1 ? "uniform_random" : "whittaker");
        }
    }

    public void SetSubtype(string? subtypeId)
    {
        _subtypeId = subtypeId;
        if (string.IsNullOrEmpty(subtypeId))
        {
            ClearAll();
            return;
        }
        var def = _model.GetById(subtypeId);
        if (def == null) { ClearAll(); return; }

        _suppress = true;
        try
        {
            if (_idLabel != null) _idLabel.Text = def.Id;
            if (_displayEdit != null) _displayEdit.Text = def.DisplayName;
            if (_familyLabel != null) _familyLabel.Text = def.Family.ToString();
            if (_atmMin != null) _atmMin.Value = def.AtmosphereMin;
            if (_atmMax != null) _atmMax.Value = def.AtmosphereMax;
            if (_baseHazard != null) _baseHazard.Value = def.BaseHazard;
            if (_baseWeight != null) _baseWeight.Value = def.BaseResourceWeight;
            if (_moistureMode != null) _moistureMode.Selected = def.MoistureMode == "uniform_random" ? 1 : 0;
        }
        finally { _suppress = false; }
    }

    private void ClearAll()
    {
        _suppress = true;
        try
        {
            if (_idLabel != null) _idLabel.Text = "";
            if (_displayEdit != null) _displayEdit.Text = "";
            if (_familyLabel != null) _familyLabel.Text = "";
            if (_atmMin != null) _atmMin.Value = 0;
            if (_atmMax != null) _atmMax.Value = 0;
            if (_baseHazard != null) _baseHazard.Value = 0;
            if (_baseWeight != null) _baseWeight.Value = 1;
            if (_moistureMode != null) _moistureMode.Selected = 0;
        }
        finally { _suppress = false; }
    }

    private void CommitDisplay(string s) => Commit(d => d.DisplayName = s);

    private void CommitAtmosphere()
    {
        Commit(d =>
        {
            d.AtmosphereMin = (float)(_atmMin?.Value ?? 0);
            d.AtmosphereMax = (float)(_atmMax?.Value ?? 0);
        });
    }

    private void Commit(Action<SubtypeDefinition> mutate)
    {
        if (_suppress) return;
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        mutate(def);
        _model.MarkDirty(_subtypeId);
    }
}