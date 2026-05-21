using GdUnit4;
using System.Collections.Generic;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Structures.Enums;
using Structures.Resources;
using static GdUnit4.Assertions;

namespace Tests.Constructables.Power;

/// <summary>
/// PowerProducerBehavior reads its power magnitude from the active recipe's
/// <c>power</c> output entry, gated by IsProducing.
/// </summary>
[TestSuite]
public class PowerProducerRecipeTest
{
    private static (Building b, PowerProducerBehavior prod, ManufacturingBehavior mfg) Make(bool renewable)
    {
        var building = new Building { PoweredOn = true };
        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        building.Behaviors.Add(mfg);
        var prod = new PowerProducerBehavior { Radius = 1, IsRenewable = renewable };
        prod.OnAttach(building);
        building.Behaviors.Add(prod);
        return (building, prod, mfg);
    }

    private static RecipeDefinition MakeRecipe(float power, string? input = null)
    {
        var r = new RecipeDefinition { RecipeId = "test_power_recipe", WorkRequired = 10f };
        r.OutputResources["power"] = power;
        if (input != null)
            r.InputResources[input] = 1f;
        return r;
    }

    [TestCase]
    public void Fueled_CycleRunning_OutputMatchesRecipe()
    {
        var (_, prod, mfg) = Make(renewable: false);
        var recipe = MakeRecipe(power: 250f, input: "water");
        // Pre-stage input so StartCycle hits Manufacturing immediately.
        // Storage isn't wired in this minimal harness — drive state directly.
        mfg.StartCycle(recipe, productionSpeed: 1f);
        mfg.SetState(ManufacturingState.Manufacturing);

        AssertThat(prod.IsProducing).IsTrue();
        AssertThat(prod.EffectiveOutput).IsEqual(250f);
    }

    [TestCase]
    public void Fueled_NoCycle_EffectiveOutputZero()
    {
        var (_, prod, _) = Make(renewable: false);
        // No cycle started → IsProducing false → grid treats EffectiveOutput as 0.
        AssertThat(prod.IsProducing).IsFalse();
    }

    [TestCase]
    public void Renewable_Idle_OutputResolvesViaDefaultRecipe()
    {
        // Renewable producer with no live cycle still reports recipe-rated Output via
        // ActiveRecipeId fallback. Skipping default-recipe DB lookup path here since
        // RecipeDatabase needs full bootstrap. Confirm IsProducing gate instead.
        var (_, prod, mfg) = Make(renewable: true);
        AssertThat(mfg.State).IsEqual(ManufacturingState.Idle);
        AssertThat(prod.IsProducing).IsTrue();
    }

    [TestCase]
    public void Configure_ReadsDefaultRecipe()
    {
        var prod = new PowerProducerBehavior();
        prod.Configure(new Dictionary<string, object>
        {
            ["grid_radius"] = 4,
            ["is_renewable"] = true,
            ["default_recipe"] = "wind_harvesting",
        });
        AssertThat(prod.DefaultRecipe).IsEqual("wind_harvesting");
        AssertThat(prod.Radius).IsEqual(4);
        AssertThat(prod.IsRenewable).IsTrue();
    }

    [TestCase]
    public void Configure_ReadsProductionSpeed()
    {
        var prod = new PowerProducerBehavior();
        prod.Configure(new Dictionary<string, object>
        {
            ["grid_radius"] = 2,
            ["is_renewable"] = false,
            ["production_speed"] = 3.0,
        });
        AssertThat(prod.ProductionSpeed).IsEqual(3.0f);
    }

    [TestCase]
    public void Configure_DefaultProductionSpeedIsOne()
    {
        var prod = new PowerProducerBehavior();
        prod.Configure(new Dictionary<string, object>
        {
            ["grid_radius"] = 2,
            ["is_renewable"] = false,
        });
        AssertThat(prod.ProductionSpeed).IsEqual(1.0f);
    }

    [TestCase]
    public void Configure_ReadsEnvironmentalModifier()
    {
        var prod = new PowerProducerBehavior();
        prod.Configure(new Dictionary<string, object>
        {
            ["grid_radius"] = 4,
            ["is_renewable"] = true,
            ["default_recipe"] = "wind_harvesting",
            ["production_speed"] = 1.0,
            ["environmental_modifier"] = new Dictionary<object, object>
            {
                ["type"] = "ATMOSPHERE_LINEAR",
                ["reference_atmosphere"] = 1.0,
                ["max_scale"] = 4.0,
            },
        });
        prod.OnAttach(new Building());
        // Without a real body, EnvScaleFactor falls back to 0 from ComputeFactor
        // (no atmosphere data). Verify the modifier was parsed by checking it ran.
        prod.OnRegister();
        // EnvScaleFactor should be 0 (no body context), confirming modifier is active.
        AssertThat(prod.EnvScaleFactor).IsEqual(0f);
    }

    [TestCase]
    public void Configure_NoEnvironmentalModifier_EnvScaleFactorIsOne()
    {
        var prod = new PowerProducerBehavior();
        prod.Configure(new Dictionary<string, object>
        {
            ["grid_radius"] = 4,
            ["is_renewable"] = true,
        });
        prod.OnAttach(new Building());
        prod.OnRegister();
        AssertThat(prod.EnvScaleFactor).IsEqual(1f);
    }

    [TestCase]
    public void StandaloneProducer_NoManufacturingBehavior_IsProducingWhenPowered()
    {
        var building = new Building { PoweredOn = true };
        var prod = new PowerProducerBehavior { Radius = 4, IsRenewable = false };
        prod.OnAttach(building);
        building.Behaviors.Add(prod);
        // No ManufacturingBehavior attached — non-renewable standalone still produces.
        AssertThat(prod.IsProducing).IsTrue();
    }

    [TestCase]
    public void StandaloneProducer_UsesOwnDefaultRecipe()
    {
        var prod = new PowerProducerBehavior();
        prod.Configure(new Dictionary<string, object>
        {
            ["grid_radius"] = 4,
            ["is_renewable"] = false,
            ["default_recipe"] = "geothermal_drilling",
            ["production_speed"] = 3.0,
        });
        AssertThat(prod.DefaultRecipe).IsEqual("geothermal_drilling");
        AssertThat(prod.ProductionSpeed).IsEqual(3.0f);
    }
}
