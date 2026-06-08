using Godot;
using ProceduralGeneration.PlanetGeneration;

namespace UI.Construction;

/// <summary>
/// Draws the target body's orbit bands (and natural-moon orbits) as flat line
/// loops on the XZ plane for the top-down placement board, plus a movable ghost
/// ring at the cursor radius. Runs <see cref="Node3D.TopLevel"/> and re-pins to
/// the body's world position each frame so planet axial rotation never spins the
/// rings; it is still parented under the body so it is freed with it.
///
/// Inclined natural-moon orbits are not in the XZ plane; v1 draws their
/// horizontal-projected radius as an approximate flat ring. Station bands are
/// always XZ (stations orbit in the XZ plane) so those are exact.
/// </summary>
public partial class PlacementOrbitRings : Node3D
{
    private const int RING_SEGMENTS = 96;

    private static readonly Color BandColor = new(0.40f, 0.80f, 1f, 0.70f);
    private static readonly Color MoonOrbitColor = new(0.60f, 0.60f, 0.65f, 0.35f);
    private static readonly Color GhostValidColor = new(0.45f, 1f, 0.55f, 0.90f);
    private static readonly Color GhostInvalidColor = new(1f, 0.35f, 0.30f, 0.90f);

    private IOrbitalBody? _body;
    private Node3D? _bodyNode;
    private MeshInstance3D? _staticRings;
    private MeshInstance3D? _ghostRing;

    public void Initialize(IOrbitalBody body)
    {
        _body = body;
        _bodyNode = body as Node3D;
        TopLevel = true;

        _staticRings = new MeshInstance3D { Name = "StaticRings" };
        AddChild(_staticRings);
        _ghostRing = new MeshInstance3D { Name = "GhostRing", Visible = false };
        AddChild(_ghostRing);

        RebuildRings();
    }

    public override void _Process(double delta)
    {
        if (_bodyNode == null || !IsInstanceValid(_bodyNode))
            return;
        // Re-pin to the body's world position with an identity basis so rings stay
        // axis-aligned regardless of the body's own rotation.
        GlobalTransform = new Transform3D(Basis.Identity, _bodyNode.GlobalPosition);
    }

    /// <summary>(Re)builds the static band + moon-orbit loops.</summary>
    public void RebuildRings()
    {
        if (_body == null || _staticRings == null)
            return;

        var mesh = new ImmediateMesh();
        var mat = MakeLineMaterial();

        if (_body.UsesBandPlacement)
        {
            int count = _body.GetBandCount();
            for (int i = 0; i < count; i++)
                AddRing(mesh, mat, _body.GetOrbitBandRadius(i), BandColor);
        }

        AddMoonOrbitRings(mesh, mat);

        _staticRings.Mesh = mesh;
    }

    /// <summary>Positions the ghost ring at <paramref name="radius"/>.</summary>
    public void UpdateGhostRing(float radius, bool valid)
    {
        if (_ghostRing == null || radius <= 0.001f)
            return;

        var mesh = new ImmediateMesh();
        AddRing(mesh, MakeLineMaterial(), radius, valid ? GhostValidColor : GhostInvalidColor);
        _ghostRing.Mesh = mesh;
        _ghostRing.Visible = true;
    }

    private void AddMoonOrbitRings(ImmediateMesh mesh, StandardMaterial3D mat)
    {
        if (_bodyNode == null)
            return;

        Vector3 center = _bodyNode.GlobalPosition;
        foreach (var child in _bodyNode.GetChildren())
        {
            if (child is not CelestialBody moon)
                continue;
            Vector3 offset = moon.GlobalPosition - center;
            float radius = new Vector2(offset.X, offset.Z).Length();
            if (radius > 0.5f)
                AddRing(mesh, mat, radius, MoonOrbitColor);
        }
    }

    private static void AddRing(ImmediateMesh mesh, StandardMaterial3D mat, float radius, Color color)
    {
        mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip, mat);
        for (int i = 0; i <= RING_SEGMENTS; i++)
        {
            float a = Mathf.Tau * i / RING_SEGMENTS;
            mesh.SurfaceSetColor(color);
            mesh.SurfaceAddVertex(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        mesh.SurfaceEnd();
    }

    private static StandardMaterial3D MakeLineMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        AlbedoColor = Colors.White,
    };
}
