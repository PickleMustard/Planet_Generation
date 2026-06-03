using GdUnit4;
using Godot;
using Godot.Collections;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.DataLoading;

/// <summary>
/// Regression guard for the "0 satellite bodies" bug, retargeted to the flattened template format:
/// a moon now lives in the top-level <c>satellites:</c> section and must survive parsing carrying a
/// <c>parent</c> string naming the planetary body it orbits.
/// </summary>
[TestSuite]
public class PlanetarySatelliteParsingTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void MultiBodyTest_ProximaCarriesMoonSatellite()
    {
        var data = TemplateHelpers.LoadSystemTemplate("Multi-body-test");

        AssertThat(data.Planetary.Count).IsGreater(0);

        // The flattened satellites section holds exactly one moon (luna), parented to proxima.
        AssertThat(data.Satellites.Count)
            .OverrideFailureMessage("Expected exactly one flattened satellite")
            .IsEqual(1);

        var luna = data.Satellites[0];
        AssertThat(luna.ContainsKey("parent"))
            .OverrideFailureMessage("luna lost its 'parent' field during parsing")
            .IsTrue();
        AssertThat((string)luna["parent"])
            .OverrideFailureMessage("luna should be parented to proxima")
            .IsEqual("proxima");
        AssertThat((string)luna["type"]).IsEqual("Moon");
    }
}
