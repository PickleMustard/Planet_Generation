#if DEBUG
using System;
using System.Collections.Generic;
using Structures.Logistics;
using Debug.DatabaseViewer;

namespace Logistics.Resources
{
    public partial class ShipDatabase : IDebugDataProvider
    {
        string IDataProvider.Name => "Ships";
        string IDataProvider.Category => "Game";
        bool IDataProvider.NeedsRefresh => true;
        object IDebugDataProvider.SourceObject => this;
        string IDebugDataProvider.InstanceNamespace => "ShipDatabase";
        bool IDebugDataProvider.IsSourceValid => IsLoaded;

        DebugDataNode IDataProvider.GetData()
        {
            if (!IsLoaded)
            {
                var unloadedNode = new DebugDataNode("Ships");
                unloadedNode.AddProperty("Status", "Not loaded");
                unloadedNode.AddProperty("Load Progress", $"{LoadProgress:P0}");
                return unloadedNode;
            }

            var node = new DebugDataNode("Ships").AddProperty(
                "Total Definitions",
                _ships.Count
            );

            var categoriesNode = node.AddChild("By Category");
            var categories = new Dictionary<string, List<ShipDefinition>>();

            foreach (var ship in _ships.Values)
            {
                var category = ship.EngineCategory ?? "Uncategorized";
                if (!categories.ContainsKey(category))
                {
                    categories[category] = new List<ShipDefinition>();
                }
                categories[category].Add(ship);
            }

            foreach (var category in categories)
            {
                var categoryNode = categoriesNode.AddChild(category.Key)
                    .AddProperty("Count", category.Value.Count);

                foreach (var ship in category.Value)
                {
                    var shipNode = categoryNode.AddChild(ship.Name ?? "Unknown")
                        .AddProperty("Dry Mass", $"{ship.DryMass}kg")
                        .AddProperty("Cargo Capacity", ship.CargoCapacity)
                        .AddProperty("Fuel Capacity", ship.FuelCapacity)
                        .AddProperty("Engine Category", ship.EngineCategory ?? "")
                        .AddProperty("Construction Time", $"{ship.ConstructionTime}s")
                        .AddProperty("Work Required", ship.ConstructionTime);

                    var resourcesNode = shipNode.AddChild("Required Resources");
                    foreach (var resource in ship.RequiredResources)
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
            foreach (var kvp in _ships)
            {
                if (
                    kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                    || (kvp.Value.EngineCategory?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false)
                )
                {
                    results.Add($"By Category/{kvp.Value.EngineCategory ?? "Uncategorized"}/{kvp.Key}");
                }
            }
            return results;
        }
    }
}
#endif
