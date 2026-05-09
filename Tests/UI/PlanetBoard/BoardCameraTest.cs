using GdUnit4;
using Godot;
using UI.PlanetBoard;
using static GdUnit4.Assertions;

namespace Tests.UI.PlanetBoard;

[TestSuite]
public class BoardCameraTest
{
    [TestCase]
    public void MinZoom_FitsContentInsideViewport()
    {
        var cam = new BoardCamera();
        cam.SetViewportSize(new Vector2(800, 600));
        cam.SetContentBBox(new Rect2(Vector2.Zero, new Vector2(1600, 1200)));
        // Both axes scale by 0.5; the 0.95 fit margin reduces it further.
        AssertThat(Mathf.IsEqualApprox(cam.MinZoom, 0.5f * 0.95f, 1e-4f)).IsTrue();
    }

    [TestCase]
    public void FitToContent_LocksZoomAndCentersOnBBox()
    {
        var cam = new BoardCamera();
        cam.SetViewportSize(new Vector2(400, 400));
        var bbox = new Rect2(new Vector2(100, 100), new Vector2(200, 200));
        cam.SetContentBBox(bbox);
        cam.FitToContent();
        AssertThat(cam.Zoom).IsEqual(cam.MinZoom);
        AssertThat(cam.PanBoard).IsEqual(bbox.GetCenter());
    }

    [TestCase]
    public void PanLockedAtMinZoom()
    {
        var cam = new BoardCamera();
        cam.SetViewportSize(new Vector2(400, 400));
        cam.SetContentBBox(new Rect2(Vector2.Zero, new Vector2(800, 600)));
        cam.FitToContent();
        var before = cam.PanBoard;
        cam.ApplyPanFromScreenDelta(new Vector2(50, 50));
        AssertThat(cam.PanBoard).IsEqual(before);
    }

    [TestCase]
    public void PanDelta_ScalesInverselyWithZoom()
    {
        var cam = new BoardCamera();
        cam.SetViewportSize(new Vector2(800, 600));
        cam.SetContentBBox(new Rect2(Vector2.Zero, new Vector2(8000, 6000)));
        cam.ZoomAt(new Vector2(400, 300), 100f); // jump well above MinZoom
        // Force zoom to a known value
        cam.ZoomAt(new Vector2(400, 300), 2f / cam.Zoom);
        var before = cam.PanBoard;
        cam.ApplyPanFromScreenDelta(new Vector2(100, 0));
        // Δboard = -Δscreen / zoom = -100 / 2 = -50
        AssertThat(Mathf.IsEqualApprox(cam.PanBoard.X - before.X, -50f, 1e-3f)).IsTrue();
    }

    [TestCase]
    public void ZoomAt_KeepsBoardPointUnderCursorInvariant()
    {
        var cam = new BoardCamera();
        cam.SetViewportSize(new Vector2(800, 600));
        cam.SetContentBBox(new Rect2(Vector2.Zero, new Vector2(4000, 3000)));
        var cursor = new Vector2(123, 456);
        var before = cam.ScreenToBoard(cursor);
        cam.ZoomAt(cursor, 1.5f);
        var after = cam.ScreenToBoard(cursor);
        AssertThat(Mathf.IsEqualApprox(before.X, after.X, 1e-3f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(before.Y, after.Y, 1e-3f)).IsTrue();
    }
}
