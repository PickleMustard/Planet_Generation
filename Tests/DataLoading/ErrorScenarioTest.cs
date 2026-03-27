using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using static GdUnit4.Assertions;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;

namespace Tests.DataLoading;

[TestSuite]
public class ErrorScenarioTest
{
    [TestCase]
    public void DatabaseNotLoadedException_AccessBeforeLoading()
    {
        var db = new TestDatabase("TestDB");
        
        // Should throw when accessing before loading
        AssertThat(() => db.SimulateDataAccess())
            .Throws<DatabaseNotLoadedException>()
            .WithProperty("DatabaseName", "TestDB");
    }

    [TestCase]
    public void DatabaseLoadFailedException_PropagatesCorrectly()
    {
        var failingDb = new TestDatabase("FailingDB", 50f, true);
        
        // Should throw DatabaseLoadFailedException when load fails
        AssertThat(() => failingDb.LoadData())
            .Throws<DatabaseLoadFailedException>()
            .WithProperty("DatabaseName", "FailingDB");
    }

    [TestCase]
    public void DatabaseLoadFailedException_ContainsInnerException()
    {
        var failingDb = new TestDatabase("FailingDB", 50f, true);
        
        try
        {
            failingDb.LoadData();
        }
        catch (DatabaseLoadFailedException ex)
        {
            AssertThat(ex.InnerException).IsNotNull();
            AssertThat(ex.InnerException!.Message).Contains("Simulated database load failure");
        }
    }

    [TestCase]
    public void WorkPackage_StepFailure_ContinuesToNextStep()
    {
        var successDb = new TestDatabase("SuccessDB", 10f);
        var failingDb = new TestDatabase("FailingDB", 10f, true);
        var anotherSuccessDb = new TestDatabase("AnotherSuccessDB", 10f);
        
        var builder = new WorkPackageBuilder()
            .WithName("MixedSuccessFailurePackage")
            .AddStep("Load_Success", () => successDb.LoadData())
            .AddStep("Load_Failure", () => failingDb.LoadData())
            .AddStep("Load_AnotherSuccess", () => anotherSuccessDb.LoadData());
            
        var package = builder.Build();
        
        // Execute all steps
        List<int> results = new();
        while (!package.IsComplete)
        {
            results.Add(package.ExecuteNextStep());
        }
        
        // Verify results: success (0), failure (non-zero), success (0)
        AssertThat(results.Count).IsEqual(3);
        AssertThat(results[0]).IsEqual(0); // First success
        AssertThat(results[1]).IsNotEqual(0); // Failure
        AssertThat(results[2]).IsEqual(0); // Second success
        
        // Verify database states
        AssertThat(successDb.IsLoaded).IsTrue();
        AssertThat(failingDb.IsLoaded).IsFalse();
        AssertThat(anotherSuccessDb.IsLoaded).IsTrue();
    }

    [TestCase]
    public void WorkPackage_MaxRetries_Exhaustion()
    {
        int attempts = 0;
        var alwaysFailingDb = new AlwaysFailingTestDatabase("AlwaysFails", (attempt) => attempts += attempt);
        
        var builder = new WorkPackageBuilder()
            .WithName("MaxRetriesTest")
            .WithMaxRetries(3)
            .AddStep("Load_AlwaysFails", () => alwaysFailingDb.LoadData());
            
        var package = builder.Build();
        
        // Execute step (will fail and retry)
        while (!package.IsComplete)
        {
            package.ExecuteNextStep();
        }
        
        // Should have attempted 4 times (1 initial + 3 retries)
        AssertThat(attempts).IsEqual(4);
        
        // Package should be complete (exhausted retries)
        AssertThat(package.IsComplete).IsTrue();
    }

    [TestCase]
    public void DatabaseEvents_ErrorScenarios()
    {
        var failingDb = new TestDatabase("EventTestDB", 10f, true);
        
        List<string> events = new();
        failingDb.OnLoadStarted += (name) => events.Add($"Started: {name}");
        failingDb.OnLoadCompleted += (name, success) => events.Add($"Completed: {name} - {success}");
        
        try
        {
            failingDb.LoadData();
        }
        catch
        {
            // Expected to throw
        }
        
        // Verify events were fired even on failure
        AssertThat(events).Contains("Started: EventTestDB");
        AssertThat(events).Contains("Completed: EventTestDB - False");
    }

