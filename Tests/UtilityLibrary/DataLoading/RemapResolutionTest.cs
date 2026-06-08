using GdUnit4;
using static GdUnit4.Assertions;
using UtilityLibrary.DataLoading;

namespace Tests.UtilityLibrary.DataLoading;

/// <summary>
/// Verifies <see cref="BaseConfigLoader.ParseRemapTarget"/> — the parser that recovers the
/// real packed path out of a Godot ".remap" sidecar. This is the core of export-safe config
/// loading: on export Godot remaps resource paths and only ".remap" files name the real
/// target. The parser is pure (no Godot runtime), so it is unit-testable directly.
/// </summary>
[TestSuite]
public class RemapResolutionTest
{
    [TestCase]
    public void ParseRemapTarget_StandardRemap_ReturnsPath()
    {
        string remap = "[remap]\n\npath=\"res://Configuration/Buildings/foo.yaml\"\n";
        AssertThat(BaseConfigLoader.ParseRemapTarget(remap))
            .IsEqual("res://Configuration/Buildings/foo.yaml");
    }

    [TestCase]
    public void ParseRemapTarget_CrlfLineEndings_ReturnsPath()
    {
        string remap = "[remap]\r\npath=\"res://a/b.yaml\"\r\n";
        AssertThat(BaseConfigLoader.ParseRemapTarget(remap)).IsEqual("res://a/b.yaml");
    }

    [TestCase]
    public void ParseRemapTarget_ExtraWhitespace_IsTrimmed()
    {
        string remap = "[remap]\n   path =  \"res://x.yaml\"  \n";
        AssertThat(BaseConfigLoader.ParseRemapTarget(remap)).IsEqual("res://x.yaml");
    }

    [TestCase]
    public void ParseRemapTarget_NoPathEntry_ReturnsEmpty()
    {
        string remap = "[remap]\nother=\"value\"\n";
        AssertThat(BaseConfigLoader.ParseRemapTarget(remap)).IsEqual("");
    }

    [TestCase]
    public void ParseRemapTarget_EmptyOrNull_ReturnsEmpty()
    {
        AssertThat(BaseConfigLoader.ParseRemapTarget("")).IsEqual("");
        AssertThat(BaseConfigLoader.ParseRemapTarget(null!)).IsEqual("");
    }
}
