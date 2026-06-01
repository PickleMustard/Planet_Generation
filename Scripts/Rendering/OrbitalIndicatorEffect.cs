using System;
using System.Runtime.InteropServices;
using Godot;
using Godot.Collections;
using UtilityLibrary;

namespace Rendering;

/// <summary>
/// CompositorEffect that renders orbital body indicators via a fullscreen compute shader.
/// Runs on the rendering thread. Receives body data from OrbitalIndicatorCoordinator
/// on the main thread via thread-safe UpdateBodyData/UpdateTextureArray.
/// </summary>
[GlobalClass]
public partial class OrbitalIndicatorEffect : CompositorEffect
{
    private const uint BODY_STRIDE = sizeof(float) * 8; // 8 floats * 4 bytes per body
    private const int CAMERA_UBO_SIZE = 160; // std140: mat4(64) + vec4(16) + vec2(8) + float(4) + float(4)
    private const int TEX_RESOLUTION = 64; // Billboard texture resolution per layer
    private const float MIN_INDICATOR_PX = 4.0f;

    /// <summary>
    /// When true, the shader draws a debug checkerboard in the corner and skips depth test.
    /// Toggle this from the coordinator or debugger.
    /// </summary>
    public bool DebugMode { get; set; } = true;

    // Long-lived GPU resources
    private Rid _shader;
    private Rid _pipeline;
    private Rid _bodySsbo;
    private Rid _sampler;
    private Rid _textureArray;
    private uint _ssboCapacity;
    private int _textureLayerCount;
    private RenderingDevice? _rd;

    // Persistent camera uniform buffer
    private Rid _camUniformBuffer;

    // Intermediate color texture for compute write + copy-back
    private Rid _colorCopy;
    private Vector2I _colorCopySize;

    // Thread-safe data from coordinator
    private byte[]? _pendingBodyData;
    private uint _pendingBodyCount;
    private byte[]? _pendingTextureData;
    private int _pendingTextureLayerCount;
    private bool _texturesDirty;
    private readonly object _dataLock = new();

    // Diagnostic logging throttle
    private int _frameCount;
    private const int LOG_INTERVAL = 300; // Log every N frames
    public Callable initialize;

    public OrbitalIndicatorEffect()
    {
        initialize = new Callable(this, "InitializeRender");
        RenderingServer.CallOnRenderThread(initialize);
    }

    public void InitializeRender()
    {
        _rd = RenderingServer.GetRenderingDevice();
        EffectCallbackType = EffectCallbackTypeEnum.PostTransparent;
        NeedsMotionVectors = false;
        NeedsNormalRoughness = false;
        AccessResolvedColor = true;
        AccessResolvedDepth = true;
        Enabled = true;

        if (_rd != null)
        {
            EnsureShaderSetup(_rd);
            EnsureSampler(_rd);
            GameLogger.Info(
                $"OrbitalIndicatorEffect: shader valid={_shader.IsValid}, pipeline valid={_pipeline.IsValid}, sampler valid={_sampler.IsValid}"
            );
        }
        else
        {
            GameLogger.Error("OrbitalIndicatorEffect: RenderingDevice is null in constructor");
        }
    }

    /// <summary>
    /// Called from main thread by OrbitalIndicatorCoordinator each frame.
    /// </summary>
    public void UpdateBodyData(byte[] data, uint bodyCount)
    {
        lock (_dataLock)
        {
            _pendingBodyData = data;
            _pendingBodyCount = bodyCount;
        }
    }

    /// <summary>
    /// Called from main thread when body textures change.
    /// pixelData: all layers concatenated, layerCount * 64 * 64 * 4 bytes (RGBA8).
    /// </summary>
    public void UpdateTextureArray(byte[] pixelData, int layerCount)
    {
        lock (_dataLock)
        {
            _pendingTextureData = pixelData;
            _pendingTextureLayerCount = layerCount;
            _texturesDirty = true;
        }
    }

