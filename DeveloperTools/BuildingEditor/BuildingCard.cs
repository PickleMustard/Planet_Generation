#if DEBUG
using System;
using Godot;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Per-building card with inline editing for scalar fields plus the four
/// composite subsections (required resources, placement, behaviors, visual)
/// and an icon block. Built programmatically; subsections live in their own
/// child classes (RequiredResourceRow, PlacementRequirementsSection,
/// BehaviorRow, VisualSection, IconSection) and are wired in later passes.
/// </summary>
public partial class BuildingCard : PanelContainer
{
    [Signal]
    public delegate void CardsNeedRebuildEventHandler();

    private BuildingEditorModel? _model;
    private string _categoryName = "";
    private int _buildingIndex;
    private BuildingEditorModel.BuildingEditEntry? _entry;

    private LineEdit _idNameEdit = null!;
    private LineEdit _displayNameEdit = null!;
    private TextEdit _descriptionEdit = null!;
    private LineEdit _categoryEdit = null!;
    private SpinBox _maxResourceTierSpin = null!;
    private SpinBox _workRequiredSpin = null!;
    private SpinBox _buildingLimitSpin = null!;
    private CheckBox _demolishableCheck = null!;
    private LineEdit _linkProfileEdit = null!;
    private LineEdit _allowedRecipeCategoryEdit = null!;

    private VBoxContainer _requiredResourcesContainer = null!;
    private Button _addRequiredResourceButton = null!;

    private VBoxContainer _placementContainer = null!;
    private VBoxContainer _behaviorsContainer = null!;
    private Button _addBehaviorButton = null!;
    private VBoxContainer _visualContainer = null!;
    private VBoxContainer _iconContainer = null!;

    private Button _moveUpButton = null!;
    private Button _moveDownButton = null!;
    private Button _deleteButton = null!;

