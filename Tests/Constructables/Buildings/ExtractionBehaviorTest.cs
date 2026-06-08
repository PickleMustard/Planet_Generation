using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Structures.Enums;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Resources;

namespace Tests.Constructables.Buildings;

[TestSuite]
public class ExtractionBehaviorTest
{
    private static VoronoiCell MakeCellWithResources(Dictionary<string, float> resources)
    {
        var p = new Point(new Vector3(1f, 0f, 0f));
        var cell = new VoronoiCell(0, new[] { p }, System.Array.Empty<Triangle>(), System.Array.Empty<Edge>());
        cell.Resources = resources;
        cell.Center = new Vector3(1f, 0f, 0f);
        return cell;
    }

    /// <summary>
    /// Helper: builds a Building + ExtractionBehavior with a tag-output recipe configured.
    /// The recipe's <c>tag:ore</c> output drives AvailableDeposits filtering.
    /// </summary>
    private static (Building b, ExtractionBehavior ext) MakeMine(
        Dictionary<string, float> cellResources,
        int maxResourceTier = 99,
        string? defaultRecipe = "surface_mine")
    {
        var def = new BuildingDefinition
        {
            IdName = "test_mine",
            DisplayName = "Test Mine",
            WorkRequired = 0f,
            MaxResourceTier = maxResourceTier,
        };
        var building = new Building();
        building.ApplyDefinition(def);
        building.SetPlacement(MakeCellWithResources(cellResources), null);

        var ext = new ExtractionBehavior();
        if (!string.IsNullOrEmpty(defaultRecipe))
            ext.Configure(new Dictionary<string, object> { ["default_recipe"] = defaultRecipe });
        ext.OnAttach(building);
        building.Behaviors.Add(ext);

        // Set ActiveRecipeId so RebuildAvailableDeposits can find the recipe
        if (!string.IsNullOrEmpty(defaultRecipe))
            building.ActiveRecipeId = defaultRecipe;

        // Ensure RecipeDatabase is loaded so LookupRecipe works
        var rdb = RecipeDatabase.Instance;
        if (!rdb.IsLoaded) rdb.LoadData();

        ext.OnRegister();
        return (building, ext);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AvailableDeposits_FiltersByRecipeOutputTags_SumsAbundance()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMine(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.7f,
            ["copper_ore"] = 0.3f,
            ["clay"] = 0.5f,   // not tagged "ore"
        });

