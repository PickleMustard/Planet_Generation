#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UI.Debug;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Data provider for physics: bodies, collision shapes, and contact info.
/// </summary>
[DebugData("Physics Server", Category = "Physics")]
public class PhysicsServerProvider : IDataProvider
{
    private DebugDataNode _cachedData;
    private bool _needsRefresh = true;

    public string Name => "Physics Server";
    public string Category => "Physics";
    public bool NeedsRefresh => _needsRefresh;

    public DebugDataNode GetData()
    {
        return _cachedData ??= BuildPhysicsData();
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

    private DebugDataNode BuildPhysicsData()
    {
        var root = new DebugDataNode("Physics Server");

        var bodies2D = new List<(Node Node, PhysicsBody2D Body)>();
        var bodies3D = new List<(Node Node, PhysicsBody3D Body)>();
        var area2DList = new List<(Node Node, Area2D Area)>();
        var area3DList = new List<(Node Node, Area3D Area)>();

        CollectPhysicsNodes(bodies2D, bodies3D, area2DList, area3DList);

        var bodiesNode = root.AddChild("Physics Bodies");

        var bodies2DNode = bodiesNode.AddChild("2D Bodies");
        bodies2DNode.AddProperty("Count", bodies2D.Count);

        foreach (var (node, body) in bodies2D.OrderBy(b => b.Node.Name))
        {
            var bodyNode = bodies2DNode.AddChild(node.Name);
            bodyNode.AddProperty("Type", body.GetType().Name);
            bodyNode.AddProperty("Position", body.Position);
            bodyNode.AddProperty("Collision Layer", body.CollisionLayer);
            bodyNode.AddProperty("Collision Mask", body.CollisionMask);

            if (body is RigidBody2D rb2D)
            {
                bodyNode.AddProperty("Sleeping", rb2D.Sleeping);
            }
            else if (body is CharacterBody2D cb2D)
            {
                bodyNode.AddProperty("Velocity", cb2D.Velocity);
            }

            var shapesNode = bodyNode.AddChild("Collision Shapes");
            foreach (var child in node.GetChildren())
            {
                if (child is CollisionShape2D shape && shape.Shape != null)
                {
                    var shapeNode = shapesNode.AddChild(child.Name);
                    shapeNode.AddProperty("Type", shape.Shape.GetType().Name);
                    shapeNode.AddProperty("Disabled", shape.Disabled);
                    shapeNode.AddProperty("One Way Collision", shape.OneWayCollision);
                }
            }
        }

        var bodies3DNode = bodiesNode.AddChild("3D Bodies");
        bodies3DNode.AddProperty("Count", bodies3D.Count);

        foreach (var (node, body) in bodies3D.OrderBy(b => b.Node.Name))
        {
            var bodyNode = bodies3DNode.AddChild(node.Name);
            bodyNode.AddProperty("Type", body.GetType().Name);
            bodyNode.AddProperty("Position", body.Position);
            bodyNode.AddProperty("Collision Layer", body.CollisionLayer);
            bodyNode.AddProperty("Collision Mask", body.CollisionMask);

            if (body is RigidBody3D rb3D)
            {
                bodyNode.AddProperty("Sleeping", rb3D.Sleeping);
            }
            else if (body is CharacterBody3D cb3D)
            {
                bodyNode.AddProperty("Velocity", cb3D.Velocity);
            }

            var shapesNode = bodyNode.AddChild("Collision Shapes");
            foreach (var child in node.GetChildren())
            {
                if (child is CollisionShape3D shape && shape.Shape != null)
                {
                    var shapeNode = shapesNode.AddChild(child.Name);
                    shapeNode.AddProperty("Type", shape.Shape.GetType().Name);
                    shapeNode.AddProperty("Disabled", shape.Disabled);
                }
            }
        }

        var areasNode = root.AddChild("Areas");

        var areas2DNode = areasNode.AddChild("2D Areas");
        areas2DNode.AddProperty("Count", area2DList.Count);

        foreach (var (node, area) in area2DList.OrderBy(a => a.Node.Name))
        {
            var areaNode = areas2DNode.AddChild(node.Name);
            areaNode.AddProperty("Position", area.Position);
            areaNode.AddProperty("Collision Layer", area.CollisionLayer);
            areaNode.AddProperty("Collision Mask", area.CollisionMask);
            areaNode.AddProperty("Monitoring", area.Monitoring);
            areaNode.AddProperty("Monitorable", area.Monitorable);
        }

        var areas3DNode = areasNode.AddChild("3D Areas");
        areas3DNode.AddProperty("Count", area3DList.Count);

        foreach (var (node, area) in area3DList.OrderBy(a => a.Node.Name))
        {
            var areaNode = areas3DNode.AddChild(node.Name);
            areaNode.AddProperty("Position", area.Position);
            areaNode.AddProperty("Collision Layer", area.CollisionLayer);
            areaNode.AddProperty("Collision Mask", area.CollisionMask);
            areaNode.AddProperty("Monitoring", area.Monitoring);
            areaNode.AddProperty("Monitorable", area.Monitorable);
        }

        return root;
    }

    private void CollectPhysicsNodes(
        List<(Node, PhysicsBody2D)> bodies2D,
        List<(Node, PhysicsBody3D)> bodies3D,
        List<(Node, Area2D)> area2DList,
        List<(Node, Area3D)> area3DList)
    {
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree?.Root == null) return;

        void CollectFromNode(Node node)
        {
            if (node is PhysicsBody2D body2D)
                bodies2D.Add((node, body2D));
            if (node is PhysicsBody3D body3D)
                bodies3D.Add((node, body3D));
            if (node is Area2D area2D)
                area2DList.Add((node, area2D));
            if (node is Area3D area3D)
                area3DList.Add((node, area3D));

            foreach (var child in node.GetChildren())
            {
                CollectFromNode(child);
            }
        }

        for (int i = 0; i < sceneTree.Root.GetChildCount(); i++)
        {
            CollectFromNode(sceneTree.Root.GetChild(i));
        }
    }
}
#endif
