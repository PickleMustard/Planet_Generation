using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using UtilityLibrary.TaskSystem;

namespace UtilityLibrary.DataLoading
{
    /// <summary>
    /// Simple test database for verifying the ILoadableDatabase implementation.
    /// </summary>
    public partial class TestDatabase : ILoadableDatabase
    {
        public string DatabaseName { get; }
        public bool IsLoaded { get; private set; }
        public float LoadProgress { get; private set; }
        public IReadOnlyList<string> Dependencies { get; }

        public event Action<string>? OnLoadStarted;
        public event Action<string, bool>? OnLoadCompleted;
        public event Action<string, float>? OnLoadProgressChanged;

        private readonly float _loadDurationMs;
        private readonly bool _shouldFail;

        public TestDatabase(string name, float loadDurationMs = 1000f, bool shouldFail = false, string[]? dependencies = null)
        {
            DatabaseName = name ?? throw new ArgumentNullException(nameof(name));
            _loadDurationMs = loadDurationMs;
            _shouldFail = shouldFail;
            Dependencies = dependencies ?? Array.Empty<string>();
            IsLoaded = false;
            LoadProgress = 0f;
        }

        public void LoadData()
        {
            LoadAsync().GetAwaiter().GetResult();
        }

        public async Task LoadAsync()
        {
            OnLoadStarted?.Invoke(DatabaseName);
            GD.Print($"TestDatabase: Starting load of '{DatabaseName}'");

            IsLoaded = false;
            LoadProgress = 0f;

            try
            {
                // Simulate loading with progress updates
                int totalSteps = 10;
                for (int i = 1; i <= totalSteps; i++)
                {
                    await Task.Delay((int)(_loadDurationMs / totalSteps));
                    LoadProgress = (float)i / totalSteps;
                    OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                }

                if (_shouldFail)
                {
                    throw new Exception("Simulated database load failure");
                }

                IsLoaded = true;
                LoadProgress = 1.0f;
                OnLoadCompleted?.Invoke(DatabaseName, true);
                GD.Print($"TestDatabase: '{DatabaseName}' loaded successfully");
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                LoadProgress = 0f;
                OnLoadCompleted?.Invoke(DatabaseName, false);
                GD.PrintErr($"TestDatabase: '{DatabaseName}' failed to load: {ex.Message}");
                throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
            }
        }

        public void Unload()
        {
            IsLoaded = false;
            LoadProgress = 0f;
            GD.Print($"TestDatabase: '{DatabaseName}' unloaded");
        }

        public WorkPackage CreateLoadPackage()
        {
            var builder = new WorkPackageBuilder()
                .WithName($"Load_{DatabaseName}")
                .AddStep($"Load_Database_{DatabaseName}", () => LoadData());

            return builder.Build();
        }

        public void SimulateDataAccess()
        {
            if (!IsLoaded)
            {
                throw new DatabaseNotLoadedException(DatabaseName);
            }

            GD.Print($"TestDatabase: Accessing data from '{DatabaseName}'");
        }
    }
}