    public override void _RenderCallback(int effectCallbackType, RenderData renderData)
    {
        if (_rd == null || !_shader.IsValid || !_pipeline.IsValid)
            return;

        var renderSceneBuffers = renderData.GetRenderSceneBuffers();
        var renderSceneData = renderData.GetRenderSceneData();
        if (renderSceneBuffers == null || renderSceneData == null)
            return;

        var buffers = (RenderSceneBuffersRD)renderSceneBuffers;
        var size = buffers.GetInternalSize();
        if (size.X == 0 || size.Y == 0)
            return;

        // Snapshot data from main thread
        byte[]? bodyData;
        uint bodyCount;
        byte[]? textureData;
        int texLayerCount;
        bool texDirty;

        lock (_dataLock)
        {
            bodyData = _pendingBodyData;
            bodyCount = _pendingBodyCount;
            textureData = _pendingTextureData;
            texLayerCount = _pendingTextureLayerCount;
            texDirty = _texturesDirty;
            _texturesDirty = false;
        }

        if (bodyData == null || bodyCount == 0)
            return;

        // Periodic diagnostic logging
        bool shouldLog = (_frameCount % LOG_INTERVAL == 0);
        _frameCount++;

        if (shouldLog)
        {
            GameLogger.Info(
                $"[OrbitalIndicator] Frame {_frameCount}: bodies={bodyCount}, size={size}, debug={DebugMode}"
            );
        }

        // Update texture array if dirty
        if (texDirty && textureData != null && texLayerCount > 0)
        {
            RebuildTextureArray(_rd, textureData, texLayerCount);
            if (shouldLog)
                GameLogger.Info(
                    $"[OrbitalIndicator] Rebuilt texture array: {texLayerCount} layers"
                );
        }

        // Ensure we have a texture array (even if empty, need a placeholder)
        if (!_textureArray.IsValid)
            CreatePlaceholderTextureArray(_rd);

        // Update body SSBO
        uint requiredSize = bodyCount * BODY_STRIDE;
        EnsureSsboCapacity(_rd, requiredSize);
        _rd.BufferUpdate(_bodySsbo, 0, requiredSize, bodyData);

        // Build camera uniform data
        float minPxValue = DebugMode ? (1000.0f + MIN_INDICATOR_PX) : MIN_INDICATOR_PX;
        byte[] camBytes = BuildCameraUniformData(renderSceneData, size, bodyCount, minPxValue);
        EnsureCameraBuffer(_rd);
        _rd.BufferUpdate(_camUniformBuffer, 0, (uint)camBytes.Length, camBytes);

        uint viewCount = buffers.GetViewCount();
        for (uint i = 0; i < viewCount; i++)
        {
            // Get the actual framebuffer color and depth layers
            // p_msaa=false to get the resolved (non-MSAA) layers
            var colorImage = buffers.GetTexture("render_buffers", "color");
            var depthImage = buffers.GetTexture("render_buffers", "depth");

            if (!colorImage.IsValid || !depthImage.IsValid)
            {
                if (shouldLog)
                    GameLogger.Warning(
                        $"[OrbitalIndicator] Invalid framebuffer RIDs: color={colorImage.IsValid}, depth={depthImage.IsValid}"
                    );
                return;
            }

            // --- Build uniform set 0: camera, bodies, color, depth ---
            var uniforms0 = new Array<RDUniform>();

            var uCam = new RDUniform();
            uCam.UniformType = RenderingDevice.UniformType.UniformBuffer;
            uCam.Binding = 0;
            uCam.AddId(_camUniformBuffer);
            uniforms0.Add(uCam);

            var uBodies = new RDUniform();
            uBodies.UniformType = RenderingDevice.UniformType.StorageBuffer;
            uBodies.Binding = 1;
            uBodies.AddId(_bodySsbo);
            uniforms0.Add(uBodies);

            var uColor = new RDUniform();
            uColor.UniformType = RenderingDevice.UniformType.Image;
            uColor.Binding = 2;
            uColor.AddId(colorImage);
            uniforms0.Add(uColor);

            var uDepth = new RDUniform();
            uDepth.UniformType = RenderingDevice.UniformType.Image;
            uDepth.Binding = 3;
            uDepth.AddId(depthImage);
            uniforms0.Add(uDepth);

            //var uniformSet0 = _rd.UniformSetCreate(uniforms0, _shader, 0);
            var uniformSet0 = UniformSetCacheRD.GetCache(_shader, 0, uniforms0);

            // --- Build uniform set 1: billboard texture array ---
            var uniforms1 = new Array<RDUniform>();

            var uTex = new RDUniform();
            uTex.UniformType = RenderingDevice.UniformType.SamplerWithTexture;
            uTex.Binding = 0;
            uTex.AddId(_sampler);
            uTex.AddId(_textureArray);
            uniforms1.Add(uTex);

            //var uniformSet1 = _rd.UniformSetCreate(uniforms1, _shader, 1);
            var uniformSet1 = UniformSetCacheRD.GetCache(_shader, 1, uniforms1);

            if (!uniformSet0.IsValid || !uniformSet1.IsValid)
            {
                if (shouldLog)
                    GameLogger.Warning(
                        $"[OrbitalIndicator] Invalid uniform sets: set0={uniformSet0.IsValid}, set1={uniformSet1.IsValid}"
                    );
                if (uniformSet0.IsValid)
                    _rd.FreeRid(uniformSet0);
                if (uniformSet1.IsValid)
                    _rd.FreeRid(uniformSet1);
                return;
            }

            // Dispatch compute
            int groupsX = (size.X + 15) / 16;
            int groupsY = (size.Y + 15) / 16;

            var computeList = _rd.ComputeListBegin();
            _rd.ComputeListBindComputePipeline(computeList, _pipeline);
            _rd.ComputeListBindUniformSet(computeList, uniformSet0, 0);
            _rd.ComputeListBindUniformSet(computeList, uniformSet1, 1);
            _rd.ComputeListDispatch(computeList, (uint)groupsX, (uint)groupsY, 1);
            _rd.ComputeListEnd();

            // Free per-frame uniform sets
            _rd.FreeRid(uniformSet0);
            _rd.FreeRid(uniformSet1);

            if (shouldLog)
            {
                GameLogger.Info(
                    $"[OrbitalIndicator] Dispatch: {groupsX}x{groupsY} groups, colorCopy={_colorCopy.IsValid}"
                );
            }
        }
    }

