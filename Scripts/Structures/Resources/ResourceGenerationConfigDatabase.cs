using System;
using System.Collections.Generic;
using Godot;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using Debug;
#endif

namespace Structures.Resources;

/// <summary>
/// Singleton database that loads and provides access to resource generation configurations
/// for planetary types and biomes used in resource generation.
/// </summary>
#if DEBUG
[DebugData("ResourceGenConfig", Category = "Game")]
#endif
public partial class ResourceGenerationConfigDatabase : ILoadableDatabase
{
    private static ResourceGenerationConfigDatabase? _instance;
    private static readonly object _lock = new object();
    public static ResourceGenerationConfigDatabase Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new ResourceGenerationConfigDatabase();
                return _instance;
            }
        }
    }

    private PlanetaryResourceConfig? _planetaryResourceConfig;
    private BiomeResourceConfig? _biomeResourceConfig;

    public string DatabaseName => "ResourceGenerationConfigDatabase";
    public bool IsLoaded { get; private set; }
    public float LoadProgress { get; private set; }

    public IReadOnlyList<string> Dependencies => new[] { "ResourceDatabase" };

    public event Action<string>? OnLoadStarted;
    public event Action<string, bool>? OnLoadCompleted;
    public event Action<string, float>? OnLoadProgressChanged;

    public PlanetaryResourceConfig PlanetaryResources
    {
        get
        {
            EnsureLoaded();
            return _planetaryResourceConfig!;
        }
    }

    public BiomeResourceConfig BiomeResources
    {
        get
        {
            EnsureLoaded();
            return _biomeResourceConfig!;
        }
    }

    private ResourceGenerationConfigDatabase() { }

    public void LoadData()
    {
        OnLoadStarted?.Invoke(DatabaseName);
        GameLogger.Info($"ResourceGenerationConfigDatabase: Starting load of '{DatabaseName}'");

        IsLoaded = false;
        LoadProgress = 0f;

        try
        {
            // Step 1: Load resource groups
            LoadProgress = 0.1f;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

            var groups = ResourceConfigLoader.LoadResourceGroups();
            if (groups == null)
            {
                throw new InvalidOperationException("Failed to load resource groups");
            }

            // Step 2: Load and build planetary resource config
            LoadProgress = 0.3f;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

            _planetaryResourceConfig = ResourceConfigLoader.LoadPlanetaryResourceConfig();
            if (_planetaryResourceConfig == null)
            {
                throw new InvalidOperationException("Failed to load planetary resource configuration");
            }

            // Assign groups and validate
            _planetaryResourceConfig.ResourceGroups = groups;

            LoadProgress = 0.5f;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

            if (!_planetaryResourceConfig.Validate())
            {
                throw new InvalidOperationException("Planetary resource configuration failed validation");
            }

            // Step 3: Load and build biome resource config
            LoadProgress = 0.7f;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

            _biomeResourceConfig = ResourceConfigLoader.LoadBiomeResourceConfig();
            if (_biomeResourceConfig == null)
            {
                throw new InvalidOperationException("Failed to load biome resource configuration");
            }

            if (!_biomeResourceConfig.Validate())
            {
                throw new InvalidOperationException("Biome resource configuration failed validation");
            }

            // Done
            LoadProgress = 1.0f;
            IsLoaded = true;
            OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
            OnLoadCompleted?.Invoke(DatabaseName, true);

            GameLogger.Info($"ResourceGenerationConfigDatabase: '{DatabaseName}' loaded successfully");
        }
        catch (Exception ex)
        {
            IsLoaded = false;
            LoadProgress = 0f;
            OnLoadCompleted?.Invoke(DatabaseName, false);
            GameLogger.Error($"ResourceGenerationConfigDatabase: '{DatabaseName}' failed to load: {ex.Message}");
            throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
        }
    }

    public void Unload()
    {
        _planetaryResourceConfig = null;
        _biomeResourceConfig = null;
        IsLoaded = false;
        LoadProgress = 0f;
        GameLogger.Info($"ResourceGenerationConfigDatabase: '{DatabaseName}' unloaded");
    }

    public WorkPackage CreateLoadPackage()
    {
        var builder = new WorkPackageBuilder()
            .WithName($"Load_{DatabaseName}")
            .WithPriority(TaskPriority.Normal)
            .AddStep(
                $"Load_Database_{DatabaseName}",
                () =>
                {
                    LoadData();
                }
            );

        return builder.Build();
    }

    private void EnsureLoaded()
    {
        if (!IsLoaded)
        {
            throw new DatabaseNotLoadedException(DatabaseName);
        }
    }
}
