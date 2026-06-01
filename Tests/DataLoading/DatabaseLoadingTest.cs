using System;
using System.Threading.Tasks;
using GdUnit4;
using static GdUnit4.Assertions;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;

namespace Tests.DataLoading;

[TestSuite]
public class DatabaseLoadingTest
{
    private TestDatabase? _testDatabase1;
    private TestDatabase? _testDatabase2;

    [Before]
    public void Before()
    {
        // Create test databases
        _testDatabase1 = new TestDatabase("TestDB1", 100f); // Fast load
        _testDatabase2 = new TestDatabase("TestDB2", 200f); // Slower load
    }

    [After]
    public void After()
    {
        _testDatabase1?.Unload();
        _testDatabase2?.Unload();
    }

    [TestCase]
    public void ILoadableDatabase_PropertiesAndEvents()
    {
        var db = new TestDatabase("TestDB", 100f);

        AssertThat(db.DatabaseName).IsEqual("TestDB");
        AssertThat(db.IsLoaded).IsFalse();
        AssertThat(db.LoadProgress).IsEqual(0f);

        // Test events
        bool loadStarted = false;
        bool loadCompleted = false;
        bool progressChanged = false;

        db.OnLoadStarted += (name) => loadStarted = true;
        db.OnLoadCompleted += (name, success) => loadCompleted = true;
        db.OnLoadProgressChanged += (name, progress) => progressChanged = true;

        // Load and check events
        db.LoadData();

        AssertThat(loadStarted).IsTrue();
        AssertThat(loadCompleted).IsTrue();
        AssertThat(progressChanged).IsTrue();
        AssertThat(db.IsLoaded).IsTrue();
        AssertThat(db.LoadProgress).IsEqual(1.0f);
    }

    [TestCase]
    public void DatabaseNotLoadedException_Constructor()
    {
        var ex = new DatabaseNotLoadedException("TestDB");

        AssertThat(ex.DatabaseName).IsEqual("TestDB");
        AssertThat(ex.Message).Contains("TestDB");
        AssertThat(ex.Message).Contains("not been loaded");
    }

    [TestCase]
    public void DatabaseNotLoadedException_ConstructorWithInnerException()
    {
        var innerEx = new Exception("Inner error");
        var ex = new DatabaseNotLoadedException("TestDB", innerEx);

        AssertThat(ex.DatabaseName).IsEqual("TestDB");
        AssertThat(ex.InnerException).IsEqual(innerEx);
        AssertThat(ex.Message).Contains("TestDB");
    }

    [TestCase]
    [ThrowsException(typeof(ArgumentNullException))]
    public void DatabaseNotLoadedException_ThrowsOnNullDatabaseName()
    {
        new DatabaseNotLoadedException(null!);
    }

    [TestCase]
    public void DatabaseLoadFailedException_Constructor()
    {
        var ex = new DatabaseLoadFailedException("TestDB", "Load failed");

        AssertThat(ex.DatabaseName).IsEqual("TestDB");
        AssertThat(ex.Message).Contains("TestDB");
        AssertThat(ex.Message).Contains("Load failed");
    }

