using System;
using System.Collections.Generic;
using Godot;
using Structures.Resources;
using UtilityLibrary.UI;

namespace UI.SubtypeEditor;

/// <summary>
/// Mesh + Tectonic gen-range editor. One <see cref="FloatRangeRow"/> per knob declared in
/// <see cref="SubtypeDefinition.MeshRanges"/> / <see cref="SubtypeDefinition.TectonicRanges"/>.
/// Layout defined in MeshTectonicRangesTab.tscn; FloatRangeRow widgets added dynamically from data arrays.
/// </summary>
public partial class MeshTectonicRangesTab : ScrollContainer
{
    private SubtypeEditorModel _model = null!;
    private string? _subtypeId;

    private const int VpeMinSpin = 1;
    private const int VpeMaxSpin = 50;
    private const int VpeMaxLevels = 5;

    private static readonly (string key, string label, double min, double max, double step, string tooltip)[] MeshKnobs =
    {
        ("subdivisions",         "Subdivisions",          1,  5,   1,    "Icosahedron subdivision count. Higher = denser base mesh."),
        ("num_abberations",      "Aberrations Per Cycle",  0,  50,  1,    "Random vertex perturbations applied per deformation cycle."),
        ("num_deformation_cycles","Deformation Cycles",   0,  20,  1,    "Number of mesh deformation passes. More = smoother."),
    };

    private static readonly (string key, string label, double min, double max, double step, string tooltip)[] TectonicKnobs =
    {
        ("num_continents",            "Continents",                1,   60,   1,   "Number of tectonic plates to spawn."),
        ("stress_scale",              "Stress Scale",              0,   10,   0.05,"Multiplier on plate-boundary stress."),
        ("shear_scale",               "Shear Scale",               0,   10,   0.05,"Multiplier on transform-fault shear."),
        ("max_propagation_distance",  "Max Propagation Distance",  0,   2,    0.01,"Limits how far boundary stress propagates."),
        ("propagation_falloff",       "Propagation Falloff",       0,   1,    0.01,"Falloff curve sharpness for boundary stress."),
        ("inactive_stress_threshold", "Inactive Stress Threshold", 0,   1,    0.01,"Stress under this is treated as no-op."),
        ("general_height_scale",      "General Height Scale",      0,   10,   0.05,"Master multiplier on terrain height."),
        ("general_shear_scale",       "General Shear Scale",       0,   10,   0.05,"Master multiplier on shear deformation."),
        ("general_compression_scale", "General Compression Scale", 0,   10,   0.05,"Master multiplier on compression uplift."),
        ("general_transform_scale",   "General Transform Scale",   0,   10,   0.05,"Master multiplier on transform-fault offset."),
        ("boundary_smoothing_iterations","Boundary Smoothing Iterations",0, 10, 1,   "Laplacian smoothing passes over per-edge boundary stress. 0 = raw stress field."),
        ("boundary_smoothing_weight", "Boundary Smoothing Weight", 0,   1,    0.05,"Blend weight (0-1) toward neighbor mean per smoothing pass."),
        ("max_continent_rotation",    "Max Continent Rotation",    0,   180,  1,   "Cap on rotational ω×r contribution to plate movement (degrees)."),
    };

    [Export] private Label? _meshHeader;
    [Export] private VBoxContainer? _meshRows;
    [Export] private Label? _vpeHeader;
    [Export] private SpinBox? _vpeCountSpin;
    [Export] private VBoxContainer? _vpeRowsContainer;
    [Export] private Label? _tectonicHeader;
    [Export] private VBoxContainer? _tectRows;
    [Export] private HBoxContainer? _sampleVelocityRow;
    [Export] private CheckBox? _sampleVelocityCheck;

    private readonly Dictionary<string, FloatRangeRow> _meshRowMap = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FloatRangeRow> _tectRowMap = new(StringComparer.Ordinal);
    private readonly List<FloatRangeRow> _vpeRows = new();
    private bool _suppressVpeCount;
    private bool _suppressSampleVelocity;

