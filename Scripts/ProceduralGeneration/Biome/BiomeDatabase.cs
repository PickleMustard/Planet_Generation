using System;
using System.Collections.Generic;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using Debug;
#endif

namespace ProceduralGeneration.BiomeSystem;

/// <summary>
/// Registry of all data-driven biomes. Singleton. Replaces the legacy
/// <c>Biome.BiomeType</c> enum + scattered switch tables for color, hazard,
/// geothermal vent probability, tags, and resource weight modifiers.
/// </summary>
#if DEBUG
[DebugData("BiomeDatabase", Category = "Game")]
#endif
public partial class BiomeDatabase : ILoadableDatabase
{
    private static BiomeDatabase? _instance;
    private static readonly object _lock = new();

    public static BiomeDatabase Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new BiomeDatabase();
                return _instance;
            }
        }
    }

    private Dictionary<string, BiomeDefinition> _biomes = new(StringComparer.Ordinal);

    public string DatabaseName => "BiomeDatabase";
    public bool IsLoaded { get; private set; }
    public float LoadProgress { get; private set; }

    public IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public event Action<string>? OnLoadStarted;
    public event Action<string, bool>? OnLoadCompleted;
    public event Action<string, float>? OnLoadProgressChanged;

    private BiomeDatabase() { }

    public void LoadData()
    {
        OnLoadStarted?.Invoke(DatabaseName);
        GameLogger.Info($"BiomeDatabase: Starting load of '{DatabaseName}'");

        IsLoaded = false;
        LoadProgress = 0f;

        try
        {
            LoadProgress = 0.3f;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

            var loaded = BiomeDefinitionLoader.Load();
            if (loaded == null)
            {
                throw new InvalidOperationException(
                    "BiomeDefinitionLoader returned null for default path");
            }

            _biomes.Clear();
            foreach (var def in loaded)
            {
                if (string.IsNullOrEmpty(def.Id))
                {
                    GameLogger.Error("BiomeDatabase: skipping biome with empty id");
                    continue;
                }
                if (_biomes.ContainsKey(def.Id))
                {
                    GameLogger.Error($"BiomeDatabase: duplicate biome id '{def.Id}'");
                    continue;
                }
                _biomes[def.Id] = def;
            }

            LoadProgress = 1f;
            IsLoaded = true;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
            OnLoadCompleted?.Invoke(DatabaseName, true);

            GameLogger.Info($"BiomeDatabase: '{DatabaseName}' loaded with {_biomes.Count} biomes");
        }
        catch (Exception ex)
        {
            IsLoaded = false;
            LoadProgress = 0f;
            OnLoadCompleted?.Invoke(DatabaseName, false);
            GameLogger.Error($"BiomeDatabase: '{DatabaseName}' failed to load: {ex.Message}");
            throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
        }
    }

    public void Unload()
    {
        _biomes.Clear();
        IsLoaded = false;
        LoadProgress = 0f;
        GameLogger.Info($"BiomeDatabase: '{DatabaseName}' unloaded");
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

    public BiomeDefinition? GetById(string id)
    {
        EnsureLoaded();
        return _biomes.TryGetValue(id, out var def) ? def : null;
    }

    public bool Exists(string id)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(id) && _biomes.ContainsKey(id);
    }

    public IReadOnlyCollection<BiomeDefinition> All
    {
        get
        {
            EnsureLoaded();
            return _biomes.Values;
        }
    }

    public IReadOnlyDictionary<string, BiomeDefinition> AsDictionary
    {
        get
        {
            EnsureLoaded();
            return _biomes;
        }
    }

    // ── Editor-facing mutation API ──────────────────────────────────────

    /// <summary>
    /// Adds a new biome to the in-memory registry. Throws if the id is empty or already exists.
    /// Persistence is the caller's responsibility (BiomeEditorYamlIO.WriteAll).
    /// </summary>
    public void Add(BiomeDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (string.IsNullOrEmpty(def.Id))
            throw new ArgumentException("BiomeDefinition.Id must be non-empty", nameof(def));
        if (_biomes.ContainsKey(def.Id))
            throw new InvalidOperationException($"Biome '{def.Id}' already exists");
        _biomes[def.Id] = def;
        IsLoaded = true;
    }

    public bool Remove(string id)
    {
        return _biomes.Remove(id);
    }

    /// <summary>
    /// Renames a biome's stable ID. References elsewhere (assigner rules, color overrides,
    /// resource configs) are NOT updated — that's the editor model's job.
    /// </summary>
    public void Rename(string oldId, string newId)
    {
        if (!_biomes.TryGetValue(oldId, out var def))
            throw new InvalidOperationException($"Biome '{oldId}' not found");
        if (_biomes.ContainsKey(newId))
            throw new InvalidOperationException($"Biome '{newId}' already exists");
        _biomes.Remove(oldId);
        def.Id = newId;
        _biomes[newId] = def;
    }

    /// <summary>
    /// Replaces the in-memory contents without touching disk. Used by the developer-tools
    /// Body Preview tab so designers can render unsaved edits.
    /// </summary>
    public void ReplaceInMemory(IEnumerable<BiomeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _biomes.Clear();
        foreach (var def in definitions)
        {
            if (!string.IsNullOrEmpty(def.Id))
                _biomes[def.Id] = def;
        }
        IsLoaded = true;
        LoadProgress = 1f;
    }
}
