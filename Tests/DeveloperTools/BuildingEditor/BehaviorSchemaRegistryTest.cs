#if DEBUG
using System.Collections.Generic;
using System.Linq;
using Constructables.Buildings;
using DeveloperTools.BuildingEditor;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Resources;

namespace Tests.DeveloperTools.BuildingEditor;

/// <summary>
/// Schema-drift guard: every concrete IBuildingBehavior / IPlacementBehavior
/// discoverable in the runtime assembly must have an entry in the corresponding
/// editor schema registry. New behaviors otherwise fail open with an empty UI
/// instead of surfacing their config fields.
/// </summary>
[TestSuite]
public class BehaviorSchemaRegistryTest
{
    private static readonly HashSet<string> ExemptBuildingBehaviors = new()
    {
        // Auto-attached by BuildingDefinition.Instantiate when StorageHubBehavior is configured.
        // Never referenced directly in YAML, so no editor schema is required.
        "BulkStorageRoutingBehavior",
    };

    private static readonly HashSet<string> ExemptPlacementBehaviors = new()
    {
        // Implicit fallback when no configurable_behavior is set in YAML.
        "DefaultPlacementBehavior",
    };

    [TestCase]
    public void EveryConcreteBuildingBehaviorHasASchemaEntry()
    {
        var missing = DiscoverConcrete(typeof(IBuildingBehavior))
            .Where(name => !ExemptBuildingBehaviors.Contains(name))
            .Where(name => !BehaviorSchemaRegistry.IsKnown(name))
            .ToList();

        AssertThat(missing.Count)
            .OverrideFailureMessage(
                "Concrete IBuildingBehavior types missing from BehaviorSchemaRegistry: "
                + string.Join(", ", missing))
            .IsEqual(0);
    }

    [TestCase]
    public void EveryConcretePlacementBehaviorHasASchemaEntry()
    {
        var missing = DiscoverConcrete(typeof(IPlacementBehavior))
            .Where(name => !ExemptPlacementBehaviors.Contains(name))
            .Where(name => !PlacementBehaviorSchemaRegistry.IsKnown(name))
            .ToList();

        AssertThat(missing.Count)
            .OverrideFailureMessage(
                "Concrete IPlacementBehavior types missing from PlacementBehaviorSchemaRegistry: "
                + string.Join(", ", missing))
            .IsEqual(0);
    }

    private static IEnumerable<string> DiscoverConcrete(System.Type iface)
    {
        foreach (var t in iface.Assembly.GetTypes())
        {
            if (t.IsAbstract || t.IsInterface) continue;
            if (!iface.IsAssignableFrom(t)) continue;
            // Skip test fixtures and mocks (they live under Tests.* namespaces).
            var ns = t.Namespace ?? string.Empty;
            if (ns.StartsWith("Tests")) continue;
            yield return t.Name;
        }
    }
}
#endif
