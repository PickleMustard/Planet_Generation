using GdUnit4;
using static GdUnit4.Assertions;
using Structures.GameState;
using Structures.MeshGeneration;
using Godot;

namespace Tests.Structures.GameState;

[TestSuite]
public class OctreeGrowTest
{
    [TestCase]
    public void Grow_FactorOne_DoublesSizeStaysCentered()
    {
        // Original cube: [-1, +1] on every axis (Position = (-1,-1,-1), Size = (2,2,2)).
        var oct = new Octree<Point>(new Aabb(Vector3.One * -1f, Vector3.One * 2f));

        oct.Grow(1f);

        var b = oct.root.boundary;
        AssertThat(b.GetCenter().IsEqualApprox(Vector3.Zero)).IsTrue();
        AssertThat(b.Size.IsEqualApprox(Vector3.One * 4f)).IsTrue();
    }

    [TestCase]
    public void Grow_FactorHalf_ExpandsSymmetrically()
    {
        var oct = new Octree<Point>(new Aabb(Vector3.One * -2f, Vector3.One * 4f));

        oct.Grow(0.5f);

        var b = oct.root.boundary;
        AssertThat(b.GetCenter().IsEqualApprox(Vector3.Zero)).IsTrue();
        AssertThat(b.Size.IsEqualApprox(Vector3.One * 6f)).IsTrue();
    }
}
