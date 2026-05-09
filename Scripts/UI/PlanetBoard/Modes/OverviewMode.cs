using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using Structures.Resources;

namespace UI.PlanetBoard.Modes;

/// <summary>
/// Stub for the read-only overview experience. No interaction beyond hover.
/// </summary>
public sealed class OverviewMode : IPlanetBoardMode
{
    public string DisplayName => "Overview";

    public void OnEnter(PlanetBoardView view, CelestialBody body) { }
    public void OnExit() { }

    public bool OnPortClick(ResourceNode port, MouseButton button) => false;
    public bool OnPortDragStart(ResourceNode port) => false;
    public void OnPortDragUpdate(Vector2 boardPos, ResourceNode? hoverPort) { }
    public bool OnPortDragEnd(ResourceNode? dropPort) => false;

    public bool OnEdgeClick(ResourceLink link, MouseButton button) => false;
    public bool OnEmptyClick(Vector2 boardPos, MouseButton button) => false;

    public void DrawOverlay(CanvasItem ci, BoardCamera cam) { }

    public string? GetTooltip(object hovered) => null;
}
