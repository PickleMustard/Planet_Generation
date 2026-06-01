using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using static GdUnit4.Assertions;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;

namespace Tests.DataLoading;

/// <summary>
/// Custom test database for retry logic testing
/// </summary>
public class RetryTestDatabase : ILoadableDatabase
{
    public string DatabaseName { get; }
    public bool IsLoaded { get; private set; }
    public float LoadProgress { get; private set; }

    public event Action<string>? OnLoadStarted;
    public event Action<string, bool>? OnLoadCompleted;
    public event Action<string, float>? OnLoadProgressChanged;

    private readonly float _loadDurationMs;
    private int _loadAttempts;
    private readonly Action<int> _onAttempt;

    public RetryTestDatabase(string name, float loadDurationMs, Action<int> onAttempt)
    {
        DatabaseName = name ?? throw new ArgumentNullException(nameof(name));
        _loadDurationMs = loadDurationMs;
        _onAttempt = onAttempt;
        IsLoaded = false;
        LoadProgress = 0f;
    }

    public void LoadData()
    {
        _loadAttempts++;
        _onAttempt?.Invoke(_loadAttempts);

        OnLoadStarted?.Invoke(DatabaseName);

        if (_loadAttempts == 1)
        {
            // First attempt fails
            IsLoaded = false;
            LoadProgress = 0f;
            OnLoadCompleted?.Invoke(DatabaseName, false);
            throw new Exception("First attempt failed");
        }
        else
        {
            // Subsequent attempts succeed
            IsLoaded = true;
            LoadProgress = 1.0f;
            OnLoadProgressChanged?.Invoke(DatabaseName, 1.0f);
            OnLoadCompleted?.Invoke(DatabaseName, true);
        }
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

[TestSuite]
public class DatabaseIntegrationTest
{
    private TestDatabase? _testDb1;
    private TestDatabase? _testDb2;
    private TestDatabase? _failingDb;

    [Before]
    public void Before()
    {
        // Create test databases for integration testing
        _testDb1 = new TestDatabase("IntegrationTestDB1", 50f);
        _testDb2 = new TestDatabase("IntegrationTestDB2", 100f);
        _failingDb = new TestDatabase("FailingDB", 50f, true);
    }

    [After]
    public void After()
    {
        _testDb1?.Unload();
        _testDb2?.Unload();
        _failingDb?.Unload();
    }

    [TestCase]
    public void DatabaseLoadingFlow_Sequential()
    {
        // Test sequential database loading
        AssertThat(_testDb1!.IsLoaded).IsFalse();
        AssertThat(_testDb2!.IsLoaded).IsFalse();

        // Load first database
        _testDb1.LoadData();
        AssertThat(_testDb1.IsLoaded).IsTrue();
        AssertThat(_testDb1.LoadProgress).IsEqual(1.0f);
        AssertThat(_testDb2.IsLoaded).IsFalse();

        // Load second database
        _testDb2.LoadData();
        AssertThat(_testDb2.IsLoaded).IsTrue();
        AssertThat(_testDb2.LoadProgress).IsEqual(1.0f);
    }

    [TestCase]
    public void DatabaseLoadingFlow_ConcurrentViaWorkPackages()
    {
        // Test concurrent loading using WorkPackageBuilder
        var builder = new WorkPackageBuilder()
            .WithName("ConcurrentDatabaseLoad")
            .AddStep("Load_DB1", () => _testDb1!.LoadData())
            .AddStep("Load_DB2", () => _testDb2!.LoadData());

        var package = builder.Build();
        AssertThat(package).IsNotNull();
        AssertThat(package.TotalSteps).IsEqual(2);

        // Execute first step
        int result1 = package.ExecuteNextStep();
        AssertThat(result1).IsEqual(0); // Success
        AssertThat(_testDb1!.IsLoaded).IsTrue();
        AssertThat(_testDb2!.IsLoaded).IsFalse();

        // Execute second step
        int result2 = package.ExecuteNextStep();
        AssertThat(result2).IsEqual(0); // Success
        AssertThat(_testDb2.IsLoaded).IsTrue();
        AssertThat(package.IsComplete).IsTrue();
    }

    [TestCase]
    public void DatabaseLoadingFlow_MixedSuccessFailure()
    {
        // Test loading flow with both successful and failing databases
        var builder = new WorkPackageBuilder()
            .WithName("MixedSuccessFailure")
            .AddStep("Load_Success1", () => _testDb1!.LoadData())
            .AddStep("Load_Failure", () => _failingDb!.LoadData())
            .AddStep("Load_Success2", () => _testDb2!.LoadData());

        var package = builder.Build();

        // First step should succeed
        int result1 = package.ExecuteNextStep();
        AssertThat(result1).IsEqual(0);
        AssertThat(_testDb1!.IsLoaded).IsTrue();

        // Second step should fail
        int result2 = package.ExecuteNextStep();
        AssertThat(result2).IsNotEqual(0); // Non-zero indicates failure
        AssertThat(_failingDb!.IsLoaded).IsFalse();

        // Third step should still execute
        int result3 = package.ExecuteNextStep();
        AssertThat(result3).IsEqual(0);
        AssertThat(_testDb2!.IsLoaded).IsTrue();

        AssertThat(package.IsComplete).IsTrue();
    }

    [TestCase]
    public void DatabaseAccess_IntegrationWithTestDatabases()
    {
        // Load databases first
        _testDb1!.LoadData();
        _testDb2!.LoadData();

        // Test DatabaseAccess helper methods
        // Note: These tests only work when DatabaseLoadManager is initialized
        // For integration tests without Godot, we test the logic through direct calls

        // Simulate what DatabaseAccess would check
        AssertThat(_testDb1.IsLoaded).IsTrue();
        AssertThat(_testDb2.IsLoaded).IsTrue();

        // Test accessing data (simulating DatabaseAccess.GetDatabase)
        AssertThat(() => _testDb1.SimulateDataAccess()).IsNotThrown();
        AssertThat(() => _testDb2.SimulateDataAccess()).IsNotThrown();
    }

    [TestCase]
    public void DatabaseUnload_Integration()
    {
        // Load databases
        _testDb1!.LoadData();
        _testDb2!.LoadData();

        AssertThat(_testDb1.IsLoaded).IsTrue();
        AssertThat(_testDb2.IsLoaded).IsTrue();

        // Unload first database
        _testDb1.Unload();
        AssertThat(_testDb1.IsLoaded).IsFalse();
        AssertThat(_testDb1.LoadProgress).IsEqual(0f);

        // Second database should still be loaded
        AssertThat(_testDb2.IsLoaded).IsTrue();

        // Attempting to access unloaded database should throw
        AssertThat(() => _testDb1.SimulateDataAccess())
            .Throws<DatabaseNotLoadedException>();

        // Load it again
        _testDb1.LoadData();
        AssertThat(_testDb1.IsLoaded).IsTrue();
    }

    [TestCase]
    public void DatabaseEvents_Integration()
    {
        // Track events
        var events = new List<string>();
        float lastProgress = 0f;

        _testDb1!.OnLoadStarted += (name) => events.Add($"Started: {name}");
        _testDb1.OnLoadProgressChanged += (name, progress) =>
        {
            events.Add($"Progress: {name} - {progress}");
            lastProgress = progress;
        };
        _testDb1.OnLoadCompleted += (name, success) => events.Add($"Completed: {name} - {success}");

        // Load database
        _testDb1.LoadData();

        // Verify events were fired
        AssertThat(events).Contains("Started: IntegrationTestDB1");
        AssertThat(events).Contains("Completed: IntegrationTestDB1 - True");

        // Should have progress events (at least one)
        var progressEvents = events.Where(e => e.StartsWith("Progress:")).ToList();
        AssertThat(progressEvents.Count).IsGreater(0);

        // Final progress should be 1.0
        AssertThat(lastProgress).IsEqual(1.0f);
    }

    [TestCase]
    public void DatabaseErrorHandling_Integration()
    {
        // Test error handling in loading flow
        var errorEvents = new List<string>();

        _failingDb!.OnLoadCompleted += (name, success) =>
        {
            if (!success)
                errorEvents.Add($"Failed: {name}");
        };

        // Attempt to load failing database
        AssertThat(() => _failingDb.LoadData())
            .Throws<DatabaseLoadFailedException>();

        AssertThat(_failingDb.IsLoaded).IsFalse();
        AssertThat(_failingDb.LoadProgress).IsEqual(0f);
        AssertThat(errorEvents).Contains("Failed: FailingDB");
    }

    [TestCase]
    public void WorkPackageRetryLogic_Integration()
    {
        // Create a custom test database class for retry testing
        int loadAttempts = 0;
        var retryDb = new RetryTestDatabase("RetryDB", 10f, (attempt) => loadAttempts = attempt);

        var builder = new WorkPackageBuilder()
            .WithName("RetryTest")
            .WithMaxRetries(2)
            .AddStep("Load_RetryDB", () => retryDb.LoadData());

        var package = builder.Build();

        // First execution should fail but not throw due to retry logic
        int result1 = package.ExecuteNextStep();
        AssertThat(result1).IsNotEqual(0); // Failed

        // Package should not be complete (has retries)
        AssertThat(package.IsComplete).IsFalse();

        // Retry should succeed
        int result2 = package.ExecuteNextStep();
        AssertThat(result2).IsEqual(0); // Success

        AssertThat(package.IsComplete).IsTrue();
        AssertThat(loadAttempts).IsEqual(2);
    }

    [TestCase]
    public void DatabaseDependencies_SimulatedIntegration()
    {
        // Simulate database dependency loading
        var dependentDb1 = new TestDatabase("DependentDB1", 50f);
        var dependentDb2 = new TestDatabase("DependentDB2", 50f);
        var mainDb = new TestDatabase("MainDB", 100f);

        // Create a loading chain: dependent databases must load before main
        var builder = new WorkPackageBuilder()
            .WithName("DependencyChain")
            .AddStep("Load_Dependent1", () => dependentDb1.LoadData())
            .AddStep("Load_Dependent2", () => dependentDb2.LoadData())
            .AddStep("Load_Main", () => mainDb.LoadData());

        var package = builder.Build();

        // Execute all steps
        while (!package.IsComplete)
        {
            package.ExecuteNextStep();
        }

        // Verify all databases are loaded
        AssertThat(dependentDb1.IsLoaded).IsTrue();
        AssertThat(dependentDb2.IsLoaded).IsTrue();
        AssertThat(mainDb.IsLoaded).IsTrue();

        // Cleanup
        dependentDb1.Unload();
        dependentDb2.Unload();
        mainDb.Unload();
    }
}