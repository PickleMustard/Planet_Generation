using System;
using System.Collections.Generic;
using Godot;
using Structures.Resources;
using UtilityLibrary.UI;

namespace UI.SubtypeEditor;

/// <summary>
/// Spherical harmonics range editor. Five knobs: amplitude + band_scale_l0..l3.
/// Layout defined in SphericalHarmonicsTab.tscn; FloatRangeRow widgets added dynamically from data arrays.
/// </summary>
public partial class SphericalHarmonicsTab : ScrollContainer
{
    private SubtypeEditorModel _model = null!;
    private string? _subtypeId;

    private static readonly (string key, string label, double min, double max, double step, string tooltip)[] SHKnobs =
    {
        ("amplitude",     "Amplitude",       0, 5, 0.01, "Overall magnitude of the harmonic displacement."),
        ("band_scale_l0", "Band Scale L0",   0, 2, 0.01, "Scale of the constant (l=0) band."),
        ("band_scale_l1", "Band Scale L1",   0, 2, 0.01, "Scale of the dipole (l=1) band."),
        ("band_scale_l2", "Band Scale L2",   0, 2, 0.01, "Scale of the quadrupole (l=2) band."),
        ("band_scale_l3", "Band Scale L3",   0, 2, 0.01, "Scale of the octupole (l=3) band."),
    };

    [Export] private VBoxContainer? _rows;

    private readonly Dictionary<string, FloatRangeRow> _rowMap = new(StringComparer.Ordinal);

    public void Initialize(SubtypeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    public override void _Ready()
    {
        BuildRows();
    }

    private void BuildRows()
    {
        if (_rows == null) return;
        foreach (var k in SHKnobs)
        {
            string tooltip = GenSettingTooltips.Get($"spherical_harmonics.{k.key}", k.tooltip);
            var row = FloatRangeRow.Create(k.key, k.label, k.min, k.max, k.step, tooltip);
            row.ValueChanged += OnChanged;
            _rowMap[k.key] = row;
            _rows.AddChild(row);
        }
    }

    public void SetSubtype(string? subtypeId)
    {
        _subtypeId = subtypeId;
        if (string.IsNullOrEmpty(subtypeId))
        {
            foreach (var r in _rowMap.Values) r.SetValue(null);
            return;
        }
        var def = _model.GetById(subtypeId);
        if (def == null) return;
        foreach (var (key, row) in _rowMap)
            row.SetValue(def.SphericalHarmonicsRanges.TryGetValue(key, out var r) ? r : null);
    }

    private void OnChanged(string key, FloatRange range)
    {
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        def.SphericalHarmonicsRanges[key] = range;
        _model.MarkDirty(_subtypeId);
    }
}