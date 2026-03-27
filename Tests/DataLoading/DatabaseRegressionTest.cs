using System;
using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using static GdUnit4.Assertions;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;

namespace Tests.DataLoading;

[TestSuite]
public class DatabaseRegressionTest
{
    [TestCase]
    public void DatabaseInterface_BackwardCompatibility_Properties()
    {
        // Test that all ILoadableDatabase implementers have required properties
        var testDb = new TestDatabase("RegressionTestDB");
        
        // Required properties should exist and be accessible
        AssertThat(testDb.DatabaseName).IsEqual("RegressionTestDB");
        AssertThat(testDb.IsLoaded).IsFalse();
        AssertThat(testDb.LoadProgress).IsEqual(0f);
        
        // Events should be nullable (not required to have subscribers)
        // We can subscribe to them to verify they can be used
        AssertThat(() => testDb.OnLoadStarted += (name) => { }).IsNotThrown();
        AssertThat(() => testDb.OnLoadCompleted += (name, success) => { }).IsNotThrown();
        AssertThat(() => testDb.OnLoadProgressChanged += (name, progress) => { }).IsNotThrown();
    }

    [TestCase]
    public void DatabaseInterface_BackwardCompatibility_Methods()
    {
        var testDb = new TestDatabase("MethodTestDB");
        
        // All interface methods should be callable
        AssertThat(() => testDb.LoadData()).IsNotThrown();
        AssertThat(testDb.IsLoaded).IsTrue();
        
        AssertThat(() => testDb.Unload()).IsNotThrown();
        AssertThat(testDb.IsLoaded).IsFalse();
        
        var package = testDb.CreateLoadPackage();
        AssertThat(package).IsNotNull();
        AssertThat(package.Name).Contains("Load_MethodTestDB");
    }

    [TestCase]
    public void DatabaseNotLoadedException_BackwardCompatibility()
    {
        // Old code accessing unloaded database should throw DatabaseNotLoadedException
        var db = new TestDatabase("OldCodeTestDB");
        
        // Simulate old code trying to access database
        AssertThat(() => db.SimulateDataAccess())
            .Throws<DatabaseNotLoadedException>()
            .WithProperty("DatabaseName", "OldCodeTestDB");
            
        // After loading, access should work
        db.LoadData();
        AssertThat(() => db.SimulateDataAccess()).IsNotThrown();
    }

    [TestCase]
    public void DatabaseAccess_Helper_BackwardCompatibility()
    {
        // DatabaseAccess helper methods should handle null/empty cases gracefully
        
        // Without DatabaseLoadManager initialized
        AssertThat(DatabaseAccess.IsDatabaseLoaded("AnyDB")).IsFalse();
        AssertThat(DatabaseAccess.GetDatabaseProgress("AnyDB")).IsEqual(-1f);
        AssertThat(DatabaseAccess.GetRegisteredDatabaseNames()).IsEmpty();
        
        // Validation methods
        AssertThat(DatabaseAccess.ValidateAllDatabasesLoaded()).IsTrue(); // Empty list
        AssertThat(DatabaseAccess.ValidateAllDatabasesLoaded("NonExistent")).IsFalse();
        AssertThat(DatabaseAccess.ValidateAnyDatabaseLoaded()).IsFalse(); // Empty list
        AssertThat(DatabaseAccess.ValidateAnyDatabaseLoaded("NonExistent")).IsFalse();
    }

    [TestCase]
    public void MixedInitializationStates_Regression()
    {
        // Test various initialization states
        
        // 1. Fresh database
        var db1 = new TestDatabase("FreshDB");
        AssertThat(db1.IsLoaded).IsFalse();
        AssertThat(db1.LoadProgress).IsEqual(0f);
        
        // 2. Loaded database
        var db2 = new TestDatabase("LoadedDB");
        db2.LoadData();
        AssertThat(db2.IsLoaded).IsTrue();
        AssertThat(db2.LoadProgress).IsEqual(1.0f);
        
        // 3. Unloaded database
        var db3 = new TestDatabase("UnloadedDB");
        db3.LoadData();
        db3.Unload();
        AssertThat(db3.IsLoaded).IsFalse();
        AssertThat(db3.LoadProgress).IsEqual(0f);
        
        // 4. Partially loaded (in progress) - simulate by creating custom db
        var partialDb = new PartialLoadTestDatabase("PartialDB");
        AssertThat(partialDb.IsLoaded).IsFalse();
        AssertThat(partialDb.LoadProgress).IsEqual(0.5f); // Set to 50% in constructor
    }

    [TestCase]
    public void EventSubscription_Regression()
    {
        // Test that event subscriptions work correctly
        
        var db = new TestDatabase("EventTestDB");
        int startCount = 0;
        int progressCount = 0;
        int completeCount = 0;
        
        db.OnLoadStarted += (name) => startCount++;
        db.OnLoadProgressChanged += (name, progress) => progressCount++;
        db.OnLoadCompleted += (name, success) => completeCount++;
        
        // Load database
        db.LoadData();
        
        // Verify events fired
        AssertThat(startCount).IsEqual(1);
        AssertThat(progressCount).IsGreater(0); // Multiple progress updates
        AssertThat(completeCount).IsEqual(1);
        
        // Unload (no events expected for unload)
        db.Unload();
        
        // Load again - events should fire again
        db.LoadData();
        
        AssertThat(startCount).IsEqual(2);
        AssertThat(completeCount).IsEqual(2);
    }