    private void EnsureShaderSetup(RenderingDevice rd)
    {
        if (_shader.IsValid)
            return;

        var shaderFile = GD.Load<RDShaderFile>("res://Shaders/orbital_indicator.glsl");
        if (shaderFile == null)
        {
            GameLogger.Error("OrbitalIndicatorEffect: Failed to load orbital_indicator.glsl");
            return;
        }

        var spirv = shaderFile.GetSpirV();
        var compileError = spirv.GetStageCompileError(RenderingDevice.ShaderStage.Compute);
        if (!string.IsNullOrEmpty(compileError))
        {
            GameLogger.Error($"OrbitalIndicatorEffect: Shader compile error: {compileError}");
            return;
        }

        _shader = rd.ShaderCreateFromSpirV(spirv);
        if (!_shader.IsValid)
        {
            GameLogger.Error("OrbitalIndicatorEffect: Failed to create shader from SPIR-V");
            return;
        }

        _pipeline = rd.ComputePipelineCreate(_shader);
        if (!_pipeline.IsValid)
            GameLogger.Error("OrbitalIndicatorEffect: Failed to create compute pipeline");
    }

    private void EnsureSampler(RenderingDevice rd)
    {
        if (_sampler.IsValid)
            return;

        var samplerState = new RDSamplerState();
        samplerState.MinFilter = RenderingDevice.SamplerFilter.Linear;
        samplerState.MagFilter = RenderingDevice.SamplerFilter.Linear;
        samplerState.RepeatU = RenderingDevice.SamplerRepeatMode.ClampToEdge;
        samplerState.RepeatV = RenderingDevice.SamplerRepeatMode.ClampToEdge;
        _sampler = rd.SamplerCreate(samplerState);
    }

