#if DEBUG
using System;
using System.Collections.Generic;
using Debug.DatabaseViewer;

namespace Structures.Resources
{
    public partial class ResourceDatabase : IDebugDataProvider
    {
        string IDataProvider.Name => "IngameResources";
        string IDataProvider.Category => "Game";
        bool IDataProvider.NeedsRefresh => true;
        object IDebugDataProvider.SourceObject => this;
        string IDebugDataProvider.InstanceNamespace => "ResourceDatabase";
        bool IDebugDataProvider.IsSourceValid => IsLoaded;

        DebugDataNode IDataProvider.GetData()
        {
            var node = new DebugDataNode("Resources").AddProperty(
                "Total Definitions",
                _resources.Count
            );

            var definitionsNode = node.AddChild("Definitions");
            foreach (var kvp in _resources)
            {
                var def = kvp.Value;
                definitionsNode
                    .AddChild(def.IdName ?? "Unknown")
                    .AddProperty("Resource Type", def.ResourceType ?? "Unknown")
                    .AddProperty("Resource Tier", def.ResourceTier);
            }

            return node;
        }

        void IDataProvider.Refresh() { }

        IEnumerable<string> IDataProvider.Search(string pattern)
        {
            var results = new List<string>();
            foreach (var kvp in _resources)
            {
                if (
                    kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                    || (
                        kvp.Value.ResourceType?.Contains(
                            pattern,
                            StringComparison.OrdinalIgnoreCase
                        ) ?? false
                    )
                )
                {
                    results.Add($"Definitions/{kvp.Key}");
                }
            }
            return results;
        }
    }
}
#endif
