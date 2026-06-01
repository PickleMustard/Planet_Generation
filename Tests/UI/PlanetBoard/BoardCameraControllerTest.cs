using GdUnit4;
using Godot;
using UI.PlanetBoard;
using static GdUnit4.Assertions;

namespace Tests.UI.PlanetBoard;

[TestSuite]
public class BoardCameraControllerTest
{
    // ── Tests requiring a Godot runtime (Camera2D is a Godot node) ───

    [TestCase]
    [RequireGodotRuntime]
    public void FitToContent_SetsZoomAndPosition()
    {
        var cam = new BoardCameraController();
        // BoardCameraController._Ready() calls MakeCurrent() which needs a viewport,
        // so we simulate what _Ready does manually for the test.
        cam.AnchorMode = Camera2D.AnchorModeEnum.DragCenter;
        cam.Zoom = Vector2.One;

        var bbox = new Rect2(new Vector2(-500, -300), new Vector2(1000, 600));

        // Update viewport size before fitting
        cam.UpdateViewportSize(new Vector2(800, 600));
        cam.FitToContent(bbox);

        // After fitting, position should be at bbox center
        AssertThat(Mathf.IsEqualApprox(cam.Position.X, bbox.GetCenter().X, 0.5f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(cam.Position.Y, bbox.GetCenter().Y, 0.5f)).IsTrue();

        // Zoom should be set to MinZoom which fits the content
        float expectedMinZoom = Mathf.Min(
            800f / Mathf.Max(1f, 1000f),
            600f / Mathf.Max(1f, 600f)
        ) * 0.95f;

        AssertThat(Mathf.IsEqualApprox(cam.Zoom.X, expectedMinZoom, 0.001f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(cam.Zoom.Y, expectedMinZoom, 0.001f)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ZoomAtScreen_KeepsCursorInvariant()
    {
        var cam = new BoardCameraController();
        cam.AnchorMode = Camera2D.AnchorModeEnum.DragCenter;
        cam.Zoom = Vector2.One;
        cam.UpdateViewportSize(new Vector2(800, 600));

        // Position camera at origin
        cam.Position = Vector2.Zero;

        // Get the board point at screen center before zoom
        Vector2 screenCenter = new Vector2(400, 300);
        Vector2 boardBefore = cam.ScreenToBoard(screenCenter);

        // Zoom in by ZoomStep (default 1.1)
        cam.ZoomAtScreen(screenCenter, 1.1f);

        // The board point under the cursor should remain the same
        Vector2 boardAfter = cam.ScreenToBoard(screenCenter);

        AssertThat(Mathf.IsEqualApprox(boardBefore.X, boardAfter.X, 0.1f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(boardBefore.Y, boardAfter.Y, 0.1f)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MinZoom_ComputedFromContentAndViewport()
    {
        var cam = new BoardCameraController();
        cam.AnchorMode = Camera2D.AnchorModeEnum.DragCenter;
        cam.Zoom = Vector2.One;
        cam.UpdateViewportSize(new Vector2(800, 600));

        var bbox = new Rect2(new Vector2(-500, -300), new Vector2(1000, 600));
        cam.FitToContent(bbox);

        float expectedMinZoom = Mathf.Min(
            800f / Mathf.Max(1f, 1000f),
            600f / Mathf.Max(1f, 600f)
        ) * 0.95f;

        AssertThat(Mathf.IsEqualApprox(cam.MinZoom, expectedMinZoom, 0.001f)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BoardToScreen_And_ScreenToBoard_AreInverses()
    {
        var cam = new BoardCameraController();
        cam.AnchorMode = Camera2D.AnchorModeEnum.DragCenter;
        cam.Zoom = Vector2.One;
        cam.UpdateViewportSize(new Vector2(800, 600));
        cam.Position = new Vector2(100, 50);

        Vector2 boardPos = new Vector2(200, -100);
        Vector2 screenPos = cam.BoardToScreen(boardPos);
        Vector2 roundTrip = cam.ScreenToBoard(screenPos);

        AssertThat(Mathf.IsEqualApprox(roundTrip.X, boardPos.X, 0.01f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(roundTrip.Y, boardPos.Y, 0.01f)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void UpdateViewportSize_RecomputesMinZoom()
    {
        var cam = new BoardCameraController();
        cam.AnchorMode = Camera2D.AnchorModeEnum.DragCenter;
        cam.Zoom = Vector2.One;

        // Large viewport
        cam.UpdateViewportSize(new Vector2(1600, 1200));
        var bbox = new Rect2(new Vector2(-500, -300), new Vector2(1000, 600));
        cam.FitToContent(bbox);

        float minZoomLarge = cam.MinZoom;

        // Smaller viewport should produce smaller min zoom
        cam.UpdateViewportSize(new Vector2(400, 300));
        cam.FitToContent(bbox);

        AssertThat(cam.MinZoom).IsLess(minZoomLarge);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ZoomAtScreen_ClampsToMinAndMax()
    {
        var cam = new BoardCameraController();
        cam.AnchorMode = Camera2D.AnchorModeEnum.DragCenter;
        cam.Zoom = Vector2.One;
        cam.UpdateViewportSize(new Vector2(800, 600));

        // Try to zoom way past MaxZoom
        cam.ZoomAtScreen(new Vector2(400, 300), 1000f);
        AssertThat(cam.Zoom.X).IsLessEqual(cam.MaxZoom + 0.01f);

        // Set very small MinZoom and try to zoom below it
        cam.Zoom = new Vector2(0.5f, 0.5f);
        var bbox = new Rect2(Vector2.Zero, new Vector2(100, 100));
        cam.FitToContent(bbox);

        // Try to zoom way below min
        cam.ZoomAtScreen(new Vector2(400, 300), 0.001f);
        AssertThat(cam.Zoom.X).IsGreaterEqual(cam.MinZoom - 0.01f);
    }
}
