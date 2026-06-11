#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using DeveloperTools.Common;
using Structures.Enums;

namespace DeveloperTools.LinkProfileEditor;

/// <summary>
/// Per-link-profile card with inline editing for IdName, Tier, TransportSpeed,
/// PackageSize, BundleTime, SlotCapacity, and StateOfMatter. Mirrors the
/// ResourceEditor.ResourceCard pattern. GUI layout in LinkProfileCard.tscn.
/// </summary>
public partial class LinkProfileCard : PanelContainer
{
    [Signal]
    public delegate void CardsNeedRebuildEventHandler();

    private LinkProfileEditorModel? _model;
    private int _index;
    private LinkProfileEditorModel.LinkProfileEditEntry? _entry;

    private LineEdit _idNameEdit = null!;
    private SpinBox _tierSpin = null!;
    private SpinBox _transportSpeedSpin = null!;
    private SpinBox _packageSizeSpin = null!;
    private SpinBox _bundleTimeSpin = null!;
    private SpinBox _slotCapacitySpin = null!;
    private OptionButton _stateOption = null!;
    private SpinBox _constructionWorkSpin = null!;
    private VBoxContainer _costList = null!;
    private Button _addCostButton = null!;
    private Button _deleteButton = null!;

    /// <summary>
    /// Manufacture tick rate (see ManufactureTickEngine.TickHz). BundleTime is stored as ticks
    /// in config; this card presents it as milliseconds and converts on the boundary.
    /// </summary>
    private const float TICK_HZ = 60f;

    private static int TicksToMs(int ticks) => Mathf.RoundToInt(ticks * 1000f / TICK_HZ);

    private static int MsToTicks(double ms) => Mathf.RoundToInt((float)(ms * TICK_HZ / 1000f));

