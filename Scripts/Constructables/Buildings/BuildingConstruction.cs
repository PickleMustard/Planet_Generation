using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;
using GodotDict = Godot.Collections.Dictionary;
using GodotTypedDict = Godot.Collections.Dictionary<string, int>;

namespace Constructables;

public partial class BuildingConstruction : Node3D, IConstructable
{
    [Signal]
    public delegate void OnCompletionEventHandler();

    [Signal]
    public delegate void OnConstructionBlockedEventHandler();

    [Signal]
    public delegate void OnConstructionResumedEventHandler();

    private ConstructionState? _constructionState;
    private BuildingDefinition? _buildingDefinition;
    private bool _isUnderConstruction;
    private MeshInstance3D? _meshInstance;
    private StandardMaterial3D? _originalMaterial;
    private Node3D? _parentBody;

    public Buildings.BuildingManufacturing Manufacturing { get; }

    public BuildingConstruction()
    {
        Manufacturing = new Buildings.BuildingManufacturing(this);
    }

    public VoronoiCell? PrimaryCell { get; private set; }
    public List<VoronoiCell> OccupiedCells { get; private set; } = new();
    public bool IsUnderConstruction => _isUnderConstruction;
    public BuildingDefinition? Definition => _buildingDefinition;

    /// <summary>The orbital-architect station managing this building's construction, if any.</summary>
    public StationSatellite? ConstructingStation { get; set; }

    /// <summary>
    /// The currently active recipe ID for this building's production.
    /// Set when the building is registered with the continent economy.
    /// </summary>
    public string? ActiveRecipeId { get; set; }

    #region IConstructable

    [Export]
    public float workRequired
    {
        get => _constructionState?.WorkRequired ?? 0f;
        set { if (_constructionState != null) _constructionState.WorkRequired = value; }
    }

    [Export]
    public float workDone
    {
        get => _constructionState?.WorkDone ?? 0f;
        set { if (_constructionState != null) _constructionState.WorkDone = value; }
    }

    [Export]
    public GodotTypedDict requiredResources
    {
        get
        {
            var dict = new GodotTypedDict();
            if (_constructionState != null)
                foreach (var kvp in _constructionState.RequiredResources)
                    dict[kvp.Key] = kvp.Value;
            return dict;
        }
        set
        {
            if (_constructionState != null)
            {
                _constructionState.RequiredResources.Clear();
                foreach (var kvp in value)
                    _constructionState.RequiredResources[kvp.Key] = kvp.Value;
            }
        }
    }

    [Export]
    public GodotTypedDict availableResources
    {
        get
        {
            var dict = new GodotTypedDict();
            if (_constructionState != null)
                foreach (var kvp in _constructionState.AvailableResources)
                    dict[kvp.Key] = kvp.Value;
            return dict;
        }
        set
        {
            if (_constructionState != null)
            {
                _constructionState.AvailableResources.Clear();
                foreach (var kvp in value)
                    _constructionState.AvailableResources[kvp.Key] = kvp.Value;
            }
        }
    }

    public bool CanConstruct(GodotDict LocationDetails) => true;

    public IConstructable StartConstruction(GodotDict LocationDetails)
    {
        if (_constructionState == null) return this;

        _isUnderConstruction = true;
        ApplyConstructionMaterial();
        _constructionState.TryStart();

        GameLogger.Info($"BuildingConstruction {Name}: Construction started ({_constructionState.WorkRequired}s)");
        return this;
    }

    public bool DefineTemplate(GodotDict templateData) => true;
    public bool UpdateConfiguration(GodotDict updateData) => true;

    public bool CancelConstruction()
    {
        if (_constructionState == null) return false;

        _constructionState.Cancel();
        _isUnderConstruction = false;

        GameLogger.Info($"BuildingConstruction {Name}: Construction cancelled");
        return true;
    }

    public string GetStatus() => _constructionState?.Status.ToString() ?? "None";

    public float GetProgress() => _constructionState?.GetProgress() ?? 0f;

    public void UpdateProgress(float delta)
    {
        if (_constructionState == null || !_isUnderConstruction) return;

        var previousStatus = _constructionState.Status;
        _constructionState.UpdateProgress(delta);

        if (_constructionState.StatusChanged)
        {
            if (_constructionState.Status == ConstructionStatus.Blocked
                && previousStatus == ConstructionStatus.InProgress)
            {
                EmitSignal(SignalName.OnConstructionBlocked);
            }
            else if (_constructionState.Status == ConstructionStatus.InProgress
                && previousStatus == ConstructionStatus.Blocked)
            {
                EmitSignal(SignalName.OnConstructionResumed);
            }
            else if (_constructionState.Status == ConstructionStatus.Complete)
            {
                OnConstructionComplete();
            }
        }
    }

    public bool CheckRequiredResourcesAvailable() =>
        _constructionState?.CheckRequiredResourcesAvailable() ?? true;

    public virtual bool CanDemolish() => !_isUnderConstruction;
    public bool DemolishConstructable() => false;
    public bool CanDestroy() => true;

    public bool DestroyConstructable()
    {
        QueueFree();
        return true;
    }

    #endregion

