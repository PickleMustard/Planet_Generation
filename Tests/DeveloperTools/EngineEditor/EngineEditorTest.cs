#if DEBUG
using System.Collections.Generic;
using Godot;
using GdUnit4;
using DeveloperTools.EngineEditor;
using static GdUnit4.Assertions;

namespace Tests.DeveloperTools.EngineEditor;

[TestSuite]
public class EngineEditorTest
{
    private const string TempDir = "user://test_engine_editor";

    [TestCase]
    [RequireGodotRuntime]
    public void Module_Instantiates_WithModuleName()
    {
        var scene = GD.Load<PackedScene>("res://DeveloperTools/EngineEditor/EngineEditorModule.tscn");
        var module = scene.Instantiate<EngineEditorModule>();
        AssertThat(module.ModuleName).IsEqual("Engines");
        module.QueueFree();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WriteThenLoad_RoundTripsFields()
    {
        ResetTempDir();

        var model = new EngineEditorModel(TempDir);
        model.AddCategory("Chemical");
        model.AddEngine("Chemical", new EngineEditorModel.EngineEditEntry
        {
            Name = "Chemical Rocket",
            SpecificImpulse = 450,
            Thrust = 5000,
            Description = "Traditional rocket engine",
        });

        EngineEditorYamlIO.WriteAllCategories(TempDir,
            new Dictionary<string, EngineEditorModel.EngineCategoryData>(model.Categories));

        var reloaded = new EngineEditorModel(TempDir);
        reloaded.LoadFromDisk();

        var engines = reloaded.Categories["Chemical"].Engines;
        AssertThat(engines.Count).IsEqual(1);
        var e = engines[0];
        AssertThat(e.Name).IsEqual("Chemical Rocket");
        AssertThat(e.SpecificImpulse).IsEqual(450f);
        AssertThat(e.Thrust).IsEqual(5000f);
        AssertThat(e.Description).IsEqual("Traditional rocket engine");

        ResetTempDir();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Validate_FlagsEmptyName()
    {
        ResetTempDir();
        var model = new EngineEditorModel(TempDir);
        model.AddCategory("Chemical");
        model.AddEngine("Chemical", new EngineEditorModel.EngineEditEntry { Name = "", SpecificImpulse = 1, Thrust = 1 });

        var errors = model.Validate();
        AssertThat(errors.Find(e => e.Contains("empty name"))).IsNotNull();
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