    public void Initialize(
        LinkProfileEditorModel model,
        int index,
        LinkProfileEditorModel.LinkProfileEditEntry entry
    )
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);
        _model = model;
        _index = index;
        _entry = entry;
        Name = $"LinkProfileCard_{_entry.IdName}";
    }

    public override void _Ready()
    {
        base._Ready();

        _idNameEdit = GetNode<LineEdit>("%IdNameEdit");
        _tierSpin = GetNode<SpinBox>("%TierSpin");
        _transportSpeedSpin = GetNode<SpinBox>("%TransportSpeedSpin");
        _packageSizeSpin = GetNode<SpinBox>("%PackageSizeSpin");
        _bundleTimeSpin = GetNode<SpinBox>("%BundleTimeSpin");
        _slotCapacitySpin = GetNode<SpinBox>("%SlotCapacitySpin");
        _stateOption = GetNode<OptionButton>("%StateOption");
        _constructionWorkSpin = GetNode<SpinBox>("%ConstructionWorkSpin");
        _costList = GetNode<VBoxContainer>("%CostList");
        _addCostButton = GetNode<Button>("%AddCostButton");
        _deleteButton = GetNode<Button>("%DeleteButton");

        _stateOption.Clear();
        _stateOption.AddItem("Solid", (int)StateOfMatter.Solid);
        _stateOption.AddItem("Fluid", (int)StateOfMatter.Fluid);

        _idNameEdit.TextChanged += OnIdNameChanged;
        _tierSpin.ValueChanged += OnTierChanged;
        _transportSpeedSpin.ValueChanged += OnTransportSpeedChanged;
        _packageSizeSpin.ValueChanged += OnPackageSizeChanged;
        _bundleTimeSpin.ValueChanged += OnBundleTimeChanged;
        _slotCapacitySpin.ValueChanged += OnSlotCapacityChanged;
        _stateOption.ItemSelected += OnStateSelected;
        _constructionWorkSpin.ValueChanged += OnConstructionWorkChanged;
        _addCostButton.Pressed += OnAddCostPressed;
        _deleteButton.Pressed += OnDeletePressed;

        RefreshControls();
    }

    private void RefreshControls()
    {
        if (_entry == null)
            return;

        _idNameEdit.Text = _entry.IdName;
        _tierSpin.SetValueNoSignal(_entry.Tier);
        _transportSpeedSpin.SetValueNoSignal(_entry.TransportSpeed);
        _packageSizeSpin.SetValueNoSignal(_entry.PackageSize);
        _bundleTimeSpin.SetValueNoSignal(TicksToMs(_entry.BundleTime));
        _slotCapacitySpin.SetValueNoSignal(_entry.SlotCapacity);
        _constructionWorkSpin.SetValueNoSignal(_entry.ConstructionWork);

        for (int i = 0; i < _stateOption.ItemCount; i++)
        {
            if (_stateOption.GetItemId(i) == (int)_entry.StateOfMatter)
            {
                _stateOption.Select(i);
                break;
            }
        }

        RebuildCostRows();
    }

    /// <summary>
    /// Rebuilds the cost_per_distance rows from <c>_entry.CostPerDistance</c>. Each row is a
    /// resource label + amount SpinBox + delete button, built programmatically (mirrors
    /// RequiredResourceRow). Resources are added via the shared ResourcePickerPopup.
    /// </summary>
    private void RebuildCostRows()
    {
        if (_entry == null)
            return;

        foreach (var child in _costList.GetChildren())
            child.QueueFree();

        foreach (var kvp in _entry.CostPerDistance)
        {
            string resourceId = kvp.Key;

            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            var label = new Label
            {
                Text = resourceId,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
                TooltipText = resourceId,
            };
            row.AddChild(label);

            var amountSpin = new SpinBox
            {
                MinValue = 1,
                MaxValue = 1000000,
                Step = 1,
                Value = kvp.Value,
                CustomMinimumSize = new Vector2(96, 0),
                AllowGreater = true,
            };
            amountSpin.ValueChanged += value =>
            {
                if (_entry == null)
                    return;
                _entry.CostPerDistance[resourceId] = (int)value;
                MarkDirty();
            };
            row.AddChild(amountSpin);

            var deleteButton = new Button { Text = "✕", TooltipText = "Remove resource" };
            deleteButton.Pressed += () =>
            {
                if (_entry == null)
                    return;
                _entry.CostPerDistance.Remove(resourceId);
                MarkDirty();
                RebuildCostRows();
            };
            row.AddChild(deleteButton);

            _costList.AddChild(row);
        }
    }

    private void OnAddCostPressed()
    {
        if (_entry == null)
            return;

        var popup = ResourcePickerPopup.Create();
        popup.ResourcePicked += id =>
        {
            if (_entry == null || string.IsNullOrEmpty(id))
                return;
            if (!_entry.CostPerDistance.ContainsKey(id))
                _entry.CostPerDistance[id] = 1;
            MarkDirty();
            RebuildCostRows();
        };
        GetTree().Root.AddChild(popup);
        popup.PopupCentered();
    }

    private void OnConstructionWorkChanged(double value)
    {
        if (_entry == null)
            return;
        _entry.ConstructionWork = (float)value;
        MarkDirty();
    }

    private void MarkDirty()
    {
        if (_entry == null)
            return;
        _entry.IsDirty = true;
    }

    private void OnIdNameChanged(string newText)
    {
        if (_entry == null)
            return;
        _entry.IdName = newText.Trim();
        Name = $"LinkProfileCard_{_entry.IdName}";
        MarkDirty();
    }

    private void OnTierChanged(double value)
    {
        if (_entry == null)
            return;
        _entry.Tier = (int)value;
        MarkDirty();
    }

    private void OnTransportSpeedChanged(double value)
    {
        if (_entry == null)
            return;
        _entry.TransportSpeed = (float)value;
        MarkDirty();
    }

    private void OnPackageSizeChanged(double value)
    {
        if (_entry == null)
            return;
        _entry.PackageSize = (int)value;
        MarkDirty();
    }

    private void OnBundleTimeChanged(double value)
    {
        if (_entry == null)
            return;
        _entry.BundleTime = MsToTicks(value);
        MarkDirty();
    }

    private void OnSlotCapacityChanged(double value)
    {
        if (_entry == null)
            return;
        _entry.SlotCapacity = (int)value;
        MarkDirty();
    }

    private void OnStateSelected(long index)
    {
        if (_entry == null)
            return;
        int id = _stateOption.GetItemId((int)index);
        _entry.StateOfMatter = (StateOfMatter)id;
        MarkDirty();
    }

    private void OnDeletePressed()
    {
        if (_entry == null)
            return;

        var dialog = new ConfirmationDialog
        {
            Title = "Delete Link Profile",
            DialogText = $"Delete profile '{_entry.IdName}'? This cannot be undone.",
        };
        dialog.Confirmed += () =>
        {
            if (_model == null)
                return;
            _model.DeleteProfile(_index);
            EmitSignal(SignalName.CardsNeedRebuild);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }
}
#endif
