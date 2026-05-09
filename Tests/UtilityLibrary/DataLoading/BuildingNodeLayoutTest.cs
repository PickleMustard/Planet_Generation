using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Resources;
using UtilityLibrary.DataLoading;

namespace Tests.UtilityLibrary.DataLoading;

/// <summary>
/// Verifies the YAML loader translates a building's <c>nodes:</c> block into a
/// populated <see cref="BuildingDefinition.NodeLayout"/>, and that buildings
/// without a <c>nodes:</c> block load with an empty layout (back-compat).
/// </summary>
[TestSuite]
public class BuildingNodeLayoutTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void Mine_HasExportNodes()
    {
        var defs = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/Extraction/Mine.yaml");

        // Validation may strip individual buildings; require at least one extraction
        // building loaded with an Export node layout.
        AssertThat(defs.Count).IsGreater(0);

        bool foundExtractionExport = false;
        foreach (var d in defs)
        {
            foreach (var spec in d.NodeLayout)
            {
                if (spec.Kind == ResourceNodeKind.Export)
                {
                    foundExtractionExport = true;
                    break;
                }
            }
            if (foundExtractionExport) break;
        }
        AssertThat(foundExtractionExport).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PowerPlant_HasImportNodeForFuel()
    {
        var defs = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/Power/PowerPlant.yaml");

        BuildingDefinition? fission = null;
        foreach (var d in defs)
        {
            if (d.IdName == "fission_reactor") { fission = d; break; }
        }
        AssertThat(fission).IsNotNull();
        AssertThat(fission!.NodeLayout.Count).IsGreater(0);
        AssertThat(fission.NodeLayout[0].Kind).IsEqual(ResourceNodeKind.Import);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Solar_HasNoNodes()
    {
        var defs = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/Power/Solar.yaml");

        AssertThat(defs.Count).IsGreater(0);
        foreach (var d in defs)
            AssertThat(d.NodeLayout.Count).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HQ_HasFlexNodes()
    {
        var defs = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/Administration/CompanyHeadquarters.yaml");

        BuildingDefinition? hq = null;
        foreach (var d in defs)
        {
            if (d.IdName == "company_headquarters") { hq = d; break; }
        }
        AssertThat(hq).IsNotNull();
        AssertThat(hq!.NodeLayout.Count).IsEqual(4);
        foreach (var spec in hq.NodeLayout)
            AssertThat(spec.Kind).IsEqual(ResourceNodeKind.Flex);
    }
}