    public void SetBuildingDefinition(BuildingDefinition definition, float bodyRadius = 1.0f)
    {
        _buildingDefinition = definition;
        _constructionState = new ConstructionState(
            definition.WorkRequired,
            definition.RequiredResources
        );

        Name = definition.DisplayName ?? definition.IdName ?? "Building";

        // Create model from pre-loaded prototype with body-relative scaling
        Node3D? model = definition.Visual?.CreateModelInstance(bodyRadius);

        if (model != null)
        {
            AddChild(model);
            _meshInstance = NodeUtils.FindMeshInstanceRecursive(model);

            if (_meshInstance == null)
            {
                GameLogger.Warning($"BuildingConstruction: No MeshInstance3D found in model for '{definition.IdName}'. Using fallback.");
            }
            else
            {
                GameLogger.Debug($"BuildingConstruction: Created building model for '{definition.IdName}' from prototype");
            }
        }
        else
        {
            GameLogger.Warning($"BuildingConstruction: No model prototype for '{definition.IdName}'. Using fallback.");
        }

        // Fallback: use the generic default building prefab
        if (_meshInstance == null)
        {
            var fallbackModel = DefaultModelRegistry.InstantiateBuildingDefault();
            if (fallbackModel != null)
            {
                float fallbackScale = bodyRadius * 0.5f;
                if (fallbackScale > 0f)
                    fallbackModel.Scale = Vector3.One * fallbackScale;
                fallbackModel.Name = "FallbackModel";
                AddChild(fallbackModel);
                _meshInstance = NodeUtils.FindMeshInstanceRecursive(fallbackModel);
                GameLogger.Debug($"BuildingConstruction: Installed default building prefab for '{definition.IdName}' (scale {fallbackScale})");
            }
            else
            {
                GameLogger.Error($"BuildingConstruction: Default building prefab unavailable for '{definition.IdName}'");
            }
        }

        // Capture original material immediately after model creation
        // This must happen BEFORE construction starts and before ApplyConstructionMaterial is called
        if (_meshInstance != null)
        {
            _originalMaterial = _meshInstance.MaterialOverride?.Duplicate() as StandardMaterial3D;
            GameLogger.Debug($"BuildingConstruction: Captured original material for '{definition.IdName}'");
        }
    }

    public void SetPlacement(VoronoiCell primaryCell, List<VoronoiCell>? additionalCells, Node3D parentBody)
    {
        PrimaryCell = primaryCell;
        _parentBody = parentBody;
        OccupiedCells.Clear();
        OccupiedCells.Add(primaryCell);
        if (additionalCells != null)
            OccupiedCells.AddRange(additionalCells);

        // Calculate centroid of all occupied cells for multi-cell buildings
        Vector3 centroidLocal;
        if (OccupiedCells.Count > 1)
        {
            // Calculate centroid in local space
            centroidLocal = Vector3.Zero;
            foreach (var cell in OccupiedCells)
                centroidLocal += cell.Center;
            centroidLocal /= OccupiedCells.Count;
        }
        else
        {
            // Single cell building - use primary cell center
            centroidLocal = primaryCell.Center;
        }

        // Transform centroid to world space
        var centroidWorldPos = parentBody.GlobalTransform * centroidLocal;

        // Calculate local position relative to parent body
        var localPosition = parentBody.GlobalTransform.AffineInverse() * centroidWorldPos;

        // Calculate up vector from body center to centroid (for proper surface alignment)
        var up = (centroidWorldPos - parentBody.GlobalPosition).Normalized();
        var forward = up.Cross(Vector3.Right).Normalized();
        if (forward.LengthSquared() < 0.001f)
            forward = up.Cross(Vector3.Forward).Normalized();
        var right = forward.Cross(up).Normalized();
        forward = up.Cross(right).Normalized();

        // Create transform with correct orientation and position
        var localBasis = parentBody.GlobalTransform.Basis.Inverse() * new Basis(right, up, forward);
        Transform = new Transform3D(localBasis, localPosition);

        GameLogger.Debug($"BuildingConstruction.SetPlacement: Building '{Name}' placed at centroid local position {localPosition}, world position {centroidWorldPos}, up vector {up}, cells={OccupiedCells.Count}");
    }

    public void DeliverResources(string resourceId, int amount)
    {
        _constructionState?.DeliverResources(resourceId, amount);
    }

    private void OnConstructionComplete()
    {
        _isUnderConstruction = false;
        RestoreOriginalMaterial();
        EmitSignal(SignalName.OnCompletion);
        GameLogger.Info($"BuildingConstruction {Name}: Construction complete");
    }

    private void ApplyConstructionMaterial()
    {
        // Note: _originalMaterial is captured in SetBuildingDefinition() immediately after model creation
        // We don't capture it here because by the time this is called, the material has already been set

        var constructionMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.6f, 0.4f, 0.3f),
            Metallic = 0.3f,
            Roughness = 0.7f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        if (_meshInstance != null)
            _meshInstance.MaterialOverride = constructionMat;
    }

    private void RestoreOriginalMaterial()
    {
        if (_meshInstance == null) return;

        if (_originalMaterial != null)
        {
            _meshInstance.MaterialOverride = _originalMaterial;
            GameLogger.Debug($"BuildingConstruction: Restored original material for '{Name}'");
        }
        else
        {
            // No original material was captured, apply a default opaque material
            var defaultMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 1f, 1f, 1f),
                Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
            };
            _meshInstance.MaterialOverride = defaultMat;
            GameLogger.Debug($"BuildingConstruction: Applied default opaque material for '{Name}' (no original captured)");
        }
    }

}
