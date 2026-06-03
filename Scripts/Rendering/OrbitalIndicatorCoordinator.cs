using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures;
using UtilityLibrary;

namespace Rendering;

/// <summary>
/// Main-thread coordinator that gathers orbital body positions and textures
/// from the scene tree and feeds them to OrbitalIndicatorEffect each frame.
/// </summary>
public partial class OrbitalIndicatorCoordinator : Node
{
    private const uint BODY_STRIDE = sizeof(float) * 8; // 8 floats per body
    private const int MAX_BODIES = 128;
    private const int TEX_RESOLUTION = 64;
    private const int BYTES_PER_TEX_LAYER = TEX_RESOLUTION * TEX_RESOLUTION * 4; // RGBA8

    /// <summary>
    /// Static flag checked by CelestialBody/SatelliteBody to skip old billboard creation.
    /// </summary>
    public static bool CompositorIndicatorsActive { get; private set; }

    [Export]
    public TextureRect? textureRect;

    private OrbitalIndicatorEffect? _effect;
    private byte[] _bodyBuffer = new byte[MAX_BODIES * BODY_STRIDE];
    private readonly List<IOrbitalBody> _allBodies = new();

    // Texture tracking
    private readonly Dictionary<IOrbitalBody, int> _textureLayerMap = new();
    private byte[] _textureBuffer = new byte[MAX_BODIES * BYTES_PER_TEX_LAYER];
    private int _nextLayerIndex;
    private bool _texturesDirty;

    // Classification fallback colors
    private static readonly Dictionary<string, Vector3> FallbackColors = new()
    {
        { "Star", new Vector3(1.0f, 0.95f, 0.5f) },
        { "NeutronStar", new Vector3(0.6f, 0.8f, 1.0f) },
        { "BlackHole", new Vector3(0.8f, 0.2f, 1.0f) },
        { "RockyPlanet", new Vector3(0.7f, 0.5f, 0.3f) },
        { "GasGiant", new Vector3(0.9f, 0.6f, 0.3f) },
        { "IceGiant", new Vector3(0.4f, 0.7f, 0.9f) },
        { "DwarfPlanet", new Vector3(0.6f, 0.6f, 0.6f) },
        { "Satellite", new Vector3(0.7f, 0.7f, 0.7f) },
        { "Belt", new Vector3(0.5f, 0.5f, 0.4f) },
    };

    public override void _Ready()
    {
        CompositorIndicatorsActive = true;
        SetupEffect();
    }

    public override void _ExitTree()
    {
        CompositorIndicatorsActive = false;
    }

    private void SetupEffect()
    {
        var worldEnv = FindWorldEnvironment();
        if (worldEnv == null)
        {
            GameLogger.Error("OrbitalIndicatorCoordinator: No WorldEnvironment found in scene");
            return;
        }

        var compositor = worldEnv.Compositor;
        if (compositor == null)
        {
            GameLogger.Error("OrbitalIndicatorCoordinator: WorldEnvironment has no Compositor");
            return;
        }

        _effect = new OrbitalIndicatorEffect();
        var effects =
            compositor.CompositorEffects ?? new Godot.Collections.Array<CompositorEffect>();
        effects.Add(_effect);
        compositor.CompositorEffects = effects;

        GameLogger.Info("OrbitalIndicatorCoordinator: CompositorEffect registered");
    }

    private WorldEnvironment? FindWorldEnvironment()
    {
        // Search immediate children of the scene root for WorldEnvironment
        var parent = GetParent();
        if (parent == null)
            return null;

        foreach (var child in parent.GetChildren())
        {
            if (child is WorldEnvironment we)
                return we;
        }
        return null;
    }

    private int _logFrameCounter;
    private const int LOG_INTERVAL = 300;

    public override void _Process(double delta)
    {
        if (_effect == null)
            return;

        GatherBodies();
        PackBodyData();
        CheckTextures();

        if (_texturesDirty)
        {
            _effect.UpdateTextureArray(_textureBuffer, _nextLayerIndex);
            _texturesDirty = false;
        }

        // Periodic diagnostic logging
        _logFrameCounter++;
        if (_logFrameCounter % LOG_INTERVAL == 0)
        {
            var cam = GetViewport()?.GetCamera3D();
            string camInfo =
                cam != null
                    ? $"cam=({cam.GlobalPosition.X:F1},{cam.GlobalPosition.Y:F1},{cam.GlobalPosition.Z:F1})"
                    : "cam=null";
            GameLogger.Info(
                $"[OrbitalCoordinator] bodies={_allBodies.Count}, texLayers={_nextLayerIndex}, {camInfo}"
            );

            // Log first body data for debugging
            if (_allBodies.Count > 0)
            {
                var first = _allBodies[0];
                var pos = ((Node3D)first).GlobalPosition;
                GameLogger.Info(
                    $"[OrbitalCoordinator] Body[0]: name={first.BodyName}, pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1}), radius={first.Radius:F1}, texReady={first.BillboardTextures.AreTexturesReady}"
                );
            }
        }
    }

