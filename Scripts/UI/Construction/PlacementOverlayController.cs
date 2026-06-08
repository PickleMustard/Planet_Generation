using System.Collections.Generic;
using System.Linq;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Godot;
using Logistics.Resources;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using Structures.Resources;
using UI.StateMachine;
using UtilityLibrary;

namespace UI.Construction;

/// <summary>
/// Drives building/station placement as an overlay, decoupled from the HSM.
/// Launched by <see cref="ConstructionMenuController.CardActivated"/>. When an
/// Orbital Body Window is open it places directly on the inspected body (skips
/// aiming) and leaves that window visible; otherwise it runs a
/// <see cref="PlanetAimSelector"/> first. Placement teardown closes nothing but
/// itself. The HQ (GameStartBehavior) building is the one exception — it still
/// dispatches into the HSM so the game-start naming dialog can run.
/// </summary>
public partial class PlacementOverlayController : Node
{
    private GUIControllerHSM? _hsm;

    private bool _active;
    private bool _overOrbitalWindow;
    private string _itemType = "";
    private string _definitionName = "";

    private PlanetAimSelector? _aimSelector;
    private BuildingPlacementMode? _buildingMode;
    private StationOrbitPlacementMode? _stationMode;
    private BuildingDefinition? _buildingDef;

    private bool _cursorPushed;

    /// <summary>Provides the HSM used only for the headquarters dispatch path.</summary>
    public void Initialize(GUIControllerHSM hsm) => _hsm = hsm;

    /// <summary>
    /// Maintain exactly one cursor request for the placement lifecycle, swapping the
    /// mode in place as the flow moves aim → placement. Released in <see cref="Teardown"/>.
    /// </summary>
    private void SetPlacementCursor(Input.MouseModeEnum mode)
    {
        if (_cursorPushed)
            WorldInputController.Instance?.PopCursorMode();
        WorldInputController.Instance?.PushCursorMode(mode);
        _cursorPushed = true;
    }

    private void ReleasePlacementCursor()
    {
        if (!_cursorPushed)
            return;
        WorldInputController.Instance?.PopCursorMode();
        _cursorPushed = false;
    }

