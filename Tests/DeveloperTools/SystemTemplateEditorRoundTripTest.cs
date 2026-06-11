#if DEBUG
using System.Linq;
using DeveloperTools.SystemTemplateEditor;
using GdUnit4;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.DeveloperTools;

/// <summary>
/// Verifies the System Template Editor model round-trips a real template: load → tree →
/// ToSections → GenerateYamlContent → reparse yields the same bodies and subtype weights, and that
/// the reparent invariants (no dominant nesting, no cycles) hold.
/// </summary>
[TestSuite]
public class SystemTemplateEditorRoundTripTest
{
    private const string Template = "Multi-body-test"; // 1 star + 3 rocky planets + 1 moon, all named

    [TestCase]
    [RequireGodotRuntime]
    public void Template_RoundTrips()
    {
        var data = TemplateHelpers.LoadSystemTemplate(Template);
        var model = SystemTemplateEditorYamlIO.FromTemplate(data, Template + ".yaml");

        // One dominant root; planets nest beneath it (parent_body: barycenter → attaches to root).
        AssertThat(model.Roots.Count).IsEqual(data.Dominant.Count);
        AssertThat(model.Roots.Count).IsGreater(0);

        int nodeCount = model.AllNodes().Count();
        int expected = data.Dominant.Count + data.Belts.Count + data.Planetary.Count + data.Satellites.Count;
        AssertThat(nodeCount).IsEqual(expected);
        AssertThat(model.Roots[0].Children.Count(c => c.Category == BodyCategory.Planetary))
            .IsEqual(data.Planetary.Count);

        // Round-trip through the canonical writer + section flattener.
        string yaml = SystemTemplateEditorYamlIO.ToYaml(model);
        AssertThat(yaml.Length).IsGreater(0);

        var (dom, belts, plan, sats) = SystemTemplateEditorYamlIO.ToSections(model);
        AssertThat(dom.Count).IsEqual(data.Dominant.Count);
        AssertThat(belts.Count).IsEqual(data.Belts.Count);
        AssertThat(plan.Count).IsEqual(data.Planetary.Count);
        AssertThat(sats.Count).IsEqual(data.Satellites.Count);

        // Subtype weights survive on a planetary body.
        var planet = model.AllNodes().First(n => n.Category == BodyCategory.Planetary);
        var weights = planet.GetSubtypeWeights();
        AssertThat(weights.Count).IsGreater(0);
        AssertThat(weights.ContainsKey("subtype_rocky_temperate")).IsTrue();

        // The moon nests under its named parent planet (proxima), not the root.
        var moon = model.AllNodes().FirstOrDefault(n => n.Category == BodyCategory.Satellite);
        AssertThat(moon).IsNotNull();
        AssertThat(moon!.Parent!.Name).IsEqual("proxima");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Reparent_EnforcesInvariants()
    {
        var data = TemplateHelpers.LoadSystemTemplate(Template);
        var model = SystemTemplateEditorYamlIO.FromTemplate(data, Template + ".yaml");

        var dominant = model.Roots[0];
        var planets = model.AllNodes().Where(n => n.Category == BodyCategory.Planetary).ToList();
        AssertThat(planets.Count).IsGreaterEqual(2);
        var a = planets[0];
        var b = planets[1];

        // A dominant can never be nested under anything.
        AssertThat(model.CanReparent(dominant, a)).IsFalse();

        // Nest b under a — allowed — then dropping a onto b must be rejected (cycle).
        AssertThat(model.CanReparent(b, a)).IsTrue();
        model.Reparent(b, a);
        AssertThat(b.Parent).IsEqual(a);
        AssertThat(model.CanReparent(a, b)).IsFalse();

        // b is no longer a direct child of the dominant root.
        AssertThat(dominant.Children.Contains(b)).IsFalse();
        AssertThat(a.Children.Contains(b)).IsTrue();
    }
}
#endif
