#if DEBUG
using System.Collections.Generic;
using Godot;
using GdUnit4;
using DeveloperTools.Common;
using DeveloperTools.ShipEditor;
using static GdUnit4.Assertions;

namespace Tests.DeveloperTools.ShipEditor;

[TestSuite]
public class ShipEditorTest
{
    private const string TempDir = "user://test_ship_editor";

    [TestCase]
    [RequireGodotRuntime]
    public void Module_Instantiates_WithModuleName()
    {
        var scene = GD.Load<PackedScene>("res://DeveloperTools/ShipEditor/ShipEditorModule.tscn");
        var module = scene.Instantiate<ShipEditorModule>();
        AssertThat(module.ModuleName).IsEqual("Ships");
        module.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WriteThenLoad_RoundTripsFields()
    {
        ResetTempDir();

        var model = new ShipEditorModel(TempDir);
        model.AddCategory("Cargo");
        model.AddShip("Cargo", new ShipEditorModel.ShipEditEntry
        {
            Name = "Test_Freighter",
            DryMass = 500,
            CargoCapacity = 1000,
            FuelCapacity = 200,
            EngineCategory = "Chemical",
            WorkRequired = 15,
            RequiredResources = new List<EditorResourceAmount>
            {
                new() { ResourceId = "Steel", Amount = 200 },
                new() { ResourceId = "Electronics", Amount = 50 },
            },
            Icon = new EditorIcon { BasePath = "res://Assets/Icons/Ships/freighter/test" },
        });

        ShipEditorYamlIO.WriteAllCategories(TempDir,
            new Dictionary<string, ShipEditorModel.ShipCategoryData>(model.Categories));

        var reloaded = new ShipEditorModel(TempDir);
        reloaded.LoadFromDisk();

        AssertThat(reloaded.Categories.ContainsKey("Cargo")).IsTrue();
        var ships = reloaded.Categories["Cargo"].Ships;
        AssertThat(ships.Count).IsEqual(1);
        var s = ships[0];
        AssertThat(s.Name).IsEqual("Test_Freighter");
        AssertThat(s.DryMass).IsEqual(500f);
        AssertThat(s.CargoCapacity).IsEqual(1000f);
        AssertThat(s.EngineCategory).IsEqual("Chemical");
        AssertThat(s.WorkRequired).IsEqual(15f);
        AssertThat(s.RequiredResources.Count).IsEqual(2);
        AssertThat(s.Icon.BasePath).IsEqual("res://Assets/Icons/Ships/freighter/test");

        ResetTempDir();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Validate_FlagsDuplicateNames()
    {
        ResetTempDir();
        var model = new ShipEditorModel(TempDir);
        model.AddCategory("A");
        model.AddCategory("B");
        model.AddShip("A", new ShipEditorModel.ShipEditEntry { Name = "Dup" });
        model.AddShip("B", new ShipEditorModel.ShipEditEntry { Name = "Dup" });

        var errors = model.Validate();
        AssertThat(errors.Find(e => e.Contains("Duplicate ship name 'Dup'"))).IsNotNull();
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