    private void EnsureSsboCapacity(RenderingDevice rd, uint requiredBytes)
    {
        if (_bodySsbo.IsValid && _ssboCapacity >= requiredBytes)
            return;

        if (_bodySsbo.IsValid)
            rd.FreeRid(_bodySsbo);

        _bodySsbo = rd.StorageBufferCreate(requiredBytes);
        _ssboCapacity = requiredBytes;
    }

    private void EnsureCameraBuffer(RenderingDevice rd)
    {
        if (_camUniformBuffer.IsValid)
            return;

        _camUniformBuffer = rd.UniformBufferCreate(CAMERA_UBO_SIZE);
    }

    /// <summary>
    /// Creates or recreates the intermediate color texture that the compute shader writes to.
    /// Matches the format of the actual color layer but with StorageBit for image2D access.
    /// </summary>
    private void EnsureColorCopy(RenderingDevice rd, Rid colorImage, Vector2I size)
    {
        if (_colorCopy.IsValid && _colorCopySize == size)
            return;

        if (_colorCopy.IsValid)
            rd.FreeRid(_colorCopy);

        // Match the actual color buffer's format
        var srcFmt = rd.TextureGetFormat(colorImage);

        var fmt = new RDTextureFormat();
        fmt.Width = (uint)size.X;
        fmt.Height = (uint)size.Y;
        fmt.Depth = 1;
        fmt.ArrayLayers = 1;
        fmt.Format = srcFmt.Format; // Match the real framebuffer format
        fmt.TextureType = RenderingDevice.TextureType.Type2D;
        fmt.UsageBits =
            RenderingDevice.TextureUsageBits.StorageBit
            | // For image2D in compute
            RenderingDevice.TextureUsageBits.CanCopyToBit
            | // For copy FROM framebuffer
            RenderingDevice.TextureUsageBits.CanCopyFromBit; // For copy BACK to framebuffer

        _colorCopy = rd.TextureCreate(fmt, new RDTextureView());
        _colorCopySize = size;

        GameLogger.Info(
            $"[OrbitalIndicator] Created color copy texture: {size.X}x{size.Y}, format={srcFmt.Format}"
        );
    }

    private void RebuildTextureArray(RenderingDevice rd, byte[] pixelData, int layerCount)
    {
        if (_textureArray.IsValid)
            rd.FreeRid(_textureArray);

        var format = new RDTextureFormat();
        format.Width = TEX_RESOLUTION;
        format.Height = TEX_RESOLUTION;
        format.Depth = 1;
        format.ArrayLayers = (uint)layerCount;
        format.Format = RenderingDevice.DataFormat.R8G8B8A8Unorm;
        format.TextureType = RenderingDevice.TextureType.Type2DArray;
        format.UsageBits =
            RenderingDevice.TextureUsageBits.SamplingBit
            | RenderingDevice.TextureUsageBits.CanUpdateBit;

        var layers = new Array<byte[]>();
        int bytesPerLayer = TEX_RESOLUTION * TEX_RESOLUTION * 4;
        for (int i = 0; i < layerCount; i++)
        {
            var layer = new byte[bytesPerLayer];
            Buffer.BlockCopy(pixelData, i * bytesPerLayer, layer, 0, bytesPerLayer);
            layers.Add(layer);
        }

        _textureArray = rd.TextureCreate(format, new RDTextureView(), layers);
        _textureLayerCount = layerCount;
    }

