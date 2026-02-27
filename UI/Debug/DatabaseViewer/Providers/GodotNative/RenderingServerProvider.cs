#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UI.Debug;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Data provider for rendering: GPU info, draw calls, and materials.
/// </summary>
[DebugData("Rendering Server", Category = "Rendering")]
public class RenderingServerProvider : IDataProvider
{
    private DebugDataNode _cachedData;
    private bool _needsRefresh = true;

    public string Name => "Rendering Server";
    public string Category => "Rendering";
    public bool NeedsRefresh => _needsRefresh;

    public DebugDataNode GetData()
    {
        return _cachedData ??= BuildRenderingData();
    }

    public void Refresh()
    {
        _cachedData = null;
        _needsRefresh = false;
    }

    public IEnumerable<string> Search(string pattern)
    {
        var data = GetData();
        var results = new List<string>();
        SearchRecursive(data, "", pattern.ToLower(), results);
        return results;
    }

    private void SearchRecursive(DebugDataNode node, string path, string pattern, List<string> results)
    {
        var currentPath = string.IsNullOrEmpty(path) ? node.Name : $"{path}/{node.Name}";

        if (node.Name.ToLower().Contains(pattern) ||
            (node.HasValue && node.Value?.ToString()?.ToLower()?.Contains(pattern) == true))
        {
            results.Add(currentPath);
        }

        foreach (var prop in node.Properties.Values)
        {
            var propPath = $"{currentPath}.{prop.Name}";
            if (prop.Name.ToLower().Contains(pattern) ||
                (prop.HasValue && prop.Value?.ToString()?.ToLower()?.Contains(pattern) == true))
            {
                results.Add(propPath);
            }
        }

        foreach (var child in node.Children)
        {
            SearchRecursive(child, currentPath, pattern, results);
        }
    }

    private DebugDataNode BuildRenderingData()
    {
        var root = new DebugDataNode("Rendering Server");

        var gpuInfo = root.AddChild("GPU Information");
        gpuInfo.AddProperty("Adapter Name", RenderingServer.GetVideoAdapterName());
        gpuInfo.AddProperty("Adapter Vendor", RenderingServer.GetVideoAdapterVendor());
        gpuInfo.AddProperty("API Version", RenderingServer.GetVideoAdapterApiVersion());

        var stats = root.AddChild("Render Statistics");
        stats.AddProperty("Video Mem Used (MB)", Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / (1024 * 1024));
        stats.AddProperty("Texture Mem Used (MB)", Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / (1024 * 1024));
        stats.AddProperty("Buffer Mem Used (MB)", Performance.GetMonitor(Performance.Monitor.RenderBufferMemUsed) / (1024 * 1024));

        var environment = root.AddChild("Environment");
        environment.AddProperty("Default Clear Color", RenderingServer.GetDefaultClearColor());

        var materials = root.AddChild("Materials").SetCollapsed();
        CollectMaterials(materials);

        var viewports = root.AddChild("Viewports").SetCollapsed();
        CollectViewports(viewports);

        return root;
    }

    private void CollectMaterials(DebugDataNode materialsNode)
    {
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree?.Root == null) return;

        var materials = new HashSet<Material>();
        var shaders = new HashSet<Shader>();

        void CollectFromNode(Node node)
        {
            if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
            {
                for (int i = 0; i < meshInstance.Mesh.GetSurfaceCount(); i++)
                {
                    var mat = meshInstance.Mesh.SurfaceGetMaterial(i);
                    if (mat != null)
                    {
                        materials.Add(mat);
                        if (mat is ShaderMaterial shaderMat && shaderMat.Shader != null)
                        {
                            shaders.Add(shaderMat.Shader);
                        }
                    }
                }

                for (int i = 0; i < meshInstance.GetSurfaceOverrideMaterialCount(); i++)
                {
                    var mat = meshInstance.GetSurfaceOverrideMaterial(i);
                    if (mat != null)
                    {
                        materials.Add(mat);
                    }
                }
            }

            if (node is CanvasItem canvasItem && canvasItem.Material != null)
            {
                materials.Add(canvasItem.Material);
            }

            if (node is GeometryInstance3D geom && geom.MaterialOverride != null)
            {
                materials.Add(geom.MaterialOverride);
            }

            foreach (var child in node.GetChildren())
            {
                CollectFromNode(child);
            }
        }

        for (int i = 0; i < sceneTree.Root.GetChildCount(); i++)
        {
            CollectFromNode(sceneTree.Root.GetChild(i));
        }

        materialsNode.AddProperty("Total Materials", materials.Count);

        var byType = materials.GroupBy(m => m.GetType().Name).OrderByDescending(g => g.Count());
        foreach (var group in byType)
        {
            var typeNode = materialsNode.AddChild(group.Key);
            typeNode.AddProperty("Count", group.Count());

            foreach (var mat in group)
            {
                var matNode = typeNode.AddChild(mat.ResourceName ?? mat.ResourcePath ?? "[unnamed]");
                matNode.AddProperty("Resource Path", mat.ResourcePath ?? "Built-in");
                if (mat is StandardMaterial3D stdMat)
                {
                    matNode.AddProperty("Albedo Color", stdMat.AlbedoColor);
                    matNode.AddProperty("Metallic", stdMat.Metallic);
                    matNode.AddProperty("Roughness", stdMat.Roughness);
                }
                else if (mat is ShaderMaterial shaderMat)
                {
                    matNode.AddProperty("Shader", shaderMat.Shader?.ResourcePath ?? "None");
                }
            }
        }

        var shadersNode = materialsNode.AddChild("Shaders");
        shadersNode.AddProperty("Count", shaders.Count);
        foreach (var shader in shaders)
        {
            var shaderNode = shadersNode.AddChild(shader.ResourceName ?? shader.ResourcePath ?? "[unnamed]");
            shaderNode.AddProperty("Path", shader.ResourcePath ?? "Built-in");
        }
    }

    private void CollectViewports(DebugDataNode viewportsNode)
    {
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree?.Root == null) return;

        void CollectFromNode(Node node, DebugDataNode parentNode)
        {
            if (node is Viewport viewport)
            {
                var vpNode = parentNode.AddChild(node.Name);
                vpNode.AddProperty("Type", viewport.GetType().Name);
                vpNode.AddProperty("Size", viewport.GetVisibleRect().Size);
                vpNode.AddProperty("Transparent BG", viewport.TransparentBg);
                vpNode.AddProperty("Use HDR 2D", viewport.UseHdr2D);

                foreach (var child in node.GetChildren())
                {
                    CollectFromNode(child, vpNode);
                }
            }
            else
            {
                foreach (var child in node.GetChildren())
                {
                    CollectFromNode(child, parentNode);
                }
            }
        }

        CollectFromNode(sceneTree.Root, viewportsNode);
    }
}
#endif
