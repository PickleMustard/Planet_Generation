#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary;
using Debug;

namespace DeveloperTools.Building2DEditor;

/// <summary>
/// Debug module for authoring <see cref="BuildingShape2D"/> entries.
/// Two-panel split: shape list on the left, vertex/slot editor on the right.
/// Save writes YAML files to res://Configuration/Building2D/ via
/// <see cref="Building2DEditorYamlIO"/>.
/// </summary>
public partial class Building2DEditorModule : BaseDebugModule
{
    public override string ModuleName => "Shapes (2D)";

    private const string ShapesDirectory = "res://Configuration/Building2D";

    private Building2DEditorModel? _model;
    private Building2DEditorModel.ShapeEditEntry? _selected;

    private ItemList _shapeList = null!;
    private LineEdit _idEdit = null!;
    private LineEdit _displayNameEdit = null!;
    private Building2DEditorCanvas _canvas = null!;
    private VBoxContainer _sidesContainer = null!;
    private Button _saveButton = null!;
    private Button _revertButton = null!;
    private Label _feedbackLabel = null!;
    private VBoxContainer _editorPane = null!;

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
        if (_model != null)
        {
            bool dirty = _model.HasUnsavedChanges;
            _saveButton.Disabled = !dirty;
            _revertButton.Disabled = !dirty;
            Name = dirty ? ModuleName + " *" : ModuleName;
        }
        if (_feedbackTimer > 0)
        {
            _feedbackTimer -= delta;
            if (_feedbackTimer <= 0) _feedbackLabel.Visible = false;
        }
    }

    public override void OnModuleEnabled()
    {
        if (_model == null) LoadModel();
        base.OnModuleEnabled();
    }

    private void BuildLayout()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var rootVBox = new VBoxContainer();
        rootVBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(rootVBox);

        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rootVBox.AddChild(split);
        var viewportWidth = (int)GetViewport().GetVisibleRect().Size.X;
        split.SplitOffsets = new[] { viewportWidth / 5 };

        var leftPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        leftPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.14f) });
        split.AddChild(leftPanel);

        var leftVBox = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        leftPanel.AddChild(leftVBox);

        var leftHeader = new Label { Text = "Shapes", HorizontalAlignment = HorizontalAlignment.Center };
        leftHeader.AddThemeFontSizeOverride("font_size", 14);
        leftVBox.AddChild(leftHeader);

        _shapeList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AllowReselect = true
        };
        _shapeList.ItemSelected += OnShapeSelected;
        leftVBox.AddChild(_shapeList);

        var newBtn = new Button { Text = "+ New", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        newBtn.Pressed += OnNewPressed;
        leftVBox.AddChild(newBtn);

        var dupBtn = new Button { Text = "Duplicate", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        dupBtn.Pressed += OnDuplicatePressed;
        leftVBox.AddChild(dupBtn);

        var delBtn = new Button { Text = "Delete", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        delBtn.Pressed += OnDeletePressed;
        leftVBox.AddChild(delBtn);

        var rightPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rightPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.14f) });
        split.AddChild(rightPanel);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rightPanel.AddChild(scroll);

        _editorPane = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _editorPane.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_editorPane);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _editorPane.AddChild(grid);

        grid.AddChild(new Label { Text = "Id" });
        _idEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _idEdit.TextChanged += OnIdChanged;
        grid.AddChild(_idEdit);

        grid.AddChild(new Label { Text = "Display Name" });
        _displayNameEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _displayNameEdit.TextChanged += OnDisplayNameChanged;
        grid.AddChild(_displayNameEdit);

        var canvasHeader = new Label { Text = "Vertices  (left-drag to move, right-click handle to remove, right-click edge to insert)" };
        canvasHeader.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _editorPane.AddChild(canvasHeader);

        _canvas = new Building2DEditorCanvas { CustomMinimumSize = new Vector2(360, 360) };
        _canvas.ShapeMutated += OnCanvasMutated;
        _editorPane.AddChild(_canvas);

        var sidesHeader = new Label { Text = "Sides" };
        sidesHeader.AddThemeFontSizeOverride("font_size", 13);
        _editorPane.AddChild(sidesHeader);

        _sidesContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _editorPane.AddChild(_sidesContainer);

        var toolbar = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rootVBox.AddChild(toolbar);
        toolbar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _revertButton = new Button { Text = "Revert", Disabled = true };
        _revertButton.Pressed += OnRevertPressed;
        toolbar.AddChild(_revertButton);

        _saveButton = new Button { Text = "Save", Disabled = true };
        _saveButton.Pressed += OnSavePressed;
        toolbar.AddChild(_saveButton);

        _feedbackLabel = new Label { Visible = false };
        _feedbackLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1.0f, 0.4f));
        toolbar.AddChild(_feedbackLabel);
    }

    private void LoadModel()
    {
        try
        {
            _model = new Building2DEditorModel(ShapesDirectory);
            _model.LoadFromDisk();
            RebuildShapeList();
            if (_shapeList.ItemCount > 0)
            {
                _shapeList.Select(0);
                OnShapeSelected(0);
            }
            else
            {
                SetSelected(null);
            }
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Building2DEditor: load failed: {ex.Message}");
            ShowAcceptDialog("Load Error", ex.Message);
        }
    }

    private void RebuildShapeList()
    {
        _shapeList.Clear();
        if (_model == null) return;
        foreach (var shape in _model.Shapes)
        {
            string label = shape.IsNew || shape.IsDirty ? $"{shape.Id} *" : shape.Id;
            _shapeList.AddItem(label);
        }
    }

    private void OnShapeSelected(long index)
    {
        if (_model == null || index < 0 || index >= _model.Shapes.Count)
        {
            SetSelected(null);
            return;
        }
        SetSelected(_model.Shapes[(int)index]);
    }

    private void SetSelected(Building2DEditorModel.ShapeEditEntry? entry)
    {
        _selected = entry;
        if (_selected == null)
        {
            _idEdit.Text = "";
            _displayNameEdit.Text = "";
            _canvas.SetEntry(null);
            RebuildSideRows();
            return;
        }
        _idEdit.Text = _selected.Id;
        _displayNameEdit.Text = _selected.DisplayName;
        _canvas.SetEntry(_selected);
        RebuildSideRows();
    }

    private void OnIdChanged(string text)
    {
        if (_selected == null) return;
        _selected.Id = text;
        _selected.IsDirty = true;
        RebuildShapeList();
    }

    private void OnDisplayNameChanged(string text)
    {
        if (_selected == null) return;
        _selected.DisplayName = text;
        _selected.IsDirty = true;
    }

    private void OnCanvasMutated()
    {
        if (_selected == null) return;
        RebuildSideRows();
        RebuildShapeList();
    }

    private void RebuildSideRows()
    {
        foreach (var child in _sidesContainer.GetChildren())
            child.QueueFree();
        if (_selected == null) return;

        for (int i = 0; i < _selected.Sides.Count; i++)
        {
            int sideIdx = i;
            var side = _selected.Sides[i];

            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _sidesContainer.AddChild(row);

            row.AddChild(new Label { Text = $"Side {i}: " });

            var countSpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = 16,
                Step = 1,
                Value = side.Slots.Count,
                AllowGreater = false,
            };
            countSpin.ValueChanged += v =>
            {
                int newCount = (int)v;
                while (side.Slots.Count < newCount)
                    side.Slots.Add(new BuildingShape2D.SlotSpec
                    {
                        Kind = ResourceNodeKind.Flex,
                        StateOfMatter = StateOfMatter.Solid,
                    });
                if (side.Slots.Count > newCount) side.Slots.RemoveRange(newCount, side.Slots.Count - newCount);
                _selected.IsDirty = true;
                RebuildSideRows();
                _canvas.QueueRedraw();
                RebuildShapeList();
            };
            row.AddChild(countSpin);

            for (int slot = 0; slot < side.Slots.Count; slot++)
            {
                int slotIdx = slot;
                var spec = side.Slots[slot];

                var opt = new OptionButton();
                opt.AddItem("import", 0);
                opt.AddItem("export", 1);
                opt.AddItem("flex", 2);
                opt.Select(spec.Kind switch
                {
                    ResourceNodeKind.Import => 0,
                    ResourceNodeKind.Export => 1,
                    _ => 2,
                });
                opt.ItemSelected += selected =>
                {
                    side.Slots[slotIdx].Kind = selected switch
                    {
                        0 => ResourceNodeKind.Import,
                        1 => ResourceNodeKind.Export,
                        _ => ResourceNodeKind.Flex,
                    };
                    _selected.IsDirty = true;
                    _canvas.QueueRedraw();
                    RebuildShapeList();
                };
                row.AddChild(opt);

                var stateOpt = new OptionButton();
                stateOpt.AddItem("solid", (int)StateOfMatter.Solid);
                stateOpt.AddItem("fluid", (int)StateOfMatter.Fluid);
                stateOpt.Select(spec.StateOfMatter == StateOfMatter.Solid ? 0 : 1);
                stateOpt.ItemSelected += selected =>
                {
                    side.Slots[slotIdx].StateOfMatter = selected == 0
                        ? StateOfMatter.Solid
                        : StateOfMatter.Fluid;
                    _selected.IsDirty = true;
                    _canvas.QueueRedraw();
                    RebuildShapeList();
                };
                row.AddChild(stateOpt);
            }
        }
    }

    private void OnNewPressed()
    {
        if (_model == null) return;
        string baseId = "new_shape";
        string id = baseId;
        int n = 1;
        while (_model.IdExists(id)) id = $"{baseId}_{n++}";
        var entry = _model.AddNewShape(id);
        RebuildShapeList();
        SelectEntry(entry);
    }

    private void OnDuplicatePressed()
    {
        if (_model == null || _selected == null) return;
        string baseId = _selected.Id + "_copy";
        string id = baseId;
        int n = 1;
        while (_model.IdExists(id)) id = $"{baseId}_{n++}";
        var entry = _model.DuplicateShape(_selected, id);
        RebuildShapeList();
        SelectEntry(entry);
    }

    private void OnDeletePressed()
    {
        if (_model == null || _selected == null) return;
        var dialog = new ConfirmationDialog
        {
            Title = "Delete Shape",
            DialogText = $"Delete shape '{_selected.Id}'? Does not take effect until Save."
        };
        var toDelete = _selected;
        dialog.Confirmed += () =>
        {
            _model.DeleteShape(toDelete);
            RebuildShapeList();
            SetSelected(null);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    private void SelectEntry(Building2DEditorModel.ShapeEditEntry entry)
    {
        if (_model == null) return;
        int idx = -1;
        for (int i = 0; i < _model.Shapes.Count; i++)
        {
            if (ReferenceEquals(_model.Shapes[i], entry)) { idx = i; break; }
        }
        if (idx < 0) return;
        _shapeList.Select(idx);
        OnShapeSelected(idx);
    }

    private void OnSavePressed()
    {
        if (_model == null) return;
        try
        {
            Building2DEditorYamlIO.WriteAll(ShapesDirectory, _model.Shapes, _model.LoadedSourceFiles);
            _model.LoadFromDisk();
            RebuildShapeList();
            SetSelected(null);
            ShowFeedback("Saved");
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Building2DEditor: save failed: {ex.Message}");
            ShowAcceptDialog("Save Error", ex.Message);
        }
    }

    private void OnRevertPressed()
    {
        if (_model == null) return;
        var dialog = new ConfirmationDialog
        {
            Title = "Revert",
            DialogText = "Discard unsaved changes?"
        };
        dialog.Confirmed += () =>
        {
            _model.LoadFromDisk();
            RebuildShapeList();
            SetSelected(null);
            ShowFeedback("Reverted");
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(350, 100));
    }

    private void ShowFeedback(string msg)
    {
        _feedbackLabel.Text = msg;
        _feedbackLabel.Visible = true;
        _feedbackTimer = 2.0;
    }

    private void ShowAcceptDialog(string title, string message)
    {
        var dialog = new AcceptDialog { Title = title, DialogText = message };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }
}
#endif
