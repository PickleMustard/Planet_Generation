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

    private CheckBox _wildcardCheck = null!;
    private HFlowContainer _currentFlow = null!;
    private VBoxContainer _categoryListVBox = null!;
    private HFlowContainer _biomeFlow = null!;
    private Button _closeButton = null!;

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
        BuildLayout();
        RefreshDisplay();
    }

    private void BuildLayout()
    {
        Size = new Vector2I(500, 600);

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(480, 580) };
        AddChild(root);

        // Top: wildcard + current selection pills
        _wildcardCheck = new CheckBox { Text = "Allow any biome (*)" };
        _wildcardCheck.Toggled += OnWildcardToggled;
        root.AddChild(_wildcardCheck);

        var currentLabel = new Label { Text = "Current selection:" };
        currentLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 1.0f));
        root.AddChild(currentLabel);

        _currentFlow = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddChild(_currentFlow);

        root.AddChild(new HSeparator());

        // Scroll body
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(480, 380)
        };
        root.AddChild(scroll);

        var scrollBody = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(scrollBody);

        var catHeader = new Label { Text = "Biome Categories" };
        catHeader.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.6f));
        scrollBody.AddChild(catHeader);

        _categoryListVBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scrollBody.AddChild(_categoryListVBox);

        scrollBody.AddChild(new HSeparator());

        var biomeHeader = new Label { Text = "Individual Biomes" };
        biomeHeader.AddThemeColorOverride("font_color", new Color(0.8f, 1.0f, 0.8f));
        scrollBody.AddChild(biomeHeader);

        _biomeFlow = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scrollBody.AddChild(_biomeFlow);

        _closeButton = new Button { Text = "Close" };
        _closeButton.Pressed += OnClosePressed;
        root.AddChild(_closeButton);
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
