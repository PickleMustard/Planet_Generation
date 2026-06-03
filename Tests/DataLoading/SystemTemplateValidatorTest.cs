using System.IO;
using GdUnit4;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.DataLoading;

/// <summary>
/// Phase 6 validator behaviour: legacy per-body gen-range blocks must hard-reject.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SystemTemplateValidatorTest
{
    private string _tempDir = string.Empty;

    [Before]
    public void Before()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"validator_test_{System.Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(_tempDir);
    }

    [After]
    public void After()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestCase]
    public void Validator_RejectsLegacyBaseMesh()
    {
        var result = Validate(@"planetary:
  - type: RockyPlanet
    template: { mass: 1000, size: 150 }
    orbital_parameters: { apogee: 3000, perigee: 3000 }
    base_mesh:
      subdivisions: 2
");
        AssertThat(result.IsValid).IsFalse();
        AssertThat(result.Errors.Exists(e => e.Contains("base_mesh") && e.Contains("SystemTemplateMigrator")))
            .OverrideFailureMessage("expected rejection of base_mesh referencing migrator; got: " + string.Join("; ", result.Errors))
            .IsTrue();
    }

    [TestCase]
    public void Validator_RejectsLegacyTectonics()
    {
        var result = Validate(@"planetary:
  - type: RockyPlanet
    template: { mass: 1000, size: 150 }
    orbital_parameters: { apogee: 3000, perigee: 3000 }
    tectonics:
      num_continents: [30, 35]
");
        AssertThat(result.IsValid).IsFalse();
        AssertThat(result.Errors.Exists(e => e.Contains("tectonics") && e.Contains("SystemTemplateMigrator"))).IsTrue();
    }

    [TestCase]
    public void Validator_RejectsLegacySphericalHarmonics()
    {
        var result = Validate(@"dominant:
  - type: Star
    template: { mass: 500000, size: 500 }
    spherical_harmonics_settings:
      amplitude_range: [0.3, 0.7]
");
        AssertThat(result.IsValid).IsFalse();
        AssertThat(result.Errors.Exists(e => e.Contains("spherical_harmonics_settings"))).IsTrue();
    }

    [TestCase]
    public void Validator_AcceptsSlimShape()
    {
        var result = Validate(@"planetary:
  - type: RockyPlanet
    template: { mass: 1000, size: 150 }
    orbital_parameters: { apogee: 3000, perigee: 3000 }
    subtype: subtype_rocky_temperate
");
        AssertThat(result.IsValid).IsTrue();
    }

    [TestCase]
    public void Validator_AcceptsSubtypeWeights()
    {
        var result = Validate(@"planetary:
  - type: RockyPlanet
    template: { mass: 1000, size: 150 }
    orbital_parameters: { apogee: 3000, perigee: 3000 }
    subtype_weights:
      subtype_rocky_temperate: 0.6
      subtype_rocky_desert: 0.4
");
        AssertThat(result.IsValid).IsTrue();
    }

    [TestCase]
    public void Validator_RejectsLegacyKeysInSatellites()
    {
        // Satellites now live in a flattened top-level section and name their parent by name.
        var result = Validate(@"planetary:
  - type: RockyPlanet
    name: terra
    template: { mass: 1000, size: 150 }
    orbital_parameters: { apogee: 3000, perigee: 3000 }
satellites:
  - type: Moon
    parent: terra
    template: { apogee: 200, perigee: 200, mass: 50, size: 10 }
    base_mesh:
      subdivisions: 2
");
        AssertThat(result.IsValid).IsFalse();
        AssertThat(result.Errors.Exists(e => e.Contains("satellites[0]") && e.Contains("base_mesh"))).IsTrue();
    }

    private ValidationResult Validate(string yaml)
    {
        string path = Path.Combine(_tempDir, $"case_{System.Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        return YamlValidator.ValidateSystemTemplate(path);
    }
}
