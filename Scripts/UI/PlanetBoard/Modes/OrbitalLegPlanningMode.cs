using System;
using Constructables;
using Godot;
using Structures.Logistics;
using Structures.Resources;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.PlanetBoard.Modes;

/// <summary>
/// Planet-board mode for picking an orbital-transfer leg endpoint. Orbital transfers
/// only interact with satellites/orbital stations, so this mode selects
/// <see cref="StationNode2D"/> diamonds on the outer ring and ignores surface
/// buildings entirely. Fires <see cref="EndpointPicked"/> with the chosen station and
/// the body it orbits.
/// </summary>
public sealed class OrbitalLegPlanningMode : IPlanetBoardMode
{
    public string DisplayName => "Orbital Leg";

    /// <summary>Id of the leg's origin station, drawn faintly and not selectable.</summary>
    public string OriginStationId { get; set; } = "";

    /// <summary>Most recently picked station id, or empty if none.</summary>
    public string SelectedStationId { get; private set; } = "";

    /// <summary>Fired when the user picks a destination station. Args: station, the body it orbits.</summary>
    public event Action<StationSatellite, IOrbitalBody>? EndpointPicked;

    private BoardWorld? _world;
    private IOrbitalBody? _body;

    public void OnEnter(BoardWorld world, IOrbitalBody body)
    {
        _world = world;
        _body = body;
        SelectedStationId = "";
        world.QueueRedraw();
    }

    public void OnExit()
    {
        _world = null;
        _body = null;
        SelectedStationId = "";
    }

    // Surface buildings / resource ports are not valid orbital endpoints.
    public bool OnPortClick(ResourceNode port, MouseButton button) => false;
    public bool OnPortDragStart(ResourceNode port) => false;
    public void OnPortDragUpdate(Vector2 boardPos, ResourceNode? hoverPort) { }
    public bool OnPortDragEnd(ResourceNode? dropPort) => false;
    public bool OnEdgeClick(ResourceLink link, MouseButton button) => false;
    public bool OnEmptyClick(Vector2 boardPos, MouseButton button) => false;

    public bool OnStationClick(StationNode2D node, MouseButton button)
    {
        if (button != MouseButton.Left || _body == null || node?.Station == null)
            return false;

        var station = node.Station;
        string id = station.Id ?? "";
        if (string.IsNullOrEmpty(id))
            return false;

        if (id == OriginStationId)
        {
            ToastSystem.Instance?.ShowWarning("Cannot route back to the leg's origin.");
            return false;
        }

        if (station.IsUnderConstruction)
        {
            ToastSystem.Instance?.ShowWarning("Station is still under construction.");
            return false;
        }

        SelectedStationId = id;
        EndpointPicked?.Invoke(station, _body);
        _world?.QueueRedraw();
        return true;
    }

    public void DrawOverlay(CanvasItem ci, BoardCameraController cam)
    {
        if (_world == null || _body == null)
            return;

        foreach (var sn in _world.StationNodes)
        {
            string id = sn.Station?.Id ?? "";
            if (string.IsNullOrEmpty(id))
                continue;

            bool isOrigin = id == OriginStationId;
            bool isSelected = id == SelectedStationId;
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
        if (hovered is StationNode2D sn && sn.Station != null)
            return string.IsNullOrEmpty(sn.Station.Name) ? "Orbital station" : sn.Station.Name;
        return null;
    }
}