    private void CreatePlaceholderTextureArray(RenderingDevice rd)
    {
        int bytesPerLayer = TEX_RESOLUTION * TEX_RESOLUTION * 4;
        var gray = new byte[bytesPerLayer];
        for (int i = 0; i < bytesPerLayer; i += 4)
        {
            gray[i] = 128; // R
            gray[i + 1] = 128; // G
            gray[i + 2] = 128; // B
            gray[i + 3] = 255; // A
        }

        var format = new RDTextureFormat();
        format.Width = TEX_RESOLUTION;
        format.Height = TEX_RESOLUTION;
        format.Depth = 1;
        format.ArrayLayers = 1;
        format.Format = RenderingDevice.DataFormat.R8G8B8A8Unorm;
        format.TextureType = RenderingDevice.TextureType.Type2DArray;
        format.UsageBits =
            RenderingDevice.TextureUsageBits.SamplingBit
            | RenderingDevice.TextureUsageBits.CanUpdateBit;

        var layers = new Array<byte[]> { gray };
        _textureArray = rd.TextureCreate(format, new RDTextureView(), layers);
        _textureLayerCount = 1;
    }

    private byte[] BuildCameraUniformData(
        RenderSceneData sceneData,
        Vector2I size,
        uint bodyCount,
        float minIndicatorPx
    )
    {
        var buf = new byte[CAMERA_UBO_SIZE];
        int offset = 0;

        // View-projection matrix (combined): 16 floats, column-major
        var vp = sceneData.GetViewProjection(0);
        WriteProjection(buf, ref offset, vp);

        var camProj = sceneData.GetCamProjection();
        WriteProjection(buf, ref offset, camProj);

        // Camera position as vec4
        var camTransform = sceneData.GetCamTransform();
        WriteFloat(buf, ref offset, (float)camTransform.Origin.X);
        WriteFloat(buf, ref offset, (float)camTransform.Origin.Y);
        WriteFloat(buf, ref offset, (float)camTransform.Origin.Z);
        WriteFloat(buf, ref offset, 0.0f); // padding

        // Screen size
        WriteFloat(buf, ref offset, size.X);
        WriteFloat(buf, ref offset, size.Y);

        // Object count
        WriteFloat(buf, ref offset, bodyCount);

        // Minimum indicator pixel size (encoded with debug flag)
        WriteFloat(buf, ref offset, minIndicatorPx);

        return buf;
    }

    private static void WriteFloat(byte[] buf, ref int offset, float value)
    {
        MemoryMarshal.Write(buf.AsSpan(offset), in value);
        offset += 4;
    }

    private static void WriteProjection(byte[] buf, ref int offset, Projection proj)
    {
        WriteVector4(buf, ref offset, proj.X);
        WriteVector4(buf, ref offset, proj.Y);
        WriteVector4(buf, ref offset, proj.Z);
        WriteVector4(buf, ref offset, proj.W);
    }

    private static void WriteVector4(byte[] buf, ref int offset, Vector4 v)
    {
        WriteFloat(buf, ref offset, v.X);
        WriteFloat(buf, ref offset, v.Y);
        WriteFloat(buf, ref offset, v.Z);
        WriteFloat(buf, ref offset, v.W);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
            Cleanup();
    }

    private void Cleanup()
    {
        var rd = RenderingServer.GetRenderingDevice();
        if (rd == null)
            return;

        if (_pipeline.IsValid)
            rd.FreeRid(_pipeline);
        if (_shader.IsValid)
            rd.FreeRid(_shader);
        if (_bodySsbo.IsValid)
            rd.FreeRid(_bodySsbo);
        if (_sampler.IsValid)
            rd.FreeRid(_sampler);
        if (_textureArray.IsValid)
            rd.FreeRid(_textureArray);
        if (_camUniformBuffer.IsValid)
            rd.FreeRid(_camUniformBuffer);
        if (_colorCopy.IsValid)
            rd.FreeRid(_colorCopy);
    }
}
