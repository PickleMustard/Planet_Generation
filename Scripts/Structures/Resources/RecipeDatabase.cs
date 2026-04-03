using System;
using System.Collections.Generic;
using Godot;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using UI.Debug;
#endif

namespace Structures.Resources
{
#if DEBUG
    [DebugData("Recipes", Category = "Game")]
#endif
    public partial class RecipeDatabase : ILoadableDatabase
    {
        private static RecipeDatabase? _instance;
        private static readonly object _lock = new object();
        public static RecipeDatabase Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new RecipeDatabase();
                    }
                    return _instance;
                }
            }
        }

        private Dictionary<string, RecipeDefinition> _recipes = new();

        public string DatabaseName => "RecipeDatabase";
        public bool IsLoaded { get; private set; } = false;
        public float LoadProgress { get; private set; } = 0f;

        public event Action<string>? OnLoadStarted;
        public event Action<string, bool>? OnLoadCompleted;
        public event Action<string, float>? OnLoadProgressChanged;

        private RecipeDatabase()
        {
        }

        public void LoadData()
        {
            OnLoadStarted?.Invoke(DatabaseName);
            GD.Print($"RecipeDatabase: Starting load of '{DatabaseName}'");

            IsLoaded = false;
            LoadProgress = 0f;

            try
            {
                LoadProgress = 0.25f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                string basePath = "res://Configuration/Recipes/";
                if (!DirAccess.DirExistsAbsolute(basePath))
                {
                    throw new DatabaseLoadFailedException(
                        DatabaseName,
                        $"Recipe configuration directory not found: {basePath}"
                    );
                }

                var recipeFiles = GetYamlFilesRecursive(basePath);
                if (recipeFiles.Count == 0)
                {
                    GD.Print($"RecipeDatabase: No YAML files found in {basePath}");
                }

                LoadProgress = 0.5f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                _recipes.Clear();
                foreach (var filePath in recipeFiles)
                {
                    try
                    {
                        var definitions = RecipeConfigLoader.LoadRecipeDefinitions(filePath);

                        foreach (var definition in definitions)
                        {
                            if (string.IsNullOrEmpty(definition.RecipeId))
                            {
                                GD.PrintErr(
                                    $"Recipe definition missing 'recipe_id' field in {filePath}"
                                );
                                continue;
                            }

                            if (_recipes.ContainsKey(definition.RecipeId))
                            {
                                GD.PrintErr(
                                    $"Duplicate recipe_id '{definition.RecipeId}' in {filePath}"
                                );
                                continue;
                            }

                            _recipes[definition.RecipeId] = definition;
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr(
                            $"Error loading recipe definitions from {filePath}: {e.Message}"
                        );
                    }
                }

                LoadProgress = 0.75f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                if (_recipes.Count == 0)
                {
                    GD.Print(
                        $"RecipeDatabase: '{DatabaseName}' loaded but contains no recipe definitions"
                    );
                }

                LoadProgress = 1.0f;
                IsLoaded = true;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                OnLoadCompleted?.Invoke(DatabaseName, true);

                GD.Print(
                    $"RecipeDatabase: '{DatabaseName}' loaded successfully with {_recipes.Count} recipes"
                );
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                LoadProgress = 0f;
                OnLoadCompleted?.Invoke(DatabaseName, false);
                GD.PrintErr($"RecipeDatabase: '{DatabaseName}' failed to load: {ex.Message}");
                throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
            }
        }

        public void Unload()
        {
            _recipes.Clear();
            IsLoaded = false;
            LoadProgress = 0f;
            GD.Print($"RecipeDatabase: '{DatabaseName}' unloaded");
        }

        public WorkPackage CreateLoadPackage()
        {
            var builder = new WorkPackageBuilder()
                .WithName($"Load_{DatabaseName}")
                .WithPriority(TaskPriority.Normal)
                .AddStep(
                    $"Scan_Recipes_Directory",
                    () =>
                    {
                        LoadProgress = 0.25f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Parse_Recipe_YAML_Files",
                    () =>
                    {
                        LoadProgress = 0.5f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Process_Recipe_Definitions",
                    () =>
                    {
                        LoadProgress = 0.75f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Finalize_Recipe_Database",
                    () =>
                    {
                        LoadData();
                        return 0;
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

        private List<string> GetYamlFilesRecursive(string directory)
        {
            var files = new List<string>();

            if (!DirAccess.DirExistsAbsolute(directory))
                return files;

            var currentFiles = DirAccess.GetFilesAt(directory);
            foreach (var file in currentFiles)
            {
                if (file.EndsWith(".yaml") || file.EndsWith(".yml"))
                {
                    files.Add(directory + file);
                }
            }

            var subdirs = DirAccess.GetDirectoriesAt(directory);
            foreach (var subdir in subdirs)
            {
                files.AddRange(GetYamlFilesRecursive(directory + subdir + "/"));
            }

            return files;
        }

        public bool TryGetRecipe(string recipeId, out RecipeDefinition? recipe)
        {
            EnsureLoaded();
            return _recipes.TryGetValue(recipeId, out recipe);
        }

        public IReadOnlyDictionary<string, RecipeDefinition> GetAllRecipes()
        {
            EnsureLoaded();
            return _recipes;
        }

        public List<RecipeDefinition> GetRecipesByCategory(string category)
        {
            EnsureLoaded();
            var result = new List<RecipeDefinition>();

            foreach (var recipe in _recipes.Values)
            {
                if (recipe.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true)
                {
                    result.Add(recipe);
                }
            }

            return result;
        }

        public bool ValidateRecipeExists(string recipeId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(recipeId) && _recipes.ContainsKey(recipeId);
        }
    }
}
