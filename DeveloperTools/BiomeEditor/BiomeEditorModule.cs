#if DEBUG
using System;
using Godot;
using UtilityLibrary;
using Debug;
using DeveloperTools.BiomeEditor.Tabs;

namespace DeveloperTools.BiomeEditor;

/// <summary>
/// Debug module exposing all biome-related YAML configuration through a single tabbed UI.
/// Tabs: Tables (edit), Whittaker (assigner preview), ResourceHeatmap (per-biome weights),
/// BodyPreview (3D regen). Save/Revert toolbar buffers writes.
/// </summary>
public partial class BiomeEditorModule : BaseDebugModule
{
    public override string ModuleName => "Biomes";

    private BiomeEditorModel? _model;

    private TabContainer _tabContainer = null!;
    private TablesTab _tablesTab = null!;
    private WhittakerTab _whittakerTab = null!;
    private ResourceHeatmapTab _heatmapTab = null!;
    private BodyPreviewTab _bodyPreviewTab = null!;
    private Button _saveButton = null!;
    private Button _revertButton = null!;
    private Label _feedbackLabel = null!;
    private double _feedbackTimer;

    public override void _Ready()
    {
        base._Ready();
        BuildLayout();
        LoadModel();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdateButtonStates();
        UpdateDirtyIndicator();
        UpdateFeedbackLabel(delta);
    }

    private void BuildLayout()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var rootVBox = new VBoxContainer();
        rootVBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        rootVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rootVBox.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(rootVBox);

        _tabContainer = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        rootVBox.AddChild(_tabContainer);

        _tablesTab = new TablesTab { Name = "Tables" };
        _tabContainer.AddChild(_tablesTab);

        _whittakerTab = new WhittakerTab { Name = "Whittaker" };
        _tabContainer.AddChild(_whittakerTab);

        _heatmapTab = new ResourceHeatmapTab { Name = "Resource Heatmap" };
        _tabContainer.AddChild(_heatmapTab);

        _bodyPreviewTab = new BodyPreviewTab { Name = "Body Preview" };
        _tabContainer.AddChild(_bodyPreviewTab);

        // ── Toolbar ──────────────────────────────────────────────────────
        var toolbar = new PanelContainer();
        toolbar.AddThemeStyleboxOverride("panel", MakeStyleBox(new Color(0.10f, 0.10f, 0.12f)));
        rootVBox.AddChild(toolbar);

        var toolbarHBox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        toolbar.AddChild(toolbarHBox);
        toolbarHBox.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _revertButton = new Button { Text = "Revert", Disabled = true };
        _revertButton.Pressed += OnRevertPressed;
        toolbarHBox.AddChild(_revertButton);

        _saveButton = new Button { Text = "Save", Disabled = true };
        _saveButton.Pressed += OnSavePressed;
        toolbarHBox.AddChild(_saveButton);

        _feedbackLabel = new Label { Visible = false };
        _feedbackLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1.0f, 0.4f));
        _feedbackLabel.AddThemeFontSizeOverride("font_size", 12);
        toolbarHBox.AddChild(_feedbackLabel);
    }

    private static StyleBoxFlat MakeStyleBox(Color color) => new() { BgColor = color };

    private void LoadModel()
    {
        try
        {
            _model = new BiomeEditorModel();
            _model.LoadFromDisk();
            _tablesTab.Initialize(_model);
            _whittakerTab.Initialize(_model);
            _heatmapTab.Initialize(_model);
            _bodyPreviewTab.Initialize(_model);
        }
        catch (Exception ex)
        {
            GameLogger.Error($"BiomeEditor load failed: {ex.Message}");
            ShowAcceptDialog("Load Error", ex.Message);
        }
    }

    private void OnSavePressed()
    {
        if (_model == null) return;
        try
        {
            BiomeEditorYamlIO.WriteAll(_model);
            _model.LoadFromDisk();
            _tablesTab.Refresh();
            ShowFeedback("Saved successfully!");
        }
        catch (Exception ex)
        {
            GameLogger.Error($"BiomeEditor save failed: {ex.Message}");
            ShowAcceptDialog("Save Error", ex.Message);
        }
    }

    private void OnRevertPressed()
    {
        if (_model == null || !_model.HasUnsavedChanges) return;
        var dialog = new ConfirmationDialog
        {
            Title = "Revert",
            DialogText = "Discard all unsaved changes?",
        };
        dialog.Confirmed += () =>
        {
            try
            {
                _model.LoadFromDisk();
                _tablesTab.Refresh();
                ShowFeedback("Reverted");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"BiomeEditor revert failed: {ex.Message}");
                ShowAcceptDialog("Revert Error", ex.Message);
            }
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(350, 100));
    }

    private void UpdateButtonStates()
    {
        if (_model == null) return;
        bool dirty = _model.HasUnsavedChanges;
        _saveButton.Disabled = !dirty;
        _revertButton.Disabled = !dirty;
    }

    private void UpdateDirtyIndicator()
    {
        if (_model == null) return;
        Name = _model.HasUnsavedChanges ? ModuleName + " *" : ModuleName;
    }

    private void ShowFeedback(string message)
    {
        _feedbackTimer = 2.0;
        _feedbackLabel.Text = message;
        _feedbackLabel.Visible = true;
        _feedbackLabel.Modulate = Colors.White;
    }

    private void UpdateFeedbackLabel(double delta)
    {
        if (_feedbackTimer <= 0) return;
        _feedbackTimer -= delta;
        if (_feedbackTimer <= 0)
        {
            _feedbackLabel.Visible = false;
            _feedbackTimer = 0;
        }
        else
        {
            float alpha = _feedbackTimer < 0.5 ? (float)_feedbackTimer / 0.5f : 1.0f;
            _feedbackLabel.Modulate = new Color(1f, 1f, 1f, alpha);
        }
    }

    public override void OnModuleEnabled()
    {
        if (_model == null) LoadModel();
        base.OnModuleEnabled();
    }

    private void ShowAcceptDialog(string title, string message)
    {
        var dialog = new AcceptDialog { Title = title, DialogText = message };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(420, 150));
    }
}
#endif
