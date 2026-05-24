using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;

namespace Tests.Constructables.Buildings;

/// <summary>
/// Tag-resolution behavior in ManufacturingBehavior.StartCycle. A recipe with `tag:ore`
/// input and `tag:metal` output must pick the highest-quantity matching resource from
/// InputStorage, capture its `material:*` discriminator, and resolve the output to the
/// resource carrying the same discriminator + the output tag.
/// </summary>
[TestSuite]
public class ManufacturingBehaviorTagResolutionTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_TagInput_PicksHighestQuantityResource_AndResolvesMatchingOutput()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var building = BuildSmelter();
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;
        var recipe = SmeltingRecipe();

        mfg.EnsureSlotsForRecipe(recipe);
        building.InputStorage.Deposit("iron_ore", 5f);
        building.InputStorage.Deposit("copper_ore", 2f);

        mfg.StartCycle(recipe, productionSpeed: 10f);

        AssertThat(mfg.ResolvedTagInputs.ContainsKey("tag:ore")).IsTrue();
        AssertThat(mfg.ResolvedTagInputs["tag:ore"]).IsEqual("iron_ore");
        AssertThat(mfg.CycleMaterialDiscriminator).IsEqual("material:iron");
        AssertThat(mfg.ResolvedTagOutputs.ContainsKey("tag:metal")).IsTrue();
        AssertThat(mfg.ResolvedTagOutputs["tag:metal"]).IsEqual("iron");
        AssertThat(mfg.ExpectedOutputs.ContainsKey("iron")).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_TagInput_SwitchHighestQuantity_PicksOtherMaterial()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var building = BuildSmelter();
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;
        var recipe = SmeltingRecipe();

        mfg.EnsureSlotsForRecipe(recipe);
        building.InputStorage.Deposit("iron_ore", 1f);
        building.InputStorage.Deposit("copper_ore", 9f);

        mfg.StartCycle(recipe, productionSpeed: 10f);

        AssertThat(mfg.ResolvedTagInputs["tag:ore"]).IsEqual("copper_ore");
        AssertThat(mfg.CycleMaterialDiscriminator).IsEqual("material:copper");
        AssertThat(mfg.ResolvedTagOutputs["tag:metal"]).IsEqual("copper");
        AssertThat(mfg.ExpectedOutputs.ContainsKey("copper")).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_TagInput_EndToEnd_DepositsResolvedIngot()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var building = BuildSmelter();
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;
        var recipe = SmeltingRecipe();

        mfg.EnsureSlotsForRecipe(recipe);
        building.InputStorage.Deposit("iron_ore", 3f);

        mfg.StartCycle(recipe, productionSpeed: 10f);
        AssertThat(building.InputStorage.GetQuantity("iron_ore")).IsEqual(2);
        mfg.OnManufactureTick(1f, building);

        AssertThat(building.OutputStorage.GetQuantity("iron")).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_LiteralRecipeRegression_Unchanged()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var building = BuildSmelter();
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;
        var recipe = new RecipeDefinition
        {
            WorkRequired = 0.1f,
            InputResources = new Dictionary<string, float>(),
            OutputResources = new Dictionary<string, float> { ["iron"] = 1f },
        };

        mfg.StartCycle(recipe, productionSpeed: 10f);
        mfg.OnManufactureTick(1f, building);

        AssertThat(building.OutputStorage.GetQuantity("iron")).IsEqual(1);
        AssertThat(mfg.ResolvedTagInputs.Count).IsEqual(0);
        AssertThat(mfg.ResolvedTagOutputs.Count).IsEqual(0);
        AssertThat(mfg.CycleMaterialDiscriminator).IsNull();
    }

    private static Building BuildSmelter()
    {
        var building = new Building();
        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        building.Behaviors.Add(mfg);
        return building;
    }

    /// <summary>Builds a smelter with a specific MaxResourceTier on its definition.</summary>
    private static Building BuildSmelter(int maxResourceTier)
    {
        var def = new BuildingDefinition
        {
            IdName = "test_smelter",
            MaxResourceTier = maxResourceTier,
            WorkRequired = 100f,
        };
        var building = def.Instantiate();
        return building;
    }

    private static RecipeDefinition SmeltingRecipe() => new RecipeDefinition
    {
        RecipeId = "test_smelt",
        WorkRequired = 0.1f,
        InputResources = new Dictionary<string, float> { ["tag:ore"] = 1f },
        OutputResources = new Dictionary<string, float> { ["tag:metal"] = 1f },
    };

    // ── Tier-filtering tests ──────────────────────────────────────────────

    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_Tier0Building_ExcludesHighTierTagInputs()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var building = BuildSmelter(maxResourceTier: 0);
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;
        var recipe = SmeltingRecipe();

        mfg.EnsureSlotsForRecipe(recipe);
        // Deposit both tier-0 and tier-1 ores
        building.InputStorage.Deposit("iron_ore", 1f);   // tier 0
        building.InputStorage.Deposit("uranium_ore", 10f); // tier 1

        mfg.StartCycle(recipe, productionSpeed: 10f);

        // Must pick iron_ore (tier 0), not uranium_ore (tier 1) even though it has more quantity
        AssertThat(mfg.ResolvedTagInputs.ContainsKey("tag:ore")).IsTrue();
        AssertThat(mfg.ResolvedTagInputs["tag:ore"]).IsEqual("iron_ore");
        AssertThat(mfg.CycleMaterialDiscriminator).IsEqual("material:iron");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_Tier1Building_IncludesTier0And1TagInputs()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var building = BuildSmelter(maxResourceTier: 1);
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;
        var recipe = SmeltingRecipe();

        mfg.EnsureSlotsForRecipe(recipe);
        building.InputStorage.Deposit("iron_ore", 1f);     // tier 0
        building.InputStorage.Deposit("uranium_ore", 10f);  // tier 1

        mfg.StartCycle(recipe, productionSpeed: 10f);

        // With tier 1, uranium_ore (highest quantity) should be picked
        AssertThat(mfg.ResolvedTagInputs.ContainsKey("tag:ore")).IsTrue();
        AssertThat(mfg.ResolvedTagInputs["tag:ore"]).IsEqual("uranium_ore");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_LiteralInput_ExceedingTier_Blocked()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var building = BuildSmelter(maxResourceTier: 0);
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;
        var recipe = new RecipeDefinition
        {
            RecipeId = "test_literal_tier",
            WorkRequired = 0.1f,
            InputResources = new Dictionary<string, float> { ["uranium_ore"] = 1f }, // tier 1 literal
            OutputResources = new Dictionary<string, float> { ["uranium"] = 1f },
        };

        mfg.EnsureSlotsForRecipe(recipe);
        building.InputStorage.Deposit("uranium_ore", 5f);

        mfg.StartCycle(recipe, productionSpeed: 10f);

        // Should be waiting for inputs because uranium_ore (tier 1) exceeds max tier 0
        AssertThat(mfg.State == ManufacturingState.WaitingForInputs
            || mfg.State == ManufacturingState.Idle).IsTrue();
        AssertThat(mfg.InputsHeld.ContainsKey("uranium_ore")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartCycle_LiteralOutput_ExceedingTier_Skipped()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var building = BuildSmelter(maxResourceTier: 0);
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;
        var recipe = new RecipeDefinition
        {
            RecipeId = "test_output_tier",
            WorkRequired = 0.1f,
            InputResources = new Dictionary<string, float>(),
            OutputResources = new Dictionary<string, float> { ["iron"] = 1f, ["titanium"] = 1f },
        };
        // iron is tier 0, titanium is tier 2

        mfg.StartCycle(recipe, productionSpeed: 10f);
        mfg.OnManufactureTick(1f, building);

        // iron (tier 0) should be deposited; titanium (tier 2) should be skipped
        AssertThat(building.OutputStorage.GetQuantity("iron")).IsEqual(1);
        AssertThat(building.OutputStorage.GetQuantity("titanium")).IsEqual(0);
    }
}
