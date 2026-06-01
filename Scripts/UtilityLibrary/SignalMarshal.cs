using System.Threading;
using Godot;

namespace UtilityLibrary;

/// <summary>
/// Thread-marshaling helpers for Godot signal emission. Godot rejects EmitSignal /
/// CallDeferred / SceneTree mutation from non-main threads, so any code reachable from
/// <c>IManufactureTickable.OnManufactureTick</c> (or other background workers) must route
/// node-local signal emissions through here, and SignalBus emissions through
/// <c>SignalBus.SafeEmit*</c>. <c>Initialize</c> is called from <c>SignalBus._Ready</c>.
/// </summary>
public static class SignalMarshal
{
    private static int _mainThreadId = -1;

    public static bool IsOnMainThread => _mainThreadId == Thread.CurrentThread.ManagedThreadId;

    /// <summary>
    /// Records the calling thread as the main thread. Idempotent. Must be invoked from
    /// the Godot main thread (e.g. an autoload's _Ready).
    /// </summary>
    public static void Initialize()
    {
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    public static void EmitNode(GodotObject target, StringName signal)
    {
        if (IsOnMainThread)
            target.EmitSignal(signal);
        else
            target.CallDeferred(GodotObject.MethodName.EmitSignal, signal);
    }

    public static void EmitNode(GodotObject target, StringName signal, Variant arg0)
    {
        if (IsOnMainThread)
            target.EmitSignal(signal, arg0);
        else
            target.CallDeferred(GodotObject.MethodName.EmitSignal, signal, arg0);
    }

    public static void EmitNode(GodotObject target, StringName signal, Variant arg0, Variant arg1)
    {
        if (IsOnMainThread)
            target.EmitSignal(signal, arg0, arg1);
        else
            target.CallDeferred(GodotObject.MethodName.EmitSignal, signal, arg0, arg1);
    }
}
