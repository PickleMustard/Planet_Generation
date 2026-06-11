#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Popup for selecting placement biomes. Supports:
///  - wildcard "*" (allow any biome)
///  - "category:&lt;name&gt;" entries (resolved at runtime to a set of biomes)
///  - individual Biome.BiomeType enum entries
/// Current selection renders as removable pills; available options render
/// as section-grouped CheckBoxes (categories + individual biomes).
/// Built programmatically.
/// </summary>
public partial class BiomesPickerPopup : PopupPanel
{
    [Signal]
    public delegate void BiomesChangedEventHandler();

    private BuildingEditorModel? _model;
    private string _categoryName = "";
    private int _buildingIndex;
    private BuildingEditorModel.BuildingEditEntry? _entry;

    [Export] private CheckBox _wildcardCheck = null!;
    [Export] private HFlowContainer _currentFlow = null!;
    [Export] private VBoxContainer _categoryListVBox = null!;
    [Export] private HFlowContainer _biomeFlow = null!;

    private static PackedScene? _scene;

    public static BiomesPickerPopup Create(
        BuildingEditorModel model,
        string categoryName,
        int buildingIndex,
        BuildingEditorModel.BuildingEditEntry entry)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/BuildingEditor/BiomesPickerPopup.tscn");
        var popup = _scene.Instantiate<BiomesPickerPopup>();
        popup.Initialize(model, categoryName, buildingIndex, entry);
        return popup;
    }

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
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (_entry == null) return;

        _wildcardCheck.SetPressedNoSignal(_entry.Placement.AllowAnyBiome);
        RefreshCurrentPills();
        RefreshCategoryRows();
        RefreshBiomeBoxes();
    }

    private void RefreshCurrentPills()
    {
        foreach (var c in _currentFlow.GetChildren()) c.QueueFree();
        if (_entry == null) return;

        if (_entry.Placement.AllowAnyBiome)
        {
            var pill = new Button { Text = "*" };
            pill.Pressed += () => OnWildcardToggled(false);
            _currentFlow.AddChild(pill);
            return;
        }

        foreach (var entry in _entry.Placement.Biomes.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            var pill = new Button { Text = $"{entry}  ✕" };
            pill.AddThemeFontSizeOverride("font_size", 11);
            string captured = entry;
            pill.Pressed += () => RemoveBiome(captured);
            _currentFlow.AddChild(pill);
        }
    }

    private void RefreshCategoryRows()
    {
        foreach (var c in _categoryListVBox.GetChildren()) c.QueueFree();
        if (_entry == null) return;

        bool disabled = _entry.Placement.AllowAnyBiome;
        foreach (var cat in BuildingEditorModel.GetAllBiomeCategories())
        {
            string token = $"category:{cat}";
            var cb = new CheckBox
            {
                Text = token,
                ButtonPressed = _entry.Placement.Biomes.Contains(token),
                Disabled = disabled
            };
            string captured = token;
            cb.Toggled += pressed => OnBiomeToggled(captured, pressed);
            _categoryListVBox.AddChild(cb);
        }
    }

    private void RefreshBiomeBoxes()
    {
        foreach (var c in _biomeFlow.GetChildren()) c.QueueFree();
        if (_entry == null) return;

        bool disabled = _entry.Placement.AllowAnyBiome;
        foreach (var biome in BuildingEditorModel.GetAllBiomes())
        {
            var cb = new CheckBox
            {
                Text = biome,
                ButtonPressed = _entry.Placement.Biomes.Contains(biome),
                Disabled = disabled
            };
            string captured = biome;
            cb.Toggled += pressed => OnBiomeToggled(captured, pressed);
            _biomeFlow.AddChild(cb);
        }
    }

    private void OnWildcardToggled(bool pressed)
    {
        if (_model == null || _entry == null) return;
        _model.SetAllowAnyBiome(_categoryName, _buildingIndex, pressed);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RefreshDisplay();
        EmitSignal(SignalName.BiomesChanged);
    }

    private void OnBiomeToggled(string token, bool pressed)
    {
        if (_model == null || _entry == null) return;
        var next = new HashSet<string>(_entry.Placement.Biomes, StringComparer.OrdinalIgnoreCase);
        if (pressed) next.Add(token); else next.Remove(token);
        _model.SetPlacementBiomes(_categoryName, _buildingIndex, next);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RefreshCurrentPills();
        EmitSignal(SignalName.BiomesChanged);
    }

    private void RemoveBiome(string token)
    {
        if (_model == null || _entry == null) return;
        var next = new HashSet<string>(_entry.Placement.Biomes, StringComparer.OrdinalIgnoreCase);
        next.Remove(token);
        _model.SetPlacementBiomes(_categoryName, _buildingIndex, next);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RefreshDisplay();
        EmitSignal(SignalName.BiomesChanged);
    }

    private void OnClosePressed()
    {
        Hide();
        QueueFree();
    }
}
#endif
