using System;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using UtilityLibrary;

namespace UI.Construction;

/// <summary>
/// Center-of-screen planet aiming, extracted from the old PlanetSelectionState.
/// Used by <see cref="PlacementOverlayController"/> only when construction is
/// launched from the plain HUD (no body already in view). The player aims at a
/// body and left-clicks to pick it. Escape is owned by the overlay, not here.
/// </summary>
public partial class PlanetAimSelector : Node
{
    /// <summary>Raised with the picked body on confirm (C# event — IOrbitalBody can't marshal through a Godot signal).</summary>
    public event Action<IOrbitalBody>? BodySelected;

    private Camera3D? _camera;
    private IOrbitalBody? _aimedBody;
    private bool _active;

    public override void _Ready()
    {
        _camera = GetViewport().GetCamera3D();
        _active = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_active && _camera != null)
            CastRayFromScreenCenter();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_active)
            return;

        if (@event is InputEventMouseButton mb
            && mb.Pressed
            && mb.ButtonIndex == MouseButton.Left
            && _aimedBody != null)
        {
            _active = false;
            var body = _aimedBody;
            GetViewport().SetInputAsHandled();
            BodySelected?.Invoke(body);
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
            _aimedBody = null;
            return;
        }

        var collider = (Node3D)result["collider"];
        _aimedBody = FindOrbitalBody(collider);
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
