using System.Collections.Generic;
using System.IO;
using GdUnit4;
using Godot;
using ProceduralGeneration.SubtypeSystem;
using Structures;
using Structures.Resources;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.DataLoading;

/// <summary>
/// Round-trip + resolver coverage for <see cref="SubtypeDefinition.VerticesPerEdgeRanges"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SubtypeVerticesPerEdgeTest
{
    private string _tempDir = string.Empty;

    [Before]
    public void Before()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vpe_test_{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [After]
    public void After()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [TestCase]
    public void Loader_ParsesVerticesPerEdgeList()
    {
        string yaml = @"subtypes:
  - id: subtype_rocky_temperate
    display_name: Temperate
    mesh:
      subdivisions: [2, 4]
      vertices_per_edge:
        - [4, 6]
        - [2, 3]
        - [1, 2]
";
        string path = WriteFixture("rocky.yaml", yaml);
        var defs = SubtypeDefinitionLoader.LoadFile(path, BodyFamily.RockyPlanet);
        AssertThat(defs).IsNotNull();
        AssertThat(defs!.Count).IsEqual(1);

        var def = defs[0];
        AssertThat(def.VerticesPerEdgeRanges.Count).IsEqual(3);
        AssertThat(def.VerticesPerEdgeRanges[0]).IsEqual(new FloatRange(4, 6));
        AssertThat(def.VerticesPerEdgeRanges[1]).IsEqual(new FloatRange(2, 3));
        AssertThat(def.VerticesPerEdgeRanges[2]).IsEqual(new FloatRange(1, 2));
    }

    [TestCase]
    public void Loader_TreatsMissingVerticesPerEdgeAsEmpty()
    {
        string yaml = @"subtypes:
  - id: subtype_x
    display_name: X
    mesh:
      subdivisions: [1, 2]
";
        string path = WriteFixture("missing.yaml", yaml);
        var defs = SubtypeDefinitionLoader.LoadFile(path, BodyFamily.RockyPlanet);
        AssertThat(defs![0].VerticesPerEdgeRanges.Count).IsEqual(0);
    }

    [TestCase]
    public void Resolver_RollsOneEntryPerRolledSubdivisionLevel()
    {
        var def = new SubtypeDefinition
        {
            Id = "subtype_rocky_test",
            Family = BodyFamily.RockyPlanet,
            MeshRanges = { ["subdivisions"] = new FloatRange(3, 3) },
            VerticesPerEdgeRanges =
            {
                new FloatRange(5, 5),
                new FloatRange(3, 3),
                new FloatRange(2, 2),
            },
        };
        EnsureRegistered(def);

        var meshParams = new Godot.Collections.Dictionary();
        var rng = new RandomNumberGenerator { Seed = 1UL };
        var classification = new BodyClassification.RockyPlanet(global::Structures.Enums.RockyPlanetSubtype.Temperate);

        SubtypeGenParamResolver.ApplyMeshParams(meshParams, classification, rng, useMidpoint: true);

        AssertThat(meshParams.ContainsKey("vertices_per_edge")).IsTrue();
        var arr = (int[])meshParams["vertices_per_edge"];
        AssertThat(arr.Length).IsEqual(3);
        AssertThat(arr[0]).IsEqual(5);
        AssertThat(arr[1]).IsEqual(3);
        AssertThat(arr[2]).IsEqual(2);
    }

    [TestCase]
    public void Resolver_ClampsToLastEntry_WhenSubdivisionsExceedsListLength()
    {
        var def = new SubtypeDefinition
        {
            Id = "subtype_rocky_test",
            Family = BodyFamily.RockyPlanet,
            MeshRanges = { ["subdivisions"] = new FloatRange(5, 5) },
            VerticesPerEdgeRanges =
            {
                new FloatRange(8, 8),
                new FloatRange(2, 2),
            },
        };
        EnsureRegistered(def);

        var meshParams = new Godot.Collections.Dictionary();
        var rng = new RandomNumberGenerator { Seed = 1UL };
        var classification = new BodyClassification.RockyPlanet(global::Structures.Enums.RockyPlanetSubtype.Temperate);

        SubtypeGenParamResolver.ApplyMeshParams(meshParams, classification, rng, useMidpoint: true);

        var arr = (int[])meshParams["vertices_per_edge"];
        AssertThat(arr.Length).IsEqual(5);
        AssertThat(arr[0]).IsEqual(8);
        AssertThat(arr[1]).IsEqual(2);
        AssertThat(arr[2]).IsEqual(2);
        AssertThat(arr[3]).IsEqual(2);
        AssertThat(arr[4]).IsEqual(2);
    }

    private string WriteFixture(string name, string content)
    {
        string path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static void EnsureRegistered(SubtypeDefinition def)
    {
        // Use existing subtype id "subtype_rocky_temperate" so BiomeIdMapper resolves;
        // but the resolver only needs SubtypeDatabase lookup by id we will override.
        var db = SubtypeDatabase.Instance;
        if (!db.IsLoaded) db.LoadData();

        // Replace temperate entry in-memory with our test def so resolver picks it up.
        const string id = "subtype_rocky_temperate";
        def.Id = id;
        db.Remove(id);
        db.Add(def);
    }
}
