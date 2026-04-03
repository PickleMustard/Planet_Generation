using Godot;

namespace UI.Construction;

public partial class RadiusInputPopup : PanelContainer
{
    [Signal]
    public delegate void RadiusSelectedEventHandler(float radius);

    [Signal]
    public delegate void PopupCancelledEventHandler();

    [Export]
    private Label _infoLabel = null!;

    [Export]
    private SpinBox _radiusSpinBox = null!;

    [Export]
    private Button _confirmButton = null!;

    [Export]
    private Button _cancelButton = null!;

    public override void _Ready()
    {
        // Connect signals
        _confirmButton.Pressed += OnConfirmPressed;
        _cancelButton.Pressed += OnCancelPressed;
    }

    public void Populate(IOrbitalBody body)
    {
        float minRadius = body.Radius * 1.5f;
        float maxRadius = body.Radius * 20f;
        float defaultRadius = body.Radius * 3f;
        float step = body.Radius * 0.1f;

        _infoLabel.Text = $"{body.BodyName}  (body radius: {body.Radius:F0})";

        _radiusSpinBox.MinValue = (int)minRadius;
        _radiusSpinBox.MaxValue = (int)maxRadius;
        _radiusSpinBox.Step = (int)step;
        _radiusSpinBox.Value = defaultRadius;
    }

    private void OnConfirmPressed()
    {
        EmitSignal(SignalName.RadiusSelected, (float)_radiusSpinBox.Value);
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