        AssertThat(ext.AvailableDeposits.Count).IsEqual(2);
        AssertThat(ext.AvailableDeposits.ContainsKey("iron_ore")).IsTrue();
        AssertThat(ext.AvailableDeposits.ContainsKey("copper_ore")).IsTrue();
        AssertThat(ext.AvailableDeposits.ContainsKey("clay")).IsFalse();
        AssertThat(ext.AvailableDeposits["iron_ore"]).IsEqual(0.7f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AvailableDeposits_FiltersByMaxResourceTier()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        // iron_ore and copper_ore are tier 0, uranium_ore is tier 1, lithium_ore is tier 2
        var (_, ext) = MakeMine(
            new Dictionary<string, float>
            {
                ["iron_ore"] = 0.5f,
                ["copper_ore"] = 0.4f,
                ["uranium_ore"] = 0.8f,
                ["lithium_ore"] = 0.6f,
            },
            maxResourceTier: 0);

        // Only tier-0 ores should appear
        AssertThat(ext.AvailableDeposits.Count).IsEqual(2);
        AssertThat(ext.AvailableDeposits.ContainsKey("iron_ore")).IsTrue();
        AssertThat(ext.AvailableDeposits.ContainsKey("copper_ore")).IsTrue();
        AssertThat(ext.AvailableDeposits.ContainsKey("uranium_ore")).IsFalse();
        AssertThat(ext.AvailableDeposits.ContainsKey("lithium_ore")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AvailableDeposits_EmptyCell_LeavesEmpty()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMine(new Dictionary<string, float>());

        AssertThat(ext.AvailableDeposits.Count).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryResolveOutput_PicksHighestWeight()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMine(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.2f,
            ["copper_ore"] = 0.9f,
        });

        bool ok = ext.TryResolveOutput("ore", out var id, out var weight);
        AssertThat(ok).IsTrue();
        AssertThat(id).IsEqual("copper_ore");
        AssertThat(weight).IsEqual(0.9f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryResolveOutput_NoMatchingDeposit_ReturnsFalse()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMine(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.5f,
        });

        bool ok = ext.TryResolveOutput("metal", out var id, out var weight);
        AssertThat(ok).IsFalse();
        AssertThat(id).IsNull();
        AssertThat(weight).IsEqual(0f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryResolveOutput_RespectsMaxResourceTier()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        // iron_ore (tier 0, abundance 0.2) vs uranium_ore (tier 1, abundance 0.9)
        // With maxTier=0, uranium_ore should be excluded
        var (_, ext) = MakeMine(
            new Dictionary<string, float>
            {
                ["iron_ore"] = 0.2f,
                ["uranium_ore"] = 0.9f,
            },
            maxResourceTier: 0);

        bool ok = ext.TryResolveOutput("ore", out var id, out var weight);
        AssertThat(ok).IsTrue();
        AssertThat(id).IsEqual("iron_ore");
        AssertThat(weight).IsEqual(0.2f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ResolveTagOutputs_ResolvesAtOnRegister()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMine(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.8f,
            ["copper_ore"] = 0.3f,
        });

        // ResolvedTagOutputs should be populated from OnRegister
        AssertThat(ext.ResolvedTagOutputs.ContainsKey("tag:ore")).IsTrue();
        AssertThat(ext.ResolvedTagOutputs["tag:ore"]).IsEqual("iron_ore");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ResolveTagOutputs_RespectsMaxResourceTier()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        // uranium_ore (tier 1) has higher abundance, but maxTier=0 should exclude it
        var (_, ext) = MakeMine(
            new Dictionary<string, float>
            {
                ["iron_ore"] = 0.2f,
                ["uranium_ore"] = 0.9f,
            },
            maxResourceTier: 0);

        AssertThat(ext.ResolvedTagOutputs.ContainsKey("tag:ore")).IsTrue();
        AssertThat(ext.ResolvedTagOutputs["tag:ore"]).IsEqual("iron_ore");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void OnRecipeChanged_UpdatesResolvedOutputs()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (building, ext) = MakeMine(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.5f,
        });

        // Verify initial resolution
        AssertThat(ext.ResolvedTagOutputs.ContainsKey("tag:ore")).IsTrue();

        // Simulate recipe change (even if same recipe, verifies OnRecipeChanged flow)
        ext.OnRecipeChanged("surface_mine");

        AssertThat(ext.ResolvedTagOutputs.ContainsKey("tag:ore")).IsTrue();
        AssertThat(ext.ResolvedTagOutputs["tag:ore"]).IsEqual("iron_ore");
    }

    // ── Extraction slots ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds a mine with explicit slot config. <paramref name="primarySlots"/> = -1 means
    /// "omit the key" (exercises the auto default = distinct output-tag count).
    /// </summary>
    private static (Building b, ExtractionBehavior ext) MakeMineSlots(
        Dictionary<string, float> cellResources,
        int primarySlots,
        int secondarySlots,
        float secondaryRate = 0.5f,
        int maxResourceTier = 99,
        string defaultRecipe = "surface_mine")
    {
        var def = new BuildingDefinition
        {
            IdName = "test_mine",
            DisplayName = "Test Mine",
            WorkRequired = 0f,
            MaxResourceTier = maxResourceTier,
        };
        var building = new Building();
        building.ApplyDefinition(def);
        building.SetPlacement(MakeCellWithResources(cellResources), null);

        var config = new Dictionary<string, object>
        {
            ["default_recipe"] = defaultRecipe,
            ["secondary_slots"] = (long)secondarySlots,
            ["secondary_rate_multiplier"] = (double)secondaryRate,
        };
        if (primarySlots >= 0)
            config["primary_slots"] = (long)primarySlots;

        var ext = new ExtractionBehavior();
        ext.Configure(config);
        ext.OnAttach(building);
        building.Behaviors.Add(ext);
        building.ActiveRecipeId = defaultRecipe;

        var rdb = RecipeDatabase.Instance;
        if (!rdb.IsLoaded) rdb.LoadData();

        ext.OnRegister();
        return (building, ext);
    }

    private static RecipeDefinition TagOreRecipe(float outputAmount, float work = 0f)
        => new()
        {
            RecipeId = "test_tag_ore_extraction",
            DisplayName = "Test Extraction",
            Category = "extraction",
            WorkRequired = work,
            InputResources = new Dictionary<string, float>(),
            OutputResources = new Dictionary<string, float> { ["tag:ore"] = outputAmount },
        };

