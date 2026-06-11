#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Debug;
using Structures;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.SystemTemplateEditor;

/// <summary>
/// Fast, parameter-only editor for system templates. Two columns: a card hierarchy on the left
/// (one scroll column per dominant root) and a 2D design board on the right (wired in M5). Edits the
/// template YAML directly via <see cref="SystemTemplateModel"/> / <see cref="SystemTemplateEditorYamlIO"/>
/// and never runs mesh generation. UI is built programmatically; the matching .tscn is a bare root
/// <see cref="Control"/> with this script attached.
/// </summary>
public partial class SystemTemplateEditorModule : BaseDebugModule
{
    public override string ModuleName => "System Templates";

    private const string TemplateDir = "res://Configuration/SystemTemplate/";

    private SystemTemplateModel? _model;

    private OptionButton _filePicker = null!;
    private HBoxContainer _columnsHost = null!;
    private Control _boardHost = null!;
    private SystemTemplateBoard _board = null!;
    private VBoxContainer _detailsHost = null!;
    private Button _saveButton = null!;
    private Button _revertButton = null!;
    private Label _feedbackLabel = null!;
    private double _feedbackTimer;

    private readonly List<string> _templatePaths = new();

    public override void _Ready()
    {
        base._Ready();
        BuildLayout();
        PopulateFilePicker();
        if (_templatePaths.Count > 0)
            LoadFile(_templatePaths[0]);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdateButtonStates();
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

        // ── Toolbar ──────────────────────────────────────────────────────────
        var toolbar = new PanelContainer();
        rootVBox.AddChild(toolbar);

        var toolbarHBox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        toolbar.AddChild(toolbarHBox);

        toolbarHBox.AddChild(new Label { Text = "Template:" });

        _filePicker = new OptionButton();
        _filePicker.ItemSelected += OnFileSelected;
        toolbarHBox.AddChild(_filePicker);

        var newButton = new Button { Text = "New" };
        newButton.Pressed += OnNewPressed;
        toolbarHBox.AddChild(newButton);

        var addDominantButton = new Button { Text = "+ Dominant" };
        addDominantButton.Pressed += OnAddDominantPressed;
        toolbarHBox.AddChild(addDominantButton);

        var addBeltButton = new Button { Text = "+ Belt" };
        addBeltButton.Pressed += OnAddBeltPressed;
        toolbarHBox.AddChild(addBeltButton);

        toolbarHBox.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _revertButton = new Button { Text = "Revert", Disabled = true };
        _revertButton.Pressed += OnRevertPressed;
        toolbarHBox.AddChild(_revertButton);

        _saveButton = new Button { Text = "Save", Disabled = true };
        _saveButton.Pressed += OnSavePressed;
        toolbarHBox.AddChild(_saveButton);

        _feedbackLabel = new Label { Visible = false };
        toolbarHBox.AddChild(_feedbackLabel);

        // ── Body split: cards (1/5) | board (4/5) ──────────────────────────────
        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        rootVBox.AddChild(split);
        var viewportWidth = (int)GetViewport().GetVisibleRect().Size.X;
        split.SplitOffsets = new[] { viewportWidth / 5 };

        var leftPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        split.AddChild(leftPanel);

        var leftScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        leftPanel.AddChild(leftScroll);

        _columnsHost = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _columnsHost.AddThemeConstantOverride("separation", 12);
        leftScroll.AddChild(_columnsHost);

        var rightPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        split.AddChild(rightPanel);

        _boardHost = rightPanel;
        _board = new SystemTemplateBoard();
        _board.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        rightPanel.AddChild(_board);

        // ── Bottom details ─────────────────────────────────────────────────
        _detailsHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rootVBox.AddChild(_detailsHost);
    }

    // ─── File handling ────────────────────────────────────────────────────

    private void PopulateFilePicker()
    {
        _filePicker.Clear();
        _templatePaths.Clear();
        foreach (var path in BaseConfigLoader.GetYamlFilesInDir(TemplateDir).OrderBy(p => p))
        {
            _templatePaths.Add(path);
            _filePicker.AddItem(System.IO.Path.GetFileName(path));
        }
    }

    private void OnFileSelected(long index)
    {
        if (index < 0 || index >= _templatePaths.Count)
            return;
        LoadFile(_templatePaths[(int)index]);
    }

    private void LoadFile(string path)
    {
        try
        {
            var data = TemplateHelpers.LoadSystemTemplate(path);
            string fileName = System.IO.Path.GetFileName(path);
            _model = SystemTemplateEditorYamlIO.FromTemplate(data, fileName);
            RebuildColumns();
            _board.SetModel(_model);
            BindDetails();
            ShowFeedback($"Loaded {fileName}");
        }
        catch (Exception ex)
        {
            GameLogger.Error($"SystemTemplateEditor: failed to load {path}: {ex.Message}");
            ShowAcceptDialog("Load Error", ex.Message);
        }
    }