    [TestCase]
    public void DatabaseNotRegisteredException_Constructor()
    {
        var ex = new DatabaseNotRegisteredException("TestDB");

        AssertThat(ex.DatabaseName).IsEqual("TestDB");
        AssertThat(ex.Message).Contains("TestDB");
        AssertThat(ex.Message).Contains("not registered");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DatabaseLoadManager_Registration()
    {
        // Note: DatabaseLoadManager is a singleton that requires Godot runtime
        // In a real test, we would need to instantiate it properly
        // For now, we test the logic through the interface

        var db = new TestDatabase("TestRegistration");

        // Test that database implements ILoadableDatabase correctly
        AssertThat(db).IsNotNull();
        AssertThat(db.DatabaseName).IsEqual("TestRegistration");

        // Test CreateLoadPackage method
        var package = db.CreateLoadPackage();
        AssertThat(package).IsNotNull();
        AssertThat(package.Name).Contains("TestRegistration");
        AssertThat(package.TotalSteps).IsEqual(1);
    }

    [TestCase]
    public void TestDatabase_CreateLoadPackage()
    {
        var db = new TestDatabase("TestPackage");
        var package = db.CreateLoadPackage();

        AssertThat(package).IsNotNull();
        AssertThat(package.Name).IsEqual("Load_TestPackage");
        AssertThat(package.TotalSteps).IsEqual(1);
        AssertThat(package.IsComplete).IsFalse();
    }

    [TestCase]
    [ThrowsException(typeof(DatabaseNotLoadedException))]
    public void TestDatabase_SimulateDataAccess_ThrowsWhenNotLoaded()
    {
        var db = new TestDatabase("TestAccess");

        // Should throw when not loaded
        db.SimulateDataAccess();
    }

    [TestCase]
    public void TestDatabase_LoadAsync_Success()
    {
        var db = new TestDatabase("TestLoadSuccess", 50f);

        AssertThat(db.IsLoaded).IsFalse();
        AssertThat(db.LoadProgress).IsEqual(0f);

        db.LoadData();

        AssertThat(db.IsLoaded).IsTrue();
        AssertThat(db.LoadProgress).IsEqual(1.0f);
    }

    [TestCase]
    [ThrowsException(typeof(DatabaseLoadFailedException))]
    public void TestDatabase_LoadAsync_Failure()
    {
        var db = new TestDatabase("TestLoadFailure", 50f, true);

        AssertThat(db.IsLoaded).IsFalse();
        AssertThat(db.LoadProgress).IsEqual(0f);

        db.LoadData();
    }

    [TestCase]
    public void TestDatabase_Unload()
    {
        var db = new TestDatabase("TestUnload");
        db.LoadData(); // Load first

        AssertThat(db.IsLoaded).IsTrue();

        db.Unload();

        AssertThat(db.IsLoaded).IsFalse();
        AssertThat(db.LoadProgress).IsEqual(0f);
    }

    [TestCase]
    public void DatabaseAccess_IsDatabaseLoaded_WithoutManager()
    {
        // Should return false when DatabaseLoadManager is not initialized
        AssertThat(DatabaseAccess.IsDatabaseLoaded("TestDB")).IsFalse();
    }

    [TestCase]
    public void DatabaseAccess_GetDatabaseProgress_WithoutManager()
    {
        // Should return -1 when DatabaseLoadManager is not initialized
        AssertThat(DatabaseAccess.GetDatabaseProgress("TestDB")).IsEqual(-1f);
    }

    [TestCase]
    public void DatabaseAccess_GetRegisteredDatabaseNames_WithoutManager()
    {
        // Should return empty list when DatabaseLoadManager is not initialized
        var names = DatabaseAccess.GetRegisteredDatabaseNames();
        AssertThat(names).IsEmpty();
    }

    [TestCase]
    public void DatabaseAccess_ValidateAllDatabasesLoaded_EmptyList()
    {
        // Should return true for empty list
        AssertThat(DatabaseAccess.ValidateAllDatabasesLoaded()).IsTrue();
    }

    [TestCase]
    public void DatabaseAccess_ValidateAnyDatabaseLoaded_EmptyList()
    {
        // Should return false for empty list
        AssertThat(DatabaseAccess.ValidateAnyDatabaseLoaded()).IsFalse();
    }

    [TestCase]
    public void DatabaseAccess_ValidateAllDatabasesLoaded_SingleNotLoaded()
    {
        // Should return false when database is not loaded
        AssertThat(DatabaseAccess.ValidateAllDatabasesLoaded("NonExistentDB")).IsFalse();
    }

    [TestCase]
    public void DatabaseAccess_ValidateAnyDatabaseLoaded_SingleNotLoaded()
    {
        // Should return false when database is not loaded
        AssertThat(DatabaseAccess.ValidateAnyDatabaseLoaded("NonExistentDB")).IsFalse();
    }

    [TestCase]
    public void WorkPackageIntegrationTest()
    {
        var db = new TestDatabase("WorkPackageTest", 100f);
        var package = db.CreateLoadPackage();

        AssertThat(package).IsNotNull();
        AssertThat(package.Name).IsEqual("Load_WorkPackageTest");
        AssertThat(package.Steps).IsNotNull();
        AssertThat(package.Steps.Count).IsEqual(1);

        // Execute the package
        int result = package.ExecuteNextStep();

        AssertThat(result).IsEqual(0); // Success code
        AssertThat(package.IsComplete).IsTrue();
        AssertThat(db.IsLoaded).IsTrue();
    }

    [TestCase]
    public void WorkPackageBuilder_Integration()
    {
        var db1 = new TestDatabase("BuilderTest1", 50f);
        var db2 = new TestDatabase("BuilderTest2", 50f);

        var builder = new WorkPackageBuilder()
            .WithName("TestBuilderPackage")
            .AddStep("Load_DB1", () => db1.LoadData())
            .AddStep("Load_DB2", () => db2.LoadData());

        var package = builder.Build();

        AssertThat(package).IsNotNull();
        AssertThat(package.Name).IsEqual("TestBuilderPackage");
        AssertThat(package.TotalSteps).IsEqual(2);

        // Execute first step
        int result1 = package.ExecuteNextStep();
        AssertThat(result1).IsEqual(0);
        AssertThat(db1.IsLoaded).IsTrue();
        AssertThat(db2.IsLoaded).IsFalse();
        AssertThat(package.IsComplete).IsFalse();

        // Execute second step
        int result2 = package.ExecuteNextStep();
        AssertThat(result2).IsEqual(0);
        AssertThat(db2.IsLoaded).IsTrue();
        AssertThat(package.IsComplete).IsTrue();
    }
}