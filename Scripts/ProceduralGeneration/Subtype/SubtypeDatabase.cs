using System;
using System.Collections.Generic;
using System.Linq;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using Debug;
#endif

namespace ProceduralGeneration.SubtypeSystem;

/// <summary>
/// Registry of all data-driven body subtypes (across all <see cref="BodyFamily"/> values).
/// Replaces the per-family subtype enums (<c>RockyPlanetSubtype</c>, <c>GasGiantSubtype</c>, …)
/// with stable string IDs and YAML-defined properties.
/// </summary>
#if DEBUG
[DebugData("SubtypeDatabase", Category = "Game")]
#endif
public partial class SubtypeDatabase : ILoadableDatabase
{
    private static SubtypeDatabase? _instance;
    private static readonly object _lock = new();

    public static SubtypeDatabase Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new SubtypeDatabase();
                return _instance;
            }
        }
    }

    private Dictionary<string, SubtypeDefinition> _subtypes = new(StringComparer.Ordinal);

    public string DatabaseName => "SubtypeDatabase";
    public bool IsLoaded { get; private set; }
    public float LoadProgress { get; private set; }

    public IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public event Action<string>? OnLoadStarted;
    public event Action<string, bool>? OnLoadCompleted;
    public event Action<string, float>? OnLoadProgressChanged;

    private SubtypeDatabase() { }

    public void LoadData()
    {
        OnLoadStarted?.Invoke(DatabaseName);
        GameLogger.Info($"SubtypeDatabase: Starting load of '{DatabaseName}'");

        IsLoaded = false;
        LoadProgress = 0f;

        try
        {
            LoadProgress = 0.3f;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

            var loaded = SubtypeDefinitionLoader.LoadAll();
            if (loaded == null)
            {
                throw new InvalidOperationException(
                    "SubtypeDefinitionLoader returned null for default path");
            }

            _subtypes.Clear();
            foreach (var def in loaded)
            {
                if (string.IsNullOrEmpty(def.Id))
                {
                    GameLogger.Error("SubtypeDatabase: skipping subtype with empty id");
                    continue;
                }
                if (_subtypes.ContainsKey(def.Id))
                {
                    GameLogger.Error($"SubtypeDatabase: duplicate subtype id '{def.Id}'");
                    continue;
                }
                _subtypes[def.Id] = def;
            }

            LoadProgress = 1f;
            IsLoaded = true;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
            OnLoadCompleted?.Invoke(DatabaseName, true);

            GameLogger.Info($"SubtypeDatabase: '{DatabaseName}' loaded with {_subtypes.Count} subtypes");
        }
        catch (Exception ex)
        {
            IsLoaded = false;
            LoadProgress = 0f;
            OnLoadCompleted?.Invoke(DatabaseName, false);
            GameLogger.Error($"SubtypeDatabase: '{DatabaseName}' failed to load: {ex.Message}");
            throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
        }
    }

    public void Unload()
    {
        _subtypes.Clear();
        IsLoaded = false;
        LoadProgress = 0f;
        GameLogger.Info($"SubtypeDatabase: '{DatabaseName}' unloaded");
    }

    public WorkPackage CreateLoadPackage()
    {
        return new WorkPackageBuilder()
            .WithName($"Load_{DatabaseName}")
            .WithPriority(TaskPriority.Normal)
            .AddStep($"Load_Database_{DatabaseName}", () => LoadData())
            .Build();
    }

    private void EnsureLoaded()
    {
        if (!IsLoaded)
            throw new DatabaseNotLoadedException(DatabaseName);
    }

    // ── Read API ────────────────────────────────────────────────────────

    public SubtypeDefinition? GetById(string id)
    {
        EnsureLoaded();
        return _subtypes.TryGetValue(id, out var def) ? def : null;
    }

    public bool Exists(string id)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(id) && _subtypes.ContainsKey(id);
    }

    public IReadOnlyCollection<SubtypeDefinition> All
    {
        get
        {
            EnsureLoaded();
            return _subtypes.Values;
        }
    }

    public IEnumerable<SubtypeDefinition> ByFamily(BodyFamily family)
    {
        EnsureLoaded();
        return _subtypes.Values.Where(s => s.Family == family);
    }

    /// <summary>
    /// Returns the first registered subtype for the given family, used as a fallback when a
    /// caller has a family but no specific subtype (replaces the old hardcoded "Temperate" default).
    /// </summary>
    public SubtypeDefinition? GetDefaultForFamily(BodyFamily family)
    {
        EnsureLoaded();
        return _subtypes.Values.FirstOrDefault(s => s.Family == family);
    }

    public IReadOnlyDictionary<string, SubtypeDefinition> AsDictionary
    {
        get
        {
            EnsureLoaded();
            return _subtypes;
        }
    }

    // ── Editor-facing mutation API ──────────────────────────────────────

    public void Add(SubtypeDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (string.IsNullOrEmpty(def.Id))
            throw new ArgumentException("SubtypeDefinition.Id must be non-empty", nameof(def));
        if (_subtypes.ContainsKey(def.Id))
            throw new InvalidOperationException($"Subtype '{def.Id}' already exists");
        _subtypes[def.Id] = def;
        IsLoaded = true;
    }

    public bool Remove(string id)
    {
        return _subtypes.Remove(id);
    }

    public void Rename(string oldId, string newId)
    {
        if (!_subtypes.TryGetValue(oldId, out var def))
            throw new InvalidOperationException($"Subtype '{oldId}' not found");
        if (_subtypes.ContainsKey(newId))
            throw new InvalidOperationException($"Subtype '{newId}' already exists");
        _subtypes.Remove(oldId);
        def.Id = newId;
        _subtypes[newId] = def;
    }

    public void ReplaceInMemory(IEnumerable<SubtypeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _subtypes.Clear();
        foreach (var def in definitions)
        {
            if (!string.IsNullOrEmpty(def.Id))
                _subtypes[def.Id] = def;
        }
        IsLoaded = true;
        LoadProgress = 1f;
    }
}
