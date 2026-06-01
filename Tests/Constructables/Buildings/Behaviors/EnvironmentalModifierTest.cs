using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Structures.Resources;

namespace Tests.Constructables.Buildings.Behaviors;

/// <summary>
/// Direct coverage for EnvironmentalModifier config parsing and ManufacturingBehavior's
/// cycle gate when env scale resolves to 0. Body-resolution paths (star distance /
/// atmosphere / vent) need a Godot scene tree, so those go through
/// ManufacturingBehavior.EnvScaleFactor with the modifier returning a known value.
/// </summary>
[TestSuite]
public class EnvironmentalModifierTest
{
    [TestCase]
    public void FromConfig_NoBlock_ReturnsNull()
    {
        var cfg = new Dictionary<string, object>();
        var mod = EnvironmentalModifier.FromConfig(cfg);
        AssertThat(mod == null).IsTrue();
    }

    [TestCase]
    public void FromConfig_ParsesStarDistance()
    {
        var cfg = new Dictionary<string, object>
        {
            ["environmental_modifier"] = new Dictionary<object, object>
            {
                ["type"] = "STAR_DISTANCE_INVERSE_SQUARE",
                ["reference_distance"] = 1500.0,
                ["max_scale"] = 4.0,
            }
        };
        var mod = EnvironmentalModifier.FromConfig(cfg);
        AssertThat(mod != null).IsTrue();
        AssertThat(mod!.Type).IsEqual(EnvironmentalModifier.ModifierType.StarDistanceInverseSquare);
        AssertThat(mod.ReferenceDistance).IsEqual(1500f);
        AssertThat(mod.MaxScale).IsEqual(4f);
    }

    [TestCase]
    public void FromConfig_ParsesAtmosphereLinear()
    {
        var cfg = new Dictionary<string, object>
        {
            ["environmental_modifier"] = new Dictionary<object, object>
            {
                ["type"] = "ATMOSPHERE_LINEAR",
                ["reference_atmosphere"] = 1.0,
                ["max_scale"] = 2.0,
            }
        };
        var mod = EnvironmentalModifier.FromConfig(cfg);
        AssertThat(mod!.Type).IsEqual(EnvironmentalModifier.ModifierType.AtmosphereLinear);
        AssertThat(mod.ReferenceAtmosphere).IsEqual(1f);
        AssertThat(mod.MaxScale).IsEqual(2f);
    }

    [TestCase]
    public void FromConfig_ParsesVentBinary()
    {
        var cfg = new Dictionary<string, object>
        {
            ["environmental_modifier"] = new Dictionary<object, object>
            {
                ["type"] = "VENT_PRESENCE_BINARY",
            }
        };
        var mod = EnvironmentalModifier.FromConfig(cfg);
        AssertThat(mod!.Type).IsEqual(EnvironmentalModifier.ModifierType.VentPresenceBinary);
    }

    [TestCase]
    public void ComputeFactor_None_ReturnsOne()
    {
        var mod = new EnvironmentalModifier { Type = EnvironmentalModifier.ModifierType.None };
        AssertThat(mod.ComputeFactor(new Building())).IsEqual(1f);
    }

    [TestCase]
    public void ComputeFactor_StarDistance_NoOwner_ReturnsZero()
    {
        var mod = new EnvironmentalModifier
        {
            Type = EnvironmentalModifier.ModifierType.StarDistanceInverseSquare,
            ReferenceDistance = 1000f,
            MaxScale = 4f,
        };
        AssertThat(mod.ComputeFactor(null)).IsEqual(0f);
    }

    [TestCase]
    public void ComputeFactor_VentBinary_NoCells_ReturnsZero()
    {
        var mod = new EnvironmentalModifier
        {
            Type = EnvironmentalModifier.ModifierType.VentPresenceBinary,
        };
        AssertThat(mod.ComputeFactor(new Building())).IsEqual(0f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Manufacturing_RefusesCycle_WhenEnvScaleIsZero()
    {
        var building = new Building();
        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        building.Behaviors.Add(mfg);
        // Force factor to 0 via VENT_PRESENCE_BINARY with no occupied cells.
        mfg.Configure(new Dictionary<string, object>
        {
            ["environmental_modifier"] = new Dictionary<object, object>
            {
                ["type"] = "VENT_PRESENCE_BINARY",
            }
        });
        mfg.OnRegister();
        AssertThat(mfg.EnvScaleFactor).IsEqual(0f);

        var recipe = new RecipeDefinition
        {
            RecipeId = "test_blocked",
            WorkRequired = 0.1f,
            InputResources = new Dictionary<string, float>(),
            OutputResources = new Dictionary<string, float> { ["iron"] = 1f },
        };
        mfg.StartCycle(recipe, productionSpeed: 1f);

        AssertThat(mfg.State).IsEqual(global::Structures.Enums.ManufacturingState.Idle);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Manufacturing_ScalesLiteralOutput_ByEnvFactor()
    {
        // No env modifier configured => factor 1.0, output unchanged.
        var building = new Building();
        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        building.Behaviors.Add(mfg);
        mfg.Configure(new Dictionary<string, object>());
        mfg.OnRegister();
        AssertThat(mfg.EnvScaleFactor).IsEqual(1f);

        var recipe = new RecipeDefinition
        {
            RecipeId = "test_scaled",
            WorkRequired = 0.1f,
            InputResources = new Dictionary<string, float>(),
            OutputResources = new Dictionary<string, float> { ["iron"] = 10f },
        };
        mfg.StartCycle(recipe, productionSpeed: 1f);
        AssertThat(mfg.ExpectedOutputs["iron"]).IsEqual(10f);
    }
}
