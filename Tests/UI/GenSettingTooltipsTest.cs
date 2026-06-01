using GdUnit4;
using UtilityLibrary.UI;
using static GdUnit4.Assertions;

namespace Tests.UI;

/// <summary>
/// Verifies every subtype range knob declared by Phase 1/Phase 3 carries a tooltip entry.
/// Risk per plan: YAML missing keys → silent empty tooltips. Test fails loudly instead.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GenSettingTooltipsTest
{
    private static readonly string[] MeshKeys =
    {
        "mesh.subdivisions",
        "mesh.num_abberations",
        "mesh.num_deformation_cycles",
    };

    private static readonly string[] TectonicKeys =
    {
        "tectonics.num_continents",
        "tectonics.stress_scale",
        "tectonics.shear_scale",
        "tectonics.max_propagation_distance",
        "tectonics.propagation_falloff",
        "tectonics.inactive_stress_threshold",
        "tectonics.general_height_scale",
        "tectonics.general_shear_scale",
        "tectonics.general_compression_scale",
        "tectonics.general_transform_scale",
    };

    private static readonly string[] SHKeys =
    {
        "spherical_harmonics.amplitude",
        "spherical_harmonics.band_scale_l0",
        "spherical_harmonics.band_scale_l1",
        "spherical_harmonics.band_scale_l2",
        "spherical_harmonics.band_scale_l3",
    };

    private static readonly string[] BodyKeys =
    {
        "body.mass",
        "body.size",
    };

    [Before]
    public void Before() => GenSettingTooltips.Reload();

    [TestCase]
    public void EveryMeshRangeKey_HasTooltip()
    {
        foreach (var key in MeshKeys)
            AssertThat(GenSettingTooltips.Get(key))
                .OverrideFailureMessage($"missing tooltip for {key}")
                .IsNotEmpty();
    }

    [TestCase]
    public void EveryTectonicRangeKey_HasTooltip()
    {
        foreach (var key in TectonicKeys)
            AssertThat(GenSettingTooltips.Get(key))
                .OverrideFailureMessage($"missing tooltip for {key}")
                .IsNotEmpty();
    }

    [TestCase]
    public void EverySphericalHarmonicsRangeKey_HasTooltip()
    {
        foreach (var key in SHKeys)
            AssertThat(GenSettingTooltips.Get(key))
                .OverrideFailureMessage($"missing tooltip for {key}")
                .IsNotEmpty();
    }

    [TestCase]
    public void EveryBodyLevelKey_HasTooltip()
    {
        foreach (var key in BodyKeys)
            AssertThat(GenSettingTooltips.Get(key))
                .OverrideFailureMessage($"missing tooltip for {key}")
                .IsNotEmpty();
    }

    [TestCase]
    public void Get_ReturnsFallback_WhenKeyMissing()
    {
        AssertThat(GenSettingTooltips.Get("does.not.exist", "fallback"))
            .IsEqual("fallback");
    }
}
