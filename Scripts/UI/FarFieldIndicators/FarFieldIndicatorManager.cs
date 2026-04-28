using System.Collections.Generic;
using Godot;
using UtilityLibrary;

namespace UI.FarFieldIndicators;

/// <summary>
/// Manages 2D screen-space icons for orbital bodies that are beyond the camera's far plane.
/// Icons appear when bodies are more than 100 units beyond the camera far plane.
/// </summary>
public partial class FarFieldIndicatorManager : Control
{
    [ExportCategory("Scene References")]
    [Export]
    public PackedScene? FarFieldIconScene { get; set; }

    [Export]
    public Camera3D? TargetCamera { get; set; }

    [ExportCategory("Settings")]
    [Export]
    public float DistanceBuffer { get; set; } = 100.0f;

    [Export]
    public float IconMargin { get; set; } = 32.0f;

    [Export]
    public int RayCastsPerFrame { get; set; } = 5;

    private readonly List<IOrbitalBody> _trackedBodies = new();
    private readonly Dictionary<IOrbitalBody, FarFieldIcon> _bodyIconMap = new();
    private readonly Dictionary<IOrbitalBody, bool> _bodyOcclusionState = new();
    private readonly Dictionary<IOrbitalBody, float> _bodyDistances = new();

    private int _rayCastIndex = 0;

    public override void _Ready()
    {
        GameLogger.EnterFunction(nameof(_Ready));

        // Load default scene if not set
        if (FarFieldIconScene == null)
        {
            FarFieldIconScene = GD.Load<PackedScene>(
                "res://UI/FarFieldIndicators/FarFieldIcon.tscn"
            );
        }

        // Find camera if not set
        if (TargetCamera == null)
        {
            TargetCamera = FindPlayerCamera();
        }

        // Connect to system generation complete signal
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.SystemGenerationComplete += OnSystemGenerationComplete;
        }

        // Initial scan for existing bodies
        ScanForOrbitalBodies();

