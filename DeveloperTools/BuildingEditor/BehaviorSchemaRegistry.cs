#if DEBUG
using System.Collections.Generic;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// UI-control type for a behavior config field. Drives BehaviorRow's choice of
/// SpinBox / CheckBox / OptionButton / LineEdit / mini-editor.
/// </summary>
public enum BehaviorFieldType
{
    Float,
    Int,
    Bool,
    String,
    ResourceId,
    RecipeId,
    StringList,
    RecipeIdList,
    /// <summary>Dictionary of slot-filter spec strings ("any", "state:solid", "category:foo", "resource:bar") to integer counts.</summary>
    SlotFiltersDict,
    /// <summary>Dictionary of resource_id to integer count (e.g. InitialStockpileBehavior.stockpiles).</summary>
    StringIntDict
}

/// <summary>
/// Describes a single configurable field of a building behavior. Used by
/// BehaviorRow to render the right control and by BuildingEditorModel to seed
/// new entries with sensible defaults.
/// </summary>
public sealed record BehaviorFieldSpec(
    string Name,
    BehaviorFieldType FieldType,
    object? Default,
    float Min = float.MinValue,
    float Max = float.MaxValue,
    string? Tooltip = null);

/// <summary>
/// Hand-maintained schema describing the inline config fields each IBuildingBehavior
/// reads inside its Configure(Dictionary&lt;string,object&gt;) body. Each new behavior
/// must be added here for the BuildingEditor to expose its fields — a unit test
/// asserts every concrete IBuildingBehavior has an entry.
/// </summary>
public static class BehaviorSchemaRegistry
{
    private static readonly Dictionary<string, List<BehaviorFieldSpec>> Schemas = new()
    {
        ["ManufacturingBehavior"] = new()
        {
            new("default_recipe", BehaviorFieldType.RecipeId, "",
                Tooltip: "Default recipe selected at startup"),
            new("alternative_recipes", BehaviorFieldType.RecipeIdList, new List<string>(),
                Tooltip: "Selectable alternative recipes"),
            new("production_speed", BehaviorFieldType.Float, 1f, 0f, 100f,
                "Multiplier on recipe work duration"),
        },
        ["ExtractionBehavior"] = new()
        {
            new("default_recipe", BehaviorFieldType.RecipeId, "",
                Tooltip: "Default recipe selected at startup"),
            new("alternative_recipes", BehaviorFieldType.RecipeIdList, new List<string>(),
                Tooltip: "Selectable alternative recipes"),
            new("production_speed", BehaviorFieldType.Float, 1f, 0f, 100f,
                "Multiplier on recipe work duration"),
            new("primary_slots", BehaviorFieldType.Int, -1, -1, 16,
                "Primary extraction slots. -1 = auto (distinct output-tag count of recipe)"),
            new("secondary_slots", BehaviorFieldType.Int, 0, 0, 16,
                "Secondary extraction slots (lower output rate)"),
            new("secondary_rate_multiplier", BehaviorFieldType.Float, 0.5f, 0f, 10f,
                "Output multiplier applied to secondary slots"),
        },
        ["PowerConsumerBehavior"] = new()
        {
            new("base_draw", BehaviorFieldType.Float, 0f, 0f, 10000f,
                "Power drawn while manufacturing"),
        },
        ["PowerProducerBehavior"] = new()
        {
            new("grid_radius", BehaviorFieldType.Int, 2, 0, 64),
            new("is_renewable", BehaviorFieldType.Bool, false),
            new("default_recipe", BehaviorFieldType.RecipeId, "",
                Tooltip: "Default recipe for power output resolution"),
            new("production_speed", BehaviorFieldType.Float, 1f, 0f, 100f,
                "Multiplier on recipe work duration"),
        },
        ["BatteryBehavior"] = new()
        {
            new("capacity", BehaviorFieldType.Float, 0f, 0f, 1000000f),
            new("grid_radius", BehaviorFieldType.Int, 2, 0, 64),
        },
        ["StorageHubBehavior"] = new()
        {
            new("storage_capacity", BehaviorFieldType.Int, 0, 0, 1000,
                "Number of bulk storage slots"),
            new("slot_filters", BehaviorFieldType.SlotFiltersDict,
                new Dictionary<string, int>(),
                Tooltip: "Slot filter spec → count. Prefixes: any, state:, category:, resource:"),
        },
        ["TransferStationBehavior"] = new()
        {
            new("cargo_capacity", BehaviorFieldType.Float, 500f, 0f, 100000f),
            new("vehicle_speed", BehaviorFieldType.Float, 50f, 0f, 10000f),
            new("max_concurrent_transfers", BehaviorFieldType.Int, 2, 0, 32),
        },
        ["TransportHubBehavior"] = new(),
        ["ResearchBehavior"] = new(),
        ["GameStartBehavior"] = new(),
        ["InitialStockpileBehavior"] = new()
        {
            new("stockpiles", BehaviorFieldType.StringIntDict,
                new Dictionary<string, int>(),
                Tooltip: "Resource id → quantity to deposit at registration"),
        },
    };

    /// <summary>Returns the schema (or an empty list when no entry is registered).</summary>
    public static IReadOnlyList<BehaviorFieldSpec> GetSchema(string behaviorId)
    {
        return Schemas.TryGetValue(behaviorId, out var schema)
            ? schema
            : System.Array.Empty<BehaviorFieldSpec>();
    }

    public static bool IsKnown(string behaviorId) => Schemas.ContainsKey(behaviorId);

    public static IEnumerable<string> KnownBehaviorIds => Schemas.Keys;
}

/// <summary>
/// Parallel registry for IPlacementBehavior implementations whose config keys
/// live inside the placement_requirements.configurable_behavior YAML mapping.
/// </summary>
public static class PlacementBehaviorSchemaRegistry
{
    private static readonly Dictionary<string, List<BehaviorFieldSpec>> Schemas = new()
    {
        ["AtmospherePlacementBehavior"] = new()
        {
            new("min_atmosphere", BehaviorFieldType.Float, 0f, 0f, 100f,
                "Minimum atmospheric pressure (atm)"),
            new("max_atmosphere", BehaviorFieldType.Float, 100f, 0f, 100f,
                "Maximum atmospheric pressure (atm)"),
        },
        ["GeothermalVentPlacementBehavior"] = new(),
    };

    public static IReadOnlyList<BehaviorFieldSpec> GetSchema(string behaviorId)
    {
        return Schemas.TryGetValue(behaviorId, out var schema)
            ? schema
            : System.Array.Empty<BehaviorFieldSpec>();
    }

    public static bool IsKnown(string behaviorId) => Schemas.ContainsKey(behaviorId);

    public static IEnumerable<string> KnownBehaviorIds => Schemas.Keys;
}
#endif
