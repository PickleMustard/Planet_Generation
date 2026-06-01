using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;

namespace Tests.DataLoading;

[TestSuite]
public class DatabaseStressTest
{
    private const int EXTREME_DATABASE_COUNT = 100;
    private const int CONCURRENT_THREAD_COUNT = 20;
    private const long STRESS_TEST_TIMEOUT_MS = 30000; // 30 seconds max

    [TestCase]
    public void StressTest_ManyDatabases_ConcurrentLoading()
    {
        var databases = new ConcurrentBag<TestDatabase>();
        var loadedCount = 0;
        var lockObj = new object();

        var stopwatch = Stopwatch.StartNew();

        // Create and load many databases concurrently
        Parallel.For(0, EXTREME_DATABASE_COUNT, new ParallelOptions
        {
            MaxDegreeOfParallelism = CONCURRENT_THREAD_COUNT
        }, i =>
        {
            var db = new TestDatabase($"StressDB_{i}", 10f); // Fast load
            databases.Add(db);

            db.LoadData();

            lock (lockObj)
            {
                loadedCount++;
            }
        });

        stopwatch.Stop();

        GD.Print($"Stress test: Loaded {loadedCount} databases in {stopwatch.ElapsedMilliseconds}ms " +
                $"({EXTREME_DATABASE_COUNT / (stopwatch.ElapsedMilliseconds / 1000.0):F2} databases/second)");

        // Verify all loaded
        AssertThat(loadedCount).IsEqual(EXTREME_DATABASE_COUNT);
        foreach (var db in databases)
        {
            AssertThat(db.IsLoaded).IsTrue();
        }

        // Should complete within timeout
        AssertThat(stopwatch.ElapsedMilliseconds).IsLessEqual(STRESS_TEST_TIMEOUT_MS);

        // Cleanup
        Parallel.ForEach(databases, db => db.Unload());
    }

    [TestCase]
    public void StressTest_MixedLoadTimes_Concurrent()
    {
        const int dbCount = 50;
        var random = new Random();
        var databases = new List<TestDatabase>();
        var results = new ConcurrentDictionary<string, bool>();

        // Create databases with random load times (10-500ms)
        for (int i = 0; i < dbCount; i++)
        {
            var loadTime = random.Next(10, 500);
            databases.Add(new TestDatabase($"MixedTimeDB_{i}", loadTime));
        }

        var stopwatch = Stopwatch.StartNew();

        // Load all concurrently
        Parallel.ForEach(databases, db =>
        {
            try
            {
                db.LoadData();
                results[db.DatabaseName] = true;
            }
            catch
            {
                results[db.DatabaseName] = false;
            }
        });

        stopwatch.Stop();

        // Count successes
        int successCount = results.Count(kv => kv.Value);

        GD.Print($"Mixed load times: {successCount}/{dbCount} successful in {stopwatch.ElapsedMilliseconds}ms");

        // All should succeed (no simulated failures)
        AssertThat(successCount).IsEqual(dbCount);

        // Should complete reasonably
        AssertThat(stopwatch.ElapsedMilliseconds).IsLessEqual(5000); // 5 seconds max

        // Cleanup
        foreach (var db in databases)
        {
            db.Unload();
        }
    }

    [TestCase]
    public void StressTest_WorkPackage_HeavyParallelism()
    {
        const int packageCount = 20;
        const int stepsPerPackage = 10;

        var packages = new List<WorkPackage>();
        var completedSteps = new ConcurrentBag<int>();

        // Create many work packages
        for (int p = 0; p < packageCount; p++)
        {
            var builder = new WorkPackageBuilder()
                .WithName($"HeavyPackage_{p}");

            for (int s = 0; s < stepsPerPackage; s++)
            {
                int packageId = p;
                int stepId = s;
                builder.AddStep($"Step_{s}", () =>
                {
                    // Simulate some work
                    Task.Delay(1).Wait();
                    completedSteps.Add(packageId * 1000 + stepId);
                });
            }

            packages.Add(builder.Build());
        }

        var stopwatch = Stopwatch.StartNew();

        // Execute all packages in parallel
        Parallel.ForEach(packages, package =>
        {
            while (!package.IsComplete)
            {
                package.ExecuteNextStep();
            }
        });

        stopwatch.Stop();

        int totalSteps = packageCount * stepsPerPackage;

        GD.Print($"Heavy parallelism: {totalSteps} steps across {packageCount} packages in {stopwatch.ElapsedMilliseconds}ms");

        AssertThat(completedSteps.Count).IsEqual(totalSteps);
        AssertThat(stopwatch.ElapsedMilliseconds).IsLessEqual(STRESS_TEST_TIMEOUT_MS);
    }

    [TestCase]
    public void StressTest_Memory_RepeatedLoadUnload()
    {
        const int iterations = 1000;
        long initialMemory = GC.GetTotalMemory(true);

        var memorySamples = new List<long>();

        for (int i = 0; i < iterations; i++)
        {
            // Create, load, and unload database
            var db = new TestDatabase($"MemoryTestDB_{i}", 1f);
            db.LoadData();
            db.Unload();

            // Sample memory every 100 iterations
            if (i % 100 == 0)
            {
                memorySamples.Add(GC.GetTotalMemory(false));
            }
        }

        // Force final GC
        GC.Collect();
        GC.WaitForPendingFinalizers();

        long finalMemory = GC.GetTotalMemory(true);
        long memoryIncrease = finalMemory - initialMemory;

        GD.Print($"Memory stress test: {iterations} iterations, memory change: {memoryIncrease} bytes");
        GD.Print($"Memory samples: {string.Join(", ", memorySamples.Select(m => m.ToString()))}");

        // Memory should not grow unbounded (allow some overhead for GC)
        AssertThat(memoryIncrease).IsLessEqual(10 * 1024 * 1024); // 10MB max increase
    }

