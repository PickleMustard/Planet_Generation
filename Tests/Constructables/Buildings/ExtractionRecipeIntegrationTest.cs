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

/// <summary>
/// End-to-end check that a tag-output extraction recipe pulls the concrete deposit id and
/// magnitude from <see cref="ExtractionBehavior.AvailableDeposits"/>: recipe base output is
/// scaled by the picked deposit's abundance weight.
/// </summary>
[TestSuite]
public class ExtractionRecipeIntegrationTest
{
    private static VoronoiCell MakeCell(Dictionary<string, float> resources)
    {
        var p = new Point(new Vector3(1f, 0f, 0f));
        var cell = new VoronoiCell(0, new[] { p }, System.Array.Empty<Triangle>(), System.Array.Empty<Edge>());
        cell.Resources = resources;
        cell.Center = new Vector3(1f, 0f, 0f);
        return cell;
    }

    private static RecipeDefinition TagOreRecipe(float outputAmount, float work = 0f)
    {
        return new RecipeDefinition
        {
            RecipeId = "test_tag_ore_extraction",
            DisplayName = "Test Extraction",
            Category = "extraction",
            WorkRequired = work,
            InputResources = new Dictionary<string, float>(),
            OutputResources = new Dictionary<string, float> { ["tag:ore"] = outputAmount },
        };
    }

    private static (Building b, ManufacturingBehavior mfg, ExtractionBehavior ext) BuildMine(
        Dictionary<string, float> cellResources)
    {
        var def = new BuildingDefinition
        {
            IdName = "test_mine",
            DisplayName = "Test Mine",
            WorkRequired = 0f,
        };
        var building = new Building();
        building.ApplyDefinition(def);
        building.SetPlacement(MakeCell(cellResources), null);

        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        building.Behaviors.Add(mfg);

        var ext = new ExtractionBehavior();
        ext.Configure(new Dictionary<string, object> { ["default_recipe"] = "surface_mine" });
        ext.OnAttach(building);
        building.Behaviors.Add(ext);

        // Set ActiveRecipeId so RebuildAvailableDeposits can find the recipe
        building.ActiveRecipeId = "surface_mine";

        // Ensure RecipeDatabase is loaded so LookupRecipe works
        var rdb = RecipeDatabase.Instance;
        if (!rdb.IsLoaded) rdb.LoadData();

        mfg.OnRegister();
        ext.OnRegister();
        return (building, mfg, ext);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TagOreRecipe_ResolvesToHighestAbundanceDeposit_ScaledByWeight()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (_, mfg, _) = BuildMine(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.8f,
            ["copper_ore"] = 0.3f,
        });

        var recipe = TagOreRecipe(10f);
        mfg.StartCycle(recipe, productionSpeed: 1f);

        AssertThat(mfg.ResolvedTagOutputs.ContainsKey("tag:ore")).IsTrue();
        AssertThat(mfg.ResolvedTagOutputs["tag:ore"]).IsEqual("iron_ore");
        AssertThat(mfg.ExpectedOutputs.ContainsKey("iron_ore")).IsTrue();
        AssertThat(mfg.ExpectedOutputs["iron_ore"]).IsEqual(10f * 0.8f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TagOreRecipe_FullCycle_DepositsScaledQuantity()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (building, mfg, _) = BuildMine(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.5f,
        });

        var recipe = TagOreRecipe(10f, work: 0f);
        mfg.StartCycle(recipe, productionSpeed: 1f);
        AssertThat(mfg.State).IsEqual(ManufacturingState.Manufacturing);

        mfg.OnManufactureTick(0f, building);

        AssertThat(building.OutputStorage.GetQuantity("iron_ore")).IsEqual(5);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TagOreRecipe_SwapDepositMix_PicksDifferentMaterial()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        var (building, mfg, ext) = BuildMine(new Dictionary<string, float>
        {
            ["iron_ore"] = 0.2f,
            ["copper_ore"] = 0.9f,
        });

        var recipe = TagOreRecipe(5f);
        mfg.StartCycle(recipe, productionSpeed: 1f);

        AssertThat(mfg.ResolvedTagOutputs["tag:ore"]).IsEqual("copper_ore");
        AssertThat(mfg.ExpectedOutputs["copper_ore"]).IsEqual(5f * 0.9f);
    }
}