    private void GatherBodies()
    {
        _allBodies.Clear();

        var celestials = GetTree().GetNodesInGroup("CelestialBody");
        foreach (var node in celestials)
        {
            if (node is not CelestialBody body)
                continue;

            _allBodies.Add(body);

            // Include satellites
            if (body.SatellitesContainer == null)
                continue;

            foreach (var child in body.SatellitesContainer.GetChildren())
            {
                if (child is IOrbitalBody satellite)
                    _allBodies.Add(satellite);
            }
        }
    }

    private void PackBodyData()
    {
        uint count = (uint)_allBodies.Count;

        // Resize buffer if needed
        uint requiredSize = count * BODY_STRIDE;
        if (_bodyBuffer.Length < requiredSize)
            _bodyBuffer = new byte[requiredSize];

        int offset = 0;
        for (int i = 0; i < count; i++)
        {
            var body = _allBodies[i];
            var pos = (body).BodyPosition;
            float radius = body.Radius;

            // Position + radius
            WriteFloat(_bodyBuffer, ref offset, pos.X);
            WriteFloat(_bodyBuffer, ref offset, pos.Y);
            WriteFloat(_bodyBuffer, ref offset, pos.Z);
            WriteFloat(_bodyBuffer, ref offset, radius);

            // Fallback color + texture layer index
            var color = GetFallbackColor(body.Classification);
            float layerIndex = _textureLayerMap.TryGetValue(body, out int idx) ? idx : -1f;

            WriteFloat(_bodyBuffer, ref offset, color.X);
            WriteFloat(_bodyBuffer, ref offset, color.Y);
            WriteFloat(_bodyBuffer, ref offset, color.Z);
            WriteFloat(_bodyBuffer, ref offset, layerIndex);
        }

        _effect!.UpdateBodyData(_bodyBuffer, count);
    }

    private void CheckTextures()
    {
        foreach (var body in _allBodies)
        {
            if (_textureLayerMap.ContainsKey(body))
                continue;

            if (!body.BillboardTextures.AreTexturesReady)
                continue;

            var distantTex = body.BillboardTextures.DistantTextureImage;
            if (distantTex == null)
                continue;

            var image = distantTex.GetImage();
            if (image == null)
                continue;

            // Ensure image is 64x64 RGBA8
            if (image.GetWidth() != TEX_RESOLUTION || image.GetHeight() != TEX_RESOLUTION)
                image.Resize(TEX_RESOLUTION, TEX_RESOLUTION);
            if (image.GetFormat() != Image.Format.Rgba8)
                image.Convert(Image.Format.Rgba8);

            byte[] pixels = image.GetData();
            if (pixels.Length != BYTES_PER_TEX_LAYER)
                continue;

            int layerIdx = _nextLayerIndex;

            // Grow texture buffer if needed
            int requiredSize = (layerIdx + 1) * BYTES_PER_TEX_LAYER;
            if (_textureBuffer.Length < requiredSize)
            {
                var newBuf = new byte[Math.Max(requiredSize, _textureBuffer.Length * 2)];
                Buffer.BlockCopy(_textureBuffer, 0, newBuf, 0, layerIdx * BYTES_PER_TEX_LAYER);
                _textureBuffer = newBuf;
            }

            Buffer.BlockCopy(
                pixels,
                0,
                _textureBuffer,
                layerIdx * BYTES_PER_TEX_LAYER,
                BYTES_PER_TEX_LAYER
            );
            _textureLayerMap[body] = layerIdx;
            _nextLayerIndex++;
            _texturesDirty = true;
        }
    }

    private static Vector3 GetFallbackColor(BodyClassification classification)
    {
        // Satellites/belts share one fallback color key; everything else keys off its OrbitalBodyType.
        string key = classification switch
        {
            BodyClassification.Satellite => "Satellite",
            BodyClassification.Belt => "Belt",
            _ => classification.Type.ToString(),
        };

        return FallbackColors.TryGetValue(key, out var color)
            ? color
            : new Vector3(0.7f, 0.7f, 0.7f);
    }

    private static void WriteFloat(byte[] buf, ref int offset, float value)
    {
        MemoryMarshal.Write(buf.AsSpan(offset), in value);
        offset += 4;
    }
}
