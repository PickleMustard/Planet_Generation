using System;
using Constructables;
using Godot;
using Structures.Logistics;
using Structures.Resources;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.PlanetBoard.Modes;

/// <summary>
/// Planet-board mode used by the Dispatch Slips "Pick Destination" step. Listens
/// for clicks on transfer-station buildings and resolves the clicked endpoint via
/// <see cref="IOrbitalBody.HasTransferEndpoint"/>. Fires <see cref="DestinationPicked"/>
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

    private BoardWorld? _world;
    private IOrbitalBody? _body;

    public void OnEnter(BoardWorld world, IOrbitalBody body)
    {
        _world = world;
        _body = body;
        SelectedBuildingId = "";
        world.QueueRedraw();
    }

    public void OnExit()
    {
        _world = null;
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

    public bool OnStationClick(StationNode2D node, MouseButton button)
    {
        if (button != MouseButton.Left || _body == null || node?.Station == null) return false;
        string id = node.Station.Id ?? "";
        if (string.IsNullOrEmpty(id) || !_body.HasTransferEndpoint(id))
        {
            ToastSystem.Instance?.ShowWarning("Station has no transfer endpoint.");
            return false;
        }
        if (id == OriginBuildingId)
        {
            ToastSystem.Instance?.ShowWarning("Cannot route back to the origin.");
            return false;
        }
        SelectedBuildingId = id;
        DestinationPicked?.Invoke(id);
        _world?.QueueRedraw();
        return true;
    }

    public void DrawOverlay(CanvasItem ci, BoardCameraController cam)
    {
        if (_world == null || _body == null) return;

        foreach (var bn in _world.BuildingNodes)
        {
            string id = bn.Building?.Id ?? "";
            if (string.IsNullOrEmpty(id) || !_body.HasTransferEndpoint(id)) continue;
            bool isOrigin = id == OriginBuildingId;
            bool isSelected = id == SelectedBuildingId;
            var center = cam.BoardToScreen(bn.Position);
            float radiusScreen = bn.Radius * cam.Zoom.X + 6f;
            Color ring = isOrigin
                ? WireColors.InkFaint
                : isSelected
                    ? WireColors.Orange
                    : new Color(WireColors.Ink, 0.4f);
            ci.DrawArc(center, radiusScreen, 0f, Mathf.Tau, 32, ring, 2f);
        }

        foreach (var sn in _world.StationNodes)
        {
            string id = sn.Station?.Id ?? "";
            if (string.IsNullOrEmpty(id) || !_body.HasTransferEndpoint(id)) continue;
            bool isOrigin = id == OriginBuildingId;
            bool isSelected = id == SelectedBuildingId;
            var center = cam.BoardToScreen(sn.Position);
            float radiusScreen = sn.Radius * cam.Zoom.X + 8f;
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
            if (_body != null
                && _body.HasTransferEndpoint(id)
                && id != OriginBuildingId)
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
        if (building == null || _body == null) return false;
        string id = building.Id ?? "";
        if (string.IsNullOrEmpty(id) || !_body.HasTransferEndpoint(id))
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
        _world?.QueueRedraw();
        return true;
    }
}
