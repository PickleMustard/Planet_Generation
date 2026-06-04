#if DEBUG
using System;
using System.Collections.Generic;
using Godot;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Composite UI section for placement_requirements: biomes picker button +
/// min/max elevation + max slope + cell count + requires_adjacent +
/// configurable_behavior dropdown + inline schema-driven config grid for the
/// chosen placement behavior. Built programmatically.
/// </summary>
public partial class PlacementRequirementsSection : VBoxContainer
{
    private BuildingEditorModel? _model;
    private string _categoryName = "";
    private int _buildingIndex;
    private BuildingEditorModel.BuildingEditEntry? _entry;

    private Button _biomesButton = null!;
    private SpinBox _minElevSpin = null!;
    private SpinBox _maxElevSpin = null!;
    private SpinBox _maxSlopeSpin = null!;
    private SpinBox _cellCountSpin = null!;
    private CheckBox _requiresAdjacentCheck = null!;
    private OptionButton _configurableBehaviorButton = null!;
    private GridContainer _behaviorConfigGrid = null!;

    private List<string> _behaviorChoices = new();

    public void Initialize(
        BuildingEditorModel model,
        string categoryName,
        int buildingIndex,
        BuildingEditorModel.BuildingEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);
        _model = model;
        _categoryName = categoryName;
        _buildingIndex = buildingIndex;
        _entry = entry;
    }

    public override void _Ready()
    {
        base._Ready();
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildLayout();
        RefreshControls();
    }

    private void BuildLayout()
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(grid);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Biomes" });
        _biomesButton = new Button { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _biomesButton.Pressed += OnBiomesPressed;
        grid.AddChild(_biomesButton);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Min Elevation" });
        _minElevSpin = MakeSpin(0, 1, 0.01f);
        _minElevSpin.ValueChanged += v => OnFieldEdited("MinElevation", (float)v);
        grid.AddChild(_minElevSpin);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Max Elevation" });
        _maxElevSpin = MakeSpin(0, 1, 0.01f);
        _maxElevSpin.ValueChanged += v => OnFieldEdited("MaxElevation", (float)v);
        grid.AddChild(_maxElevSpin);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Max Slope" });
        _maxSlopeSpin = MakeSpin(0, 90, 0.5f);
        _maxSlopeSpin.ValueChanged += v => OnFieldEdited("MaxSlope", (float)v);
        grid.AddChild(_maxSlopeSpin);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Cell Count" });
        _cellCountSpin = MakeSpin(1, 64, 1);
        _cellCountSpin.ValueChanged += v => OnFieldEdited("CellCount", (int)v);
        grid.AddChild(_cellCountSpin);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Requires Adjacent" });
        _requiresAdjacentCheck = new CheckBox();
        _requiresAdjacentCheck.Toggled += b => OnFieldEdited("RequiresAdjacent", b);
        grid.AddChild(_requiresAdjacentCheck);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Configurable Behavior" });
        _configurableBehaviorButton = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _configurableBehaviorButton.ItemSelected += OnConfigurableBehaviorSelected;
        grid.AddChild(_configurableBehaviorButton);

        _behaviorConfigGrid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(_behaviorConfigGrid);
    }

    private static SpinBox MakeSpin(double min, double max, double step)
    {
        return new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            AllowGreater = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
    }

    private void RefreshControls()
    {
        if (_entry == null) return;
        UpdateBiomesButtonLabel();
        _minElevSpin.SetValueNoSignal(_entry.Placement.MinElevation);
        _maxElevSpin.SetValueNoSignal(_entry.Placement.MaxElevation);
        _maxSlopeSpin.SetValueNoSignal(_entry.Placement.MaxSlope);
        _cellCountSpin.SetValueNoSignal(_entry.Placement.CellCount);
        _requiresAdjacentCheck.SetPressedNoSignal(_entry.Placement.RequiresAdjacent);
        RebuildConfigurableBehaviorOptions();
        RebuildBehaviorConfigGrid();
    }

    private void UpdateBiomesButtonLabel()
    {
        if (_entry == null) return;
        if (_entry.Placement.AllowAnyBiome)
            _biomesButton.Text = "Biomes: * (any)";
        else
            _biomesButton.Text = $"Biomes ({_entry.Placement.Biomes.Count})";
    }

    private void RebuildConfigurableBehaviorOptions()
    {
        _configurableBehaviorButton.Clear();
        _behaviorChoices.Clear();
        _behaviorChoices.Add(""); // None
        _configurableBehaviorButton.AddItem("(none)", 0);
        var discovered = BuildingEditorModel.GetAllPlacementBehaviorTypes();
        for (int i = 0; i < discovered.Count; i++)
        {
            _behaviorChoices.Add(discovered[i]);
            _configurableBehaviorButton.AddItem(discovered[i], i + 1);
        }

        string current = _entry?.Placement.ConfigurableBehavior ?? "";
        int idx = _behaviorChoices.IndexOf(current);
        if (idx < 0 && !string.IsNullOrEmpty(current))
        {
            _behaviorChoices.Add(current);
            _configurableBehaviorButton.AddItem($"{current} (unknown)", _behaviorChoices.Count - 1);
            idx = _behaviorChoices.Count - 1;
        }
        _configurableBehaviorButton.Select(idx < 0 ? 0 : idx);
    }

    private void RebuildBehaviorConfigGrid()
    {
        foreach (var c in _behaviorConfigGrid.GetChildren()) c.QueueFree();
        if (_entry == null || string.IsNullOrEmpty(_entry.Placement.ConfigurableBehavior))
            return;

        var schema = PlacementBehaviorSchemaRegistry.GetSchema(_entry.Placement.ConfigurableBehavior);
        foreach (var field in schema)
        {
            _behaviorConfigGrid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = field.Name, TooltipText = field.Tooltip ?? "" });
            object? current = _entry.Placement.ConfigurableBehaviorConfig.TryGetValue(field.Name, out var v)
                ? v : field.Default;
            var control = BehaviorFieldControlFactory.Create(field, current,
                value => OnPlacementBehaviorConfigEdited(field.Name, value));
            _behaviorConfigGrid.AddChild(control);
        }
    }

    private void OnBiomesPressed()
    {
        if (_model == null || _entry == null) return;
        var popup = new BiomesPickerPopup();
        popup.Initialize(_model, _categoryName, _buildingIndex, _entry);
        AddChild(popup);
        popup.BiomesChanged += () =>
        {
            _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
            UpdateBiomesButtonLabel();
        };
        var rect = _biomesButton.GetGlobalRect();
        popup.Position = new Vector2I((int)rect.Position.X, (int)rect.End.Y);
        popup.Popup();
    }

    private void OnFieldEdited(string fieldName, object value)
    {
        if (_model == null || _entry == null) return;
        _model.UpdatePlacementField(_categoryName, _buildingIndex, fieldName, value);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
    }

    private void OnConfigurableBehaviorSelected(long index)
    {
        if (_model == null || _entry == null) return;
        if (index < 0 || index >= _behaviorChoices.Count) return;
        string chosen = _behaviorChoices[(int)index];
        _model.UpdatePlacementField(_categoryName, _buildingIndex, "ConfigurableBehavior", chosen);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildBehaviorConfigGrid();
    }

    private void OnPlacementBehaviorConfigEdited(string fieldName, object? value)
    {
        if (_model == null) return;
        _model.UpdatePlacementBehaviorConfig(_categoryName, _buildingIndex, fieldName, value);
    }
}
#endif
