using System;
using System.Collections.Generic;
using Godot;
using Structures.Resources;
using UtilityLibrary;
#if DEBUG
using UI.Debug;
using UI.Debug.DatabaseViewer;
#endif

namespace Structures.Resources
{
#if DEBUG
    [DebugData("Resources", Category = "Game")]
#endif
    public partial class ResourceDatabase : Node
#if DEBUG
        , IDebugDataProvider
#endif
    {
        private static ResourceDatabase _instance;
        public static ResourceDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    GD.PrintErr("ResourceDatabase not initialized. Ensure it is registered as an autoload.");
                }
                return _instance;
            }
        }

        private Dictionary<string, ResourceDefinition> _resources = new();
        private bool _isInitialized = false;

        public override void _Ready()
        {
            if (_instance == null)
            {
                _instance = this;
                LoadResources();
                _isInitialized = true;
                GD.Print($"ResourceDatabase initialized with {_resources.Count} resources");
            }
            else
            {
                GD.PrintErr("ResourceDatabase already initialized. Duplicate instance detected.");
            }
        }

        private void LoadResources()
        {
            string configPath = "res://Configuration/ResourceDefinition/ResourceDefinition.yaml";
            var definitions = ResourceConfigLoader.LoadResourceDefinitions(configPath);

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
        }

        public bool TryGetResource(string resourceId, out ResourceDefinition resource)
        {
            return _resources.TryGetValue(resourceId, out resource);
        }

        public IReadOnlyDictionary<string, ResourceDefinition> GetAllResources()
        {
            return _resources;
        }

        public Color GetResourceColor(string resourceId)
        {
            if (TryGetResource(resourceId, out var resource))
            {
                return resource.DisplayColor;
            }
            return Colors.White;
        }

        public bool ValidateResourceExists(string resourceId)
        {
            return !string.IsNullOrEmpty(resourceId) && _resources.ContainsKey(resourceId);
        }

        public void ValidateAllBodyConfigResources(string bodyConfigName, IEnumerable<string> resourceIds)
        {
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

        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
                _isInitialized = false;
            }
            base._ExitTree();
        }

#if DEBUG
        string IDataProvider.Name => "Resources";
        string IDataProvider.Category => "Game";
        bool IDataProvider.NeedsRefresh => false;
        object IDebugDataProvider.SourceObject => this;
        string IDebugDataProvider.InstanceNamespace => "ResourceDatabase";
        bool IDebugDataProvider.IsSourceValid => _isInitialized && IsInstanceValid(this);

        DebugDataNode IDataProvider.GetData()
        {
            var node = new DebugDataNode("Resources")
                .AddProperty("Total Definitions", _resources.Count);

            var definitionsNode = node.AddChild("Definitions");
            foreach (var kvp in _resources)
            {
                var def = kvp.Value;
                definitionsNode.AddChild(def.IdName)
                    .AddProperty("Resource Type", def.ResourceType)
                    .AddProperty("Resource Tier", def.ResourceTier)
                    .AddProperty("Color", def.DisplayColor.ToString())
                    .AddProperty("Min Elevation", def.MinElevation)
                    .AddProperty("Max Elevation", def.MaxElevation)
                    .AddProperty("Biome Affinities", def.BiomeAffinity.Count);
            }

            return node;
        }

        void IDataProvider.Refresh()
        {
        }

        IEnumerable<string> IDataProvider.Search(string pattern)
        {
            var results = new List<string>();
            foreach (var kvp in _resources)
            {
                if (kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Value.ResourceType.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add($"Definitions/{kvp.Key}");
                }
            }
            return results;
        }
#endif
    }
}
