using System;
using System.Collections.Generic;
using ProceduralGeneration.ColorSystem;
using ProceduralGeneration.SubtypeSystem;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using Debug;
#endif

namespace ProceduralGeneration.BiomeSystem;

#if DEBUG
[DebugData("BiomeAssignerConfig", Category = "Game")]
#endif
public partial class BiomeAssignerConfigDatabase : ILoadableDatabase
{
    private static BiomeAssignerConfigDatabase? _instance;
    private static readonly object _lock = new();

    public static BiomeAssignerConfigDatabase Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new BiomeAssignerConfigDatabase();
                return _instance;
            }
        }
    }

    private BiomeAssignerConfig? _config;

    public string DatabaseName => "BiomeAssignerConfigDatabase";
    public bool IsLoaded { get; private set; }
    public float LoadProgress { get; private set; }

    public IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public event Action<string>? OnLoadStarted;
    public event Action<string, bool>? OnLoadCompleted;
    public event Action<string, float>? OnLoadProgressChanged;

    public BiomeAssignerConfig Config
    {
        get
        {
            EnsureLoaded();
            return _config!;
        }
    }

    private BiomeAssignerConfigDatabase() { }

    /// <summary>
    /// Lookup by stable subtype ID (e.g. <c>subtype_rocky_temperate</c>). If not found,
    /// falls back to the first registered subtype of the same <see cref="BodyFamily"/>
    /// (via <see cref="SubtypeDatabase.GetDefaultForFamily"/>); if that also misses, falls
    /// back to any available entry.
    /// </summary>
    public BiomeAssignerEntry GetForSubtype(string subtypeId)
    {
        EnsureLoaded();
        if (!string.IsNullOrEmpty(subtypeId) && _config!.Assigners.TryGetValue(subtypeId, out var entry))
            return entry;

        var subtypeDef = SubtypeDatabase.Instance.IsLoaded
            ? SubtypeDatabase.Instance.GetById(subtypeId ?? string.Empty)
            : null;
        if (subtypeDef != null)
        {
            var familyDefault = SubtypeDatabase.Instance.GetDefaultForFamily(subtypeDef.Family);
            if (familyDefault != null
                && _config!.Assigners.TryGetValue(familyDefault.Id, out var familyEntry))
                return familyEntry;
        }

        foreach (var anyEntry in _config!.Assigners.Values)
            return anyEntry;

        throw new InvalidOperationException(
            $"BiomeAssignerConfigDatabase: no entry for '{subtypeId}' and no fallback available");
    }

    /// <summary>
    /// Legacy enum-keyed overload; converts to stable ID internally. Remove once enums die.
    /// </summary>
    public BiomeAssignerEntry GetForSubtype(RockyPlanetSubtype subtype) =>
        GetForSubtype(BiomeIdMapper.RockyPlanetSubtypeToId(subtype));

    /// <summary>
    /// Replaces the in-memory config without touching disk. Used by the developer-tools
    /// Body Preview tab so designers can render unsaved edits.
    /// </summary>
    public void ReplaceInMemory(BiomeAssignerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        IsLoaded = true;
        LoadProgress = 1f;
    }

    public void LoadData()
    {
        OnLoadStarted?.Invoke(DatabaseName);
        GameLogger.Info($"BiomeAssignerConfigDatabase: Starting load of '{DatabaseName}'");

        IsLoaded = false;
        LoadProgress = 0f;

        try
        {
            LoadProgress = 0.3f;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

            _config = BiomeAssignerConfigLoader.Load();
            if (_config == null)
            {
                throw new InvalidOperationException(
                    "BiomeAssignerConfigLoader returned null for default path");
            }

            LoadProgress = 1f;
            IsLoaded = true;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
            OnLoadCompleted?.Invoke(DatabaseName, true);

            GameLogger.Info($"BiomeAssignerConfigDatabase: '{DatabaseName}' loaded with {_config.Assigners.Count} subtype entries");
        }
        catch (Exception ex)
        {
            IsLoaded = false;
            LoadProgress = 0f;
            OnLoadCompleted?.Invoke(DatabaseName, false);
            GameLogger.Error($"BiomeAssignerConfigDatabase: '{DatabaseName}' failed to load: {ex.Message}");
            throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
        }
    }

    public void Unload()
    {
        _config = null;
        IsLoaded = false;
        LoadProgress = 0f;
        GameLogger.Info($"BiomeAssignerConfigDatabase: '{DatabaseName}' unloaded");
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
}
