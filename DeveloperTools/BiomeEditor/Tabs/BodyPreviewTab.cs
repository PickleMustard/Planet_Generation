#if DEBUG
using System;
using Godot;
using ProceduralGeneration.BiomeSystem;
using Structures.Enums;
using UtilityLibrary;

namespace DeveloperTools.BiomeEditor.Tabs;

/// <summary>
/// Pushes the in-memory assigner edits into BiomeAssignerConfigDatabase via ReplaceInMemory
/// so any spawned planet (in this tab or the game scene) uses the unsaved config. A SubViewport
/// is set up but mesh instancing is deferred to the user pressing View Planet, which currently
/// only logs the push since full UnifiedCelestialMesh boot from the editor requires a parent
/// CelestialBody pipeline beyond the scope of this preview.
/// </summary>
public partial class BodyPreviewTab : Control
{
    private BiomeEditorModel? _model;
    private OptionButton _subtypeOpt = null!;
    private SpinBox _seedSpin = null!;
    private Label _statusLabel = null!;
    private SubViewport _viewport = null!;
    private Node3D? _previewRoot;

    public override void _Ready()
    {
        base._Ready();
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLayout();
    }

    public void Initialize(BiomeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    private void BuildLayout()
    {
        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(root);

        var ctrlRow = new HBoxContainer();
        ctrlRow.AddChild(new Label { Text = "Subtype:" });
        _subtypeOpt = new OptionButton();
        foreach (var s in Enum.GetNames<RockyPlanetSubtype>()) _subtypeOpt.AddItem(s);
        ctrlRow.AddChild(_subtypeOpt);

        ctrlRow.AddChild(new Label { Text = "Seed:" });
        _seedSpin = new SpinBox { MinValue = 0, MaxValue = 99999, Step = 1, Value = 42 };
        ctrlRow.AddChild(_seedSpin);

        var viewBtn = new Button { Text = "View Planet" };
        viewBtn.Pressed += OnViewPressed;
        ctrlRow.AddChild(viewBtn);
        root.AddChild(ctrlRow);

        _statusLabel = new Label
        {
            Text = "Preview reflects in-memory config; click Save to persist.",
        };
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.4f));
        root.AddChild(_statusLabel);

        var container = new SubViewportContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Stretch = true,
        };
        root.AddChild(container);

        _viewport = new SubViewport
        {
            Size = new Vector2I(768, 768),
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
        };
        container.AddChild(_viewport);

        _previewRoot = new Node3D();
        _viewport.AddChild(_previewRoot);

        var camera = new Camera3D { Position = new Vector3(0, 0, 8) };
        _previewRoot.AddChild(camera);
        var light = new DirectionalLight3D { Rotation = new Vector3(-0.6f, -0.3f, 0) };
        _previewRoot.AddChild(light);
    }

    private void OnViewPressed()
    {
        if (_model == null) return;
        try
        {
            BiomeAssignerConfigDatabase.Instance.ReplaceInMemory(_model.BuildAssignerConfig());
            var subtype = (RockyPlanetSubtype)_subtypeOpt.Selected;
            int seed = (int)_seedSpin.Value;
            _statusLabel.Text = $"Pushed in-memory assigner config. Subtype={subtype}, seed={seed}. " +
                                "(Live mesh regen TODO — config now active for next scene reload.)";
            GameLogger.Info($"BiomeEditor BodyPreview: pushed assigner config for {subtype} (seed {seed})");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "View failed: " + ex.Message;
            GameLogger.Error($"BodyPreview view failed: {ex.Message}");
        }
    }
}
#endif
