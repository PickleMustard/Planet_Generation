using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Resources;

namespace Tests.Structures.Resources;

[TestSuite]
public class ResourceDatabaseTagIndexTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void GetResourcesByTag_ReturnsOresForOreTag()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var ores = db.GetResourcesByTag("ore");
        AssertThat(ores.Count).IsGreater(0);

        bool foundIronOre = false;
        bool foundCopperOre = false;
        foreach (var def in ores)
        {
            if (def.IdName == "iron_ore") foundIronOre = true;
            if (def.IdName == "copper_ore") foundCopperOre = true;
        }
        AssertThat(foundIronOre).IsTrue();
        AssertThat(foundCopperOre).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetResourcesByTag_EmptyForUnknownTag()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var result = db.GetResourcesByTag("definitely_not_a_real_tag_xyz");
        AssertThat(result.Count).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryResolveTaggedOutput_FindsIronIngotForIronMaterial()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        bool resolved = db.TryResolveTaggedOutput("metal", "material:iron", out var def);
        AssertThat(resolved).IsTrue();
        AssertThat(def).IsNotNull();
        AssertThat(def!.IdName).IsEqual("iron");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryResolveTaggedOutput_FindsCopperIngotForCopperMaterial()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        bool resolved = db.TryResolveTaggedOutput("metal", "material:copper", out var def);
        AssertThat(resolved).IsTrue();
        AssertThat(def!.IdName).IsEqual("copper");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryResolveTaggedOutput_FailsForUnknownDiscriminator()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        bool resolved = db.TryResolveTaggedOutput("metal", "material:nonexistent_xyz", out var def);
        AssertThat(resolved).IsFalse();
        AssertThat(def).IsNull();
    }

    // ── Tier-filtered query tests ──────────────────────────────────────────

    [TestCase]
    [RequireGodotRuntime]
    public void GetResourcesByTagAndMaxTier_ReturnsOnlyTier0Ores()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var tier0Ores = db.GetResourcesByTagAndMaxTier("ore", 0);
        AssertThat(tier0Ores.Count).IsGreater(0);

        foreach (var def in tier0Ores)
            AssertThat(def.ResourceTier).IsLessEqual(0);

        // iron_ore and copper_ore are tier 0 — must be present
        bool foundIronOre = false;
        bool foundCopperOre = false;
        foreach (var def in tier0Ores)
        {
            if (def.IdName == "iron_ore") foundIronOre = true;
            if (def.IdName == "copper_ore") foundCopperOre = true;
        }
        AssertThat(foundIronOre).IsTrue();
        AssertThat(foundCopperOre).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetResourcesByTagAndMaxTier_ExcludesHighTierResources()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var tier0Ores = db.GetResourcesByTagAndMaxTier("ore", 0);
        var allOres = db.GetResourcesByTag("ore");

        // Tier-0 list should be strictly smaller than the full list
        AssertThat(tier0Ores.Count).IsLess(allOres.Count);

        // Uranium_ore (tier 1) must not appear in tier-0 results
        foreach (var def in tier0Ores)
            AssertThat(def.IdName != "uranium_ore").IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetResourcesByTagAndMaxTier_Tier1IncludesTier0And1()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        var tier1Ores = db.GetResourcesByTagAndMaxTier("ore", 1);
        AssertThat(tier1Ores.Count).IsGreater(0);

        foreach (var def in tier1Ores)
            AssertThat(def.ResourceTier).IsLessEqual(1);

        // uranium_ore (tier 1) must be present
        bool foundUranium = false;
        foreach (var def in tier1Ores)
        {
            if (def.IdName == "uranium_ore") foundUranium = true;
        }
        AssertThat(foundUranium).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryResolveTaggedOutput_WithMaxTier_RespectsTierCap()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        // iron (tier 0) should resolve at maxTier 0
        bool resolved = db.TryResolveTaggedOutput("metal", "material:iron", 0, out var def);
        AssertThat(resolved).IsTrue();
        AssertThat(def).IsNotNull();
        AssertThat(def!.IdName).IsEqual("iron");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryResolveTaggedOutput_WithMaxTier_BlocksHighTierOutput()
    {
        var db = ResourceDatabase.Instance;
        if (!db.IsLoaded)
            db.LoadData();

        // Find a high-tier metal (if one exists) and verify it's excluded at tier 0
        var allMetals = db.GetResourcesByTag("metal");
        foreach (var metalDef in allMetals)
        {
            if (metalDef.ResourceTier > 0)
            {
                // This high-tier metal's discriminator should NOT resolve at tier 0
                var discriminator = $"material:{metalDef.IdName}";
                bool resolved = db.TryResolveTaggedOutput("metal", discriminator, 0, out var _);
                AssertThat(resolved).IsFalse();
            }
        }
    }
}
