using System;
using System.Collections.Generic;
using Godot;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using Debug;
#endif

namespace Structures.Resources
{
#if DEBUG
    [DebugData("BuildingShape2D", Category = "Game")]
#endif
    public partial class BuildingShape2DDatabase : ILoadableDatabase
    {
        private static BuildingShape2DDatabase? _instance;
        private static readonly object _lock = new();
        public static BuildingShape2DDatabase Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new BuildingShape2DDatabase();
                    return _instance;
                }
            }
        }

        private readonly Dictionary<string, BuildingShape2D> _shapes = new();

        public string DatabaseName => "BuildingShape2DDatabase";
        public bool IsLoaded { get; private set; } = false;
        public float LoadProgress { get; private set; } = 0f;

        public event Action<string>? OnLoadStarted;
        public event Action<string, bool>? OnLoadCompleted;
        public event Action<string, float>? OnLoadProgressChanged;

        private BuildingShape2DDatabase() { }

        public void LoadData()
        {
            OnLoadStarted?.Invoke(DatabaseName);
            IsLoaded = false;
            LoadProgress = 0f;

            try
            {
                string basePath = "res://Configuration/Building2D/";
                if (!DirAccess.DirExistsAbsolute(basePath))
                {
                    GameLogger.Warning($"BuildingShape2DDatabase: directory not found: {basePath}");
                    IsLoaded = true;
                    LoadProgress = 1f;
                    OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                    OnLoadCompleted?.Invoke(DatabaseName, true);
                    return;
                }

                LoadProgress = 0.25f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                var files = GetYamlFilesRecursive(basePath);

                _shapes.Clear();
                foreach (var filePath in files)
                {
                    var shape = BuildingShape2DLoader.LoadShape(filePath);
                    if (shape == null) continue;
                    if (string.IsNullOrEmpty(shape.Id))
                    {
                        GD.PrintErr($"BuildingShape2DDatabase: shape in {filePath} missing 'id'");
                        continue;
                    }
                    if (_shapes.ContainsKey(shape.Id))
                    {
                        GD.PrintErr($"BuildingShape2DDatabase: duplicate shape id '{shape.Id}' in {filePath}");
                        continue;
                    }
                    _shapes[shape.Id] = shape;
                }

                LoadProgress = 1f;
                IsLoaded = true;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                OnLoadCompleted?.Invoke(DatabaseName, true);

                GameLogger.Info($"BuildingShape2DDatabase: loaded {_shapes.Count} shape(s)");
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                LoadProgress = 0f;
                OnLoadCompleted?.Invoke(DatabaseName, false);
                GD.PrintErr($"BuildingShape2DDatabase: load failed: {ex.Message}");
                throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
            }
        }

        public void Unload()
        {
            _shapes.Clear();
            IsLoaded = false;
            LoadProgress = 0f;
        }

        public WorkPackage CreateLoadPackage()
        {
            return new WorkPackageBuilder()
                .WithName($"Load_{DatabaseName}")
                .WithPriority(TaskPriority.Normal)
                .AddStep($"Load_{DatabaseName}", () => LoadData())
                .Build();
        }

        public BuildingShape2D? Get(string id)
        {
            EnsureLoadedLazy();
            if (string.IsNullOrEmpty(id)) return null;
            return _shapes.TryGetValue(id, out var shape) ? shape : null;
        }

        public bool TryGet(string id, out BuildingShape2D? shape)
        {
            EnsureLoadedLazy();
            shape = null;
            if (string.IsNullOrEmpty(id)) return false;
            return _shapes.TryGetValue(id, out shape);
        }

        public IReadOnlyDictionary<string, BuildingShape2D> All
        {
            get { EnsureLoadedLazy(); return _shapes; }
        }

        private void EnsureLoadedLazy()
        {
            if (IsLoaded) return;
            lock (_lock)
            {
                if (IsLoaded) return;
                try { LoadData(); }
                catch (Exception ex)
                {
                    GameLogger.Error($"BuildingShape2DDatabase: lazy load failed: {ex.Message}");
                }
            }
        }

        private static List<string> GetYamlFilesRecursive(string directory)
        {
            var files = new List<string>();
            if (!DirAccess.DirExistsAbsolute(directory)) return files;

            foreach (var file in DirAccess.GetFilesAt(directory))
            {
                if (file.EndsWith(".yaml") || file.EndsWith(".yml"))
                    files.Add(directory + file);
            }

            foreach (var subdir in DirAccess.GetDirectoriesAt(directory))
                files.AddRange(GetYamlFilesRecursive(directory + subdir + "/"));

            return files;
        }
    }
}
