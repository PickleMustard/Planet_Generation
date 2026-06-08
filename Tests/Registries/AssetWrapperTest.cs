using Godot;
using GdUnit4;
using Registries;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.Registries;

/// <summary>
/// Verifies the export-safe asset-wrapper pipeline: configuration references an
/// <c>IconConfig</c>/<c>ModelConfig</c> <c>.tres</c>, and the loaders pull the typed
/// asset out of the wrapper instead of probing raw files.
/// </summary>
[TestSuite]
public class AssetWrapperTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void IconConfigTres_YieldsTexture()
    {
        var config = GD.Load<IconConfig>("res://Materials/Icons/ore.tres");
        AssertThat(config).IsNotNull();
        AssertThat(config!.Texture).IsNotNull();
        AssertThat(config.Id.ToString()).IsEqual("ore");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ModelConfigTres_YieldsScene()
    {
        var config = GD.Load<ModelConfig>("res://Mesh/external/factory_a.glb.tres");
        AssertThat(config).IsNotNull();
        AssertThat(config!.Model).IsNotNull();
        AssertThat(config.Model!.CanInstantiate()).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void IconDataLoader_LoadsTextureFromWrapper()
    {
        var icon = IconDataLoader.LoadIcon("res://Materials/Icons/ore.tres", "test");
        AssertThat(icon.IsValid).IsTrue();
        AssertThat(icon.Texture).IsNotNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void IconDataLoader_MissingWrapper_FailsGracefully()
    {
        var icon = IconDataLoader.LoadIcon("res://Materials/Icons/does_not_exist.tres", "test");
        AssertThat(icon.IsValid).IsFalse();
    }
}