    [TestCase]
    public void StressTest_EventHandlers_ManySubscribers()
    {
        const int subscriberCount = 100;
        var db = new TestDatabase("EventStressDB", 50f);

        var eventCounts = new ConcurrentBag<int>();

        // Add many subscribers
        for (int i = 0; i < subscriberCount; i++)
        {
            int subscriberId = i;
            int count = 0;

            db.OnLoadStarted += (name) => count++;
            db.OnLoadProgressChanged += (name, progress) => count++;
            db.OnLoadCompleted += (name, success) =>
            {
                count++;
                eventCounts.Add(count);
            };
        }

        var stopwatch = Stopwatch.StartNew();
        db.LoadData();
        stopwatch.Stop();

        GD.Print($"Event stress: {subscriberCount} subscribers, load time: {stopwatch.ElapsedMilliseconds}ms");

        // Each subscriber should have received multiple events
        AssertThat(eventCounts.Count).IsEqual(subscriberCount);

        // Each count should be at least 3 (start, progress, complete)
        foreach (var count in eventCounts)
        {
            AssertThat(count).IsGreaterEqual(3);
        }

        // Should still complete in reasonable time
        AssertThat(stopwatch.ElapsedMilliseconds).IsLessEqual(1000); // 1 second max
    }

    [TestCase]
    public void StressTest_ConcurrentAccess_WhileLoading()
    {
        const int accessorCount = 20;
        var db = new TestDatabase("ConcurrentAccessDB", 200f); // Slow load

        var accessResults = new ConcurrentBag<bool>();
        var accessExceptions = new ConcurrentBag<Exception>();

        // Start loading
        var loadTask = Task.Run(() => db.LoadData());

        // Many threads try to access while loading
        Parallel.For(0, accessorCount, i =>
        {
            try
            {
                db.SimulateDataAccess();
                accessResults.Add(true);
            }
            catch (DatabaseNotLoadedException ex)
            {
                accessResults.Add(false);
                accessExceptions.Add(ex);
            }
            catch (Exception ex)
            {
                accessExceptions.Add(ex);
            }
        });

        // Wait for load to complete
        loadTask.Wait();

        // Now all accesses should succeed
        for (int i = 0; i < 10; i++)
        {
            AssertThat(() => db.SimulateDataAccess()).IsNotThrown();
        }

        GD.Print($"Concurrent access: {accessResults.Count(r => r)} successful during load, " +
                $"{accessResults.Count(r => !r)} failed (expected)");

        // Most should have failed (database not loaded yet)
        AssertThat(accessResults.Count(r => !r)).IsGreater(0);

        // All failures should be DatabaseNotLoadedException
        foreach (var ex in accessExceptions)
        {
            AssertThat(ex).IsInstanceOf<DatabaseNotLoadedException>();
        }
    }

    [TestCase]
    public void StressTest_LargeWorkPackage_ComplexDependencies()
    {
        const int stepCount = 1000;
        var builder = new WorkPackageBuilder()
            .WithName("MegaPackage");

        int currentValue = 0;
        var lockObj = new object();

        // Create complex dependency chain
        for (int i = 0; i < stepCount; i++)
        {
            int stepNumber = i;
            builder.AddStep($"Step_{i}", () =>
            {
                // Each step depends on previous via shared variable
                lock (lockObj)
                {
                    currentValue = stepNumber;
                    // Simulate some work
                    Task.Delay(0).Wait();
                }
            });
        }

        var package = builder.Build();
        var stopwatch = Stopwatch.StartNew();

        // Execute sequentially (dependencies prevent parallel execution)
        while (!package.IsComplete)
        {
            package.ExecuteNextStep();
        }

        stopwatch.Stop();

        GD.Print($"Large package: {stepCount} steps in {stopwatch.ElapsedMilliseconds}ms");

        AssertThat(currentValue).IsEqual(stepCount - 1);
        AssertThat(package.IsComplete).IsTrue();
        AssertThat(stopwatch.ElapsedMilliseconds).IsLessEqual(STRESS_TEST_TIMEOUT_MS);
    }

    [TestCase]
    public void StressTest_RapidCreateDestroy_Databases()
    {
        const int cycles = 500;
        var exceptions = new ConcurrentBag<Exception>();

        var stopwatch = Stopwatch.StartNew();

        Parallel.For(0, cycles, i =>
        {
            try
            {
                // Rapid create, load, unload
                var db = new TestDatabase($"RapidDB_{i}", 0f); // Instant load
                db.LoadData();

                // Access data
                db.SimulateDataAccess();

                // Unload
                db.Unload();

                // Verify unloaded
                AssertThat(db.IsLoaded).IsFalse();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        stopwatch.Stop();

        GD.Print($"Rapid create/destroy: {cycles} cycles in {stopwatch.ElapsedMilliseconds}ms, " +
                $"{exceptions.Count} exceptions");

        // Should have no exceptions
        AssertThat(exceptions).IsEmpty();
        AssertThat(stopwatch.ElapsedMilliseconds).IsLessEqual(STRESS_TEST_TIMEOUT_MS);
    }
}