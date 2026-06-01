#if DEBUG
using System.Collections.Generic;
using Godot;
using GdUnit4;
using DeveloperTools.Common;
using DeveloperTools.StationEditor;
using static GdUnit4.Assertions;

namespace Tests.DeveloperTools.StationEditor;

[TestSuite]
public class StationEditorTest
{
    private const string TempDir = "user://test_station_editor";

    [TestCase]
    [RequireGodotRuntime]
    public void Module_Instantiates_WithModuleName()
    {
        var scene = GD.Load<PackedScene>("res://DeveloperTools/StationEditor/StationEditorModule.tscn");
        var module = scene.Instantiate<StationEditorModule>();
        AssertThat(module.ModuleName).IsEqual("Stations");
        module.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WriteThenLoad_RoundTripsFullFidelity()
    {
        ResetTempDir();

        var model = new StationEditorModel(TempDir);
        model.AddCategory("Architect");

        var transferConfig = new Dictionary<string, object>
        {
            { "cargo_capacity", 500.0 },
            { "vehicle_speed", 50.0 },
            { "max_concurrent_transfers", 2 }
        };
        var slotFilters = new Dictionary<string, object>
        {
            { "category:ore", 3 },
            { "any", 5 }
        };

        model.AddStation("Architect", new StationEditorModel.StationEditEntry
        {
            Name = "Test_Architect",
            StationType = "Orbital_Architect",
            ConstructionTime = 45,
            Behaviors = new List<StationEditorModel.BehaviorConfigEdit>
            {
                new()
                {
                    BehaviorId = "StorageHubBehavior",
                    Config = new Dictionary<string, object>
                    {
                        { "storage_capacity", 10 },
                        { "slot_filters", slotFilters }
                    }
                },
                new()
                {
                    BehaviorId = "OrbitalConstructorBehavior",
                    Config = new Dictionary<string, object>
                    {
                        { "work_budget_per_tick", 1.0 },
                        { "regular_slots", 1 },
                        { "overtime_slots", 1 }
                    }
                },
                new()
                {
                    BehaviorId = "TransferHubBehavior",
                    Config = new Dictionary<string, object>
                    {
                        { "transfer_station", transferConfig }
                    }
                }
            },
            RequiredResources = new List<EditorResourceAmount>
            {
                new() { ResourceId = "Steel", Amount = 800 },
            },
        });

        StationEditorYamlIO.WriteAllCategories(TempDir,
            new Dictionary<string, StationEditorModel.StationCategoryData>(model.Categories));

        var reloaded = new StationEditorModel(TempDir);
        reloaded.LoadFromDisk();

        var stations = reloaded.Categories["Architect"].Stations;
        AssertThat(stations.Count).IsEqual(1);
        var s = stations[0];
        AssertThat(s.Name).IsEqual("Test_Architect");
        AssertThat(s.StationType).IsEqual("Orbital_Architect");
        AssertThat(s.Behaviors.Count).IsEqual(3);
        AssertThat(s.Behaviors[0].BehaviorId).IsEqual("StorageHubBehavior");
        AssertThat(s.Behaviors[1].BehaviorId).IsEqual("OrbitalConstructorBehavior");
        AssertThat(s.Behaviors[2].BehaviorId).IsEqual("TransferHubBehavior");

        // Verify StorageHubBehavior config persisted
        AssertThat(s.Behaviors[0].Config.ContainsKey("storage_capacity")).IsTrue();

        // Verify TransferHubBehavior config persisted
        AssertThat(s.Behaviors[2].Config.ContainsKey("transfer_station")).IsTrue();

        AssertThat(s.RequiredResources.Count).IsEqual(1);

        ResetTempDir();
    }

    private static void ResetTempDir()
    {
        if (DirAccess.DirExistsAbsolute(TempDir))
        {
            var dir = DirAccess.Open(TempDir);
            dir.ListDirBegin();
            string f = dir.GetNext();
            while (!string.IsNullOrEmpty(f)) { dir.Remove(f); f = dir.GetNext(); }
            dir.ListDirEnd();
        }
        else
        {
            DirAccess.MakeDirRecursiveAbsolute(TempDir);
        }
    }
}
#endif