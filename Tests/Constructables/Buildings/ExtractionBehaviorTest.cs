using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Constructables.Tick;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
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

    private static (Building b, ManufacturingBehavior mfg, ExtractionBehavior ext) MakeMine(
        Dictionary<string, float> cellResources, int extractTypes, float ratePerTick, float workPerCycle)
    {
        var def = new BuildingDefinition
        {
            IdName = "test_mine",
            DisplayName = "Test Mine",
            WorkRequired = 0f,
        };
        // Storage capacity now derived from recipe / extraction outputs; no fixed slot amounts.

        var building = new Building();
        building.ApplyDefinition(def);
        building.SetPlacement(MakeCellWithResources(cellResources), null);

        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        building.Behaviors.Add(mfg);

        var ext = new ExtractionBehavior
        {
            ExtractTypes = extractTypes,
            RatePerTick = ratePerTick,
            WorkPerCycle = workPerCycle,
        };
        ext.OnAttach(building);
        building.Behaviors.Add(ext);
        ext.OnRegister(); // builds synthetic recipe from cell mix

        return (building, mfg, ext);
    }

    [TestCase]
    public void SyntheticRecipe_BuiltFromCellMix_TopByAbundance()
    {
        var (_, _, ext) = MakeMine(
            new Dictionary<string, float> { ["iron_ore"] = 0.7f, ["coal"] = 0.5f, ["stone"] = 0.2f },
            extractTypes: 2,
            ratePerTick: 10f,
            workPerCycle: 1f);

        AssertThat(ext.SyntheticRecipe).IsNotNull();
        AssertThat(ext.SyntheticRecipe!.OutputResources.Count).IsEqual(2);
        AssertThat(ext.SyntheticRecipe.OutputResources.ContainsKey("iron_ore")).IsTrue();
        AssertThat(ext.SyntheticRecipe.OutputResources.ContainsKey("coal")).IsTrue();
        AssertThat(ext.SyntheticRecipe.OutputResources["iron_ore"]).IsEqual(10f);
        AssertThat(ext.SyntheticRecipe.InputResources.Count).IsEqual(0);
    }

    [TestCase]
    public void SyntheticRecipe_EmptyCell_LeavesRecipeNull()
    {
        var (_, _, ext) = MakeMine(
            new Dictionary<string, float>(),
            extractTypes: 2, ratePerTick: 10f, workPerCycle: 1f);

        AssertThat(ext.SyntheticRecipe).IsNull();
    }

    [TestCase]
    public void Extraction_ProducesOutputAfterWorkRequired()
    {
        var engine = ManufactureTickEngine.CreateForTesting();
        try
        {
            var (building, mfg, _) = MakeMine(
                new Dictionary<string, float> { ["iron_ore"] = 1f },
                extractTypes: 1,
                ratePerTick: 5f,
                workPerCycle: 1f);

            engine.Register(building);

            // Tick once: drains Register, ticks → TryStart picks synthetic recipe → Manufacturing
            engine.SingleTickForTesting();
            AssertThat(mfg.State).IsEqual(ManufacturingState.Manufacturing);

            // Tick repeatedly until WorkProgress accumulates ≥ WorkRequired (1f).
            // Engine TickDelta = 1/60s, so ~60 ticks needed.
            for (int i = 0; i < 70; i++)
                engine.SingleTickForTesting();

            AssertThat(building.OutputStorage.GetQuantity("iron_ore")).IsGreater(0f);
        }
        finally { engine.Stop(); }
    }

    [TestCase]
    public void Extraction_ExtractTypesEqualsTwo_ProducesTwoResources()
    {
        var (_, _, ext) = MakeMine(
            new Dictionary<string, float> { ["iron_ore"] = 0.6f, ["coal"] = 0.4f },
            extractTypes: 2, ratePerTick: 5f, workPerCycle: 1f);

        AssertThat(ext.SyntheticRecipe!.OutputResources.Count).IsEqual(2);
    }
}
