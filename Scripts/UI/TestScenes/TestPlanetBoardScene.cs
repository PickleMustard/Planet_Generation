using System;
using Godot;
using UI.PlanetBoard;
using UI.PlanetBoard.Modes;
using UI.PlanetBoard.Testing;
using UtilityLibrary;

namespace UI.TestScenes;

/// <summary>
/// Standalone dev harness for the PlanetBoard widget. Mounts a <see cref="MockOrbitalBody"/>,
/// embeds a <see cref="PlanetBoardWindow"/>, and exposes a side panel for spawning mock
/// buildings, picking modes, and clearing/resetting. Reachable from the main-menu Debug
/// submenu. The layout is defined in <c>TestPlanetBoard.tscn</c>; this class only handles
/// behaviour.
/// </summary>
public partial class TestPlanetBoardScene : Control
{
    private static readonly string[] _shapes = { "triangle", "square", "rectangle", "pentagon", "hexagon" };
    private static readonly Color[] _palette =
    {
        new(0.30f, 0.55f, 0.80f),
        new(0.85f, 0.50f, 0.30f),
        new(0.40f, 0.75f, 0.50f),
        new(0.75f, 0.40f, 0.70f),
        new(0.80f, 0.75f, 0.30f),
    };

    [Export]
    private PlanetBoardWindow _board = null!;

    private MockOrbitalBody _body = null!;
    private string? _firstTransferStationId;
    private readonly Random _rng = new();

    public override void _Ready()
    {
        _body = new MockOrbitalBody { Name = "MockBody" };
        AddChild(_body);

        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/SpawnTriangleBtn", () => OnSpawnShape("triangle"));
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/SpawnSquareBtn", () => OnSpawnShape("square"));
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/SpawnRectangleBtn", () => OnSpawnShape("rectangle"));
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/SpawnPentagonBtn", () => OnSpawnShape("pentagon"));
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/SpawnHexagonBtn", () => OnSpawnShape("hexagon"));
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/SpawnTransferStationBtn", OnSpawnTransferStation);
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/ResourceLinksBtn", () => OnSwitchMode("ResourceLink"));
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/TransferRoutesBtn", () => OnSwitchMode("TransferRoute"));
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/OverviewBtn", () => OnSwitchMode("Overview"));
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/ResetCameraBtn", OnResetCamera);
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/ResetLayoutBtn", OnResetLayout);
        ConnectButton("MainSplit/SidePanel/SideMargin/SideCol/ClearBoardBtn", OnClearBoard);

        _board.OpenForBody(_body, "ResourceLink");

        GameLogger.Info("TestPlanetBoardScene ready. Spawn mock buildings via the side panel.");
    }

    private void ConnectButton(string path, Action action)
    {
        var btn = GetNodeOrNull<Button>(path);
        if (btn != null)
            btn.Pressed += action;
        else
            GameLogger.Warning($"TestPlanetBoardScene: button not found at {path}");
    }

    private void OnSpawnShape(string shape)
    {
        var color = _palette[_rng.Next(_palette.Length)];
        var b = MockBuildingFactory.Create(shape, color, 1, 1);
        _body.AddBuilding(b);
        SignalBus.Instance?.EmitBuildingConstructed(0);
    }

    private void OnSpawnTransferStation()
    {
        var b = MockBuildingFactory.Create("square", new Color(0.92f, 0.78f, 0.32f), 0, 1, isTransferStation: true);
        _body.AddBuilding(b);
        var tsb = b.GetBehavior<Constructables.Buildings.Behaviors.TransferStationBehavior>();
        if (tsb != null)
        {
            var def = new Structures.Transfers.TransferStationDefinition
            {
                CargoCapacity = tsb.CargoCapacity,
                VehicleSpeed = tsb.VehicleSpeed,
                MaxConcurrentTransfers = tsb.MaxConcurrentTransfers,
            };
            _body.RegisterTransferEndpoint(b.Id, def, b);
        }
        _firstTransferStationId ??= b.Id;
        ApplyTransferOrigin();
        SignalBus.Instance?.EmitBuildingConstructed(0);
    }

    private void OnSwitchMode(string mode)
    {
        _board.OpenForBody(_body, mode);
        ApplyTransferOrigin();
    }

    private void ApplyTransferOrigin()
    {
        if (_board.View?.ActiveMode is TransferRoutePlanningMode tr && _firstTransferStationId != null)
            tr.OriginBuildingId = _firstTransferStationId;
    }

    private void OnResetCamera()
    {
        _board.View.ResetCamera();
    }

    private void OnResetLayout()
    {
        _board.View.RefreshFromBody();
        GameLogger.Info("Reset all building positions to computed layout.");
    }

    private void OnClearBoard()
    {
        _body.ClearBuildings();
        _firstTransferStationId = null;
        SignalBus.Instance?.EmitBuildingConstructed(0);
    }

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
