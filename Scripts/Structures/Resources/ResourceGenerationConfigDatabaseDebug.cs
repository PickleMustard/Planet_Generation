#if DEBUG
using System;
using System.Collections.Generic;
using Debug.DatabaseViewer;

namespace Structures.Resources
{
    public partial class ResourceGenerationConfigDatabase : IDebugDataProvider
    {
        string IDataProvider.Name => "ResourceGenConfig";
        string IDataProvider.Category => "Game";
        bool IDataProvider.NeedsRefresh => true;
        object IDebugDataProvider.SourceObject => this;
        string IDebugDataProvider.InstanceNamespace => "ResourceGenerationConfigDatabase";
        bool IDebugDataProvider.IsSourceValid => IsLoaded;

        DebugDataNode IDataProvider.GetData()
        {
            if (!IsLoaded)
            {
                var unloadedNode = new DebugDataNode("ResourceGenConfig");
                unloadedNode.AddProperty("Status", "Not loaded");
                unloadedNode.AddProperty("Load Progress", $"{LoadProgress:P0}");
                return unloadedNode;
            }

            var node = new DebugDataNode("ResourceGenConfig");

            // Planetary Resources Section
            var planetaryNode = node.AddChild("Planetary Resources");
            if (_planetaryResourceConfig != null)
            {
                var resourceGroupsNode = planetaryNode.AddChild("Resource Groups");
                foreach (var group in _planetaryResourceConfig.ResourceGroups)
                {
                    var groupNode = resourceGroupsNode.AddChild(group.Key)
                        .AddProperty("Resource Count", group.Value.Count);

                    var resourcesNode = groupNode.AddChild("Resources");
                    foreach (var resourceId in group.Value)
                    {
                        resourcesNode.AddProperty(resourceId, "✓");
                    }
                }

                var rockyPlanetNode = planetaryNode.AddChild("Rocky Planet Subtypes");
                foreach (var subtype in _planetaryResourceConfig.RockyPlanetSubtypes)
                {
                    var subtypeNode = rockyPlanetNode.AddChild(subtype.Key.ToString())
                        .AddProperty("Base Resource Weight", subtype.Value.BaseResourceWeight);

                    if (subtype.Value.ResolvedResources != null && subtype.Value.ResolvedResources.Count > 0)
                    {
                        var resourcesNode = subtypeNode.AddChild("Resolved Resources");
                        foreach (var resource in subtype.Value.ResolvedResources)
                        {
                            resourcesNode.AddProperty(resource, "✓");
                        }
                    }
                }

                var gasGiantNode = planetaryNode.AddChild("Gas Giant Subtypes");
                foreach (var subtype in _planetaryResourceConfig.GasGiantSubtypes)
                {
                    var subtypeNode = gasGiantNode.AddChild(subtype.Key.ToString())
                        .AddProperty("Base Resource Weight", subtype.Value.BaseResourceWeight);

                    if (subtype.Value.ResolvedResources != null && subtype.Value.ResolvedResources.Count > 0)
                    {
                        var resourcesNode = subtypeNode.AddChild("Resolved Resources");
                        foreach (var resource in subtype.Value.ResolvedResources)
                        {
                            resourcesNode.AddProperty(resource, "✓");
                        }
                    }
                }
            }

            // Biome Resources Section
            var biomeNode = node.AddChild("Biome Resources");
            if (_biomeResourceConfig != null)
            {
                foreach (var biomeEntry in _biomeResourceConfig.Biomes)
                {
                    var biomeTypeNode = biomeNode.AddChild(biomeEntry.Key.ToString());

                    if (biomeEntry.Value.ResourceWeightModifiers != null && biomeEntry.Value.ResourceWeightModifiers.Count > 0)
                    {
                        var weightsNode = biomeTypeNode.AddChild("Resource Weight Modifiers");
                        foreach (var modifier in biomeEntry.Value.ResourceWeightModifiers)
                        {
                            weightsNode.AddProperty(modifier.Key, modifier.Value);
                        }
                    }
                }
            }

            return node;
        }

        void IDataProvider.Refresh() { }

        IEnumerable<string> IDataProvider.Search(string pattern)
        {
            if (!IsLoaded)
            {
                return Array.Empty<string>();
            }

            var results = new List<string>();

            // Search planet types
            if (_planetaryResourceConfig != null)
            {
                foreach (var subtype in _planetaryResourceConfig.RockyPlanetSubtypes)
                {
                    if (subtype.Key.ToString().Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add($"Planetary Resources/Rocky Planet Subtypes/{subtype.Key}");
                    }
                }
            }

            // Search biomes
            if (_biomeResourceConfig != null)
            {
                foreach (var biome in _biomeResourceConfig.Biomes)
                {
                    if (biome.Key.ToString().Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add($"Biome Resources/{biome.Key}");
                    }
                }
            }

            return results;
        }
    }
}
#endif
