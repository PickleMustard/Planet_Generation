using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Resources;
using UtilityLibrary.DataLoading;

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
        
        // Database should not be loaded initially
        AssertThat(db.IsLoaded).IsFalse();
        
        // Load the database
        db.LoadData();
        AssertThat(db.IsLoaded).IsTrue();

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
        
        // Database should not be loaded initially
        AssertThat(db.IsLoaded).IsFalse();
        
        // Load the database
        db.LoadData();
        AssertThat(db.IsLoaded).IsTrue();

        AssertThat(db.TryGetResource("iron_ore", out var ironOre)).IsTrue();
        AssertThat(ironOre!.IdName).IsEqual("iron_ore");
        AssertThat(ironOre.ResourceType).IsEqual("ore");
        AssertThat(ironOre.DisplayColor).IsNotEqual(Colors.White);

        AssertThat(db.TryGetResource("water", out var water)).IsTrue();
        AssertThat(water!.ResourceType).IsEqual("fuel");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GeneratableResources()
    {
        var db = ResourceDatabase.Instance;
        
        // Database should not be loaded initially
        AssertThat(db.IsLoaded).IsFalse();
        
        // Load the database
        db.LoadData();
        AssertThat(db.IsLoaded).IsTrue();

        // Test that some resources are generatable
        AssertThat(db.IsResourceGeneratable("iron_ore")).IsTrue();
        AssertThat(db.IsResourceGeneratable("water")).IsTrue();
        
        // Test GetGeneratableResources
        var generatableResources = db.GetGeneratableResources();
        AssertThat(generatableResources).IsNotNull();
        AssertThat(generatableResources.Count).IsGreater(0);
        
        // All resources in generatableResources should have IsGeneratable = true
        foreach (var kvp in generatableResources)
        {
            AssertThat(kvp.Value.IsGeneratable).IsTrue();
        }
        
        // Test that non-generatable resources exist in GetAllResources but not in GetGeneratableResources
        var allResources = db.GetAllResources();
        AssertThat(allResources.Count).IsGreater(generatableResources.Count);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void UninitializedAccessThrows()
    {
        var db = ResourceDatabase.Instance;
        AssertThat(db).IsNotNull();
        
        // Database should not be loaded initially
        AssertThat(db.IsLoaded).IsFalse();
        
        // Attempting to access data should throw DatabaseNotLoadedException
        AssertThat(() => db.GetAllResources())
            .Throws<DatabaseNotLoadedException>();
            
        AssertThat(() => db.TryGetResource("iron_ore", out _))
            .Throws<DatabaseNotLoadedException>();
            
        AssertThat(() => db.ValidateResourceExists("iron_ore"))
            .Throws<DatabaseNotLoadedException>();
            
        AssertThat(() => db.GetResourceColor("iron_ore"))
            .Throws<DatabaseNotLoadedException>();
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
    
    [TestCase]
    [RequireGodotRuntime]
    public void WorkPackageCreation()
    {
        var db = ResourceDatabase.Instance;
        AssertThat(db).IsNotNull();
        
        var workPackage = db.CreateLoadPackage();
        AssertThat(workPackage).IsNotNull();
        AssertThat(workPackage.Name).Contains("Load_ResourceDatabase");
        AssertThat(workPackage.Steps).Count().IsEqual(1);
    }
}
