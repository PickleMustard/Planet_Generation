#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Per-behavior row inside a building's Behaviors subsection: behavior_id
/// OptionButton + delete + schema-driven config grid that rebuilds when the
/// behavior id changes. Built programmatically.
/// </summary>
public partial class BehaviorRow : VBoxContainer
{
    [Signal]
    public delegate void RowDeletedEventHandler(int rowIndex);

    private BuildingEditorModel? _model;
    private string _categoryName = "";
    private int _buildingIndex;
    private int _rowIndex;
    private BuildingEditorModel.BehaviorEntryEdit? _entry;

    [Export] private OptionButton _behaviorIdButton = null!;
    [Export] private GridContainer _configGrid = null!;

    private List<string> _behaviorChoices = new();

    private static PackedScene? _scene;

    public static BehaviorRow Create(
        BuildingEditorModel model,
        string categoryName,
        int buildingIndex,
        int rowIndex,
        BuildingEditorModel.BehaviorEntryEdit entry)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/BuildingEditor/BehaviorRow.tscn");
        var row = _scene.Instantiate<BehaviorRow>();
        row.Initialize(model, categoryName, buildingIndex, rowIndex, entry);
        return row;
    }

    public void Initialize(
        BuildingEditorModel model,
        string categoryName,
        int buildingIndex,
        int rowIndex,
        BuildingEditorModel.BehaviorEntryEdit entry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);
        _model = model;
        _categoryName = categoryName;
        _buildingIndex = buildingIndex;
        _rowIndex = rowIndex;
        _entry = entry;
    }

    public override void _Ready()
    {
        base._Ready();
        RefreshControls();
    }

    private void OnDeletePressed() => EmitSignal(SignalName.RowDeleted, _rowIndex);

    private void RefreshControls()
    {
        RebuildBehaviorIdOptions();
        RebuildConfigGrid();
    }

    private void RebuildBehaviorIdOptions()
    {
        _behaviorIdButton.Clear();
        _behaviorChoices.Clear();
        var union = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var s in BuildingEditorModel.GetAllBehaviorTypes()) union.Add(s);
        foreach (var s in BehaviorSchemaRegistry.KnownBehaviorIds) union.Add(s);
        int i = 0;
        foreach (var name in union)
        {
            _behaviorChoices.Add(name);
            _behaviorIdButton.AddItem(name, i++);
        }

        string current = _entry?.BehaviorId ?? "";
        int idx = _behaviorChoices.IndexOf(current);
        if (idx < 0 && !string.IsNullOrEmpty(current))
        {
            _behaviorChoices.Add(current);
            _behaviorIdButton.AddItem($"{current} (unknown)", _behaviorChoices.Count - 1);
            idx = _behaviorChoices.Count - 1;
        }
        if (idx >= 0) _behaviorIdButton.Select(idx);
    }

    private void RebuildConfigGrid()
    {
        foreach (var c in _configGrid.GetChildren()) c.QueueFree();
        if (_entry == null) return;

        var schema = BehaviorSchemaRegistry.GetSchema(_entry.BehaviorId);
        foreach (var field in schema)
        {
            _configGrid.AddChild(new Label
            { ThemeTypeVariation = "LabelHighContrast",
                Text = field.Name,
                TooltipText = field.Tooltip ?? ""
            });
            object? current = _entry.Config.TryGetValue(field.Name, out var v) ? v : field.Default;
            var control = BehaviorFieldControlFactory.Create(field, current,
                value => OnConfigEdited(field.Name, value));
            _configGrid.AddChild(control);
        }
    }

    private void OnBehaviorIdSelected(long index)
    {
        if (_model == null || _entry == null) return;
        if (index < 0 || index >= _behaviorChoices.Count) return;
        string chosen = _behaviorChoices[(int)index];
        if (chosen == _entry.BehaviorId) return;
        _model.UpdateBehaviorId(_categoryName, _buildingIndex, _rowIndex, chosen);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex].Behaviors[_rowIndex];
        RebuildConfigGrid();
    }

    private void OnConfigEdited(string fieldName, object? value)
    {
        if (_model == null) return;
        _model.UpdateBehaviorConfig(_categoryName, _buildingIndex, _rowIndex, fieldName, value);
        if (_model.Categories.TryGetValue(_categoryName, out var cat)
            && _buildingIndex < cat.Buildings.Count
            && _rowIndex < cat.Buildings[_buildingIndex].Behaviors.Count)
        {
            _entry = cat.Buildings[_buildingIndex].Behaviors[_rowIndex];
        }
    }
}
#endif