        GameLogger.Info(
            $"FarFieldIndicatorManager initialized. Tracking {_trackedBodies.Count} bodies."
        );
        GameLogger.ExitFunction(nameof(_Ready));
    }

    public override void _ExitTree()
    {
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.SystemGenerationComplete -= OnSystemGenerationComplete;
        }
    }

    public override void _Process(double delta)
    {
        if (TargetCamera == null)
            return;

        float farPlane = TargetCamera.Far;
        Vector2 viewportSize = GetViewportRect().Size;
        Vector3 cameraPos = TargetCamera.GlobalPosition;

        // Filter and sort bodies by distance
        var farBodies = new List<(IOrbitalBody Body, float Distance, Vector3 Position)>();

        foreach (var body in _trackedBodies)
        {
            if (body is not Node3D node3D)
                continue;

            // Check if body has valid textures
            if (body.BillboardTextures == null || !body.BillboardTextures.AreTexturesReady)
                continue;

            Vector3 bodyPos = body.BodyPosition;
            float distance = cameraPos.DistanceTo(bodyPos);

            // Only track bodies beyond far plane + buffer
            if (distance > farPlane + DistanceBuffer)
            {
                farBodies.Add((body, distance, bodyPos));
                _bodyDistances[body] = distance;
            }
            else
            {
                // Hide icon if body is within visible range
                if (_bodyIconMap.TryGetValue(body, out var icon))
                {
                    icon.SetVisible(false);
                }
            }
        }

        // Sort by distance (furthest first, closest last - closer ones drawn on top)
        farBodies.Sort((a, b) => b.Distance.CompareTo(a.Distance));

        // Process staggered raycasts for occlusion checking
        PerformStaggeredRayCasts(farBodies, cameraPos);

        // Update icons
        int zIndex = 0;
        foreach (var (body, distance, position) in farBodies)
        {
            UpdateBodyIcon(body, position, distance, viewportSize, zIndex++);
        }
    }

    /// <summary>
    /// Performs staggered raycasts to check occlusion for far bodies.
    /// </summary>
    private void PerformStaggeredRayCasts(
        List<(IOrbitalBody Body, float Distance, Vector3 Position)> farBodies,
        Vector3 cameraPos
    )
    {
        if (farBodies.Count == 0 || TargetCamera == null)
            return;

        var spaceState = TargetCamera.GetWorld3D()?.DirectSpaceState;
        if (spaceState == null)
            return;

        // Process a subset of bodies each frame
        for (int i = 0; i < RayCastsPerFrame && farBodies.Count > 0; i++)
        {
            _rayCastIndex = (_rayCastIndex + 1) % farBodies.Count;
            var (body, _, position) = farBodies[_rayCastIndex];

            if (body is not Node3D)
                continue;

            // Raycast from camera to body center
            var query = PhysicsRayQueryParameters3D.Create(cameraPos, position);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;

            var result = spaceState.IntersectRay(query);

            // If raycast hits something before reaching the body, it's occluded
            bool isOccluded = false;
            if (result.Count > 0 && result.TryGetValue("position", out var hitPos))
            {
                float hitDistance = cameraPos.DistanceTo((Vector3)hitPos);
                float bodyDistance = cameraPos.DistanceTo(position);
                isOccluded = hitDistance < bodyDistance - 1.0f; // Small tolerance
            }

            _bodyOcclusionState[body] = isOccluded;
        }
    }

    /// <summary>
    /// Updates or creates the icon for a body at the specified position.
    /// </summary>
    private void UpdateBodyIcon(
        IOrbitalBody body,
        Vector3 position,
        float distance,
        Vector2 viewportSize,
        int zIndex
    )
    {
        // Get or create icon
        if (!_bodyIconMap.TryGetValue(body, out var icon))
        {
            icon = CreateIconForBody(body);
            if (icon == null)
                return;
            _bodyIconMap[body] = icon;
        }

        // Check occlusion state (default to not occluded if not yet checked)
        bool isOccluded = _bodyOcclusionState.TryGetValue(body, out var occluded) && occluded;

        if (TargetCamera!.IsPositionBehind(position))
            return;
        // Project 3D position to screen
        Vector2 screenPos = TargetCamera!.UnprojectPosition(position);

        icon.UpdatePosition(screenPos);
        icon.SetVisible(true);

        // Update texture based on distance
        ImageTexture? texture = body.BillboardTextures.GetTextureForDistance(distance);
        if (texture != null)
        {
            icon.UpdateTexture(texture);
        }

        // Update z-index so closer bodies draw on top
        icon.ZIndex = zIndex;
    }

    /// <summary>
    /// Creates a new icon instance for the specified body.
    /// </summary>
    private FarFieldIcon? CreateIconForBody(IOrbitalBody body)
    {
        if (FarFieldIconScene == null)
            return null;

        var icon = FarFieldIconScene.Instantiate<FarFieldIcon>();
        if (icon == null)
            return null;

        AddChild(icon);

        // Set up with initial texture and name
        ImageTexture? initialTexture = body.BillboardTextures.DistantTextureImage;
        icon.Setup(body.BodyName, initialTexture);

        GameLogger.Debug($"Created far-field icon for body: {body.BodyName}");

        return icon;
    }

    /// <summary>
    /// Called when system generation is complete - rescan for new bodies.
    /// </summary>
    private void OnSystemGenerationComplete(string batchId, int totalBodies, int successfulBodies)
    {
        GameLogger.Info(
            $"System generation complete (batch: {batchId}, bodies: {successfulBodies}/{totalBodies}) - rescanning for orbital bodies."
        );
        ScanForOrbitalBodies();
    }

    /// <summary>
    /// Scans the scene for all IOrbitalBody instances.
    /// </summary>
    public void ScanForOrbitalBodies()
    {
        _trackedBodies.Clear();

        // Find system container
        var systemContainer =
            GetNodeOrNull("/root/GameScene/system_container")
            ?? GetTree().CurrentScene?.GetNodeOrNull("system_container");

        if (systemContainer == null)
        {
            // Fallback: search entire scene
            var root = GetTree().Root;
            CollectOrbitalBodiesRecursive(root);
        }
        else
        {
            CollectOrbitalBodiesRecursive(systemContainer);
        }

        // Clean up icons for bodies that no longer exist
        var bodiesToRemove = new List<IOrbitalBody>();
        foreach (var body in _bodyIconMap.Keys)
        {
            if (body is Node node && !IsInstanceValid(node))
            {
                bodiesToRemove.Add(body);
            }
            else if (!_trackedBodies.Contains(body))
            {
                bodiesToRemove.Add(body);
            }
        }

        foreach (var body in bodiesToRemove)
        {
            if (_bodyIconMap.TryGetValue(body, out var icon))
            {
                icon.QueueFree();
                _bodyIconMap.Remove(body);
            }
            _bodyOcclusionState.Remove(body);
            _bodyDistances.Remove(body);
        }

        GameLogger.Info(
            $"FarFieldIndicatorManager scanning complete. Found {_trackedBodies.Count} orbital bodies."
        );
    }

    /// <summary>
    /// Recursively collects all IOrbitalBody instances from a node and its children.
    /// </summary>
    private void CollectOrbitalBodiesRecursive(Node node)
    {
        // Check if this node is an orbital body
        if (node is IOrbitalBody body && body is not Barycenter)
        {
            _trackedBodies.Add(body);
        }

        // Recurse through children
        foreach (Node child in node.GetChildren())
        {
            CollectOrbitalBodiesRecursive(child);
        }
    }

    /// <summary>
    /// Attempts to find the player camera in the scene.
    /// </summary>
    private Camera3D? FindPlayerCamera()
    {
        // Try common camera paths
        var camera =
            GetNodeOrNull<Camera3D>("/root/GameScene/Player/Camera3D")
            ?? GetNodeOrNull<Camera3D>("/root/GameScene/Camera3D")
            ?? GetTree().CurrentScene?.GetNodeOrNull<Camera3D>("Camera3D");

        if (camera != null)
            return camera;

        // Fallback: search recursively
        return FindCameraRecursive(GetTree().Root);
    }

    /// <summary>
    /// Recursively searches for a Camera3D in the scene.
    /// </summary>
    private Camera3D? FindCameraRecursive(Node node)
    {
        if (node is Camera3D cam && cam.Current)
            return cam;

        foreach (Node child in node.GetChildren())
        {
            var found = FindCameraRecursive(child);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Adds a body to be tracked by the manager.
    /// </summary>
    public void AddBody(IOrbitalBody body)
    {
        if (!_trackedBodies.Contains(body))
        {
            _trackedBodies.Add(body);
            GameLogger.Debug($"Added body to far-field tracking: {body.BodyName}");
        }
    }

    /// <summary>
    /// Removes a body from tracking.
    /// </summary>
    public void RemoveBody(IOrbitalBody body)
    {
        if (_trackedBodies.Remove(body))
        {
            if (_bodyIconMap.TryGetValue(body, out var icon))
            {
                icon.QueueFree();
                _bodyIconMap.Remove(body);
            }
            _bodyOcclusionState.Remove(body);
            _bodyDistances.Remove(body);

            GameLogger.Debug($"Removed body from far-field tracking: {body.BodyName}");
        }
    }
}
