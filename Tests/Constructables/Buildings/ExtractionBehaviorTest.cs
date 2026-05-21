using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
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
}
