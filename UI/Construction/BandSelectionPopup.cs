using Godot;

namespace UI.Construction;

public partial class BandSelectionPopup : PanelContainer
{
    [Signal]
    public delegate void BandSelectedEventHandler(int bandIndex);

    [Signal]
    public delegate void PopupCancelledEventHandler();

    private ItemList _bandList = null!;
    private Button _selectButton = null!;
    private Button _cancelButton = null!;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(350, 300);
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -175;
        OffsetRight = 175;
        OffsetTop = -150;
        OffsetBottom = 150;

        var vbox = new VBoxContainer();
        AddChild(vbox);

        var title = new Label
        {
            Text = "Select Orbit Band",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(title);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        vbox.AddChild(scroll);

        _bandList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AutoHeight = true,
        };
        _bandList.ItemSelected += OnBandItemSelected;
        scroll.AddChild(_bandList);

        var buttonBar = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
        };
        vbox.AddChild(buttonBar);

        _selectButton = new Button
        {
            Text = "Select",
            Disabled = true,
        };
        _selectButton.Pressed += OnSelectPressed;
        buttonBar.AddChild(_selectButton);

        _cancelButton = new Button { Text = "Cancel" };
        _cancelButton.Pressed += OnCancelPressed;
        buttonBar.AddChild(_cancelButton);
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
