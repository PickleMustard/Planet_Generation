using Godot;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Visual proxy that owns a building's mesh in the scene tree.
/// Mirrors construction state changes from its associated Building Resource
/// onto the model material; the Building itself stays out of the scene tree.
/// </summary>
public partial class BuildingNode : Node3D
{
    private Building? _building;
    private MeshInstance3D? _meshInstance;
    private StandardMaterial3D? _originalMaterial;

    public Building? Building => _building;

    /// <summary>
    /// Attaches this proxy to a Building. Caller must AddChild() this node before calling Bind().
    /// </summary>
    public void Bind(Building building, Node3D parentBody, float bodyRadius)
    {
        _building = building;
        building.VisualNode = this;
        Name = building.Name;

        InstantiateModel(building.Definition, bodyRadius);
        CaptureOriginalMaterial();
        UpdatePlacementTransform(parentBody);

        building.OnCompletion += OnBuildingCompletion;
    }

    public override void _ExitTree()
    {
        if (_building != null)
        {
            _building.OnCompletion -= OnBuildingCompletion;
            if (_building.VisualNode == this)
                _building.VisualNode = null;
        }
    }

    /// <summary>
    /// Applies the translucent green construction material to the model.
    /// </summary>
    public void ApplyConstructionMaterial()
    {
        if (_meshInstance == null) return;

        var constructionMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.6f, 0.4f, 0.3f),
            Metallic = 0.3f,
            Roughness = 0.7f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        _meshInstance.MaterialOverride = constructionMat;
    }

    /// <summary>
    /// Restores the captured original material (or a default opaque material if none was captured).
    /// </summary>
    public void RestoreOriginalMaterial()
    {
        if (_meshInstance == null) return;

        if (_originalMaterial != null)
        {
            _meshInstance.MaterialOverride = _originalMaterial;
        }
        else
        {
            _meshInstance.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 1f, 1f, 1f),
                Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
            };
        }
    }

    private void InstantiateModel(Structures.Resources.BuildingDefinition? definition, float bodyRadius)
    {
        Node3D? model = definition?.Visual?.CreateModelInstance(bodyRadius);

        if (model != null)
        {
            AddChild(model);
            _meshInstance = NodeUtils.FindMeshInstanceRecursive(model);
        }

        if (_meshInstance == null)
        {
            var fallback = DefaultModelRegistry.InstantiateBuildingDefault();
            if (fallback != null)
            {
                float fallbackScale = bodyRadius * 0.5f;
                if (fallbackScale > 0f)
                    fallback.Scale = Vector3.One * fallbackScale;
                fallback.Name = "FallbackModel";
                AddChild(fallback);
                _meshInstance = NodeUtils.FindMeshInstanceRecursive(fallback);
            }
            else
            {
                GameLogger.Error($"BuildingNode: Default building prefab unavailable for '{definition?.IdName}'");
            }
        }
    }

    private void CaptureOriginalMaterial()
    {
        if (_meshInstance == null) return;
        _originalMaterial = _meshInstance.MaterialOverride?.Duplicate() as StandardMaterial3D;
    }

    /// <summary>
    /// Positions and orients this node on the parent body's surface using the
    /// centroid of the Building's occupied cells.
    /// </summary>
    private void UpdatePlacementTransform(Node3D parentBody)
    {
        if (_building == null || _building.PrimaryCell == null)
            return;

        Vector3 centroidLocal;
        var cells = _building.OccupiedCells;
        if (cells.Count > 1)
        {
            centroidLocal = Vector3.Zero;
            foreach (var cell in cells)
                centroidLocal += cell.Center;
            centroidLocal /= cells.Count;
        }
        else
        {
            centroidLocal = _building.PrimaryCell.Center;
        }

        var centroidWorldPos = parentBody.GlobalTransform * centroidLocal;
        var localPosition = parentBody.GlobalTransform.AffineInverse() * centroidWorldPos;

        var up = (centroidWorldPos - parentBody.GlobalPosition).Normalized();
        var forward = up.Cross(Vector3.Right).Normalized();
        if (forward.LengthSquared() < 0.001f)
            forward = up.Cross(Vector3.Forward).Normalized();
        var right = forward.Cross(up).Normalized();
        forward = up.Cross(right).Normalized();

        var localBasis = parentBody.GlobalTransform.Basis.Inverse() * new Basis(right, up, forward);
        Transform = new Transform3D(localBasis, localPosition);
    }

    private void OnBuildingCompletion()
    {
        RestoreOriginalMaterial();
    }
}
