using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Abstract base for HSM-driven in-game GUI panels. Holds the shared back/close/
/// forward navigation logic against the root blackboard's <see cref="InteractionStack"/>,
/// so concrete states no longer reimplement the (previously duplicated and
/// inconsistent) back and close handlers.
///
/// Concrete states wire their window signals to <see cref="HandleBack"/> /
/// <see cref="HandleClose"/> in <c>_Enter()</c> and call <see cref="PushAndNavigate"/>
/// from their forward-navigation handlers.
/// </summary>
public abstract partial class InGamePanelState : LimboState, IInGamePanel
{
    /// <summary>
    /// Event dispatched when closing to the HUD. Override for panels whose
    /// "to HUD" transition uses a different event name. Defaults to "window_closed".
    /// </summary>
    protected virtual string CloseEvent => "window_closed";

    /// <summary>
    /// Whether this panel needs the OS cursor visible while it is open. When true
    /// (the default), the base <see cref="_Enter"/>/<see cref="_Exit"/> push and pop a
    /// cursor request on <see cref="WorldInputController"/>, so the cursor is released
    /// on entry and restored to the prior mode on exit. Override to <c>false</c> for
    /// panels that keep the gameplay cursor (e.g. center-reticle aim states).
    /// </summary>
    protected virtual bool RequiresVisibleCursor => true;

    public override void _Enter()
    {
        base._Enter();
        if (RequiresVisibleCursor)
            WorldInputController.Instance?.PushCursorMode(Input.MouseModeEnum.Visible);
    }

    public override void _Exit()
    {
        if (RequiresVisibleCursor)
            WorldInputController.Instance?.PopCursorMode();
        base._Exit();
    }

    public virtual void HandleBack()
    {
        AudioBus.Instance?.Play(MainGameUI.Instance?.GuiBack);
        var resolved = InteractionStack.ResolveBack(Blackboard?.Top());
        // ResolveBack already cleared the stack on the close path; map its
        // "window_closed" sentinel onto this panel's configured CloseEvent.
        Dispatch(resolved == "window_closed" ? CloseEvent : resolved);
    }

    public virtual void HandleClose()
    {
        AudioBus.Instance?.Play(MainGameUI.Instance?.GuiExit);
        InteractionStack.Clear(Blackboard?.Top());
        Dispatch(CloseEvent);
    }

    public void PushAndNavigate(string forwardEvent, string returnEvent, params string[] snapshotVarNames)
    {
        AudioBus.Instance?.Play(MainGameUI.Instance?.GuiForward);
        var bb = Blackboard?.Top();
        if (bb == null)
        {
            Dispatch(forwardEvent);
            return;
        }
        InteractionStack.Push(bb, returnEvent, InteractionStack.SnapshotVars(bb, snapshotVarNames));
        Dispatch(forwardEvent);
    }
}
