using Godot;
using Structures.Logistics;
using UtilityLibrary;

namespace UI.Construction;

/// <summary>
/// Builds the semi-transparent ghost preview node for a station, with a cylinder
/// fallback when the definition has no model. Lifted from the old
/// StationPlacementMode so both the placement modes can share it.
/// </summary>
public static class StationGhostFactory
{
    /// <summary>
    /// Creates a ghost container (a <see cref="Node3D"/> the caller adds to the
    /// tree and positions) holding the ghost model. The container starts hidden.
    /// </summary>
    public static (Node3D Container, Node3D Model) Create(StationDefinition? def)
    {
        Node3D? model = def?.Visual?.CreateModelInstance(scaleWithBody: false);

        if (model == null)
        {
            const float fallbackHeight = 2f;
            const float fallbackRadius = fallbackHeight * 0.15f;
            model = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    Height = fallbackHeight,
                    TopRadius = fallbackRadius,
                    BottomRadius = fallbackRadius,
                },
                Name = "GhostFallbackMesh",
            };
            GameLogger.Warning(
                $"StationGhostFactory: Using fallback ghost model for '{def?.Name}'");
        }

        ApplyGhostMaterial(model);

        var container = new Node3D { Name = "OrbitalGhostContainer", Visible = false };
        container.AddChild(model);
        return (container, model);
    }

    private static void ApplyGhostMaterial(Node node)
    {
        var ghostMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 1f, 1f, 0.4f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        if (node is MeshInstance3D meshInstance)
            meshInstance.MaterialOverride = ghostMat;

        foreach (var child in node.GetChildren())
            ApplyGhostMaterial(child);
    }
}
