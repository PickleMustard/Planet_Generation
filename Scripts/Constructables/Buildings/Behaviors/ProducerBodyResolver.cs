using Godot;
using ProceduralGeneration;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Helper for finding the <see cref="IOrbitalBody"/> that owns a given Building.
/// Walks up the VisualNode's scene-tree parent chain. Used by power producers
/// that need to read body-level properties (atmosphere, distance to star) at
/// OnRegister time.
/// </summary>
internal static class ProducerBodyResolver
{
    public static IOrbitalBody? FindOwningBody(Building? building)
    {
        if (building?.VisualNode == null)
            return null;
        Node? node = building.VisualNode.GetParent();
        int safety = 32;
        while (node != null && safety-- > 0)
        {
            if (node is IOrbitalBody body)
                return body;
            node = node.GetParent();
        }
        return null;
    }
}
