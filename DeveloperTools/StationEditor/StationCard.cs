#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using DeveloperTools.Common;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.StationEditor;

/// <summary>
/// Per-station editing card. GUI defined in StationCard.tscn; behavior only in this
/// script. Edits mutate the model entry in place and flag it dirty;
/// reorder/delete emit <see cref="CardsNeedRebuild"/>.
/// </summary>
public partial class StationCard : PanelContainer
{
    [Signal]
    public delegate void CardsNeedRebuildEventHandler();

    private StationEditorModel _model = null!;
    private string _categoryName = "";
    private int _stationIndex;
    private StationEditorModel.StationEditEntry _entry = null!;

    // Scene node references
    private LineEdit _nameEdit = null!;
    private Button _moveUpButton = null!;
    private Button _moveDownButton = null!;
    private OptionButton _typeOption = null!;
    private SpinBox _constructionTimeSpin = null!;
    private VBoxContainer _behaviorsContainer = null!;
    private VBoxContainer _requiredResourcesContainer = null!;

    private static PackedScene? _cardScene;

    /// <summary>
    /// Factory method: loads the .tscn, instantiates, and initializes.
    /// Replaces direct <c>new StationCard()</c> + <c>Initialize()</c> calls.
    /// </summary>
    public static StationCard Create(StationEditorModel model, string categoryName,
        int stationIndex, StationEditorModel.StationEditEntry entry)
    {
        _cardScene ??= GD.Load<PackedScene>("res://DeveloperTools/StationEditor/StationCard.tscn");
        var card = _cardScene.Instantiate<StationCard>();
        card.Initialize(model, categoryName, stationIndex, entry);
        return card;
    }

