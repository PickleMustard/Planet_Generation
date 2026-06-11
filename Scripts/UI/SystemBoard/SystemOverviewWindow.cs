using Godot;
using Structures.GameState;
using UI.OrbitalScheduling;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.SystemBoard;

/// <summary>
/// Full-screen, read-only System Overview overlay: a paper-shelled window hosting
/// a <see cref="SystemBoardView"/> that maps the whole star system (bodies, orbits,
/// satellites, stations, logistics units). Built entirely in code (no .tscn / HSM
/// edits), following the <c>OrbitalScheduleWindow</c> overlay pattern. Opened via
/// <see cref="ShowWindow"/> from an existing window's button.
/// </summary>
public sealed partial class SystemOverviewWindow : Control, IOverlayPanel
{
    public static SystemOverviewWindow? Instance { get; private set; }

    [Export] private SystemBoardView? _board;

    private static PackedScene? _scene;

    /// <summary>Instantiates the overlay scene. Add to the tree, then <see cref="ShowWindow"/>.</summary>
    public static SystemOverviewWindow Create()
    {
        _scene ??= GD.Load<PackedScene>("res://UI/SystemBoard/SystemOverviewWindow.tscn");
        return _scene.Instantiate<SystemOverviewWindow>();
    }

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Ready()
    {
        _board?.SetPickMode(false);
        Hide();
    }

    public void ShowWindow()
    {
        var container = OrbitalScheduleUiHelpers.FindSystemContainer(this)
            ?? GetTree()?.Root?.GetNodeOrNull("GameScene/system_container");
        if (container != null)
            _board?.SetSystem(container);
        else
            GameLogger.Warning("[SystemOverviewWindow] system_container not found.");
        Show();
        GameLogger.Info("[SystemOverviewWindow] Opened");
    }

    public void HideWindow()
    {
        Hide();
        GameLogger.Info("[SystemOverviewWindow] Closed");
    }

    /// <summary>This overlay has no inner stack, so Back is equivalent to Close.</summary>
    public void RequestBack() => RequestClose();

    public void RequestClose() => HideWindow();

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            RequestClose();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            RequestClose();
    }
}
