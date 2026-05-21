#if DEBUG
using System;
using Godot;
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
    private Button _deleteButton = null!;

    public void Initialize(
        LinkProfileEditorModel model,
        int index,
        LinkProfileEditorModel.LinkProfileEditEntry entry)
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
        _deleteButton.Pressed += OnDeletePressed;

        RefreshControls();
    }

    private void RefreshControls()
    {
        if (_entry == null) return;

        _idNameEdit.Text = _entry.IdName;
        _tierSpin.SetValueNoSignal(_entry.Tier);
        _transportSpeedSpin.SetValueNoSignal(_entry.TransportSpeed);
        _packageSizeSpin.SetValueNoSignal(_entry.PackageSize);
        _bundleTimeSpin.SetValueNoSignal(_entry.BundleTime);
        _slotCapacitySpin.SetValueNoSignal(_entry.SlotCapacity);

        for (int i = 0; i < _stateOption.ItemCount; i++)
        {
            if (_stateOption.GetItemId(i) == (int)_entry.StateOfMatter)
            {
                _stateOption.Select(i);
                break;
            }
        }
    }

    private void MarkDirty()
    {
        if (_entry == null) return;
        _entry.IsDirty = true;
    }

    private void OnIdNameChanged(string newText)
    {
        if (_entry == null) return;
        _entry.IdName = newText.Trim();
        Name = $"LinkProfileCard_{_entry.IdName}";
        MarkDirty();
    }

    private void OnTierChanged(double value)
    {
        if (_entry == null) return;
        _entry.Tier = (int)value;
        MarkDirty();
    }

    private void OnTransportSpeedChanged(double value)
    {
        if (_entry == null) return;
        _entry.TransportSpeed = (float)value;
        MarkDirty();
    }

    private void OnPackageSizeChanged(double value)
    {
        if (_entry == null) return;
        _entry.PackageSize = (int)value;
        MarkDirty();
    }

    private void OnBundleTimeChanged(double value)
    {
        if (_entry == null) return;
        _entry.BundleTime = (int)value;
        MarkDirty();
    }

    private void OnSlotCapacityChanged(double value)
    {
        if (_entry == null) return;
        _entry.SlotCapacity = (int)value;
        MarkDirty();
    }

    private void OnStateSelected(long index)
    {
        if (_entry == null) return;
        int id = _stateOption.GetItemId((int)index);
        _entry.StateOfMatter = (StateOfMatter)id;
        MarkDirty();
    }

    private void OnDeletePressed()
    {
        if (_entry == null) return;

        var dialog = new ConfirmationDialog
        {
            Title = "Delete Link Profile",
            DialogText = $"Delete profile '{_entry.IdName}'? This cannot be undone.",
        };
        dialog.Confirmed += () =>
        {
            if (_model == null) return;
            _model.DeleteProfile(_index);
            EmitSignal(SignalName.CardsNeedRebuild);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }
}
#endif