    public void Initialize(StationEditorModel model, string categoryName, int stationIndex,
        StationEditorModel.StationEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);
        _model = model;
        _categoryName = categoryName;
        _stationIndex = stationIndex;
        _entry = entry;
        Name = $"StationCard_{entry.Name}";
    }

    public override void _Ready()
    {
        base._Ready();
        AcquireNodeReferences();
        BindEntryData();
        WireScalarSignals();
        PopulateStationTypes();
        RebuildBehaviors();
        RebuildRequiredResources();
        UpdateMoveButtons();
    }

    private void AcquireNodeReferences()
    {
        _nameEdit = GetNode<LineEdit>("%NameEdit");
        _moveUpButton = GetNode<Button>("%MoveUpButton");
        _moveDownButton = GetNode<Button>("%MoveDownButton");
        _typeOption = GetNode<OptionButton>("%TypeOption");
        _constructionTimeSpin = GetNode<SpinBox>("%ConstructionTimeSpin");
        _behaviorsContainer = GetNode<VBoxContainer>("%BehaviorsContainer");
        _requiredResourcesContainer = GetNode<VBoxContainer>("%RequiredResourcesContainer");
    }

    private void BindEntryData()
    {
        _nameEdit.Text = _entry.Name;
        _constructionTimeSpin.Value = _entry.ConstructionTime;
    }

    private void WireScalarSignals()
    {
        _nameEdit.TextChanged += t => { _entry.Name = t; MarkDirty(); };
        _typeOption.ItemSelected += idx => { _entry.StationType = _typeOption.GetItemText((int)idx); MarkDirty(); };
        _constructionTimeSpin.ValueChanged += v => { _entry.ConstructionTime = (float)v; MarkDirty(); };
    }

    private void MarkDirty() => _model.MarkDirty(_categoryName, _stationIndex);

    // ── Station type dropdown ────────────────────────────────────────────

    private void PopulateStationTypes()
    {
        _typeOption.Clear();
        var types = StationEditorModel.GetAllStationTypes();
        if (!string.IsNullOrEmpty(_entry.StationType) && !types.Contains(_entry.StationType))
            types.Insert(0, _entry.StationType);
        int selected = -1;
        for (int i = 0; i < types.Count; i++)
        {
            _typeOption.AddItem(types[i]);
            if (types[i] == _entry.StationType) selected = i;
        }
        if (selected >= 0) _typeOption.Select(selected);
    }

    // ── Behaviors ────────────────────────────────────────────────────────

    private void OnAddBehavior()
    {
        var known = StationEditorModel.GetAllBehaviorTypes();
        _entry.Behaviors.Add(new StationEditorModel.BehaviorConfigEdit
        {
            BehaviorId = known.Count > 0 ? known[0] : ""
        });
        MarkDirty();
        RebuildBehaviors();
    }

    private void RebuildBehaviors()
    {
        foreach (var c in _behaviorsContainer.GetChildren()) c.QueueFree();
        var known = StationEditorModel.GetAllBehaviorTypes();
        for (int i = 0; i < _entry.Behaviors.Count; i++)
        {
            int index = i;
            var beh = _entry.Behaviors[index];
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            // Behavior type dropdown
            var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var items = new List<string>(known);
            if (!string.IsNullOrEmpty(beh.BehaviorId) && !items.Contains(beh.BehaviorId))
                items.Insert(0, beh.BehaviorId);
            int selected = -1;
            for (int j = 0; j < items.Count; j++)
            {
                option.AddItem(items[j]);
                if (items[j] == beh.BehaviorId) selected = j;
            }
            if (selected >= 0) option.Select(selected);
            option.ItemSelected += idx =>
            {
                beh.BehaviorId = option.GetItemText((int)idx);
                MarkDirty();
                RebuildBehaviors(); // Refresh config section based on new behavior type
            };
            row.AddChild(option);

            // Delete button
            var delete = new Button { Text = "✕", TooltipText = "Remove behavior" };
            delete.Pressed += () => { _entry.Behaviors.RemoveAt(index); MarkDirty(); RebuildBehaviors(); };
            row.AddChild(delete);

            _behaviorsContainer.AddChild(row);

            // Config section based on behavior type
            AddBehaviorConfigControls(beh, index);
        }
    }

    /// <summary>
    /// Adds configuration controls for known behavior types.
    /// Shows relevant config keys based on the behavior's identity.
    /// </summary>
    private void AddBehaviorConfigControls(StationEditorModel.BehaviorConfigEdit beh, int index)
    {
        if (string.IsNullOrEmpty(beh.BehaviorId)) return;

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _behaviorsContainer.AddChild(grid);

        switch (beh.BehaviorId)
        {
            case "StorageHubBehavior":
                AddConfigSpinInt(grid, "Storage Capacity", beh, "storage_capacity", 0);
                break;
            case "ShipyardBehavior":
                AddConfigSpinInt(grid, "Max Parallel Builds", beh, "max_parallel_ship_builds", 1);
                break;
            case "OrbitalConstructorBehavior":
                AddConfigSpin(grid, "Work Budget / Tick", beh, "work_budget_per_tick", 1.0f);
                AddConfigSpinInt(grid, "Regular Slots", beh, "regular_slots", 1);
                AddConfigSpinInt(grid, "Overtime Slots", beh, "overtime_slots", 0);
                AddConfigSpin(grid, "Overtime Cost %", beh, "overtime_cost_percent", 0.10f);
                break;
            case "TransferHubBehavior":
                AddConfigLabel(grid, "Transfer Station Config:");
                AddConfigSpin(grid, "Cargo Capacity", beh, "cargo_capacity", 500.0f);
                AddConfigSpin(grid, "Vehicle Speed", beh, "vehicle_speed", 50.0f);
                AddConfigSpinInt(grid, "Max Concurrent Transfers", beh, "max_concurrent_transfers", 2);
                break;
        }
    }

    private void AddConfigSpin(GridContainer grid, string label, StationEditorModel.BehaviorConfigEdit beh,
        string key, float defaultValue)
    {
        grid.AddChild(new Label { Text = label });
        float current = defaultValue;
        if (beh.Config.TryGetValue(key, out var obj))
            current = System.Convert.ToSingle(obj);
        var spin = new SpinBox
        {
            MinValue = 0, MaxValue = 10000000, Step = key.Contains("percent") ? 0.01f : 0.1f,
            AllowGreater = true, Value = current, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        spin.ValueChanged += v =>
        {
            beh.Config[key] = (float)v;
            MarkDirty();
        };
        grid.AddChild(spin);
    }

    private void AddConfigSpinInt(GridContainer grid, string label, StationEditorModel.BehaviorConfigEdit beh,
        string key, int defaultValue)
    {
        grid.AddChild(new Label { Text = label });
        int current = defaultValue;
        if (beh.Config.TryGetValue(key, out var obj))
            current = System.Convert.ToInt32(obj);
        var spin = new SpinBox
        {
            MinValue = 0, MaxValue = 1000000, Step = 1,
            AllowGreater = true, Value = current, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        spin.ValueChanged += v =>
        {
            beh.Config[key] = (int)v;
            MarkDirty();
        };
        grid.AddChild(spin);
    }

    private static void AddConfigLabel(GridContainer grid, string text)
    {
        // Span both columns for section headers
        var label = new Label { Text = text };
        grid.AddChild(label);
    }

    // ── Required resources ───────────────────────────────────────────────

    private void RebuildRequiredResources()
    {
        foreach (var c in _requiredResourcesContainer.GetChildren()) c.QueueFree();
        EditorCardControls.BuildRequiredResources(
            _requiredResourcesContainer, _entry.RequiredResources, MarkDirty, RebuildRequiredResources);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void UpdateMoveButtons()
    {
        if (_model.Categories.TryGetValue(_categoryName, out var cat))
        {
            _moveUpButton.Disabled = _stationIndex <= 0;
            _moveDownButton.Disabled = _stationIndex >= cat.Stations.Count - 1;
        }
    }

    private void OnMoveUp()
    {
        if (_stationIndex <= 0) return;
        _model.MoveStation(_categoryName, _stationIndex, _stationIndex - 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnMoveDown()
    {
        var list = _model.Categories[_categoryName].Stations;
        if (_stationIndex >= list.Count - 1) return;
        _model.MoveStation(_categoryName, _stationIndex, _stationIndex + 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnDelete()
    {
        var dialog = new ConfirmationDialog
        {
            Title = "Delete Station",
            DialogText = $"Delete station '{_entry.Name}'? Buffered until Save."
        };
        dialog.Confirmed += () =>
        {
            _model.DeleteStation(_categoryName, _stationIndex);
            EmitSignal(SignalName.CardsNeedRebuild);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(450, 150));
    }
}
#endif