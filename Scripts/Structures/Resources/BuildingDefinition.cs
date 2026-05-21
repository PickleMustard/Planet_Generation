using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using Structures.Transfers;

namespace Structures.Resources;

/// <summary>
/// Defines a building type with construction requirements, placement constraints, and production capabilities.
/// Loaded from YAML at runtime; instances are kept in BuildingDatabase.
/// </summary>
public partial class BuildingDefinition : Resource
{
    public string? IdName { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }

    public float WorkRequired { get; set; } = 100.0f;
    public int BuildingLimit { get; set; } = -1;

    /// <summary>
    /// Maximum resource tier this building can interact with. Resources with a tier
    /// exceeding this value are excluded from tag resolution and blocked from literal
    /// recipe inputs/outputs. Default 0 means only tier-0 resources are processed.
    /// </summary>
    public int MaxResourceTier { get; set; } = 0;

    public PlacementRequirements Placement { get; set; } = new();
    public Dictionary<string, int> RequiredResources { get; set; } = new();
    public VisualDefinition Visual { get; set; } = new();
    public IconDefinition Icon { get; set; } = new();
    public SoundDefinition Sound { get; set; } = new();
    public string? AllowedRecipeCategory { get; set; }

    public Godot.Collections.Array<NodeSpec> NodeLayout { get; set; } = new();

    /// <summary>
    /// Populates <see cref="NodeLayout"/> from the slot lists declared on a
    /// referenced <see cref="BuildingShape2D"/>. Shape slots are the source of
    /// truth — building YAML carries no per-node config; instead it references
    /// a shape by id and inherits the shape's slot layout.
    /// </summary>
    public void PopulateNodeLayoutFromShape(BuildingShape2D shape)
    {
        NodeLayout.Clear();
        for (int sideIdx = 0; sideIdx < shape.Sides.Count; sideIdx++)
        {
            var side = shape.Sides[sideIdx];
            for (int slotIdx = 0; slotIdx < side.Slots.Count; slotIdx++)
            {
                var slot = side.Slots[slotIdx];
                NodeLayout.Add(new NodeSpec
                {
                    SideIndex = sideIdx,
                    SlotIndex = slotIdx,
                    Kind = slot.Kind,
                    StateOfMatter = slot.StateOfMatter,
                });
            }
        }
    }

    /// <summary>
    /// Inline behavior entries parsed from YAML. Each entry pairs a behavior ID
    /// with an optional config dict. The factory calls IBehaviorConfigurable.Configure()
    /// when config is non-empty.
    /// </summary>
    public List<BehaviorConfigEntry> BehaviorEntries { get; set; } = new();



    /// <summary>
    /// Pairs a behavior class name with an inline config dict parsed from the
    /// behaviors: section of the building YAML. When config is non-empty, the
    /// factory will call IBehaviorConfigurable.Configure() before OnAttach().
    /// </summary>
    public class BehaviorConfigEntry
    {
        public string BehaviorId { get; set; } = "";
        public Dictionary<string, object> Config { get; set; } = new();
    }

    /// <summary>
    /// Whether this building can be demolished after construction completes. Defaults to
    /// true; set to false in YAML (<c>demolishable: false</c>) for buildings whose
    /// removal would invalidate game state (e.g. the company headquarters, story
    /// objectives).
    /// </summary>
    public bool Demolishable { get; set; } = true;

    /// <summary>
    /// Optional default link profile ID for this building's transfer nodes.
    /// When null or empty, nodes will use the global default or per-node override.
    /// </summary>
    public string? DefaultLinkProfile { get; set; }

    public int Footprint => Placement.CellCount;

    /// <summary>
    /// Creates a runtime Building instance configured from this definition.
    /// Caller is responsible for placing it on a cell and attaching a visual proxy.
    /// </summary>
    public Building Instantiate()
    {
        var building = new Building();
        building.Id = System.Guid.NewGuid().ToString();
        building.ApplyDefinition(this);

        // Attach behaviors from entries (config flows through IBehaviorConfigurable)
        foreach (var entry in BehaviorEntries)
        {
            var behavior = Constructables.Buildings.BehaviorFactory.Create(
                entry.BehaviorId, entry.Config);
            if (behavior == null)
                continue;
            building.Behaviors.Add(behavior);
            behavior.OnAttach(building);
        }

        // Set initial active recipe from ManufacturingBehavior or ExtractionBehavior
        var mfg = building.GetBehavior<Constructables.Buildings.Behaviors.ManufacturingBehavior>();
        if (mfg != null && !string.IsNullOrEmpty(mfg.DefaultRecipe))
            building.ActiveRecipeId = mfg.DefaultRecipe;

        var ext = building.GetBehavior<Constructables.Buildings.Behaviors.ExtractionBehavior>();
        if (ext != null && !string.IsNullOrEmpty(ext.DefaultRecipe) && string.IsNullOrEmpty(building.ActiveRecipeId))
            building.ActiveRecipeId = ext.DefaultRecipe;

        // Build resource nodes from NodeLayout
        foreach (var spec in NodeLayout)
            building.Nodes.Add(spec.Build(building));

        // Auto-attach BulkStorageRoutingBehavior if StorageHubBehavior was configured
        var hub = building.GetBehavior<Constructables.Buildings.Behaviors.StorageHubBehavior>();
        if (hub != null && hub.StorageCapacity > 0
            && building.GetBehavior<Constructables.Buildings.Behaviors.BulkStorageRoutingBehavior>() == null)
        {
            var routing = new Constructables.Buildings.Behaviors.BulkStorageRoutingBehavior();
            building.Behaviors.Add(routing);
            routing.OnAttach(building);
        }

        return building;
    }

    /// <summary>
    /// Validates whether the given cell satisfies this definition's placement constraints.
    /// Uses the configured custom behavior if present, otherwise falls back to DefaultPlacementBehavior.
    /// </summary>
    public bool ValidatePlacement(VoronoiCell? cell)
    {
        return ValidatePlacement(cell, null);
    }

    /// <summary>
    /// Body-aware overload. Custom behaviors (atmosphere, vent) consume the body reference;
    /// default placement ignores it. Callers in the UI/construction layer should prefer this
    /// overload so all checks see consistent context.
    /// </summary>
    public bool ValidatePlacement(VoronoiCell? cell, IOrbitalBody? body)
    {
        if (cell == null)
            return false;

        IPlacementBehavior behavior =
            Placement.ConfigurableBehavior ?? new DefaultPlacementBehavior(Placement);
        return behavior.IsValidPlacement(cell, body);
    }

    public partial class PlacementRequirements : Resource
    {
        public Godot.Collections.Array<Biome.BiomeType> Biomes { get; set; } = new();
        public bool AllowAnyBiome { get; set; } = false;
        public float MinElevation { get; set; } = 0.0f;
        public float MaxElevation { get; set; } = 1.0f;
        public float MaxSlope { get; set; } = 45.0f;
        public int CellCount { get; set; } = 1;
        public bool RequiresAdjacent { get; set; } = false;
        public IPlacementBehavior? ConfigurableBehavior { get; set; }
    }

    public class SoundDefinition
    {
        public string? Building { get; set; }
        public string? Finished { get; set; }
        public string? Idle { get; set; }
        public string? Fabricating { get; set; }
    }
}