    [TestCase]
    public void DatabaseUnload_AfterFailure()
    {
        var failingDb = new TestDatabase("UnloadTestDB", 10f, true);
        
        // Try to load (will fail)
        AssertThat(() => failingDb.LoadData())
            .Throws<DatabaseLoadFailedException>();
            
        // Should still be able to unload (no error)
        AssertThat(() => failingDb.Unload()).IsNotThrown();
        
        // State should be reset
        AssertThat(failingDb.IsLoaded).IsFalse();
        AssertThat(failingDb.LoadProgress).IsEqual(0f);
    }

    [TestCase]
    public void DatabaseAccess_NullOrEmptyDatabaseName()
    {
        // Test with TestDatabase (which validates in constructor)
        AssertThat(() => new TestDatabase(null!, 10f))
            .Throws<ArgumentNullException>();
            
        AssertThat(() => new TestDatabase("", 10f))
            .Throws<ArgumentNullException>();
    }

    [TestCase]
    public void WorkPackageBuilder_ErrorScenarios()
    {
        // Builder with no name should throw on Build
        var builder1 = new WorkPackageBuilder();
        AssertThat(() => builder1.Build())
            .Throws<InvalidOperationException>();
            
        // Builder with no steps should throw on Build
        var builder2 = new WorkPackageBuilder()
            .WithName("NoSteps");
        AssertThat(() => builder2.Build())
            .Throws<InvalidOperationException>();
            
        // Builder with invalid max retries
        AssertThat(() => new WorkPackageBuilder().WithMaxRetries(-1))
            .Throws<ArgumentOutOfRangeException>();
            
        AssertThat(() => new WorkPackageBuilder().WithMaxRetries(0))
            .Throws<ArgumentOutOfRangeException>();
    }

    [TestCase]
    public void ConcurrentDatabaseAccess_RaceConditionSimulation()
    {
        // Simulate concurrent access attempts
        var db = new TestDatabase("ConcurrentTestDB", 100f); // Slow load
        
        // Start loading in background
        Task loadTask = Task.Run(() => db.LoadData());
        
        // Immediately try to access (should fail)
        AssertThat(() => db.SimulateDataAccess())
            .Throws<DatabaseNotLoadedException>();
            
        // Wait for load to complete
        loadTask.Wait();
        
        // Now should succeed
        AssertThat(() => db.SimulateDataAccess()).IsNotThrown();
        AssertThat(db.IsLoaded).IsTrue();
    }

    [TestCase]
    public void DatabaseProgress_ResetOnFailure()
    {
        var failingDb = new TestDatabase("ProgressResetDB", 50f, true);
        
        // Track progress
        List<float> progressValues = new();
        failingDb.OnLoadProgressChanged += (name, progress) => progressValues.Add(progress);
        
        try
        {
            failingDb.LoadData();
        }
        catch
        {
            // Expected
        }
        
        // Progress should have been updated during load attempt
        AssertThat(progressValues.Count).IsGreater(0);
        
        // But final state should be 0
        AssertThat(failingDb.LoadProgress).IsEqual(0f);
    }
}

/// <summary>
/// Test database that always fails to load
/// </summary>
public class AlwaysFailingTestDatabase : ILoadableDatabase
{
    public string DatabaseName { get; }
    public bool IsLoaded { get; private set; }
    public float LoadProgress { get; private set; }

    public event Action<string>? OnLoadStarted;
    public event Action<string, bool>? OnLoadCompleted;
    public event Action<string, float>? OnLoadProgressChanged;

    private readonly Action<int> _onAttempt;

    public AlwaysFailingTestDatabase(string name, Action<int> onAttempt)
    {
        DatabaseName = name ?? throw new ArgumentNullException(nameof(name));
        _onAttempt = onAttempt;
        IsLoaded = false;
        LoadProgress = 0f;
    }

    public void LoadData()
    {
        _onAttempt?.Invoke(1); // Count this attempt
        OnLoadStarted?.Invoke(DatabaseName);
        
        // Always fail
        IsLoaded = false;
        LoadProgress = 0f;
        OnLoadCompleted?.Invoke(DatabaseName, false);
        throw new Exception($"Always failing database: {DatabaseName}");
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