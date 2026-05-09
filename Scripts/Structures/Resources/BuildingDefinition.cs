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

    public float BuildingTime { get; set; } = 60.0f;
    public float WorkRequired { get; set; } = 100.0f;
    public int BuildingLimit { get; set; } = -1;

    public PlacementRequirements Placement { get; set; } = new();
    public Dictionary<string, int> RequiredResources { get; set; } = new();
    public ProductionDefinition Production { get; set; } = new();
    public PowerDefinition Power { get; set; } = new();
    public ExtractionDefinition Extraction { get; set; } = new();
    public VisualDefinition Visual { get; set; } = new();
    public IconDefinition Icon { get; set; } = new();
    public SoundDefinition Sound { get; set; } = new();
    public TransferStationDefinition? TransferStation { get; set; }
    public string? AllowedRecipeCategory { get; set; }
    public Dictionary<string, int> StartingStockpiles { get; set; } = new();

    /// <summary>
    /// Total number of bulk-storage slots this building exposes via
    /// <see cref="Constructables.Buildings.Behaviors.StorageHubBehavior"/>.
    /// </summary>
    public int StorageCapacity { get; set; } = 0;

    /// <summary>
    /// Filter mix for the bulk-storage slots. The sum of <see cref="SlotFilterSpec.Count"/>
    /// values must be ≤ <see cref="StorageCapacity"/>; any remaining slots default to
    /// <see cref="SlotFilter.Any"/>.
    /// </summary>
    public List<SlotFilterSpec> SlotFilters { get; set; } = new();

    public Godot.Collections.Array<NodeSpec> NodeLayout { get; set; } = new();
    public Godot.Collections.Array<string> BehaviorRefs { get; set; } = new();

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
        if (!string.IsNullOrEmpty(Production.DefaultRecipe))
            building.ActiveRecipeId = Production.DefaultRecipe;
        foreach (var spec in NodeLayout)
            building.Nodes.Add(spec.Build(building));
        foreach (var refName in BehaviorRefs)
        {
            var behavior = Constructables.Buildings.BehaviorFactory.Create(refName);
            if (behavior == null)
                continue;
            ApplyPowerToBehavior(behavior);
            building.Behaviors.Add(behavior);
            behavior.OnAttach(building);
        }
        return building;
    }

    /// <summary>
    /// Pushes power-related fields from this definition onto power behaviors right
    /// after instantiation, before OnAttach runs. Keeps YAML the single source of
    /// truth instead of embedding values in the behavior types.
    /// </summary>
    private void ApplyPowerToBehavior(Constructables.Buildings.IBuildingBehavior behavior)
    {
        switch (behavior)
        {
            case Constructables.Buildings.Behaviors.PowerProducerBehavior producer:
                producer.Output = Power.Output;
                producer.Radius = Power.GridRadius;
                producer.IsRenewable = Power.IsRenewable;
                break;
            case Constructables.Buildings.Behaviors.BatteryBehavior battery:
                battery.Capacity = Power.BatteryCapacity;
                battery.Radius = Power.GridRadius;
                break;
            case Constructables.Buildings.Behaviors.PowerConsumerBehavior consumer:
                consumer.BaseDraw = Power.BaseDraw;
                break;
            case Constructables.Buildings.Behaviors.ExtractionBehavior extraction:
                extraction.ExtractTypes = Extraction.ExtractTypes;
                extraction.RatePerTick = Extraction.RatePerTick;
                extraction.WorkPerCycle = Extraction.WorkPerCycle;
                break;
            case Constructables.Buildings.Behaviors.StorageHubBehavior hub:
                hub.StorageCapacity = StorageCapacity;
                hub.SlotFilters = SlotFilters;
                break;
        }
    }

    /// <summary>
    /// Validates whether the given cell satisfies this definition's placement constraints.
    /// Uses the configured custom behavior if present, otherwise falls back to DefaultPlacementBehavior.
    /// </summary>
    public bool ValidatePlacement(VoronoiCell? cell)
    {
        if (cell == null)
            return false;

        IPlacementBehavior behavior =
            Placement.ConfigurableBehavior ?? new DefaultPlacementBehavior(Placement);
        return behavior.IsValidPlacement(cell);
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

    public class ProductionDefinition
    {
        public string? DefaultRecipe { get; set; }
        public List<string> AlternativeRecipes { get; set; } = new();
        public float ProductionSpeed { get; set; } = 1.0f;
    }

    /// <summary>
    /// Power-grid parameters. Producers and batteries set <see cref="GridRadius"/> +
    /// <see cref="Output"/> / <see cref="BatteryCapacity"/> to seed and extend a grid.
    /// Consumers set <see cref="BaseDraw"/>. Buildings without a power role leave this
    /// at its defaults and receive no PowerProducer/Consumer/Battery behavior.
    /// </summary>
    public class PowerDefinition
    {
        public int GridRadius { get; set; } = 0;
        public float Output { get; set; } = 0f;
        public float BaseDraw { get; set; } = 0f;
        public float BatteryCapacity { get; set; } = 0f;

        /// <summary>
        /// Renewable producers generate continuously regardless of manufacturing state.
        /// Defaults to false — fueled plants gate generation on active manufacturing.
        /// </summary>
        public bool IsRenewable { get; set; } = false;
    }

    /// <summary>
    /// Per-cycle extraction parameters. Buildings with an <c>ExtractionBehavior</c> sample up
    /// to <see cref="ExtractTypes"/> resources from their occupied cells' resource mix and emit
    /// <see cref="RatePerTick"/> units of each as the synthetic recipe's outputs every
    /// <see cref="WorkPerCycle"/> seconds of work. Early-tier extractors set ExtractTypes=1
    /// for narrow output; late-tier set higher values for parallel extraction.
    /// </summary>
    public class ExtractionDefinition
    {
        public int ExtractTypes { get; set; } = 1;
        public float RatePerTick { get; set; } = 1f;
        public float WorkPerCycle { get; set; } = 1f;
    }

    public class SoundDefinition
    {
        public string? Building { get; set; }
        public string? Finished { get; set; }
        public string? Idle { get; set; }
        public string? Fabricating { get; set; }
    }
}
