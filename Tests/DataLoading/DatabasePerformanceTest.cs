using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;

namespace Tests.DataLoading;

[TestSuite]
public class DatabasePerformanceTest
{
    private const int PERFORMANCE_ITERATIONS = 50;
    private const int PARALLEL_DATABASE_COUNT = 10;
    private const long MAX_LOAD_TIME_MS = 5000; // 5 seconds max per test

    [TestCase]
    public void DatabaseLoadTime_SingleDatabase()
    {
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < PERFORMANCE_ITERATIONS; i++)
        {
            var db = new TestDatabase($"PerfDB_{i}", 10f); // Fast load
            db.LoadData();
            AssertThat(db.IsLoaded).IsTrue();
            db.Unload();
        }

        stopwatch.Stop();
        var averageTime = stopwatch.ElapsedMilliseconds / (double)PERFORMANCE_ITERATIONS;

        GD.Print($"Single database load average time: {averageTime:F2}ms over {PERFORMANCE_ITERATIONS} iterations");

        // Performance requirement: less than 100ms average
        AssertThat(averageTime).IsLessEqual(100.0);
    }

    [TestCase]
    public void DatabaseLoadTime_MultipleDatabases_Sequential()
    {
        var databases = new List<TestDatabase>();

        // Create multiple databases
        for (int i = 0; i < PARALLEL_DATABASE_COUNT; i++)
        {
            databases.Add(new TestDatabase($"SeqDB_{i}", 20f));
        }

        var stopwatch = Stopwatch.StartNew();

        // Load sequentially
        foreach (var db in databases)
        {
            db.LoadData();
            AssertThat(db.IsLoaded).IsTrue();
        }

        stopwatch.Stop();
        var totalTime = stopwatch.ElapsedMilliseconds;

        GD.Print($"Sequential load of {PARALLEL_DATABASE_COUNT} databases: {totalTime}ms");

        // Performance requirement: less than 5 seconds total
        AssertThat(totalTime).IsLessEqual(MAX_LOAD_TIME_MS);

        // Cleanup
        foreach (var db in databases)
        {
            db.Unload();
        }
    }

    [TestCase]
    public void DatabaseLoadTime_MultipleDatabases_WorkPackage()
    {
        var databases = new List<TestDatabase>();
        var builder = new WorkPackageBuilder()
            .WithName("PerformanceTestPackage");

        // Create multiple databases and add to work package
        for (int i = 0; i < PARALLEL_DATABASE_COUNT; i++)
        {
            var db = new TestDatabase($"WorkDB_{i}", 20f);
            databases.Add(db);
            builder.AddStep($"Load_DB_{i}", () => db.LoadData());
        }

        var package = builder.Build();
        var stopwatch = Stopwatch.StartNew();

        // Execute all steps
        while (!package.IsComplete)
        {
            package.ExecuteNextStep();
        }

        stopwatch.Stop();
        var totalTime = stopwatch.ElapsedMilliseconds;

        GD.Print($"WorkPackage load of {PARALLEL_DATABASE_COUNT} databases: {totalTime}ms");

        // Verify all loaded
        foreach (var db in databases)
        {
            AssertThat(db.IsLoaded).IsTrue();
        }

        // Performance requirement: less than 5 seconds total
        AssertThat(totalTime).IsLessEqual(MAX_LOAD_TIME_MS);

        // Cleanup
        foreach (var db in databases)
        {
            db.Unload();
        }
    }

    [TestCase]
    public void DatabaseMemoryUsage_DuringLoad()
    {
        // Measure memory before
        long initialMemory = GC.GetTotalMemory(true);

        var databases = new List<TestDatabase>();

        // Load multiple databases
        for (int i = 0; i < 5; i++)
        {
            var db = new TestDatabase($"MemDB_{i}", 10f);
            databases.Add(db);
            db.LoadData();
        }

        // Measure memory after load
        long memoryAfterLoad = GC.GetTotalMemory(true);
        long memoryIncrease = memoryAfterLoad - initialMemory;

        GD.Print($"Memory increase after loading 5 databases: {memoryIncrease} bytes");

        // Unload and measure memory
        foreach (var db in databases)
        {
            db.Unload();
        }

        // Force GC to clean up
        GC.Collect();
        GC.WaitForPendingFinalizers();

        long memoryAfterUnload = GC.GetTotalMemory(true);
        long memoryDecrease = memoryAfterLoad - memoryAfterUnload;

        GD.Print($"Memory decrease after unloading: {memoryDecrease} bytes");

        // Memory should decrease after unloading (not necessarily to initial level due to GC overhead)
        AssertThat(memoryDecrease).IsGreaterEqual(0);
    }

    [TestCase]
    public void DatabaseProgressUpdates_Frequency()
    {
        const int expectedProgressUpdates = 10; // TestDatabase uses 10 progress steps

        var db = new TestDatabase("ProgressPerfDB", 100f); // Slower load to measure progress
        var progressUpdates = new List<float>();

        db.OnLoadProgressChanged += (name, progress) =>
        {
            progressUpdates.Add(progress);
        };

        db.LoadData();

        GD.Print($"Progress updates received: {progressUpdates.Count}");

        // Should get at least expected updates (might get more due to timing)
        AssertThat(progressUpdates.Count).IsGreaterEqual(expectedProgressUpdates);

        // Progress should increase monotonically
        for (int i = 1; i < progressUpdates.Count; i++)
        {
            AssertThat(progressUpdates[i]).IsGreaterEqual(progressUpdates[i - 1]);
        }

        // Final progress should be 1.0
        AssertThat(progressUpdates.Last()).IsEqual(1.0f);
    }

    [TestCase]
    public void WorkPackageThroughput_ManySmallSteps()
    {
        const int stepCount = 100;
        var builder = new WorkPackageBuilder()
            .WithName("ThroughputTest");

        int completedSteps = 0;

        // Add many small steps
        for (int i = 0; i < stepCount; i++)
        {
            builder.AddStep($"Step_{i}", () => completedSteps++);
        }

        var package = builder.Build();
        var stopwatch = Stopwatch.StartNew();

        // Execute all steps
        while (!package.IsComplete)
        {
            package.ExecuteNextStep();
        }

        stopwatch.Stop();

        var stepsPerSecond = stepCount / (stopwatch.ElapsedMilliseconds / 1000.0);

        GD.Print($"WorkPackage throughput: {stepsPerSecond:F2} steps/second for {stepCount} steps");

        AssertThat(completedSteps).IsEqual(stepCount);

        // Performance requirement: at least 1000 steps/second
        AssertThat(stepsPerSecond).IsGreaterEqual(1000.0);
    }

    [TestCase]
    public void ConcurrentDatabaseSimulation_StressTest()
    {
        const int concurrentLoads = 5;
        var tasks = new List<Task>();
        var loadedCount = 0;
        var lockObj = new object();

        var stopwatch = Stopwatch.StartNew();

        // Start concurrent loads
        for (int i = 0; i < concurrentLoads; i++)
        {
            var taskIndex = i;
            tasks.Add(Task.Run(() =>
            {
                var db = new TestDatabase($"ConcurrentDB_{taskIndex}", 50f);
                db.LoadData();

                lock (lockObj)
                {
                    loadedCount++;
                }

                db.Unload();
            }));
        }

        // Wait for all to complete
        Task.WaitAll(tasks.ToArray());

        stopwatch.Stop();

        GD.Print($"Concurrent load of {concurrentLoads} databases: {stopwatch.ElapsedMilliseconds}ms");
        AssertThat(loadedCount).IsEqual(concurrentLoads);

        // Should complete in reasonable time (less than sum of individual loads)
        AssertThat(stopwatch.ElapsedMilliseconds).IsLessEqual(concurrentLoads * 100); // 100ms each max
    }

    [TestCase]
    public void DatabaseEventOverhead_Measurement()
    {
        const int eventSubscribers = 10;
        var db = new TestDatabase("EventOverheadDB", 10f);

        // Add multiple event subscribers
        var eventCounts = new int[eventSubscribers];
        for (int i = 0; i < eventSubscribers; i++)
        {
            int index = i; // Capture for closure
            db.OnLoadStarted += (name) => eventCounts[index]++;
            db.OnLoadProgressChanged += (name, progress) => eventCounts[index]++;
            db.OnLoadCompleted += (name, success) => eventCounts[index]++;
        }

        var stopwatch = Stopwatch.StartNew();
        db.LoadData();
        stopwatch.Stop();

        GD.Print($"Database load with {eventSubscribers} event subscribers: {stopwatch.ElapsedMilliseconds}ms");

        // Each subscriber should have received 3 events (started, progress changed, completed)
        foreach (var count in eventCounts)
        {
            // Progress changed might be called multiple times, so at least 3
            AssertThat(count).IsGreaterEqual(3);
        }

        // Event overhead should be minimal (less than 50ms)
        AssertThat(stopwatch.ElapsedMilliseconds).IsLessEqual(50);
    }

    [TestCase]
    public void WorkPackageCreationPerformance()
    {
        const int packageCount = 100;
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < packageCount; i++)
        {
            var builder = new WorkPackageBuilder()
                .WithName($"Package_{i}")
                .AddStep("Step1", () => { })
                .AddStep("Step2", () => { });

            var package = builder.Build();
            AssertThat(package).IsNotNull();
        }

        stopwatch.Stop();

        var packagesPerSecond = packageCount / (stopwatch.ElapsedMilliseconds / 1000.0);

        GD.Print($"WorkPackage creation rate: {packagesPerSecond:F2} packages/second");

        // Should be able to create at least 1000 packages/second
        AssertThat(packagesPerSecond).IsGreaterEqual(1000.0);
    }
}