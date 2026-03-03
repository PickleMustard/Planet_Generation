using System;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using ProceduralGeneration.MeshGeneration.ResourceGeneration;
using Structures.Resources;

namespace Tests.ResourceGeneration;

[TestSuite]
public class ResourceDatabaseTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void DatabaseLoading()
    {
        var db = ResourceDatabase.Instance;
        AssertThat(db).IsNotNull();

        var resources = db.GetAllResources();
        AssertThat(resources).IsNotNull();
        AssertThat(resources.Count).IsGreater(0);

        AssertThat(db.ValidateResourceExists("iron_ore")).IsTrue();
        AssertThat(db.ValidateResourceExists("water_ice")).IsTrue();
        AssertThat(db.ValidateResourceExists("nonexistent_resource")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DefinitionParsing()
    {
        var db = ResourceDatabase.Instance;

        AssertThat(db.TryGetResource("iron_ore", out var ironOre)).IsTrue();
        AssertThat(ironOre.IdName).IsEqual("iron_ore");
        AssertThat(ironOre.ResourceType).IsEqual("ore");
        AssertThat(ironOre.DisplayColor).IsNotEqual(Colors.White);
        AssertThat(ironOre.BiomeAffinity).IsNotNull();
        AssertThat(ironOre.BiomeAffinity.Count).IsGreater(0);
        AssertThat(ironOre.MinElevation).IsGreaterEqual(0f);
        AssertThat(ironOre.MaxElevation).IsLessEqual(1f);

        AssertThat(db.TryGetResource("water", out var water)).IsTrue();
        AssertThat(water.ResourceType).IsEqual("fuel");
    }

    [TestCase]
    public void DepositCreation()
    {
        var deposit = new ResourceDeposit();
        AssertThat(deposit.ResourceId).IsEqual(string.Empty);
        AssertThat(deposit.Abundance).IsEqual(0f);
        AssertThat(deposit.Accessibility).IsEqual(1f);

        var deposit2 = new ResourceDeposit("iron_ore", 0.75f, 0.5f);
        AssertThat(deposit2.ResourceId).IsEqual("iron_ore");
        AssertThat(deposit2.Abundance).IsEqual(0.75f);
        AssertThat(deposit2.Accessibility).IsEqual(0.5f);

        var yield = deposit2.GetEffectiveYield();
        AssertThat(yield).IsEqual(0.375f);

        var clampedDeposit = new ResourceDeposit("test", -0.5f, 1.5f);
        AssertThat(clampedDeposit.Abundance).IsEqual(0f);
        AssertThat(clampedDeposit.Accessibility).IsEqual(1f);
    }

    [TestCase]
    public void ValidationErrorHandling()
    {
        var notFoundError = ResourceValidationError.ResourceNotFound("fake_resource", "TestBody");
        AssertThat(notFoundError.ResourceId).IsEqual("fake_resource");
        AssertThat(notFoundError.BodyConfigName).IsEqual("TestBody");
        AssertThat(notFoundError.Message).Contains("fake_resource");

        var dupError = ResourceValidationError.DuplicateResource("iron_ore");
        AssertThat(dupError.Message).Contains("Duplicate");

        var invalidError = ResourceValidationError.InvalidResourceDefinition("test", "missing field");
        AssertThat(invalidError.Message).Contains("Invalid resource definition");
    }
}
