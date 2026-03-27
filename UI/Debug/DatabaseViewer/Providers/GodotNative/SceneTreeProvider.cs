#if DEBUG
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Data provider for Godot scene tree: node hierarchy, groups, and properties.
/// </summary>
[DebugData("Scene Tree", Category = "Scene Tree")]
public class SceneTreeProvider : IDataProvider
{
    private SceneTree? _sceneTree;
    private DebugDataNode? _cachedData;
    private bool _needsRefresh = true;

    public string Name => "Scene Tree";
    public string Category => "Scene Tree";
    public bool NeedsRefresh => _needsRefresh;

    public SceneTreeProvider()
    {
        _sceneTree = Engine.GetMainLoop() as SceneTree;
    }

    public DebugDataNode GetData()
    {
        return _cachedData ??= BuildSceneTreeData();
    }

    public void Refresh()
    {
        _cachedData = null;
        _needsRefresh = false;
        _sceneTree = Engine.GetMainLoop() as SceneTree;
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

    private DebugDataNode BuildSceneTreeData()
    {
        var root = new DebugDataNode("Scene Tree");

        if (_sceneTree == null)
        {
            root.AddProperty("Error", "SceneTree not available");
            return root;
        }

        var editedSceneRoot = _sceneTree.EditedSceneRoot;
        var currentSceneRoot = _sceneTree.CurrentScene;

        root.AddProperty("Root Count", _sceneTree.Root.GetChildCount());
        root.AddProperty("Current Scene", currentSceneRoot?.Name ?? "None");
        root.AddProperty("Edited Scene", editedSceneRoot?.Name ?? "None");

        var rootNodes = root.AddChild("Root Nodes").SetCollapsed();

        for (int i = 0; i < _sceneTree.Root.GetChildCount(); i++)
        {
            var child = _sceneTree.Root.GetChild(i);
            var childNode = BuildNodeData(child, rootNodes);
            rootNodes.AddChild(childNode);
        }

        var groups = root.AddChild("Groups").SetCollapsed();
        var allGroups = _sceneTree.GetNodesInGroup("_viewports_").FirstOrDefault()?.GetTree()?.GetNodesInGroup("");

        var groupNames = new HashSet<string>();
        CollectGroups(_sceneTree.Root, groupNames);

        foreach (var groupName in groupNames.OrderBy(g => g))
        {
            if (string.IsNullOrEmpty(groupName)) continue;
            var nodesInGroup = _sceneTree.GetNodesInGroup(groupName);
            var groupNode = groups.AddChild(groupName);
            groupNode.AddProperty("Count", nodesInGroup.Count);
            foreach (var node in nodesInGroup)
            {
                if (node is Node n)
                {
                    groupNode.AddChild(n.Name).AddProperty("Type", n.GetType().Name);
                }
            }
        }

        return root;
    }

    private void CollectGroups(Node node, HashSet<string> groups)
    {
        foreach (var group in node.GetGroups())
        {
            if (group is StringName groupName)
            {
                groups.Add(groupName.ToString());
            }
        }

        foreach (var child in node.GetChildren())
        {
            CollectGroups(child, groups);
        }
    }

    private DebugDataNode BuildNodeData(Node node, DebugDataNode parent)
    {
        var nodeData = new DebugDataNode(node.Name);

        nodeData.AddProperty("Type", node.GetType().Name);
        nodeData.AddProperty("Path", node.GetPath());
        nodeData.AddProperty("Child Count", node.GetChildCount());

        if (node is Node2D node2D)
        {
            nodeData.AddProperty("Position", node2D.Position);
            nodeData.AddProperty("Visible", node2D.Visible);
            nodeData.AddProperty("Z Index", node2D.ZIndex);
        }
        else if (node is Node3D node3D)
        {
            nodeData.AddProperty("Position", node3D.Position);
            nodeData.AddProperty("Visible", node3D.Visible);
        }
        else if (node is Control control)
        {
            nodeData.AddProperty("Position", control.Position);
            nodeData.AddProperty("Size", control.Size);
            nodeData.AddProperty("Visible", control.Visible);
        }

        var groups = node.GetGroups();
        if (groups.Count > 0)
        {
            var groupsNode = nodeData.AddChild("Groups");
            foreach (var group in groups)
            {
                if (group is StringName groupName)
                {
                    groupsNode.AddProperty(groupName.ToString(), true);
                }
            }
        }

        if (node.GetChildCount() > 0)
        {
            var childrenNode = nodeData.AddChild("Children").SetCollapsed();
            foreach (var child in node.GetChildren())
            {
                childrenNode.AddChild(BuildNodeData(child, childrenNode));
            }
        }

        return nodeData;
    }
}
#endif
