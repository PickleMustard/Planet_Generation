using Godot;
using Structures.Logistics;
using Structures.Resources;

namespace UI.PlanetBoard.Modes;

/// <summary>
/// Strategy that owns the per-mode behavior of <see cref="BoardWorld"/>.
/// The world forwards every interesting input event here; if a hook returns
/// <c>false</c> the world falls through to its default handling. The mode is
/// also given a chance to draw a per-mode overlay on top of the base scene.
/// </summary>
public interface IPlanetBoardMode
{
    string DisplayName { get; }

    void OnEnter(BoardWorld world, IOrbitalBody body);
    void OnExit();

    bool OnPortClick(ResourceNode port, MouseButton button);
    bool OnPortDragStart(ResourceNode port);
    void OnPortDragUpdate(Vector2 boardPos, ResourceNode? hoverPort);
    bool OnPortDragEnd(ResourceNode? dropPort);

    bool OnEdgeClick(ResourceLink link, MouseButton button);
    bool OnEmptyClick(Vector2 boardPos, MouseButton button);

    /// <summary>
    /// Click on a station diamond on the outer ring. Default no-op; modes that
    /// support station selection (e.g. transfer-route planning) override.
    /// </summary>
    bool OnStationClick(StationNode2D station, MouseButton button) => false;

    void DrawOverlay(CanvasItem ci, BoardCameraController cam);

    string? GetTooltip(object hovered);
}
