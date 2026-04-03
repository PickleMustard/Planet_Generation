using Godot;

namespace UI.Construction;

public partial class BandSelectionPopup : PanelContainer
{
    [Signal]
    public delegate void BandSelectedEventHandler(int bandIndex);

    [Signal]
    public delegate void PopupCancelledEventHandler();

    [Export]
    private ItemList _bandList = null!;

    [Export]
    private Button _selectButton = null!;

    [Export]
    private Button _cancelButton = null!;

    public override void _Ready()
    {
        // Set up popup positioning
        CustomMinimumSize = new Vector2(350, 300);
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -175;
        OffsetRight = 175;
        OffsetTop = -150;
        OffsetBottom = 150;

        // Connect signals
        _bandList.ItemSelected += OnBandItemSelected;
        _selectButton.Pressed += OnSelectPressed;
        _cancelButton.Pressed += OnCancelPressed;
    }

    public void Populate(IOrbitalBody body)
    {
        _bandList.Clear();
        _selectButton.Disabled = true;

        int bandCount = body.GetBandCount();
        for (int i = 0; i < bandCount; i++)
        {
            int currentCount = body.GetBandSatelliteCount(i);
            bool canAdd = body.CanAddToBand(i);
            float radius = body.GetOrbitBandRadius(i);

            string label = $"Band {i}  ({currentCount} occupied, radius: {radius:F0})";
            if (!canAdd)
                label += "  [FULL]";

            int idx = _bandList.AddItem(label);
            _bandList.SetItemMetadata(idx, i);
            _bandList.SetItemDisabled(idx, !canAdd);
        }
    }

    private void OnBandItemSelected(long index)
    {
        bool disabled = _bandList.IsItemDisabled((int)index);
        _selectButton.Disabled = disabled;
    }

    private void OnSelectPressed()
    {
        var selected = _bandList.GetSelectedItems();
        if (selected.Length == 0) return;

        int bandIndex = _bandList.GetItemMetadata(selected[0]).AsInt32();
        EmitSignal(SignalName.BandSelected, bandIndex);
    }

    private void OnCancelPressed()
    {
        EmitSignal(SignalName.PopupCancelled);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            OnCancelPressed();
            GetViewport().SetInputAsHandled();
        }
    }
}