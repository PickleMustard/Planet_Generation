#if DEBUG
using System;
using System.Collections.Generic;
using Structures.Logistics;
using Debug.DatabaseViewer;

namespace Logistics.Resources
{
    public partial class StationDatabase : IDebugDataProvider
    {
        string IDataProvider.Name => "Stations";
        string IDataProvider.Category => "Game";
        bool IDataProvider.NeedsRefresh => true;
        object IDebugDataProvider.SourceObject => this;
        string IDebugDataProvider.InstanceNamespace => "StationDatabase";
        bool IDebugDataProvider.IsSourceValid => IsLoaded;

        DebugDataNode IDataProvider.GetData()
        {
            if (!IsLoaded)
            {
                var unloadedNode = new DebugDataNode("Stations");
                unloadedNode.AddProperty("Status", "Not loaded");
                unloadedNode.AddProperty("Load Progress", $"{LoadProgress:P0}");
                return unloadedNode;
            }

            var node = new DebugDataNode("Stations").AddProperty(
                "Total Definitions",
                _stations.Count
            );

            var categoriesNode = node.AddChild("By Type");
            var categories = new Dictionary<string, List<StationDefinition>>();

            foreach (var station in _stations.Values)
            {
                var category = station.StationType ?? "Uncategorized";
                if (!categories.ContainsKey(category))
                {
                    categories[category] = new List<StationDefinition>();
                }
                categories[category].Add(station);
            }

            foreach (var category in categories)
            {
                var categoryNode = categoriesNode.AddChild(category.Key)
                    .AddProperty("Count", category.Value.Count);

                foreach (var station in category.Value)
                {
                    var stationNode = categoryNode.AddChild(station.Name ?? "Unknown")
                        .AddProperty("Station Type", station.StationType ?? "")
                        .AddProperty("Construction Time", $"{station.ConstructionTime}s")
                        .AddProperty("Can Build Ships", station.HasBehavior("ShipyardBehavior"));

                    var resourcesNode = stationNode.AddChild("Required Resources");
                    foreach (var resource in station.RequiredResources)
                    {
                        resourcesNode.AddProperty(resource.Key, resource.Value);
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
            foreach (var kvp in _stations)
            {
                if (
                    kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                    || (kvp.Value.StationType?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false)
                )
                {
                    results.Add($"By Type/{kvp.Value.StationType ?? "Uncategorized"}/{kvp.Key}");
                }
            }
            return results;
        }
    }
}
#endif
