#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Debug.DatabaseViewer;

namespace Structures.Resources
{
    public partial class BuildingDatabase : IDebugDataProvider
    {
        string IDataProvider.Name => "Buildings";
        string IDataProvider.Category => "Game";
        bool IDataProvider.NeedsRefresh => true;
        object IDebugDataProvider.SourceObject => this;
        string IDebugDataProvider.InstanceNamespace => "BuildingDatabase";
        bool IDebugDataProvider.IsSourceValid => IsLoaded;

        DebugDataNode IDataProvider.GetData()
        {
            if (!IsLoaded)
            {
                var unloadedNode = new DebugDataNode("Buildings");
                unloadedNode.AddProperty("Status", "Not loaded");
                unloadedNode.AddProperty("Load Progress", $"{LoadProgress:P0}");
                return unloadedNode;
            }

            var node = new DebugDataNode("Buildings").AddProperty(
                "Total Definitions",
                _buildings.Count
            );

            var categoriesNode = node.AddChild("By Category");
            var categories = new Dictionary<string, List<BuildingDefinition>>();

            foreach (var building in _buildings.Values)
            {
                var category = building.Category ?? "Uncategorized";
                if (!categories.ContainsKey(category))
                {
                    categories[category] = new List<BuildingDefinition>();
                }
                categories[category].Add(building);
            }

            foreach (var category in categories)
            {
                var categoryNode = categoriesNode.AddChild(category.Key)
                    .AddProperty("Count", category.Value.Count);

                foreach (var building in category.Value)
                {
                    var buildingNode = categoryNode.AddChild(building.IdName ?? "Unknown")
                        .AddProperty("Display Name", building.DisplayName ?? "")
                        .AddProperty("Description", building.Description ?? "")
                        .AddProperty("Max Resource Tier", building.MaxResourceTier)
                        .AddProperty("Work Required", building.WorkRequired);

                    var placementNode = buildingNode.AddChild("Placement");
                    placementNode
                        .AddProperty("Min Elevation", building.Placement.MinElevation)
                        .AddProperty("Max Elevation", building.Placement.MaxElevation)
                        .AddProperty("Max Slope", building.Placement.MaxSlope)
                        .AddProperty("Cell Count", building.Placement.CellCount)
                        .AddProperty("Allow Any Biome", building.Placement.AllowAnyBiome);

                    var biomesNode = placementNode.AddChild("Placeable Biomes");
                    if (building.Placement.AllowAnyBiome)
                    {
                        biomesNode.AddProperty("Any", "All biomes allowed");
                    }
                    else if (building.Placement.Biomes.Count == 0)
                    {
                        biomesNode.AddProperty("None", "No specific biomes (may use default)");
                    }
                    else
                    {
                        biomesNode.AddProperty("Count", building.Placement.Biomes.Count);
                        foreach (var biome in building.Placement.Biomes)
                        {
                            biomesNode.AddProperty(biome.ToString(), "✓");
                        }
                    }

                    var resourcesNode = buildingNode.AddChild("Required Resources");
                    foreach (var resource in building.RequiredResources)
                    {
                        resourcesNode.AddProperty(resource.Key, resource.Value);
                    }

                    var productionNode = buildingNode.AddChild("Production");
                    var mfgEntry = building.BehaviorEntries
                        .FirstOrDefault(e => e.BehaviorId == "ManufacturingBehavior");
                    var mfgDefault = mfgEntry != null
                        ? Constructables.Buildings.BehaviorConfigHelper.ReadString(mfgEntry.Config, "default_recipe", "") ?? ""
                        : "";
                    var mfgAlts = mfgEntry != null
                        ? Constructables.Buildings.BehaviorConfigHelper.ReadStringList(mfgEntry.Config, "alternative_recipes")
                        : new List<string>();
                    var mfgSpeed = mfgEntry != null
                        ? Constructables.Buildings.BehaviorConfigHelper.ReadFloat(mfgEntry.Config, "production_speed", 1.0f)
                        : 1.0f;
                    var storageEntry = building.BehaviorEntries
                        .FirstOrDefault(e => e.BehaviorId == "StorageHubBehavior");
                    var storageCap = storageEntry != null
                        ? Constructables.Buildings.BehaviorConfigHelper.ReadInt(storageEntry.Config, "storage_capacity", 0)
                        : 0;
                    productionNode
                        .AddProperty("Default Recipe", mfgDefault)
                        .AddProperty("Alternative Recipes", mfgAlts.Count)
                        .AddProperty("Storage Capacity (slots)", storageCap)
                        .AddProperty("Production Speed", mfgSpeed);

                    var visualNode = buildingNode.AddChild("Visual");
                    visualNode
                        .AddProperty("Model Path", building.Visual.ModelPath ?? "")
                        .AddProperty("Scale", building.Visual.Scale)
                        .AddProperty("Rotation Offset", building.Visual.RotationOffset.ToString());
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
            foreach (var kvp in _buildings)
            {
                if (
                    kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                    || (kvp.Value.DisplayName?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (kvp.Value.Category?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false)
                )
                {
                    results.Add($"By Category/{kvp.Value.Category ?? "Uncategorized"}/{kvp.Key}");
                }
            }
            return results;
        }
    }
}
#endif
