using Constructables;
using Godot;

namespace UI.ConstructionYard;

/// <summary>
/// Row representing a ship waiting in the build queue. Supports drag-and-drop
/// reordering: emits MoveRequested with the dropped ship, this row's ship, and
/// whether the drop was on the bottom half (insertAfter=true).
/// </summary>
public partial class QueuedShipRow : HBoxContainer
{
    [Signal]
    public delegate void MoveRequestedEventHandler(LogisticsUnit movedShip, LogisticsUnit targetShip, bool insertAfter);

    [Signal]
    public delegate void CancelRequestedEventHandler(LogisticsUnit ship);

    [Export]
    private Label? _nameLabel;

    [Export]
    private Label? _etaLabel;

    [Export]
    private Label? _indexLabel;

    [Export]
    private Button? _cancelButton;

    private LogisticsUnit? _ship;

    public LogisticsUnit? Ship => _ship;

    public override void _Ready()
    {
        if (_cancelButton != null)
            _cancelButton.Pressed += OnCancelPressed;

        MouseFilter = MouseFilterEnum.Pass;
    }

    public override void _ExitTree()
    {
        if (_cancelButton != null)
            _cancelButton.Pressed -= OnCancelPressed;
    }

    public void Bind(LogisticsUnit ship, int index)
    {
        _ship = ship;

        if (_nameLabel != null)
            _nameLabel.Text = ship.Name;

        if (_indexLabel != null)
            _indexLabel.Text = $"#{index + 1}";

        float remaining = ship.workRequired;
        if (_etaLabel != null)
            _etaLabel.Text = remaining > 0 ? $"~{remaining:F0}s" : "";
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_ship == null)
            return default;

        var preview = new Label
        {
            Text = _ship.Name,
            Modulate = new Color(1f, 1f, 1f, 0.85f),
        };
        SetDragPreview(preview);

        return Variant.From(_ship);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (_ship == null)
            return false;
        var node = data.As<Node>();
        return node is LogisticsUnit other && other != _ship;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_ship == null)
            return;
        var node = data.As<Node>();
        if (node is not LogisticsUnit moved || moved == _ship)
            return;

        bool insertAfter = atPosition.Y > Size.Y * 0.5f;
        EmitSignal(SignalName.MoveRequested, moved, _ship, insertAfter);
    }

    private void OnCancelPressed()
    {
        if (_ship != null)
            EmitSignal(SignalName.CancelRequested, _ship);
    }
}