    [TestCase]
    public void WorkPackageIntegration_Regression()
    {
        // Test that WorkPackage integration still works
        
        var db = new TestDatabase("WorkPackageDB");
        var package = db.CreateLoadPackage();
        
        AssertThat(package).IsNotNull();
        AssertThat(package.Name).Contains("Load_WorkPackageDB");
        AssertThat(package.TotalSteps).IsEqual(1);
        AssertThat(package.IsComplete).IsFalse();
        
        // Execute package
        int result = package.ExecuteNextStep();
        AssertThat(result).IsEqual(0); // Success
        AssertThat(package.IsComplete).IsTrue();
        AssertThat(db.IsLoaded).IsTrue();
    }

    [TestCase]
    public void DatabaseNaming_Regression()
    {
        // Test database naming conventions and constraints
        
        // Valid names
        AssertThat(() => new TestDatabase("ValidName")).IsNotThrown();
        AssertThat(() => new TestDatabase("valid_name_123")).IsNotThrown();
        AssertThat(() => new TestDatabase("Valid-Name")).IsNotThrown();
        
        // Database name should be preserved
        var db = new TestDatabase("TestDatabaseName");
        AssertThat(db.DatabaseName).IsEqual("TestDatabaseName");
        
        // Database name should be used in WorkPackage name
        var package = db.CreateLoadPackage();
        AssertThat(package.Name).Contains("TestDatabaseName");
    }

    [TestCase]
    public void ProgressTracking_Regression()
    {
        // Test progress tracking works correctly
        
        var db = new TestDatabase("ProgressDB", 50f); // Medium load time
        
        List<float> progressValues = new();
        db.OnLoadProgressChanged += (name, progress) =>
        {
            progressValues.Add(progress);
        };
        
        db.LoadData();
        
        // Should have multiple progress updates
        AssertThat(progressValues.Count).IsGreater(0);
        
        // Progress should start at > 0 and end at 1.0
        AssertThat(progressValues.First()).IsGreater(0f);
        AssertThat(progressValues.Last()).IsEqual(1.0f);
        
        // Progress should be monotonic increasing
        for (int i = 1; i < progressValues.Count; i++)
        {
            AssertThat(progressValues[i]).IsGreaterEqual(progressValues[i - 1]);
        }
    }

    [TestCase]
    public void ErrorHandling_Regression()
    {
        // Test error handling scenarios
        
        // Failing database
        var failingDb = new TestDatabase("FailingDB", 10f, true);
        
        // Should throw on load
        AssertThat(() => failingDb.LoadData())
            .Throws<DatabaseLoadFailedException>();
            
        // State should be reset after failure
        AssertThat(failingDb.IsLoaded).IsFalse();
        AssertThat(failingDb.LoadProgress).IsEqual(0f);
        
        // Should still be able to unload after failure
        AssertThat(() => failingDb.Unload()).IsNotThrown();
    }

    [TestCase]
    public void ConcurrentAccess_Regression()
    {
        // Test concurrent access scenarios
        
        var db = new TestDatabase("ConcurrentDB", 100f); // Slow load
        
        // Start loading
        var loadTask = System.Threading.Tasks.Task.Run(() => db.LoadData());
        
        // Try to access while loading (should fail)
        AssertThat(() => db.SimulateDataAccess())
            .Throws<DatabaseNotLoadedException>();
            
        // Wait for load to complete
        loadTask.Wait();
        
        // Now should succeed
        AssertThat(() => db.SimulateDataAccess()).IsNotThrown();
        AssertThat(db.IsLoaded).IsTrue();
    }
}

/// <summary>
/// Test database that simulates partial load state
/// </summary>
public class PartialLoadTestDatabase : ILoadableDatabase
{
    public string DatabaseName { get; }
    public bool IsLoaded { get; private set; }
    public float LoadProgress { get; private set; }

    public event Action<string>? OnLoadStarted;
    public event Action<string, bool>? OnLoadCompleted;
    public event Action<string, float>? OnLoadProgressChanged;

    public PartialLoadTestDatabase(string name)
    {
        DatabaseName = name ?? throw new ArgumentNullException(nameof(name));
        IsLoaded = false;
        LoadProgress = 0.5f; // Simulate 50% loaded
    }

    public void LoadData()
    {
        OnLoadStarted?.Invoke(DatabaseName);
        
        // Simulate loading completion
        LoadProgress = 1.0f;
        OnLoadProgressChanged?.Invoke(DatabaseName, 1.0f);
        IsLoaded = true;
        OnLoadCompleted?.Invoke(DatabaseName, true);
    }

    public void Unload()
    {
        IsLoaded = false;
        LoadProgress = 0f;
    }

    public WorkPackage CreateLoadPackage()
    {
        var builder = new WorkPackageBuilder()
            .WithName($"Load_{DatabaseName}")
            .AddStep($"Load_Database_{DatabaseName}", () => LoadData());

        return builder.Build();
    }
}