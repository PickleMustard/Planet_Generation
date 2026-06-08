using Godot;

namespace PlayerInteraction.Camera;

/// <summary>The discrete states of the <see cref="PlayerCameraController"/>.</summary>
public enum CameraFsmState
{
    /// <summary>HUD: camera child of PlayerShip, full WASD + mouse-look flight.</summary>
    FreeFly,

    /// <summary>Blending from the free-fly pose into the focus pose.</summary>
    TransitionIn,

    /// <summary>Reparented under a focused object: orbit-rotate + zoom only.</summary>
    Focus,

    /// <summary>Blending back toward the saved free-fly pose.</summary>
    TransitionOut,
}

/// <summary>
/// Subnode of the PlayerCamera that holds previous/current FSM state and the
/// data needed to restore the free-fly pose when leaving focus. Purely
/// in-memory and transient — never save-game serialized.
/// </summary>
public partial class CameraState : Node
{
    public CameraFsmState CurrentState { get; private set; } = CameraFsmState.FreeFly;
    public CameraFsmState PreviousState { get; private set; } = CameraFsmState.FreeFly;

    /// <summary>Global transform of the camera in free-fly, captured on focus entry.</summary>
    public Transform3D SavedFreeFlyTransform { get; private set; }

    /// <summary>The camera's parent in free-fly (PlayerShip), to reparent back to.</summary>
    public Node3D? SavedFreeFlyParent { get; private set; }

    /// <summary>The currently focused object (null in free-fly).</summary>
    public Node3D? FocusTarget { get; set; }

    /// <summary>The anchor the camera is reparented under while focused.</summary>
    public Node3D? FocusAnchor { get; set; }

    /// <summary>Active framing strategy while focused.</summary>
    public IFocusFramingStrategy? Strategy { get; set; }

    /// <summary>Negative-space pixel offset supplied by the focusing window.</summary>
    public Vector2 ScreenOffset { get; set; }

    public void SetState(CameraFsmState next)
    {
        if (next == CurrentState)
            return;
        PreviousState = CurrentState;
        CurrentState = next;
    }

    /// <summary>Snapshots the camera's current free-fly global transform + parent.</summary>
    public void SaveFreeFly(Camera3D cam)
    {
        SavedFreeFlyTransform = cam.GlobalTransform;
        SavedFreeFlyParent = cam.GetParentOrNull<Node3D>();
    }

    /// <summary>Clears focus references after returning to free-fly.</summary>
    public void ClearFocus()
    {
        FocusTarget = null;
        FocusAnchor = null;
        Strategy = null;
        ScreenOffset = Vector2.Zero;
    }
}
