using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using Structures.Resources;
using UI;
using UtilityLibrary;

namespace UI.PlanetBoard.Modes;

/// <summary>
/// Drag-from-port to drop-on-port creates a <see cref="ResourceLink"/> through
/// <see cref="BuildingLinkService"/>. Right-click on an existing edge removes it.
/// CanConnect validation runs both during the drag (preview color) and on drop.
/// </summary>
public sealed class ResourceLinkPlanningMode : IPlanetBoardMode
{
    public string DisplayName => "Resource Links";

    private PlanetBoardView? _view;
    private CelestialBody? _body;

    public void OnEnter(PlanetBoardView view, CelestialBody body)
    {
        _view = view;
        _body = body;
    }

    public void OnExit()
    {
        _view = null;
        _body = null;
    }

    public bool OnPortClick(ResourceNode port, MouseButton button) => false;

    public bool OnPortDragStart(ResourceNode port)
    {
        // Only ports without an existing link can start a drag — connected ports
        // would otherwise need a drag-to-disconnect gesture, which is reserved for
        // right-click on the edge itself.
        return port.Link == null;
    }

    public void OnPortDragUpdate(Vector2 boardPos, ResourceNode? hoverPort)
    {
        // PlanetBoardView already redraws the preview line; nothing extra here.
    }

    public bool OnPortDragEnd(ResourceNode? dropPort)
    {
        if (_view == null || _view.DragSource == null)
            return false;
        var source = _view.DragSource.Node;
        if (dropPort == null || dropPort == source)
            return false;
        if (!ResourceLink.CanConnect(source, dropPort))
        {
            ToastSystem.Instance?.ShowWarning("Ports are not compatible.");
            return false;
        }
        if (source.Link != null || dropPort.Link != null)
        {
            ToastSystem.Instance?.ShowWarning("One of the ports is already connected.");
            return false;
        }

        var profile = ResolveLinkProfile(source) ?? ResolveLinkProfile(dropPort);
        if (profile == null)
        {
            ToastSystem.Instance?.ShowError("No link profile available.");
            GameLogger.Warning("ResourceLinkPlanningMode: no LinkProfile resolvable; check BuildingDefinition.DefaultLinkProfile.");
            return false;
        }

        if (!BuildingLinkService.Instance.TryConnect(source, dropPort, profile, out _))
        {
            ToastSystem.Instance?.ShowError("Link creation failed.");
            return false;
        }
        SignalBus.Instance?.EmitResourceLinkChanged();
        return true;
    }

    public bool OnEdgeClick(ResourceLink link, MouseButton button)
    {
        if (button != MouseButton.Right)
            return false;
        BuildingLinkService.Instance.Disconnect(link);
        SignalBus.Instance?.EmitResourceLinkChanged();
        return true;
    }

    public bool OnEmptyClick(Vector2 boardPos, MouseButton button) => false;

    public void DrawOverlay(CanvasItem ci, BoardCamera cam) { }

    public string? GetTooltip(object hovered)
    {
        if (hovered is ResourceNode port)
            return $"Side: {port.Side}\nKind: {port.Kind}";
        return null;
    }

    private static LinkProfile? ResolveLinkProfile(ResourceNode? port)
    {
        var def = port?.Owner?.Definition;
        var db = LinkProfileDatabase.Instance;
        if (db == null)
            return null;
        if (def != null && !string.IsNullOrEmpty(def.DefaultLinkProfile)
            && db.TryGetProfile(def.DefaultLinkProfile, out var profile)
            && profile != null)
            return profile;
        // Fall back to the first profile in the database — production code can refine
        // this once a "default profile" concept exists at the database level.
        foreach (var kv in db.GetAllProfiles())
            return kv.Value;
        return null;
    }
}