    public void Begin(string itemType, string definitionName)
    {
        if (_active)
            return;

        _active = true;
        _itemType = itemType;
        _definitionName = definitionName;

        // Auto-pick the focused body when the camera is focused on one (any
        // window enabling construction); otherwise run the full aim flow.
        var controller = GetViewport().GetCamera3D() as PlayerInteraction.Camera.PlayerCameraController;
        if (controller?.FocusTarget is IOrbitalBody body)
        {
            _overOrbitalWindow = true;
            StartPlacement(body, useMouse: true);
        }
        else
        {
            _overOrbitalWindow = false;
            StartAim();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!_active)
            return;

        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            Teardown();
        }
    }

    // ───────── Aim (HUD launch) ─────────

    private void StartAim()
    {
        _aimSelector = new PlanetAimSelector();
        _aimSelector.BodySelected += OnAimConfirmed;
        AddChild(_aimSelector);
        SetPlacementCursor(Input.MouseModeEnum.Captured);
    }

    private void OnAimConfirmed(IOrbitalBody body)
    {
        if (_aimSelector != null)
        {
            _aimSelector.QueueFree();
            _aimSelector = null;
        }
        StartPlacement(body, useMouse: false);
    }

    // ───────── Placement ─────────

    private void StartPlacement(IOrbitalBody body, bool useMouse)
    {
        // The single player camera stays Current in every state (it reparents on
        // focus rather than being swapped out), so the viewport camera is always
        // the right one — no injection chain needed.
        Camera3D? camera = GetViewport().GetCamera3D();

        if (_itemType == "Station")
        {
            StationDatabase.Instance.TryGetStation(_definitionName, out var def);
            if (def == null)
            {
                GameLogger.Error($"PlacementOverlayController: station '{_definitionName}' not found");
                Teardown();
                return;
            }

            // The top-down placement mode always uses the cursor and the
            // always-Current player camera; it swoops the camera overhead itself.
            _stationMode = new StationOrbitPlacementMode();
            _stationMode.PlacementConfirmed += OnStationConfirmed;
            _stationMode.PlacementCancelled += Teardown;
            AddChild(_stationMode);
            _stationMode.Initialize(def, body);
        }
        else // Building (default)
        {
            BuildingDatabase.Instance.TryGetBuilding(_definitionName, out _buildingDef);
            if (_buildingDef == null)
            {
                GameLogger.Error($"PlacementOverlayController: building '{_definitionName}' not found");
                Teardown();
                return;
            }

            _buildingMode = new BuildingPlacementMode { UseMousePosition = useMouse, OverrideCamera = camera };
            _buildingMode.PlacementConfirmed += OnBuildingConfirmed;
            _buildingMode.PlacementCancelled += Teardown;
            AddChild(_buildingMode);
            _buildingMode.Initialize(_buildingDef, body);
        }

        // Stations use the top-down board (always cursor-driven); buildings use a
        // center reticle on the HUD launch (captured) but the cursor over the
        // orbital window. Recaptured on teardown for the HUD path.
        bool wantsCursor = _itemType == "Station" || useMouse;
        SetPlacementCursor(wantsCursor
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured);

        if (_overOrbitalWindow && OrbitalBodyWindow.Instance != null)
            OrbitalBodyWindow.Instance.InteractionSuspended = true;
    }

    private void OnBuildingConfirmed(
        VoronoiCell primaryCell,
        Node3D body,
        Godot.Collections.Array<VoronoiCell> additionalCells)
    {
        if (_buildingDef == null)
            return;

        try
        {
            List<VoronoiCell>? extraCells = null;
            if (additionalCells != null && additionalCells.Count > 0)
            {
                extraCells = new List<VoronoiCell>();
                foreach (var cell in additionalCells)
                    extraCells.Add(cell);
            }

            var building = ConstructionManager.Instance.CreateBuilding(
                primaryCell,
                body,
                _buildingDef,
                extraCells);

            // The company HQ (GameStartBehavior) diverts into the GameStartState
            // naming dialog. This is the only path that touches the HSM.
            if (building.Behaviors.OfType<GameStartBehavior>().Any())
            {
                _hsm?.Blackboard?.Top().SetVar("PlacedHq", Variant.From(building));
                Teardown();
                _hsm?.Dispatch("headquarters_placed");
                return;
            }

            // Normal placement: do NOT dispatch placement_confirmed — that would
            // force the HSM back to HUD and close any open orbital window. The
            // placement mode already showed the "Construction started" toast.
            Teardown();
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"PlacementOverlayController: failed to create building — {e.Message}");
            ToastSystem.Instance?.Show($"Error: {e.Message}");
            Teardown();
        }
    }

    private void OnStationConfirmed()
    {
        // StationPlacementMode already created the station and showed its toast.
        Teardown();
    }

    // ───────── Teardown ─────────

    private void Teardown()
    {
        bool wasStation = _stationMode != null;

        if (_aimSelector != null)
        {
            _aimSelector.QueueFree();
            _aimSelector = null;
        }

        if (_buildingMode != null)
        {
            _buildingMode.PlacementConfirmed -= OnBuildingConfirmed;
            _buildingMode.PlacementCancelled -= Teardown;
            _buildingMode.Cleanup();
            _buildingMode.QueueFree();
            _buildingMode = null;
        }

        if (_stationMode != null)
        {
            _stationMode.PlacementConfirmed -= OnStationConfirmed;
            _stationMode.PlacementCancelled -= Teardown;
            _stationMode.Cleanup();
            _stationMode.QueueFree();
            _stationMode = null;
        }

        // Swoop the camera back after a station placement. Over the orbital
        // window, restore that window's framing; otherwise return to the ship.
        if (wasStation)
        {
            var controller = GetViewport().GetCamera3D() as PlayerInteraction.Camera.PlayerCameraController;
            if (_overOrbitalWindow && OrbitalBodyWindow.Instance != null)
                OrbitalBodyWindow.Instance.RefocusCamera();
            else
                controller?.ExitFocus();
        }

        if (_overOrbitalWindow && OrbitalBodyWindow.Instance != null)
        {
            // Window stays visible; just hand input back to it.
            OrbitalBodyWindow.Instance.InteractionSuspended = false;
        }

        // Pop our placement cursor request; the stack restores the underlying mode
        // (the orbital window's Visible, or the gameplay base when launched from HUD).
        ReleasePlacementCursor();

        _buildingDef = null;
        _overOrbitalWindow = false;
        _active = false;
    }
}
