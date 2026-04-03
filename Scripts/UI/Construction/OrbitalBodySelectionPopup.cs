using System.Collections.Generic;
using Godot;

namespace UI.Construction;

public partial class OrbitalBodySelectionPopup : PanelContainer
{
    [Signal]
    public delegate void OrbitalBodySelectedEventHandler(int bodyIndex);

    [Signal]
    public delegate void PopupCancelledEventHandler();

    [Export]
    private ItemList _bodyList = null!;

    [Export]
    private Button _selectButton = null!;

    [Export]
    private Button _cancelButton = null!;

    [Export]
    private Label _emptyLabel = null!;

    private List<IOrbitalBody> _bodies = new();

    public override void _Ready()
    {
        // Set up popup positioning
        CustomMinimumSize = new Vector2(400, 350);
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -200;
        OffsetRight = 200;
        OffsetTop = -175;
        OffsetBottom = 175;

        // Connect signals
        _bodyList.ItemSelected += OnBodyItemSelected;
        _selectButton.Pressed += OnSelectPressed;
        _cancelButton.Pressed += OnCancelPressed;
    }

    public void Populate()
    {
        _bodies.Clear();
        _bodyList.Clear();
        _selectButton.Disabled = true;

        var nodes = GetTree().GetNodesInGroup("CelestialBody");
        foreach (var node in nodes)
        {
            if (node is not IOrbitalBody body) continue;

            string label = body.BodyName;
            int bandCount = body.GetBandCount();
            bool usesBands = body.UsesBandPlacement;
            label += usesBands ? $"  ({bandCount} orbit bands)" : "  (continuous orbit)";

            int idx = _bodyList.AddItem(label);
            _bodyList.SetItemMetadata(idx, _bodies.Count);
            _bodies.Add(body);
        }

        if (_bodies.Count == 0)
        {
            _emptyLabel.Visible = true;
            _bodyList.Visible = false;
        }
        else
        {
            _emptyLabel.Visible = false;
            _bodyList.Visible = true;
        }
    }

    public IOrbitalBody? GetSelectedBody(int index)
    {
        if (index < 0 || index >= _bodies.Count) return null;
        return _bodies[index];
    }

    private void OnBodyItemSelected(long index)
    {
        _selectButton.Disabled = false;
    }

    private void OnSelectPressed()
    {
        var selected = _bodyList.GetSelectedItems();
        if (selected.Length == 0) return;

        int bodyIndex = _bodyList.GetItemMetadata(selected[0]).AsInt32();
        EmitSignal(SignalName.OrbitalBodySelected, bodyIndex);
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