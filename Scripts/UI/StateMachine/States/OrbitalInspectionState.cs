using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for managing the OrbitalBodyWindow.
/// Displays detailed information about the selected orbital body.
/// </summary>
public partial class OrbitalInspectionState : LimboState
{
    private OrbitalBodyWindow? _orbitalBodyWindow;
    private Camera3D? _playerCamera;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "OrbitalInspectionState");

        _orbitalBodyWindow = OrbitalBodyWindow.Instance;

        if (_orbitalBodyWindow == null)
        {
            GameLogger.Error("OrbitalInspectionState: OrbitalBodyWindow instance not found");
            return;
        }

        // Find the player's camera
        _playerCamera = FindPlayerCamera();
        if (_playerCamera == null)
        {
            GameLogger.Warning("OrbitalInspectionState: Player camera not found");
        }

        // Show the orbital body window
        //_orbitalBodyWindow.ShowWindow(selectedBody, _playerCamera!);

        // Connect to window closed signal
        // Note: OrbitalBodyWindow doesn't have a direct signal, but IsOpen property
        // We rely on the window's internal handling and check in _Process

        //GameLogger.Debug($"OrbitalInspectionState: Window shown for {selectedBody.BodyName}");
    }

    public override void _Exit()
    {
        base._Exit();

        // Hide the window if it's still open
        _orbitalBodyWindow?.HideWindow();

        _orbitalBodyWindow = null;
        _playerCamera = null;
    }

    private Camera3D? FindPlayerCamera()
    {
        // Try to find the active camera in the scene
        var viewport = GetViewport();
        if (viewport != null)
        {
            return viewport.GetCamera3D();
        }

        // Fallback: search in scene tree
        return GetTree()?.Root?.FindChild("Camera3D", true, false) as Camera3D;
    }
}
