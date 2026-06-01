using System.IO;
using System.Linq;
using GdUnit4;
using ProceduralGeneration.SubtypeSystem;
using Structures.Resources;
using UtilityLibrary.Tools;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static GdUnit4.Assertions;
using SysDict = System.Collections.Generic.Dictionary<object, object>;
using SysList = System.Collections.Generic.List<object>;

namespace Tests.Tools;

/// <summary>
/// Phase 6 migrator round-trip tests. Uses a temp-dir fixture so the real
/// Configuration/SystemTemplate is never mutated by the test runner.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SystemTemplateMigratorTest
{
    private string _tempDir = string.Empty;

    [Before]
    public void Before()
    {
        if (!SubtypeDatabase.Instance.IsLoaded)
            SubtypeDatabase.Instance.LoadData();

        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"migrator_test_{System.Guid.NewGuid():N}"
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
    public void MigrateFile_StripsLegacyGenRangeBlocks()
    {
        string yaml = LegacyPlanetaryYaml(includeSubtype: false);
        string path = WriteFixture("legacy.yaml", yaml);

        var fr = SystemTemplateMigrator.MigrateFile(path);

        AssertThat(fr.Error).IsNull();
        AssertThat(fr.Changed).IsTrue();
        AssertThat(fr.LegacyBlocksStripped).IsGreaterEqual(2);

        var after = ParseYaml(path);
        var planetary = (SysList)after["planetary"];
        var body = (SysDict)planetary[0];
        AssertThat(body.ContainsKey("base_mesh")).IsFalse();
        AssertThat(body.ContainsKey("tectonics")).IsFalse();
        AssertThat(body.ContainsKey("spherical_harmonics_settings")).IsFalse();
    }

    [TestCase]
    public void MigrateFile_FillsUniformSubtypeWeights_WhenMissing()
    {
        string yaml = LegacyPlanetaryYaml(includeSubtype: false);
        string path = WriteFixture("uniform.yaml", yaml);

        SystemTemplateMigrator.MigrateFile(path);

        var after = ParseYaml(path);
        var body = (SysDict)((SysList)after["planetary"])[0];
        AssertThat(body.ContainsKey("subtype_weights")).IsTrue();

        var weights = (SysDict)body["subtype_weights"];
        var rockyIds = SubtypeDatabase.Instance.ByFamily(BodyFamily.RockyPlanet)
            .Select(s => s.Id).ToList();

        AssertThat(weights.Count).IsEqual(rockyIds.Count);
        foreach (var id in rockyIds)
            AssertThat(weights.ContainsKey(id))
                .OverrideFailureMessage($"missing rocky subtype '{id}' in uniform weights")
                .IsTrue();

        float total = 0f;
        foreach (var kvp in weights)
        {
            string s = kvp.Value?.ToString() ?? "0";
            total += float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }
        AssertThat(System.Math.Abs(total - 1.0f) < 0.05f)
            .OverrideFailureMessage($"uniform weights total {total}, expected ~1.0")
            .IsTrue();
    }

    [TestCase]
    public void MigrateFile_LeavesExplicitSubtypeAlone()
    {
        string yaml = LegacyPlanetaryYaml(includeSubtype: true);
        string path = WriteFixture("explicit.yaml", yaml);

        SystemTemplateMigrator.MigrateFile(path);

        var after = ParseYaml(path);
        var body = (SysDict)((SysList)after["planetary"])[0];
        AssertThat(body["subtype"]).IsEqual("subtype_rocky_temperate");
        AssertThat(body.ContainsKey("subtype_weights")).IsFalse();
    }

    [TestCase]
    public void MigrateFile_IsIdempotent()
    {
        string yaml = LegacyPlanetaryYaml(includeSubtype: false);
        string path = WriteFixture("idem.yaml", yaml);

        var first = SystemTemplateMigrator.MigrateFile(path);
        AssertThat(first.Changed).IsTrue();

        var second = SystemTemplateMigrator.MigrateFile(path);
        AssertThat(second.Error).IsNull();
        AssertThat(second.Changed).IsFalse();
        AssertThat(second.LegacyBlocksStripped).IsEqual(0);
        AssertThat(second.SubtypeSlotsFilled).IsEqual(0);
    }

    [TestCase]
    public void MigrateAll_RealConfig_HasNoLegacyKeys()
    {
        // Regression guard. After the one-shot run, the real config tree should never grow legacy
        // keys back. Migrator is idempotent, so running it on already-slim files reports no change.
        var report = SystemTemplateMigrator.MigrateAll(dryRun: true);
        foreach (var fr in report.Files)
        {
            AssertThat(fr.Error)
                .OverrideFailureMessage($"{fr.Path}: {fr.Error}")
                .IsNull();
            AssertThat(fr.LegacyBlocksStripped)
                .OverrideFailureMessage(
                    $"{fr.Path} still contains legacy gen-range blocks (stripped={fr.LegacyBlocksStripped})")
                .IsEqual(0);
        }
    }

    private string WriteFixture(string name, string content)
    {
        string path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static SysDict ParseYaml(string path)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<SysDict>(File.ReadAllText(path));
    }

    private static string LegacyPlanetaryYaml(bool includeSubtype) => @"planetary:
  - type: RockyPlanet
    template:
      mass: 1000
      size: 150
    orbital_parameters:
      apogee: 3000
      perigee: 3000
      starting_angle: 0
      vertical_offset: 0
      parent_body: barycenter
" + (includeSubtype ? "    subtype: subtype_rocky_temperate\n" : "") + @"    base_mesh:
      subdivisions: 2
      num_abberations: 20
      num_deformation_cycles: 4
    tectonics:
      num_continents: [30, 35]
      stress_scale: [3.1, 3.5]
    spherical_harmonics_settings:
      amplitude_range: [0.3, 0.7]
";
}
