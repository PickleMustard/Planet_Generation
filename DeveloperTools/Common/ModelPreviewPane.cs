#if DEBUG
using System.Collections.Generic;
using System.Linq;
using Godot;
using UtilityLibrary;

namespace DeveloperTools.Common;

/// <summary>
/// SubViewport-backed 3D preview of a building model with orbit camera.
/// Loads the chosen PackedScene from a res:// model path, applies scale/rotation,
/// and (when present) plays a named clip from the first AnimationPlayer child.
/// Emits AnimationListChanged when a new model loads so the parent VisualSection
/// can refresh its animation OptionButton.
/// </summary>
public partial class ModelPreviewPane : SubViewportContainer
{
    [Signal]
    public delegate void AnimationListChangedEventHandler(string[] animations);

    private SubViewport _viewport = null!;
    private Camera3D _camera = null!;
    private DirectionalLight3D _light = null!;
    private Node3D _modelHost = null!;
    private AnimationPlayer? _currentPlayer;

    private string? _loadedPath;
    private float _yaw = 0.5f;
    private float _pitch = -0.4f;
    private float _distance = 4f;
    private float _modelRadius = 1f;
    private bool _dragging;

    public override void _Ready()
    {
        base._Ready();
        Stretch = true;
        CustomMinimumSize = new Vector2(320, 320);
        MouseFilter = MouseFilterEnum.Stop;

        _viewport = new SubViewport
        {
            Size = new Vector2I(320, 320),
            OwnWorld3D = true,
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        };
        AddChild(_viewport);

        _camera = new Camera3D { Current = true };
        _viewport.AddChild(_camera);

        _light = new DirectionalLight3D();
        _light.RotationDegrees = new Vector3(-45, 35, 0);
        _viewport.AddChild(_light);

        _modelHost = new Node3D();
        _viewport.AddChild(_modelHost);

        UpdateCameraTransform();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        // Keep render target in sync with control size for crisp preview.
        var size = (Vector2I)Size;
        if (size.X > 0 && size.Y > 0 && _viewport.Size != size)
            _viewport.Size = size;
    }

    public void ApplyVisual(string? modelPath, float scale, Vector3 rotationOffsetDeg,
        string? animationName)
    {
        if (modelPath != _loadedPath)
        {
            ReloadModel(modelPath);
            _loadedPath = modelPath;
        }

        _modelHost.Scale = Vector3.One * Mathf.Max(scale, 0.0001f);
        _modelHost.RotationDegrees = rotationOffsetDeg;

        if (_currentPlayer != null)
        {
            if (!string.IsNullOrEmpty(animationName) && _currentPlayer.HasAnimation(animationName))
            {
                if (_currentPlayer.CurrentAnimation != animationName)
                    _currentPlayer.Play(animationName);
            }
            else if (_currentPlayer.IsPlaying())
            {
                _currentPlayer.Stop();
            }
        }
    }

    private void ReloadModel(string? modelPath)
    {
        foreach (var c in _modelHost.GetChildren()) c.QueueFree();
        _currentPlayer = null;
        _modelRadius = 1f;

        if (string.IsNullOrEmpty(modelPath))
        {
            EmitSignal(SignalName.AnimationListChanged, new[] { "" }[..0]);
            FrameCamera();
            return;
        }

        if (!Godot.FileAccess.FileExists(modelPath))
        {
            GameLogger.Warning($"ModelPreviewPane: model file not found: {modelPath}");
            EmitSignal(SignalName.AnimationListChanged, new[] { "" }[..0]);
            FrameCamera();
            return;
        }

        PackedScene? packed = null;
        try { packed = GD.Load<Registries.ModelConfig>(modelPath)?.Model; }
        catch (System.Exception ex)
        {
            GameLogger.Warning($"ModelPreviewPane: failed to load '{modelPath}': {ex.Message}");
        }
        if (packed == null)
        {
            EmitSignal(SignalName.AnimationListChanged, new[] { "" }[..0]);
            FrameCamera();
            return;
        }

        Node3D? instance = null;
        try { instance = packed.Instantiate<Node3D>(); }
        catch (System.Exception ex)
        {
            GameLogger.Warning($"ModelPreviewPane: failed to instantiate '{modelPath}': {ex.Message}");
        }
        if (instance == null)
        {
            EmitSignal(SignalName.AnimationListChanged, new[] { "" }[..0]);
            FrameCamera();
            return;
        }

        _modelHost.AddChild(instance);

        _currentPlayer = FindFirstAnimationPlayer(instance);
        var anims = _currentPlayer != null
            ? _currentPlayer.GetAnimationList()
            : System.Array.Empty<string>();
        EmitSignal(SignalName.AnimationListChanged, anims);

        _modelRadius = ComputeRadius(instance);
        FrameCamera();
    }

    private static AnimationPlayer? FindFirstAnimationPlayer(Node root)
    {
        if (root is AnimationPlayer ap) return ap;
        foreach (var child in root.GetChildren())
        {
            var found = FindFirstAnimationPlayer(child);
            if (found != null) return found;
        }
        return null;
    }

    private static float ComputeRadius(Node3D instance)
    {
        var aabb = new Aabb(instance.Position, Vector3.Zero);
        bool any = false;
        Walk(instance, ref aabb, ref any);
        if (!any) return 1f;
        var size = aabb.Size;
        return Mathf.Max(0.5f, size.Length() * 0.5f);
    }

    private static void Walk(Node node, ref Aabb aabb, ref bool any)
    {
        if (node is VisualInstance3D vi)
        {
            var gAabb = vi.GetAabb();
            if (gAabb.Size != Vector3.Zero)
            {
                var transformed = vi.GlobalTransform * gAabb;
                aabb = any ? aabb.Merge(transformed) : transformed;
                any = true;
            }
        }
        foreach (var child in node.GetChildren())
            Walk(child, ref aabb, ref any);
    }

    private void FrameCamera()
    {
        _distance = Mathf.Clamp(_modelRadius * 3f, 1f, 200f);
        UpdateCameraTransform();
    }

    private void UpdateCameraTransform()
    {
        float cy = Mathf.Cos(_yaw), sy = Mathf.Sin(_yaw);
        float cp = Mathf.Cos(_pitch), sp = Mathf.Sin(_pitch);
        Vector3 offset = new(_distance * cp * sy, _distance * sp, _distance * cp * cy);
        var basis = Basis.LookingAt(-offset.Normalized(), Vector3.Up);
        _camera.GlobalTransform = new Transform3D(basis, offset);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _dragging = mb.Pressed;
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            {
                _distance = Mathf.Clamp(_distance * 0.9f, 0.2f, 200f);
                UpdateCameraTransform();
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            {
                _distance = Mathf.Clamp(_distance * 1.1f, 0.2f, 200f);
                UpdateCameraTransform();
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion mm && _dragging)
        {
            _yaw -= mm.Relative.X * 0.01f;
            _pitch = Mathf.Clamp(_pitch + mm.Relative.Y * 0.01f, -1.5f, 1.5f);
            UpdateCameraTransform();
            AcceptEvent();
        }
    }
}
#endif
