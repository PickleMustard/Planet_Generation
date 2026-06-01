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
    private StationPlacementMode? _stationMode;
    private BuildingDefinition? _buildingDef;

    /// <summary>Provides the HSM used only for the headquarters dispatch path.</summary>
    public void Initialize(GUIControllerHSM hsm) => _hsm = hsm;

    public void Begin(string itemType, string definitionName)
    {
        if (_active)
            return;

        _active = true;
        _itemType = itemType;
        _definitionName = definitionName;

        // Place on the inspected body if a window is showing one; else aim.
        if (OrbitalBodyWindow.Instance is { IsOpen: true, CurrentBody: { } body })
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
        Input.SetMouseMode(Input.MouseModeEnum.Captured);
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
        if (_itemType == "Station")
        {
            StationDatabase.Instance.TryGetStation(_definitionName, out var def);
            if (def == null)
            {
                GameLogger.Error($"PlacementOverlayController: station '{_definitionName}' not found");
                Teardown();
                return;
            }

            _stationMode = new StationPlacementMode { UseMousePosition = useMouse };
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

            _buildingMode = new BuildingPlacementMode { UseMousePosition = useMouse };
            _buildingMode.PlacementConfirmed += OnBuildingConfirmed;
            _buildingMode.PlacementCancelled += Teardown;
            AddChild(_buildingMode);
            _buildingMode.Initialize(_buildingDef, body);
        }

        // Captured/center-reticle for HUD launch; cursor stays visible over the
        // orbital window (where we suspend its own input instead).
        Input.SetMouseMode(useMouse
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

        if (_overOrbitalWindow && OrbitalBodyWindow.Instance != null)
        {
            // Window stays visible; just hand input back to it.
            OrbitalBodyWindow.Instance.InteractionSuspended = false;
        }
        else
        {
            Input.SetMouseMode(Input.MouseModeEnum.Captured);
        }

        _buildingDef = null;
        _overOrbitalWindow = false;
        _active = false;
    }
}
