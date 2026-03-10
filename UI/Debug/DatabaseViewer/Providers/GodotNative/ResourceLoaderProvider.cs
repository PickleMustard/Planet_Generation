#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UI.Debug;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Data provider for loaded resources: resource list by type and memory usage.
/// </summary>
[DebugData("ResourceLoader", Category = "Resources")]
public class ResourceLoaderProvider : IDataProvider
{
    private DebugDataNode? _cachedData;
    private bool _needsRefresh = true;

    public string Name => "ResourceLoader";
    public string Category => "Resources";
    public bool NeedsRefresh => _needsRefresh;

    public DebugDataNode GetData()
    {
        return _cachedData ??= BuildResourceData();
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

    private DebugDataNode BuildResourceData()
    {
        var root = new DebugDataNode("ResourceLoader");

        var allResources = new List<(string Path, Resource Resource)>();
        CollectResources(allResources);

        root.AddProperty("Total Resources", allResources.Count);

        var byType = allResources.GroupBy(r => r.Resource.GetType().Name)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.ToList());

        var typesNode = root.AddChild("By Type").SetCollapsed();
        foreach (var kvp in byType)
        {
            var typeNode = typesNode.AddChild(kvp.Key);
            typeNode.AddProperty("Count", kvp.Value.Count);

            var resourcesNode = typeNode.AddChild("Resources").SetCollapsed();
            foreach (var (path, resource) in kvp.Value)
            {
                var resNode = resourcesNode.AddChild(path ?? "[unnamed]");
                resNode.AddProperty("Type", resource.GetType().Name);
                resNode.AddProperty("Resource Path", resource.ResourcePath ?? "N/A");
            }
        }

        var memoryNode = root.AddChild("Memory Estimate");
        long totalMemory = 0;

        foreach (var typeGroup in byType)
        {
            long typeMemory = EstimateMemoryForType(typeGroup.Value);
            totalMemory += typeMemory;
            memoryNode.AddProperty(typeGroup.Key, $"{typeMemory / 1024} KB ({typeGroup.Value.Count} items)");
        }

        memoryNode.AddProperty("Total Estimate", $"{totalMemory / 1024} KB");

        return root;
    }

    private void CollectResources(List<(string, Resource)> resources)
    {
        var loadedResources = new HashSet<Resource>();

        void CollectFromNode(Node node)
        {
            var propertyList = node.GetPropertyList();
            foreach (var property in propertyList)
            {
                if (property.TryGetValue("name", out var nameVar) &&
                    property.TryGetValue("type", out var typeVar))
                {
                    var type = (Variant.Type)(int)typeVar;
                    if (type == Variant.Type.Object)
                    {
                        var propName = nameVar.AsStringName();
                        var value = node.Get(propName);
                        if (value.Obj is Resource res && !loadedResources.Contains(res))
                        {
                            loadedResources.Add(res);
                            resources.Add((res.ResourcePath ?? "[inline]", res));
                        }
                    }
                }
            }

            foreach (var child in node.GetChildren())
            {
                CollectFromNode(child);
            }
        }

        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree?.Root != null)
        {
            for (int i = 0; i < sceneTree.Root.GetChildCount(); i++)
            {
                CollectFromNode(sceneTree.Root.GetChild(i));
            }
        }
    }

    private long EstimateMemoryForType(List<(string Path, Resource Resource)> resources)
    {
        long total = 0;

        foreach (var (_, resource) in resources)
        {
            total += EstimateResourceSize(resource);
        }

        return total;
    }

    private long EstimateResourceSize(Resource resource)
    {
        if (resource is Texture2D tex)
            return tex.GetWidth() * tex.GetHeight() * 4L;
        if (resource is AudioStreamWav wav)
            return wav.Data?.Length ?? 0;
        if (resource is Mesh mesh)
            return mesh.GetFaces()?.Length * 12L ?? 0;
        if (resource is Font)
            return 1024;
        if (resource is Shader shader)
            return shader.Code?.Length ?? 0;
        return 256;
    }
}
#endif