    public void Initialize(SubtypeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    public override void _Ready()
    {
        BuildMeshAndTectRows();
        if (_vpeCountSpin != null)
            _vpeCountSpin.ValueChanged += OnVpeCountChanged;
        if (_sampleVelocityCheck != null)
        {
            _sampleVelocityCheck.Toggled += OnSampleVelocityToggled;
            _sampleVelocityCheck.TooltipText =
                GenSettingTooltips.Get("tectonics.sample_velocity_at_edge_midpoint");
        }
    }

    private void BuildMeshAndTectRows()
    {
        if (_meshRows != null)
        {
            foreach (var k in MeshKnobs)
            {
                string tooltip = GenSettingTooltips.Get($"mesh.{k.key}", k.tooltip);
                var row = FloatRangeRow.Create(k.key, k.label, k.min, k.max, k.step, tooltip);
                row.ValueChanged += OnMeshChanged;
                _meshRowMap[k.key] = row;
                _meshRows.AddChild(row);
            }
        }

        if (_tectRows != null)
        {
            foreach (var k in TectonicKnobs)
            {
                string tooltip = GenSettingTooltips.Get($"tectonics.{k.key}", k.tooltip);
                var row = FloatRangeRow.Create(k.key, k.label, k.min, k.max, k.step, tooltip);
                row.ValueChanged += OnTectonicChanged;
                _tectRowMap[k.key] = row;
                _tectRows.AddChild(row);
            }
        }
    }

    public void SetSubtype(string? subtypeId)
    {
        _subtypeId = subtypeId;
        if (string.IsNullOrEmpty(subtypeId))
        {
            foreach (var r in _meshRowMap.Values) r.SetValue(null);
            foreach (var r in _tectRowMap.Values) r.SetValue(null);
            SetSampleVelocitySilently(true);
            SetVpeCountSilently(0);
            RebuildVpeRows(0, ranges: null);
            ToggleTectonics(true);
            return;
        }

        var def = _model.GetById(subtypeId);
        if (def == null) return;

        foreach (var (key, row) in _meshRowMap)
            row.SetValue(def.MeshRanges.TryGetValue(key, out var r) ? r : null);

        foreach (var (key, row) in _tectRowMap)
            row.SetValue(def.TectonicRanges.TryGetValue(key, out var r) ? r : null);

        SetSampleVelocitySilently(def.SampleVelocityAtEdgeMidpoint);

        int vpeCount = Math.Clamp(def.VerticesPerEdgeRanges.Count, 0, VpeMaxLevels);
        SetVpeCountSilently(vpeCount);
        RebuildVpeRows(vpeCount, def.VerticesPerEdgeRanges);

        bool showTectonics = SupportsTectonics(def.Family);
        ToggleTectonics(showTectonics);
    }

    private void ToggleTectonics(bool show)
    {
        if (_tectonicHeader != null) _tectonicHeader.Visible = show;
        foreach (var r in _tectRowMap.Values) r.Visible = show;
        if (_sampleVelocityRow != null) _sampleVelocityRow.Visible = show;
    }

    private void SetSampleVelocitySilently(bool value)
    {
        _suppressSampleVelocity = true;
        try { if (_sampleVelocityCheck != null) _sampleVelocityCheck.ButtonPressed = value; }
        finally { _suppressSampleVelocity = false; }
    }

    private void OnSampleVelocityToggled(bool pressed)
    {
        if (_suppressSampleVelocity) return;
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        def.SampleVelocityAtEdgeMidpoint = pressed;
        _model.MarkDirty(_subtypeId);
    }

    private static bool SupportsTectonics(BodyFamily family) => family switch
    {
        BodyFamily.RockyPlanet => true,
        BodyFamily.DwarfPlanet => true,
        BodyFamily.Satellite => true,
        _ => false,
    };

    // ── Vertices-per-edge wiring ────────────────────────────────────────

    private void SetVpeCountSilently(int value)
    {
        _suppressVpeCount = true;
        try { if (_vpeCountSpin != null) _vpeCountSpin.Value = value; }
        finally { _suppressVpeCount = false; }
    }

    private void OnVpeCountChanged(double value)
    {
        if (_suppressVpeCount) return;
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;

        int target = Math.Clamp((int)value, 0, VpeMaxLevels);

        // Resize the model list. Newly added rows default to [2, 2] (a safe minimum).
        while (def.VerticesPerEdgeRanges.Count < target)
            def.VerticesPerEdgeRanges.Add(new FloatRange(2, 2));
        while (def.VerticesPerEdgeRanges.Count > target)
            def.VerticesPerEdgeRanges.RemoveAt(def.VerticesPerEdgeRanges.Count - 1);

        RebuildVpeRows(target, def.VerticesPerEdgeRanges);
        _model.MarkDirty(_subtypeId);
    }

    private void RebuildVpeRows(int count, IReadOnlyList<FloatRange>? ranges)
    {
        if (_vpeRowsContainer == null) return;
        foreach (var existing in _vpeRows)
            existing.QueueFree();
        _vpeRows.Clear();

        for (int i = 0; i < count; i++)
        {
            int level = i;
            string key = $"level_{level}";
            string label = $"Level {level}";
            var row = FloatRangeRow.Create(key, label, VpeMinSpin, VpeMaxSpin, 1,
                $"Vertex-count range for subdivision level {level}. Last level repeats if rolled subdivisions exceeds list.");
            row.SetValue(ranges != null && level < ranges.Count ? ranges[level] : new FloatRange(2, 2));
            row.ValueChanged += (_, r) => OnVpeRowChanged(level, r);
            _vpeRowsContainer.AddChild(row);
            _vpeRows.Add(row);
        }
    }

    private void OnVpeRowChanged(int level, FloatRange range)
    {
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        if (level < 0 || level >= def.VerticesPerEdgeRanges.Count) return;
        def.VerticesPerEdgeRanges[level] = range;
        _model.MarkDirty(_subtypeId);
    }

    // ── Mesh / Tectonic range commits ───────────────────────────────────

    private void OnMeshChanged(string key, FloatRange range)
    {
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        def.MeshRanges[key] = range;
        _model.MarkDirty(_subtypeId);
    }

    private void OnTectonicChanged(string key, FloatRange range)
    {
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        def.TectonicRanges[key] = range;
        _model.MarkDirty(_subtypeId);
    }
}