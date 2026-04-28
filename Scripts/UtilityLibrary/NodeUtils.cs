using Godot;

namespace UtilityLibrary;

public static class NodeUtils
{
    public static MeshInstance3D? FindMeshInstanceRecursive(Node? node)
    {
        if (node == null)
            return null;

        if (node is MeshInstance3D meshInstance)
            return meshInstance;

        foreach (var child in node.GetChildren())
        {
            var found = FindMeshInstanceRecursive(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
