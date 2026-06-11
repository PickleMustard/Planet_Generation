using System.Collections.Generic;
using Godot;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Process-wide cache for export-safe wrapper resources (<c>IconConfig</c>, <c>ModelConfig</c>,
/// …) loaded via <see cref="GD.Load{T}(string)"/>.
///
/// <para>
/// Godot returns a single shared managed wrapper per cached resource. The config loaders
/// extract one field (texture / packed scene) and discard the wrapper, which orphans that
/// shared <see cref="GodotObject"/>. When the GC later finalizes it on the background
/// finalizer thread, <c>GodotObject.Dispose</c> runs off the main thread and crashes the
/// engine with SIGILL — observed when the Building Editor reloads its database on save.
/// </para>
///
/// <para>
/// Disposing the wrapper deterministically is not an option either: the instance is shared,
/// so disposing it empties the config for every other consumer that holds it. Instead we
/// root the wrapper here so it is never collected (hence never finalized) and is reused
/// across consumers. Entries are intentionally never evicted — dropping a root would just
/// re-create the orphan-then-finalize crash on the next GC.
/// </para>
/// </summary>
public static class WrapperResourceCache
{
    private static readonly Dictionary<string, Resource> _cache = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Loads (or returns the cached) wrapper resource at <paramref name="path"/>, keeping the
    /// shared managed instance rooted for the lifetime of the process.
    /// </summary>
    /// <returns>The wrapper, or <c>null</c> if it could not be loaded as <typeparamref name="T"/>.</returns>
    public static T? Load<T>(string path) where T : Resource
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var cached) && GodotObject.IsInstanceValid(cached))
                return cached as T;

            var loaded = GD.Load<T>(path);
            if (loaded != null)
                _cache[path] = loaded;
            return loaded;
        }
    }
}
