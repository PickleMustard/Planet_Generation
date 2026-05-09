using System;
using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using Structures.Resources;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.PlanetBoard.Modes;

/// <summary>
/// Planet-board mode used by the Dispatch Slips "Pick Destination" step. Listens
/// for clicks on transfer-station buildings and resolves the clicked endpoint via
/// <see cref="BodyTransferManager.HasEndpoint"/>. Fires <see cref="DestinationPicked"/>
/// with the destination building id when a remote transfer station is chosen.
/// </summary>
public sealed class TransferRoutePlanningMode : IPlanetBoardMode
{
    public string DisplayName => "Transfer Routes";

    /// <summary>
    /// Building id the player is dispatching FROM. The originating building is not
    /// selectable as a destination.
    /// </summary>
    public string OriginBuildingId { get; set; } = "";

    /// <summary>
    /// Most recently picked destination building id, or empty if none.
    /// </summary>
    public string SelectedBuildingId { get; private set; } = "";

    /// <summary>
    /// Fired when the user picks a remote transfer-station building.
    /// Argument: the destination building's <see cref="Building.Id"/>.
    /// </summary>
    public event Action<string>? DestinationPicked;

    private PlanetBoardView? _view;
    private CelestialBody? _body;

    public void OnEnter(PlanetBoardView view, CelestialBody body)
    {
        _view = view;
        _body = body;
        SelectedBuildingId = "";
        view.QueueRedraw();
    }

    public void OnExit()
    {
        _view = null;
        _body = null;
        SelectedBuildingId = "";
    }

    public bool OnPortClick(ResourceNode port, MouseButton button)
    {
        if (button != MouseButton.Left) return false;
        return TryPickFromBuilding(port.Owner);
    }

    public bool OnPortDragStart(ResourceNode port) => false;
    public void OnPortDragUpdate(Vector2 boardPos, ResourceNode? hoverPort) { }
    public bool OnPortDragEnd(ResourceNode? dropPort) => false;

    public bool OnEdgeClick(ResourceLink link, MouseButton button) => false;
    public bool OnEmptyClick(Vector2 boardPos, MouseButton button) => false;

    public void DrawOverlay(CanvasItem ci, BoardCamera cam)
    {
        if (_view == null) return;
        var mgr = _body?.TransferMgr;
        if (mgr == null) return;

        foreach (var bv in _view.BuildingViews)
        {
            string id = bv.Building?.Id ?? "";
            if (string.IsNullOrEmpty(id) || !mgr.HasEndpoint(id)) continue;
            bool isOrigin = id == OriginBuildingId;
            bool isSelected = id == SelectedBuildingId;
            var center = cam.BoardToScreen(bv.BoardPos);
            float radiusScreen = bv.Radius * cam.Zoom + 6f;
            Color ring = isOrigin
                ? WireColors.InkFaint
                : isSelected
                    ? WireColors.Orange
                    : new Color(WireColors.Ink, 0.4f);
            ci.DrawArc(center, radiusScreen, 0f, Mathf.Tau, 32, ring, 2f);
        }
    }

    public string? GetTooltip(object hovered)
    {
        if (hovered is ResourceNode port)
        {
            string id = port.Owner?.Id ?? "";
            var mgr = _body?.TransferMgr;
            if (mgr != null && mgr.HasEndpoint(id) && id != OriginBuildingId)
            {
                int continent = port.Owner?.PrimaryCell?.ContinentIndex ?? -1;
                return continent >= 0
                    ? $"Transfer station · Continent {continent:D2}"
                    : "Transfer station";
            }
        }
        return null;
    }

    private bool TryPickFromBuilding(Building? building)
    {
        if (building == null) return false;
        var mgr = _body?.TransferMgr;
        if (mgr == null) return false;
        string id = building.Id ?? "";
        if (string.IsNullOrEmpty(id) || !mgr.HasEndpoint(id))
        {
            ToastSystem.Instance?.ShowWarning("Pick a transfer-station building.");
            return false;
        }
        if (id == OriginBuildingId)
        {
            ToastSystem.Instance?.ShowWarning("Cannot route back to the origin building.");
            return false;
        }
        SelectedBuildingId = id;
        DestinationPicked?.Invoke(id);
        _view?.QueueRedraw();
        return true;
    }
}
