using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary.DataLoading;

namespace Tests.UtilityLibrary.DataLoading;

[TestSuite]
public class BuildingShape2DLoaderTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void LoadShape_Hexagon_HasSixVertices()
    {
        var shape = BuildingShape2DLoader.LoadShape("res://Configuration/Building2D/hexagon.yaml");
        AssertThat(shape).IsNotNull();
        AssertThat(shape!.Id).IsEqual("hexagon");
        AssertThat(shape.Vertices.Length).IsEqual(6);
        AssertThat(shape.Sides.Count).IsEqual(6);
        AssertThat(shape.Sides[0].Slots[0].Kind).IsEqual(ResourceNodeKind.Import);
        AssertThat(shape.Sides[0].Slots[0].StateOfMatter).IsEqual(StateOfMatter.Solid);
        AssertThat(shape.Sides[3].Slots[0].Kind).IsEqual(ResourceNodeKind.Export);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LoadShape_Triangle_HasThreeVertices()
    {
        var shape = BuildingShape2DLoader.LoadShape("res://Configuration/Building2D/triangle.yaml");
        AssertThat(shape).IsNotNull();
        AssertThat(shape!.Id).IsEqual("triangle");
        AssertThat(shape.Vertices.Length).IsEqual(3);
        AssertThat(shape.Sides.Count).IsEqual(3);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LoadShape_Square_VerticesArePixelCorners()
    {
        var shape = BuildingShape2DLoader.LoadShape("res://Configuration/Building2D/square.yaml");
        AssertThat(shape).IsNotNull();
        AssertThat(shape!.Vertices.Length).IsEqual(4);
        AssertThat(shape.Vertices[0].X > 0 && shape.Vertices[0].Y < 0).IsTrue();
    }
}
