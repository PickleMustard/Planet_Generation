using Godot;

namespace UI.StateMachine;

/// <summary>
/// Manages a LIFO stack of navigation entries stored in the root HSM's blackboard.
/// Each entry carries the dispatch event to return to the previous panel
/// plus a snapshot of blackboard variables needed to restore that panel's context.
///
/// Stack entry format (Godot.Collections.Dictionary):
///   "event"  : StringName — dispatch event to return to previous panel
///   "bb"     : Dictionary — blackboard variable snapshot for context restoration
///
/// Lifecycle (LimboAI event flow: dispatch → source._exit → target._enter):
///   Push BEFORE dispatch. Restore snapshot BEFORE dispatch on pop.
///   source._exit cleanup runs after dispatch but before target._enter,
///   so restored vars survive the transition.
/// </summary>
public static class InteractionStack
{
    public const string BB_KEY = "InteractionStack";

    /// <summary>
    /// Push a navigation entry onto the stack.
    /// Call BEFORE Dispatch() when transitioning to a new panel.
    /// </summary>
    /// <param name="bb">The root HSM blackboard (Blackboard.Top())</param>
    /// <param name="returnEvent">Dispatch event that returns to the previous panel</param>
    /// <param name="bbSnapshot">Snapshot of blackboard vars the previous panel needs</param>
    public static void Push(
        Blackboard bb,
        string returnEvent,
        Godot.Collections.Dictionary bbSnapshot
    )
    {
        var stack = GetOrCreateStack(bb);
        var entry = new Godot.Collections.Dictionary
        {
            { "event", returnEvent },
            { "bb", bbSnapshot },
        };
        stack.Add(entry);
        bb.SetVar(BB_KEY, stack);
    }

    /// <summary>
    /// Pop the top entry and restore its blackboard snapshot.
    /// Call BEFORE Dispatch() when handling Back One Panel.
    /// </summary>
    /// <returns>The return event, or null if stack is empty</returns>
    public static string? Pop(Blackboard bb)
    {
        var stack = GetStack(bb);
        if (stack == null || stack.Count == 0)
            return null;

        var entry = stack[^1].AsGodotDictionary();
        stack.RemoveAt(stack.Count - 1);
        bb.SetVar(BB_KEY, stack);

        var returnEvent = entry.ContainsKey("event") ? entry["event"].AsString() : "";
        var snapshot = entry.ContainsKey("bb") ? entry["bb"].AsGodotDictionary() : null;

        if (snapshot != null && snapshot.Count > 0)
            RestoreSnapshot(bb, snapshot);

        return returnEvent;
    }

    /// <summary>
    /// Peek at the top entry without removing it.
    /// </summary>
    public static string? Peek(Blackboard bb)
    {
        var stack = GetStack(bb);
        if (stack == null || stack.Count == 0)
            return null;

        var entry = stack[^1].AsGodotDictionary();
        return entry.ContainsKey("event") ? entry["event"].AsString() : "";
    }

    /// <summary>
    /// Clear the entire stack. Call when closing to HUD.
    /// </summary>
    public static void Clear(Blackboard bb)
    {
        bb.SetVar(BB_KEY, new Godot.Collections.Array());
    }

    /// <summary>
    /// Check if the stack is empty.
    /// </summary>
    public static bool IsEmpty(Blackboard bb)
    {
        var stack = GetStack(bb);
        return stack == null || stack.Count == 0;
    }

    /// <summary>
    /// Get the current depth of the stack.
    /// </summary>
    public static int Depth(Blackboard bb)
    {
        var stack = GetStack(bb);
        return stack?.Count ?? 0;
    }

    /// <summary>
    /// Create a snapshot of specific blackboard variables.
    /// Only snapshots variables that actually exist in the blackboard.
    /// </summary>
    public static Godot.Collections.Dictionary SnapshotVars(Blackboard bb, params string[] varNames)
    {
        var snapshot = new Godot.Collections.Dictionary();
        foreach (var name in varNames)
        {
            var val = bb.GetVar(name);
            if (val.VariantType != Variant.Type.Nil)
                snapshot[name] = val;
        }
        return snapshot;
    }

    /// <summary>
    /// Restore blackboard variables from a snapshot.
    /// Overwrites existing values; does NOT delete variables not in the snapshot.
    /// </summary>
    public static void RestoreSnapshot(Blackboard bb, Godot.Collections.Dictionary snapshot)
    {
        foreach (var key in snapshot.Keys)
        {
            var keyStr = key.AsString();
            bb.SetVar(keyStr, snapshot[key]);
        }
    }

    // ───────── Private helpers ─────────

    private static Godot.Collections.Array? GetStack(Blackboard bb)
    {
        var val = bb.GetVar(BB_KEY);
        if (val.VariantType == Variant.Type.Nil)
            return null;
        return val.AsGodotArray();
    }

    private static Godot.Collections.Array GetOrCreateStack(Blackboard bb)
    {
        var existing = GetStack(bb);
        if (existing != null)
            return existing;
        return new Godot.Collections.Array();
    }
}
