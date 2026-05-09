using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Structures.Enums;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using UI.Debug;
#endif

namespace Structures.Resources
{
#if DEBUG
    [DebugData("IngameResources", Category = "Game")]
#endif
    public partial class ResourceDatabase : ILoadableDatabase
    {
        private static ResourceDatabase? _instance;
        private static readonly object _lock = new object();
        public static ResourceDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ResourceDatabase();
                }
                return _instance;
            }
        }

        private Dictionary<string, ResourceDefinition> _resources = new();

        public string DatabaseName => "ResourceDatabase";
        public bool IsLoaded { get; private set; } = false;
        public float LoadProgress { get; private set; } = 0f;

        public event Action<string>? OnLoadStarted;
        public event Action<string, bool>? OnLoadCompleted;
        public event Action<string, float>? OnLoadProgressChanged;

        private ResourceDatabase()
        {
            // Private constructor for singleton pattern
            // Database must be loaded via LoadAsync() or CreateLoadPackage()
        }

        public void LoadData()
        {
            OnLoadStarted?.Invoke(DatabaseName);
            GD.Print($"ResourceDatabase: Starting load of '{DatabaseName}'");

            IsLoaded = false;
            LoadProgress = 0f;

            try
            {
                // Step 1: Parse YAML configuration from category files
                LoadProgress = 0.3f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                string categoriesPath =
                    "res://Configuration/ResourceDefinition/categories";
                var definitions = ResourceConfigLoader.LoadResourceDefinitionsFromCategories(categoriesPath);

                // Step 2: Process definitions and populate dictionary
                LoadProgress = 0.6f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                _resources.Clear();
                foreach (var definition in definitions)
                {
                    if (string.IsNullOrEmpty(definition.IdName))
                    {
                        GD.PrintErr("Resource definition missing 'id_name' field");
                        continue;
                    }

                    if (_resources.ContainsKey(definition.IdName))
                    {
                        throw ResourceValidationError.DuplicateResource(definition.IdName);
                    }

                    _resources[definition.IdName] = definition;
                }

                // Step 3: Finalize database
                LoadProgress = 1.0f;
                IsLoaded = true;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                OnLoadCompleted?.Invoke(DatabaseName, true);

                GD.Print(
                    $"ResourceDatabase: '{DatabaseName}' loaded successfully with {_resources.Count} resources"
                );
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                LoadProgress = 0f;
                OnLoadCompleted?.Invoke(DatabaseName, false);
                GD.PrintErr($"ResourceDatabase: '{DatabaseName}' failed to load: {ex.Message}");
                throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
            }
        }

        public void Unload()
        {
            _resources.Clear();
            IsLoaded = false;
            LoadProgress = 0f;
            GD.Print($"ResourceDatabase: '{DatabaseName}' unloaded");
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

        public bool TryGetResource(string resourceId, out ResourceDefinition? resource)
        {
            EnsureLoaded();
            return _resources.TryGetValue(resourceId, out resource);
        }

        public IReadOnlyDictionary<string, ResourceDefinition> GetAllResources()
        {
            EnsureLoaded();
            return _resources;
        }

        /// <summary>
        /// Gets all resources that can generate naturally on celestial bodies.
        /// </summary>
        public IReadOnlyDictionary<string, ResourceDefinition> GetGeneratableResources()
        {
            EnsureLoaded();
            return _resources
                .Where(kvp => kvp.Value.IsGeneratable)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Checks if a resource can generate naturally on celestial bodies.
        /// </summary>
        public bool IsResourceGeneratable(string resourceId)
        {
            EnsureLoaded();
            if (TryGetResource(resourceId, out var resource) && resource != null)
            {
                return resource.IsGeneratable;
            }
            return false;
        }

        public Color GetResourceColor(string resourceId)
        {
            EnsureLoaded();
            if (TryGetResource(resourceId, out var resource) && resource != null)
            {
                return resource.DisplayColor;
            }
            return Colors.White;
        }

        public bool ValidateResourceExists(string resourceId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(resourceId) && _resources.ContainsKey(resourceId);
        }

        /// <summary>
        /// Minimum similarity threshold (0-100) for fuzzy matching to consider a match valid.
        /// </summary>
        public const float DefaultSimilarityThreshold = 80f;

        /// <summary>
        /// Computes the Levenshtein distance between two strings.
        /// Measures the minimum number of single-character edits required to change one string into another.
        /// </summary>
        private static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return string.IsNullOrEmpty(target) ? 0 : target.Length;
            if (string.IsNullOrEmpty(target))
                return source.Length;

            int sourceLength = source.Length;
            int targetLength = target.Length;
            var distance = new int[sourceLength + 1, targetLength + 1];

            for (int i = 0; i <= sourceLength; i++)
                distance[i, 0] = i;
            for (int j = 0; j <= targetLength; j++)
                distance[0, j] = j;

            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    int cost = target[j - 1] == source[i - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[sourceLength, targetLength];
        }

        /// <summary>
        /// Computes the similarity between two strings as a percentage (0-100).
        /// 100 means identical, 0 means completely different.
        /// Uses Levenshtein distance, case-insensitive.
        /// </summary>
        private static float ComputeSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target))
                return 100f;
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0f;

            string sourceLower = source.ToLowerInvariant();
            string targetLower = target.ToLowerInvariant();

            // Exact match
            if (sourceLower == targetLower)
                return 100f;

            int distance = LevenshteinDistance(sourceLower, targetLower);
            int maxLength = Math.Max(sourceLower.Length, targetLower.Length);
            if (maxLength == 0)
                return 100f;

            return (1f - (float)distance / maxLength) * 100f;
        }

        /// <summary>
        /// Gets a resource by ID with fuzzy matching support.
        /// If the exact resource ID is found, returns it directly.
        /// If not found, searches for the best matching resource using Levenshtein similarity
        /// and returns that resource if the similarity is greater than or equal to the threshold.
        /// </summary>
        /// <param name="resourceId">The resource ID to look up (may be misspelled).</param>
        /// <param name="similarityThreshold">Minimum similarity percentage (0-100) required for a fuzzy match. Default: 80.</param>
        /// <returns>The matched ResourceDefinition.</returns>
        /// <exception cref="ResourceValidationError">Thrown when no match above threshold is found.</exception>
        public ResourceDefinition GetResourceWithFuzzyMatch(string resourceId, float similarityThreshold = DefaultSimilarityThreshold)
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(resourceId))
            {
                throw ResourceValidationError.ResourceNotFound(resourceId ?? "null", "resource lookup");
            }

            // Try exact match first
            if (_resources.TryGetValue(resourceId, out var exactMatch))
            {
                return exactMatch;
            }

            // Case-insensitive exact match
            var caseInsensitiveKey = _resources.Keys.FirstOrDefault(k => k.Equals(resourceId, StringComparison.OrdinalIgnoreCase));
            if (caseInsensitiveKey != null)
            {
                return _resources[caseInsensitiveKey];
            }

            // Fuzzy search: find best matching resource
            string? bestMatch = null;
            float bestSimilarity = 0f;

            foreach (var knownResourceId in _resources.Keys)
            {
                float similarity = ComputeSimilarity(resourceId, knownResourceId);
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestMatch = knownResourceId;
                }
            }

            if (bestMatch != null && bestSimilarity >= similarityThreshold)
            {
                GD.Print($"ResourceDatabase: Fuzzy match '{resourceId}' -> '{bestMatch}' ({bestSimilarity:F1}% similar)");
                return _resources[bestMatch];
            }

            // No match found - build suggestion if we have any candidates
            string suggestion = "";
            if (bestMatch != null)
            {
                suggestion = $" Did you mean '{bestMatch}' ({bestSimilarity:F1}% similar)?";
            }

            throw ResourceValidationError.ResourceNotFoundWithSuggestion(
                resourceId,
                "resource lookup",
                suggestion);
        }

        /// <summary>
        /// Tries to get a resource with fuzzy matching support.
        /// Returns true if a match was found (exact or fuzzy above threshold), false otherwise.
        /// </summary>
        public bool TryGetResourceWithFuzzyMatch(string resourceId, out ResourceDefinition? resource, float similarityThreshold = DefaultSimilarityThreshold)
        {
            try
            {
                resource = GetResourceWithFuzzyMatch(resourceId, similarityThreshold);
                return true;
            }
            catch (ResourceValidationError)
            {
                resource = null;
                return false;
            }
        }

        public void ValidateAllBodyConfigResources(
            string bodyConfigName,
            IEnumerable<string> resourceIds
        )
        {
            EnsureLoaded();
            if (resourceIds == null)
            {
                return;
            }

            foreach (var resourceId in resourceIds)
            {
                if (!string.IsNullOrEmpty(resourceId) && !_resources.ContainsKey(resourceId))
                {
                    throw ResourceValidationError.ResourceNotFound(resourceId, bodyConfigName);
                }
            }
        }

        /// <summary>
        /// Gets the icon for a resource at a specific size.
        /// Always returns a valid texture (uses fallback if needed).
        /// </summary>
        public Texture2D GetResourceIcon(string resourceId, IconSize size = IconSize.Medium)
        {
            EnsureLoaded();
            if (TryGetResource(resourceId, out var resource) && resource != null)
            {
                var texture = resource.Icon?.GetTexture(size);
                if (texture != null)
                    return texture;
            }

            return IconDataLoader.GetFallbackIcon(size);
        }

        /// <summary>
        /// Gets the icon tint for a resource.
        /// </summary>
        public Color GetResourceIconTint(string resourceId)
        {
            EnsureLoaded();
            if (TryGetResource(resourceId, out var resource) && resource != null)
            {
                return resource.GetEffectiveIconTint();
            }
            return Colors.White;
        }

        /// <summary>
        /// Gets the full IconDefinition for a resource.
        /// </summary>
        public IconDefinition? GetResourceIconDefinition(string resourceId)
        {
            EnsureLoaded();
            if (TryGetResource(resourceId, out var resource) && resource != null)
            {
                return resource.Icon;
            }
            return null;
        }
    }
}
