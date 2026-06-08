namespace UI.StateMachine;

/// <summary>
/// Contract every HSM-driven in-game GUI panel implements for back/close/forward
/// navigation. Implementations record the panel's return point on the
/// <see cref="InteractionStack"/> (stored in the root blackboard) when navigating
/// forward, pop it on Back, and clear it on Close.
///
/// HSM panels implement this via <see cref="InGamePanelState"/>. Free-floating
/// overlay windows that live outside the HSM (and therefore have no blackboard)
/// implement the sibling contract <see cref="IOverlayPanel"/> instead.
/// </summary>
public interface IInGamePanel
{
    /// <summary>
    /// Back one level: pop the InteractionStack (restoring the prior panel's
    /// snapshot) and dispatch the return event. An empty stack or a "to HUD"
    /// entry closes to the HUD.
    /// </summary>
    void HandleBack();

    /// <summary>
    /// Close to HUD: clear the entire InteractionStack and dispatch the close event.
    /// </summary>
    void HandleClose();

    /// <summary>
    /// Navigate forward to a child panel: record this panel's return point
    /// (<paramref name="returnEvent"/> plus a snapshot of <paramref name="snapshotVarNames"/>)
    /// then dispatch <paramref name="forwardEvent"/>. Panel-specific
    /// <c>SetVar</c> calls remain the caller's responsibility.
    /// </summary>
    void PushAndNavigate(string forwardEvent, string returnEvent, params string[] snapshotVarNames);
}
