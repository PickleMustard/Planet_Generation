using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using Structures.Resources;

namespace UI.PlanetBoard.Modes;

/// <summary>
/// Strategy that owns the per-mode behavior of <see cref="PlanetBoardView"/>.
/// The view forwards every interesting input event here; if a hook returns
/// <c>false</c> the view falls through to its default handling. The mode is
/// also given a chance to draw a per-mode overlay on top of the base scene.
/// </summary>
public interface IPlanetBoardMode
{
    string DisplayName { get; }

    void OnEnter(PlanetBoardView view, CelestialBody body);
    void OnExit();

    bool OnPortClick(ResourceNode port, MouseButton button);
    bool OnPortDragStart(ResourceNode port);
    void OnPortDragUpdate(Vector2 boardPos, ResourceNode? hoverPort);
    bool OnPortDragEnd(ResourceNode? dropPort);

    bool OnEdgeClick(ResourceLink link, MouseButton button);
    bool OnEmptyClick(Vector2 boardPos, MouseButton button);

    void DrawOverlay(CanvasItem ci, BoardCamera cam);

    string? GetTooltip(object hovered);
}