    // ─── Card columns ─────────────────────────────────────────────────────

    private void RebuildColumns()
    {
        foreach (var child in _columnsHost.GetChildren())
            child.QueueFree();

        if (_model == null)
            return;

        if (_model.Roots.Count == 0)
        {
            _columnsHost.AddChild(new Label
            {
                Text = "No dominant bodies. Use \"+ Dominant\" to start.",
            });
            return;
        }

        foreach (var root in _model.Roots)
        {
            var column = new VBoxContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(340, 0),
            };
            column.AddThemeConstantOverride("separation", 4);
            _columnsHost.AddChild(column);
            BuildCardTree(column, root, 0);
        }
    }

    private void BuildCardTree(VBoxContainer column, BodyNode node, int depth)
    {
        var card = CelestialBodyCard.Create(_model!, node);
        card.StructureChanged += RebuildColumns;

        if (depth > 0)
        {
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", depth * 18);
            margin.AddChild(card);
            column.AddChild(margin);
        }
        else
        {
            column.AddChild(card);
        }

        foreach (var child in node.Children)
            BuildCardTree(column, child, depth + 1);
    }

    // ─── Toolbar actions ──────────────────────────────────────────────────

    private void OnNewPressed()
    {
        _model = new SystemTemplateModel { SourceFileName = null, IsDirty = false };
        _model.AddRoot(SystemTemplateFactory.NewDominant("sol"));
        _model.IsDirty = false;
        RebuildColumns();
        _board.SetModel(_model);
        BindDetails();
    }

    private void BindDetails()
    {
        foreach (var child in _detailsHost.GetChildren())
            child.QueueFree();
        if (_model == null)
            return;
        var details = new SubtypeWeightEditor { Visible = false };
        details.Initialize(_model);
        _detailsHost.AddChild(details);
    }

    private void OnAddDominantPressed()
    {
        if (_model == null)
            return;
        _model.AddRoot(SystemTemplateFactory.NewDominant($"dominant_{_model.Roots.Count + 1}"));
        RebuildColumns();
    }

    private void OnAddBeltPressed()
    {
        if (_model == null || _model.Roots.Count == 0)
            return;
        // Belts hang directly off a dominant; attach to the selected root, else the first.
        var parent = _model.Selected;
        while (parent != null && !parent.IsDominant)
            parent = parent.Parent;
        parent ??= _model.Roots[0];
        _model.AddChild(parent, SystemTemplateFactory.NewBelt($"belt_{parent.Children.Count + 1}"));
        RebuildColumns();
    }

    private void OnSavePressed()
    {
        if (_model == null)
            return;
        string? fileName = _model.SourceFileName;
        if (string.IsNullOrEmpty(fileName))
        {
            PromptFileName(name => DoSave(name));
            return;
        }
        DoSave(fileName);
    }

    private void DoSave(string fileName)
    {
        if (_model == null)
            return;
        if (SystemTemplateEditorYamlIO.Save(_model, fileName, out string error))
        {
            PopulateFilePicker();
            SelectFilePicker(_model.SourceFileName);
            ShowFeedback($"Saved {_model.SourceFileName}");
        }
        else
        {
            ShowAcceptDialog("Save Error", error);
        }
    }

    private void OnRevertPressed()
    {
        if (_model?.SourceFileName == null)
            return;
        string path = TemplateDir + _model.SourceFileName;
        LoadFile(path);
    }

    private void PromptFileName(Action<string> onConfirm)
    {
        var lineEdit = new LineEdit
        {
            PlaceholderText = "filename (no spaces, .yaml optional)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var dialog = new ConfirmationDialog
        {
            Title = "Save Template As",
            DialogText = "File name:",
        };
        dialog.AddChild(lineEdit);
        dialog.Confirmed += () =>
        {
            string name = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(name) || name.Contains(' ') || name.Contains('/'))
            {
                ShowAcceptDialog("Invalid Name", "File name cannot be empty or contain spaces/slashes.");
                return;
            }
            onConfirm(name);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    private void SelectFilePicker(string? fileName)
    {
        if (fileName == null)
            return;
        for (int i = 0; i < _templatePaths.Count; i++)
        {
            if (System.IO.Path.GetFileName(_templatePaths[i]) == fileName)
            {
                _filePicker.Select(i);
                return;
            }
        }
    }

    // ─── Toolbar state / feedback ─────────────────────────────────────────

    private void UpdateButtonStates()
    {
        bool dirty = _model?.IsDirty ?? false;
        _saveButton.Disabled = _model == null;
        _revertButton.Disabled = !dirty || _model?.SourceFileName == null;
        Name = dirty ? ModuleName + " *" : ModuleName;
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
        if (_feedbackTimer <= 0)
            return;
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

    private void ShowAcceptDialog(string title, string message)
    {
        var dialog = new AcceptDialog { Title = title, DialogText = message };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    public override void OnModuleEnabled()
    {
        base.OnModuleEnabled();
    }
}
#endif
