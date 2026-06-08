namespace UI;

/// <summary>
/// Navigation contract for free-floating overlay windows that live OUTSIDE the
/// GUI HSM (built in code, opened directly from another window's button, with no
/// blackboard access). It is the self-contained sibling of
/// <c>UI.StateMachine.IInGamePanel</c>: instead of the blackboard
/// <c>InteractionStack</c>, an overlay manages its own back behavior internally
/// (e.g. wizard → list → close) so the user always has a Back and a Close path.
/// </summary>
public interface IOverlayPanel
{
    /// <summary>
    /// Back one level within the overlay. If the overlay has no inner stack to
    /// pop, this is equivalent to <see cref="RequestClose"/>.
    /// </summary>
    void RequestBack();

    /// <summary>
    /// Close the overlay entirely, returning to whatever opened it.
    /// </summary>
    void RequestClose();
}