    [TestCase]
    [RequireGodotRuntime]
    public void DefaultSlots_OnePrimary_AutoFilledHighestAbundance()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        // No slot config -> auto default = distinct output-tag count of surface_mine (1).
        var (_, ext) = MakeMine(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.8f,
            ["copper_ore"] = 0.3f,
        });

        AssertThat(ext.Slots.Count).IsEqual(1);
        AssertThat(ext.Slots[0].Kind).IsEqual(ExtractionSlotKind.Primary);
        AssertThat(ext.Slots[0].ResourceId).IsEqual("iron_ore");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ConfiguredSlots_PrimariesAutoFilledDistinct_SecondaryEmpty()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMineSlots(
            new Dictionary<string, float> { ["iron_ore"] = 0.8f, ["copper_ore"] = 0.3f },
            primarySlots: 2, secondarySlots: 1);

        AssertThat(ext.Slots.Count).IsEqual(3);
        AssertThat(ext.Slots[0].ResourceId).IsEqual("iron_ore");
        AssertThat(ext.Slots[1].ResourceId).IsEqual("copper_ore"); // distinct preferred
        AssertThat(ext.Slots[2].Kind).IsEqual(ExtractionSlotKind.Secondary);
        AssertThat(ext.Slots[2].ResourceId).IsNull(); // secondaries start empty
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AutoFill_AllowsRepeatsWhenPoolExhausted()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        // Two primary slots, only one eligible resource -> repeat allowed.
        var (_, ext) = MakeMineSlots(
            new Dictionary<string, float> { ["iron_ore"] = 0.5f },
            primarySlots: 2, secondarySlots: 0);

        AssertThat(ext.Slots[0].ResourceId).IsEqual("iron_ore");
        AssertThat(ext.Slots[1].ResourceId).IsEqual("iron_ore");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryAssignSlot_RejectsIneligible_AcceptsEligible_AllowsDuplicate()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMineSlots(
            new Dictionary<string, float> { ["iron_ore"] = 0.8f, ["copper_ore"] = 0.3f, ["clay"] = 0.5f },
            primarySlots: 2, secondarySlots: 0);

        AssertThat(ext.TryAssignSlot(0, "clay")).IsFalse();      // clay isn't tagged ore -> not in AvailableDeposits
        AssertThat(ext.TryAssignSlot(5, "iron_ore")).IsFalse();  // out of range
        AssertThat(ext.TryAssignSlot(0, "copper_ore")).IsTrue();
        AssertThat(ext.TryAssignSlot(1, "copper_ore")).IsTrue(); // duplicate across slots allowed
        AssertThat(ext.Slots[0].ResourceId).IsEqual("copper_ore");
        AssertThat(ext.Slots[1].ResourceId).IsEqual("copper_ore");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryAssignSlot_RejectsOverTier()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMineSlots(
            new Dictionary<string, float> { ["iron_ore"] = 0.5f, ["uranium_ore"] = 0.9f },
            primarySlots: 1, secondarySlots: 0, maxResourceTier: 0);

        // uranium_ore is tier 1 -> filtered out of AvailableDeposits and rejected.
        AssertThat(ext.TryAssignSlot(0, "uranium_ore")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ClearSlot_EmptiesSlot()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMineSlots(
            new Dictionary<string, float> { ["iron_ore"] = 0.8f }, primarySlots: 1, secondarySlots: 0);

        AssertThat(ext.Slots[0].ResourceId).IsEqual("iron_ore");
        ext.ClearSlot(0);
        AssertThat(ext.Slots[0].ResourceId).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_PrimaryPlusSecondarySameResource_SumsWithRate()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMineSlots(
            new Dictionary<string, float> { ["iron_ore"] = 0.8f },
            primarySlots: 1, secondarySlots: 1, secondaryRate: 0.5f);

        // slot 0 (primary) auto-filled iron_ore; assign slot 1 (secondary) iron_ore too.
        AssertThat(ext.TryAssignSlot(1, "iron_ore")).IsTrue();

        ext.StartCycle(TagOreRecipe(10f), productionSpeed: 1f);

        // 10*0.8*1 (primary) + 10*0.8*0.5 (secondary) = 8 + 4 = 12
        AssertThat(ext.ExpectedOutputs.ContainsKey("iron_ore")).IsTrue();
        AssertThat(ext.ExpectedOutputs["iron_ore"]).IsEqual(12f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_DistinctResources_ProducesTwoOutputs()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMineSlots(
            new Dictionary<string, float> { ["iron_ore"] = 0.5f, ["copper_ore"] = 0.4f },
            primarySlots: 2, secondarySlots: 0);

        ext.StartCycle(TagOreRecipe(10f), productionSpeed: 1f);

        AssertThat(ext.ExpectedOutputs["iron_ore"]).IsEqual(5f);
        AssertThat(ext.ExpectedOutputs["copper_ore"]).IsEqual(4f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_AllSlotsEmpty_NoCycle()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, ext) = MakeMineSlots(
            new Dictionary<string, float> { ["iron_ore"] = 0.8f }, primarySlots: 1, secondarySlots: 0);
        ext.ClearSlot(0);

        ext.StartCycle(TagOreRecipe(10f), productionSpeed: 1f);

        AssertThat(ext.State).IsEqual(ManufacturingState.Idle);
        AssertThat(ext.ExpectedOutputs.Count).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void OnRecipeChanged_ClearsIneligible_RefillsPrimary()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        // Cell carries both an ore and a timber resource.
        var (building, ext) = MakeMineSlots(
            new Dictionary<string, float> { ["iron_ore"] = 0.8f, ["wood"] = 0.6f },
            primarySlots: 1, secondarySlots: 0);

        AssertThat(ext.Slots[0].ResourceId).IsEqual("iron_ore"); // ore domain

        // Switch to a timber recipe: iron_ore becomes ineligible -> cleared, refilled with wood.
        building.ActiveRecipeId = "clear_cutting";
        ext.OnRecipeChanged("clear_cutting");

        AssertThat(ext.Slots[0].ResourceId).IsEqual("wood");
    }

    // ── GetExtractableDeposits (placement-preview static) ────────────────────

    /// <summary>Builds a BuildingDefinition carrying an ExtractionBehavior entry, matching the
    /// YAML shape the placement preview reads from.</summary>
    private static BuildingDefinition MakeMineDefinition(int maxResourceTier, string defaultRecipe)
    {
        var def = new BuildingDefinition
        {
            IdName = "test_mine",
            DisplayName = "Test Mine",
            MaxResourceTier = maxResourceTier,
        };
        def.BehaviorEntries.Add(new BuildingDefinition.BehaviorConfigEntry
        {
            BehaviorId = "ExtractionBehavior",
            Config = new Dictionary<string, object> { ["default_recipe"] = defaultRecipe },
        });
        return def;
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetExtractableDeposits_FiltersByTagAndSortsByAbundance()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();
        var rdb = RecipeDatabase.Instance;
        if (!rdb.IsLoaded) rdb.LoadData();

        var def = MakeMineDefinition(maxResourceTier: 99, defaultRecipe: "surface_mine");
        var cell = MakeCellWithResources(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.3f,
            ["copper_ore"] = 0.9f,
            ["clay"] = 0.5f,   // not tagged "ore" -> excluded
        });

        var result = ExtractionBehavior.GetExtractableDeposits(def, cell);

        AssertThat(result.Count).IsEqual(2);
        // Abundance-descending: copper_ore (0.9) before iron_ore (0.3)
        AssertThat(result[0].Key).IsEqual("copper_ore");
        AssertThat(result[1].Key).IsEqual("iron_ore");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetExtractableDeposits_RespectsMaxResourceTier()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();
        var rdb = RecipeDatabase.Instance;
        if (!rdb.IsLoaded) rdb.LoadData();

        var def = MakeMineDefinition(maxResourceTier: 0, defaultRecipe: "surface_mine");
        var cell = MakeCellWithResources(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.5f,      // tier 0
            ["uranium_ore"] = 0.8f,   // tier 1 -> excluded
        });

        var result = ExtractionBehavior.GetExtractableDeposits(def, cell);

        AssertThat(result.Count).IsEqual(1);
        AssertThat(result[0].Key).IsEqual("iron_ore");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetExtractableDeposits_NoExtractionBehavior_ReturnsEmpty()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var def = new BuildingDefinition { IdName = "not_a_mine", MaxResourceTier = 99 };
        var cell = MakeCellWithResources(new Dictionary<string, float> { ["iron_ore"] = 0.5f });

        var result = ExtractionBehavior.GetExtractableDeposits(def, cell);

        AssertThat(result.Count).IsEqual(0);
    }
}
