using System;
using Godot;
using PlayerInteraction.CellSelection;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Deprecated. Blackboard population and HSM dispatch are now handled by
/// individual states (HudState, CellOverviewState, etc.) directly.
/// CellSelectionManager is called from HudState for shader highlighting.
/// </summary>
[Obsolete("Use individual state Blackboard population and Dispatch instead.")]
public partial class GUIManager : Node
{
    public static GUIManager? Instance { get; private set; }

    private Blackboard _blackboard = null!;
    private GUIControllerHSM? _guiController;

    /// <summary>
    /// The shared blackboard for all GUI states.
    /// </summary>
    public Blackboard Blackboard => _blackboard;

    /// <summary>
    /// True when any GUI window is open (not in HUD/idle state).
    /// </summary>
    public bool IsWindowOpen => _guiController?.GetActiveState() is not IdleState;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        GameLogger.Info("GUIManager initializing...");

        // Create the shared blackboard
        _blackboard = new Blackboard();

        // Get reference to GUIControllerHSM (should be sibling)
        _guiController = GetParent()?.GetNode<GUIControllerHSM>("GUIController");

        if (_guiController == null)
        {
            GameLogger.Error("GUIManager: GUIControllerHSM not found as sibling");
            return;
        }


        // Route CellSelectionManager signals through the HSM
        if (CellSelectionManager.Instance != null)
        {
            CellSelectionManager.Instance.CellSelected += OnCellSelected;
        }

        GameLogger.Info("GUIManager initialized");
    }

    /// <summary>
    /// Handles universal Escape key to close windows.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key)
            return;

        if (key.Pressed && key.Keycode == Key.Escape && IsWindowOpen)
        {
            GameLogger.Debug("GUIManager: Escape pressed, dispatching escape_pressed");
            _guiController?.Dispatch("escape_pressed");
            GetViewport().SetInputAsHandled();
        }
    }

    // ───────── Signal Handlers ─────────

    private void OnCellSelected(VoronoiCell cell, Node3D body)
    {
        GameLogger.Debug("GUIManager: Cell selected");

        // Populate blackboard
        _blackboard.SetVar("SelectedCell", cell);
        _blackboard.SetVar("SelectedBody", body);
        _blackboard.SetVar("BodyType", body.GetType().Name);

        // Dispatch to HSM
        _guiController?.Dispatch("cell_selected");
    }

    // ───────── Public API ─────────

    /// <summary>
    /// Requests to open the orbital body window for the given body.
    /// Populates blackboard and dispatches to HSM.
    /// </summary>
    public void RequestOpenOrbitalBody(IOrbitalBody body, Camera3D camera)
    {
        GameLogger.Debug($"GUIManager: Requesting orbital body window for {body.BodyName}");

        // Populate blackboard using standardized converter
        OrbitalBodyConverter.SetToBlackboard(_blackboard, "SelectedBody", body);
        _blackboard.SetVar("PlayerCamera", camera);

        // Dispatch to HSM
        _guiController?.Dispatch("orbital_body_selected");
    }

    /// <summary>
    /// Requests to open the construction menu.
    /// </summary>
    public void RequestOpenConstruction()
    {
        GameLogger.Debug("GUIManager: Requesting construction menu");
        _guiController?.Dispatch("construction_menu_opened");
    }

    /// <summary>
    /// Requests to close the current window and return to HUD.
    /// </summary>
    public void RequestCloseWindow()
    {
        GameLogger.Debug("GUIManager: Requesting window close");
        _guiController?.Dispatch("window_closed");
    }
}
