using Godot;
using ProceduralGeneration.PlanetGeneration;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// 3D planet selection state. Player aims center-of-screen at a planet
/// and clicks to select it. Replaces OrbitalBodySelectionPopup.
/// Routes to OrbitalPlacement (stations) or BuildingPlacement (buildings)
/// based on ConstructionItemType blackboard variable.
/// </summary>
public partial class PlanetSelectionState : LimboState
{
    private Camera3D? _camera;
    private IOrbitalBody? _aimedBody;
    private Node3D? _aimedBodyNode;
    private Label? _statusLabel;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "PlanetSelectionState");

        _camera = GetViewport().GetCamera3D();
        _statusLabel = GetNodeOrNull<Label>("StatusLabel");

        if (_statusLabel != null)
        {
            _statusLabel.Text = "Aim at a planet and click to select";
            _statusLabel.Visible = true;
        }

        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        GameLogger.Debug("PlanetSelectionState: Awaiting planet selection");
    }

    public override void _Exit()
    {
        base._Exit();

        _aimedBody = null;
        _aimedBodyNode = null;

        if (_statusLabel != null)
            _statusLabel.Visible = false;

        _camera = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (_camera == null)
            return;

        CastRayFromScreenCenter();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (
                mouseButton.ButtonIndex == MouseButton.Left
                && _aimedBody != null
                && _aimedBodyNode != null
            )
            {
                ConfirmSelection();
                GetViewport().SetInputAsHandled();
            }
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            Dispatch("placement_cancelled");
            GetViewport().SetInputAsHandled();
        }
    }

    private void CastRayFromScreenCenter()
    {
        var viewport = GetViewport();
        var screenCenter = viewport.GetVisibleRect().Size / 2.0f;

        Vector3 origin = _camera!.ProjectRayOrigin(screenCenter);
        Vector3 direction = origin + _camera.ProjectRayNormal(screenCenter) * 10000f;

        var query = PhysicsRayQueryParameters3D.Create(origin, direction);
        query.CollideWithAreas = true;
        var spaceState = _camera.GetWorld3D().DirectSpaceState;
        var result = spaceState.IntersectRay(query);

        if (result == null || result.Count == 0)
        {
            ClearAim();
            return;
        }

        var collider = (Node3D)result["collider"];
        var body = FindOrbitalBody(collider);

        if (body == null)
        {
            ClearAim();
            return;
        }

        _aimedBody = body;
        _aimedBodyNode = (Node3D)body;

        if (_statusLabel != null)
            _statusLabel.Text = $"Aimed at: {body.BodyName} — Click to select";
    }

    private void ClearAim()
    {
        if (_aimedBody != null)
        {
            _aimedBody = null;
            _aimedBodyNode = null;

            if (_statusLabel != null)
                _statusLabel.Text = "Aim at a planet and click to select";
        }
    }

    private void ConfirmSelection()
    {
        GameLogger.Debug($"PlanetSelectionState: Selected body '{_aimedBody!.BodyName}'");

        if (Blackboard == null)
        {
            GameLogger.Error("PlanetSelectionState: Blackboard is null");
            Dispatch("placement_cancelled");
            return;
        }

        // Use OrbitalBodyConverter to store the body in standardized format
        OrbitalBodyConverter.SetToBlackboard(
            Blackboard.Top(),
            "ConstructionTargetBody",
            _aimedBody
        );

        var itemType = Blackboard.Top().GetVar("ConstructionItemType").AsString() ?? "";

        switch (itemType)
        {
            case "Station":
                Dispatch("body_confirmed_station");
                break;
            case "Building":
                Dispatch("body_confirmed_building");
                break;
            default:
                GameLogger.Warning($"PlanetSelectionState: Unexpected item type '{itemType}'");
                Dispatch("placement_cancelled");
                break;
        }
    }

    private static IOrbitalBody? FindOrbitalBody(Node node)
    {
        Node? current = node;
        while (current != null)
        {
            if (current is IOrbitalBody body)
                return body;
            current = current.GetParentOrNull<Node>();
        }
        return null;
    }
}