    private PlacementRequirementsSection? _placementSection;
    private VisualSection? _visualSection;
    private IconSection? _iconSection;

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
        Name = $"BuildingCard_{entry.IdName}";
    }

    public override void _Ready()
    {
        base._Ready();
        BuildLayout();
        RefreshControls();
    }

    private void BuildLayout()
    {
        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.16f, 0.19f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.3f, 0.3f, 0.35f),
            ContentMarginLeft = 8,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3
        };
        AddThemeStyleboxOverride("panel", styleBox);

        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(root);

        // ── Header row ──────────────────────────────────────────────────
        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(header);

        var headerFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(headerFields);

        _idNameEdit = new LineEdit
        {
            PlaceholderText = "id_name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _idNameEdit.TextChanged += t => OnFieldEdited("IdName", t);
        headerFields.AddChild(_idNameEdit);

        _displayNameEdit = new LineEdit
        {
            PlaceholderText = "Display Name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _displayNameEdit.TextChanged += t => OnFieldEdited("DisplayName", t);
        headerFields.AddChild(_displayNameEdit);

        var actions = new VBoxContainer();
        _moveUpButton = new Button { Text = "▲", TooltipText = "Move up" };
        _moveUpButton.Pressed += OnMoveUpPressed;
        actions.AddChild(_moveUpButton);

        _moveDownButton = new Button { Text = "▼", TooltipText = "Move down" };
        _moveDownButton.Pressed += OnMoveDownPressed;
        actions.AddChild(_moveDownButton);

        _deleteButton = new Button { Text = "✕", TooltipText = "Delete building" };
        _deleteButton.Pressed += OnDeletePressed;
        actions.AddChild(_deleteButton);
        header.AddChild(actions);

        // ── Scalar grid ─────────────────────────────────────────────────
        var fields = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(fields);

        fields.AddChild(new Label { Text = "Category" });
        _categoryEdit = new LineEdit
        {
            PlaceholderText = "power / extraction / agriculture / ...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _categoryEdit.TextChanged += t => OnFieldEdited("Category", t);
        fields.AddChild(_categoryEdit);

        fields.AddChild(new Label { Text = "Description" });
        _descriptionEdit = new TextEdit
        {
            PlaceholderText = "Building description",
            CustomMinimumSize = new Vector2(0, 60),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        _descriptionEdit.TextChanged += () => OnFieldEdited("Description", _descriptionEdit.Text);
        fields.AddChild(_descriptionEdit);

        fields.AddChild(new Label { Text = "Max Resource Tier" });
        _maxResourceTierSpin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 10,
            Step = 1,
            TooltipText = "Maximum resource tier this building can interact with",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _maxResourceTierSpin.ValueChanged += v => OnFieldEdited("MaxResourceTier", (int)v);
        fields.AddChild(_maxResourceTierSpin);

        fields.AddChild(new Label { Text = "Work Required" });
        _workRequiredSpin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 100000,
            Step = 0.5f,
            AllowGreater = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _workRequiredSpin.ValueChanged += v => OnFieldEdited("WorkRequired", (float)v);
        fields.AddChild(_workRequiredSpin);

        fields.AddChild(new Label { Text = "Build Limit" });
        _buildingLimitSpin = new SpinBox
        {
            MinValue = -1,
            MaxValue = 1000000,
            Step = 1,
            AllowGreater = true,
            TooltipText = "-1 = no limit",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _buildingLimitSpin.ValueChanged += v => OnFieldEdited("BuildingLimit", (int)v);
        fields.AddChild(_buildingLimitSpin);

        fields.AddChild(new Label { Text = "Demolishable" });
        _demolishableCheck = new CheckBox();
        _demolishableCheck.Toggled += b => OnFieldEdited("Demolishable", b);
        fields.AddChild(_demolishableCheck);

        fields.AddChild(new Label { Text = "Link Profile" });
        _linkProfileEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _linkProfileEdit.TextChanged += t => OnFieldEdited("LinkProfile", t);
        fields.AddChild(_linkProfileEdit);

        fields.AddChild(new Label { Text = "Allowed Recipe Cat." });
        _allowedRecipeCategoryEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _allowedRecipeCategoryEdit.TextChanged += t => OnFieldEdited("AllowedRecipeCategory", t);
        fields.AddChild(_allowedRecipeCategoryEdit);

        // ── Required Resources subsection ───────────────────────────────
        var reqHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var reqLabel = new Label
        {
            Text = "Required Resources",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        reqLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.6f));
        reqHeader.AddChild(reqLabel);
        _addRequiredResourceButton = new Button { Text = "+ Required Resource" };
        _addRequiredResourceButton.Pressed += OnAddRequiredResourcePressed;
        reqHeader.AddChild(_addRequiredResourceButton);
        root.AddChild(reqHeader);

        _requiredResourcesContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_requiredResourcesContainer);

        // ── Placement Requirements subsection ───────────────────────────
        var placementLabel = new Label { Text = "Placement Requirements" };
        placementLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 1.0f));
        root.AddChild(placementLabel);
        _placementContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_placementContainer);

        // ── Behaviors subsection ────────────────────────────────────────
        var behHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var behLabel = new Label
        {
            Text = "Behaviors",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        behLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.7f, 0.9f));
        behHeader.AddChild(behLabel);
        _addBehaviorButton = new Button { Text = "+ Behavior" };
        _addBehaviorButton.Pressed += OnAddBehaviorPressed;
        behHeader.AddChild(_addBehaviorButton);
        root.AddChild(behHeader);
        _behaviorsContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_behaviorsContainer);

        // ── Visual subsection ───────────────────────────────────────────
        var visualLabel = new Label { Text = "Visual" };
        visualLabel.AddThemeColorOverride("font_color", new Color(0.7f, 1.0f, 0.7f));
        root.AddChild(visualLabel);
        _visualContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_visualContainer);

        // ── Icon subsection ─────────────────────────────────────────────
        var iconLabel = new Label { Text = "Icon" };
        iconLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.7f));
        root.AddChild(iconLabel);
        _iconContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_iconContainer);
    }

    private void RefreshControls()
    {
        if (_entry == null || _model == null) return;

        if (_idNameEdit.Text != _entry.IdName) _idNameEdit.Text = _entry.IdName;
        if (_displayNameEdit.Text != _entry.DisplayName) _displayNameEdit.Text = _entry.DisplayName;
        if (_descriptionEdit.Text != _entry.Description) _descriptionEdit.Text = _entry.Description;
        if (_categoryEdit.Text != _entry.Category) _categoryEdit.Text = _entry.Category;
        _maxResourceTierSpin.SetValueNoSignal(_entry.MaxResourceTier);
        _workRequiredSpin.SetValueNoSignal(_entry.WorkRequired);
        _buildingLimitSpin.SetValueNoSignal(_entry.BuildingLimit);
        _demolishableCheck.SetPressedNoSignal(_entry.Demolishable);
        string lp = _entry.LinkProfile ?? "";
        if (_linkProfileEdit.Text != lp) _linkProfileEdit.Text = lp;
        string arc = _entry.AllowedRecipeCategory ?? "";
        if (_allowedRecipeCategoryEdit.Text != arc) _allowedRecipeCategoryEdit.Text = arc;

        RebuildRequiredResourceRows();
        RebuildPlacementSection();
        RebuildBehaviorRows();
        RebuildVisualSection();
        RebuildIconSection();

        if (_model.Categories.TryGetValue(_categoryName, out var cat))
        {
            _moveUpButton.Disabled = _buildingIndex <= 0;
            _moveDownButton.Disabled = _buildingIndex >= cat.Buildings.Count - 1;
        }
    }

    // ─── Subsection rebuilds (filled in by later passes) ─────────────────

    private void RebuildRequiredResourceRows()
    {
        foreach (var c in _requiredResourcesContainer.GetChildren()) c.QueueFree();
        if (_entry == null || _model == null) return;
        for (int i = 0; i < _entry.RequiredResources.Count; i++)
        {
            var row = new RequiredResourceRow();
            row.Configure(i, _entry.RequiredResources[i]);
            row.SlotChanged += OnRequiredResourceChanged;
            row.SlotDeleted += OnRequiredResourceDeleted;
            _requiredResourcesContainer.AddChild(row);
        }
    }

    private void RebuildPlacementSection()
    {
        foreach (var c in _placementContainer.GetChildren()) c.QueueFree();
        _placementSection = null;
        if (_entry == null || _model == null) return;
        _placementSection = new PlacementRequirementsSection();
        _placementSection.Initialize(_model, _categoryName, _buildingIndex, _entry);
        _placementContainer.AddChild(_placementSection);
    }

    private void RebuildBehaviorRows()
    {
        foreach (var c in _behaviorsContainer.GetChildren()) c.QueueFree();
        if (_entry == null || _model == null) return;
        for (int i = 0; i < _entry.Behaviors.Count; i++)
        {
            var row = new BehaviorRow();
            row.Initialize(_model, _categoryName, _buildingIndex, i, _entry.Behaviors[i]);
            row.RowDeleted += OnBehaviorRowDeleted;
            _behaviorsContainer.AddChild(row);
        }
    }

    private void RebuildVisualSection()
    {
        foreach (var c in _visualContainer.GetChildren()) c.QueueFree();
        _visualSection = null;
        if (_entry == null || _model == null) return;
        _visualSection = new VisualSection();
        _visualSection.Initialize(_model, _categoryName, _buildingIndex, _entry);
        _visualContainer.AddChild(_visualSection);
    }

    private void RebuildIconSection()
    {
        foreach (var c in _iconContainer.GetChildren()) c.QueueFree();
        _iconSection = null;
        if (_entry == null || _model == null) return;
        _iconSection = new IconSection();
        _iconSection.Initialize(_model, _categoryName, _buildingIndex, _entry);
        _iconContainer.AddChild(_iconSection);
    }

    // ─── Field edits ─────────────────────────────────────────────────────

    private void OnFieldEdited(string fieldName, object value)
    {
        if (_model == null || _entry == null) return;
        _model.UpdateBuildingField(_categoryName, _buildingIndex, fieldName, value);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
    }

    private void OnAddRequiredResourcePressed()
    {
        if (_model == null) return;
        _model.AddRequiredResource(_categoryName, _buildingIndex,
            new BuildingEditorModel.RequiredResourceEdit { ResourceId = "", Amount = 1 });
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildRequiredResourceRows();
    }

    private void OnRequiredResourceChanged(int slotIndex, string resourceId, int amount)
    {
        if (_model == null) return;
        _model.UpdateRequiredResource(_categoryName, _buildingIndex, slotIndex, resourceId, amount);
    }

    private void OnRequiredResourceDeleted(int slotIndex)
    {
        if (_model == null) return;
        _model.RemoveRequiredResource(_categoryName, _buildingIndex, slotIndex);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildRequiredResourceRows();
    }

    private void OnAddBehaviorPressed()
    {
        if (_model == null) return;
        var known = new System.Collections.Generic.List<string>(BehaviorSchemaRegistry.KnownBehaviorIds);
        if (known.Count == 0) return;
        known.Sort();
        _model.AddBehavior(_categoryName, _buildingIndex, known[0]);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildBehaviorRows();
    }

    private void OnBehaviorRowDeleted(int rowIndex)
    {
        if (_model == null) return;
        _model.RemoveBehavior(_categoryName, _buildingIndex, rowIndex);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildBehaviorRows();
    }

    private void OnMoveUpPressed()
    {
        if (_model == null || _buildingIndex <= 0) return;
        _model.MoveBuilding(_categoryName, _buildingIndex, _buildingIndex - 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnMoveDownPressed()
    {
        if (_model == null) return;
        var list = _model.Categories[_categoryName].Buildings;
        if (_buildingIndex >= list.Count - 1) return;
        _model.MoveBuilding(_categoryName, _buildingIndex, _buildingIndex + 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnDeletePressed()
    {
        if (_entry == null) return;
        var dialog = new ConfirmationDialog
        {
            Title = "Delete Building",
            DialogText = $"Delete building '{_entry.IdName}'? This is buffered until Save."
        };
        dialog.Confirmed += () =>
        {
            if (_model == null) return;
            _model.DeleteBuilding(_categoryName, _buildingIndex);
            EmitSignal(SignalName.CardsNeedRebuild);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(450, 150));
    }
}
#endif
