using System;
using Godot;
using Structures.Resources;

namespace UI.SubtypeEditor;

/// <summary>
/// Inline row presenting a label and two SpinBoxes for a <see cref="FloatRange"/>.
/// Edits raise <see cref="ValueChanged"/> with the new range so the parent can persist.
/// Layout defined in FloatRangeRow.tscn; use <see cref="Create"/> to instantiate with parameters.
/// </summary>
public partial class FloatRangeRow : HBoxContainer
{
    [Export] private Label? _label;
    [Export] private SpinBox? _minSpinBox;
    [Export] private SpinBox? _maxSpinBox;

    private bool _suppress;

    public string Key { get; private set; } = "";

    public event Action<string, FloatRange>? ValueChanged;

    private static PackedScene? _scene;

    /// <summary>
    /// Factory: loads FloatRangeRow.tscn, configures the row, and returns the instance.
    /// Call <c>parent.AddChild(row)</c> after this.
    /// </summary>
    public static FloatRangeRow Create(string key, string label, double min, double max, double step, string tooltip = "")
    {
        _scene ??= GD.Load<PackedScene>("res://UI/SubtypeEditor/FloatRangeRow.tscn");
        var instance = _scene.Instantiate<FloatRangeRow>();
        instance.Setup(key, label, min, max, step, tooltip);
        return instance;
    }

    /// <summary>
    /// Configure label, spin-box ranges, and tooltip. Called by <see cref="Create"/>
    /// after instantiation; may be called again to reconfigure.
    /// </summary>
    public void Setup(string key, string label, double min, double max, double step, string tooltip = "")
    {
        Key = key;
        if (_label != null)
        {
            _label.Text = label;
            _label.TooltipText = tooltip;
        }
        if (_minSpinBox != null)
        {
            _minSpinBox.MinValue = min;
            _minSpinBox.MaxValue = max;
            _minSpinBox.Step = step;
            _minSpinBox.AllowGreater = false;
            _minSpinBox.AllowLesser = false;
            _minSpinBox.TooltipText = tooltip;
        }
        if (_maxSpinBox != null)
        {
            _maxSpinBox.MinValue = min;
            _maxSpinBox.MaxValue = max;
            _maxSpinBox.Step = step;
            _maxSpinBox.AllowGreater = false;
            _maxSpinBox.AllowLesser = false;
            _maxSpinBox.TooltipText = tooltip;
        }
    }

    public override void _Ready()
    {
        if (_minSpinBox != null)
            _minSpinBox.ValueChanged += _ => Commit();
        if (_maxSpinBox != null)
            _maxSpinBox.ValueChanged += _ => Commit();
    }

    public void SetValue(FloatRange? range)
    {
        _suppress = true;
        try
        {
            if (range.HasValue)
            {
                if (_minSpinBox != null) _minSpinBox.Value = range.Value.Min;
                if (_maxSpinBox != null) _maxSpinBox.Value = range.Value.Max;
            }
            else
            {
                if (_minSpinBox != null) _minSpinBox.Value = 0;
                if (_maxSpinBox != null) _maxSpinBox.Value = 0;
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    private void Commit()
    {
        if (_suppress) return;
        float minVal = (float)(_minSpinBox?.Value ?? 0);
        float maxVal = (float)(_maxSpinBox?.Value ?? 0);
        ValueChanged?.Invoke(Key, new FloatRange(minVal, maxVal));
    }
}