using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Resources;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;

namespace Tests.DataLoading;

[TestSuite]
public class DatabaseDependencyTest
{
    #region Pure Unit Tests (no Godot runtime)

    [TestCase]
    public void TestDatabase_RespectsCustomDependencies()
    {
        var dbNone = new TestDatabase("NoDeps", 0f);
        AssertThat(dbNone.Dependencies.Count).IsEqual(0);

        var dbWithDeps = new TestDatabase("WithDeps", 0f, dependencies: new[] { "A", "B" });
        AssertThat(dbWithDeps.Dependencies.Count).IsEqual(2);
        AssertThat(dbWithDeps.Dependencies[0]).IsEqual("A");
        AssertThat(dbWithDeps.Dependencies[1]).IsEqual("B");
    }

    [TestCase]
    public void ILoadableDatabase_DefaultDependencies_IsEmpty()
    {
        // By default, the interface default returns Array.Empty<string>()
        // and TestDatabase with no explicit dependencies also returns empty.
        ILoadableDatabase db = new TestDatabase("NoDeps", 0f);
        AssertThat(db.Dependencies.Count).IsEqual(0);
    }

    #endregion

    #region Topological Sort Reflection Tests (require Godot runtime)

    [TestCase]
    [RequireGodotRuntime]
    public void DatabaseLoadManager_TopologicalSort_LinearChain()
    {
        var dbA = new TestDatabase("DB_A", dependencies: Array.Empty<string>());
        var dbB = new TestDatabase("DB_B", dependencies: new[] { "DB_A" });
        var dbC = new TestDatabase("DB_C", dependencies: new[] { "DB_B" });

        var manager = new DatabaseLoadManager();
        var method = typeof(DatabaseLoadManager).GetMethod(
            "TopologicalSort",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        AssertThat(method).IsNotNull();

        var phases = (List<List<ILoadableDatabase>>?)method?.Invoke(
            manager,
            new object[] { new List<ILoadableDatabase> { dbA, dbB, dbC } }
        );

        AssertThat(phases).IsNotNull();
        AssertThat(phases!.Count).IsEqual(3);
        AssertThat(phases[0][0].DatabaseName).IsEqual("DB_A");
        AssertThat(phases[1][0].DatabaseName).IsEqual("DB_B");
        AssertThat(phases[2][0].DatabaseName).IsEqual("DB_C");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DatabaseLoadManager_TopologicalSort_DiamondGraph()
    {
        var dbA = new TestDatabase("DB_A", dependencies: Array.Empty<string>());
        var dbB = new TestDatabase("DB_B", dependencies: new[] { "DB_A" });
        var dbC = new TestDatabase("DB_C", dependencies: new[] { "DB_A" });
        var dbD = new TestDatabase("DB_D", dependencies: new[] { "DB_B", "DB_C" });

        var manager = new DatabaseLoadManager();
        var method = typeof(DatabaseLoadManager).GetMethod(
            "TopologicalSort",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        var phases = (List<List<ILoadableDatabase>>?)method?.Invoke(
            manager,
            new object[] { new List<ILoadableDatabase> { dbA, dbB, dbC, dbD } }
        );

        AssertThat(phases).IsNotNull();
        AssertThat(phases!.Count).IsEqual(3);
        AssertThat(phases[0][0].DatabaseName).IsEqual("DB_A");
        AssertThat(phases[1].Count).IsEqual(2);
        AssertThat(phases[2][0].DatabaseName).IsEqual("DB_D");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DatabaseLoadManager_TopologicalSort_CircularDependency()
    {
        var dbA = new TestDatabase("DB_A", dependencies: new[] { "DB_C" });
        var dbB = new TestDatabase("DB_B", dependencies: new[] { "DB_A" });
        var dbC = new TestDatabase("DB_C", dependencies: new[] { "DB_B" });

        var manager = new DatabaseLoadManager();
        var method = typeof(DatabaseLoadManager).GetMethod(
            "TopologicalSort",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        var phases = (List<List<ILoadableDatabase>>?)method?.Invoke(
            manager,
            new object[] { new List<ILoadableDatabase> { dbA, dbB, dbC } }
        );

        AssertThat(phases).IsNotNull();
        AssertThat(phases!.Count).IsEqual(1);
        AssertThat(phases[0].Count).IsEqual(3);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DatabaseLoadManager_TopologicalSort_IndependentDatabases()
    {
        var dbA = new TestDatabase("DB_A", dependencies: Array.Empty<string>());
        var dbB = new TestDatabase("DB_B", dependencies: Array.Empty<string>());
        var dbC = new TestDatabase("DB_C", dependencies: Array.Empty<string>());

        var manager = new DatabaseLoadManager();
        var method = typeof(DatabaseLoadManager).GetMethod(
            "TopologicalSort",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        var phases = (List<List<ILoadableDatabase>>?)method?.Invoke(
            manager,
            new object[] { new List<ILoadableDatabase> { dbA, dbB, dbC } }
        );

        AssertThat(phases).IsNotNull();
        AssertThat(phases!.Count).IsEqual(1);
        AssertThat(phases[0].Count).IsEqual(3);
    }

    #endregion

    #region Real Database Dependency Contract

    [TestCase]
    [RequireGodotRuntime]
    public void ResourceGenerationConfigDatabase_DeclaresResourceDatabaseDependency()
    {
        var db = ResourceGenerationConfigDatabase.Instance;
        AssertThat(db.Dependencies).IsNotNull();
        AssertThat(db.Dependencies.Contains("ResourceDatabase")).IsTrue();
    }

    #endregion
}
