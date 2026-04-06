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

    public bool CanDemolish() => !_isUnderConstruction;
    public bool DemolishConstructable() => false;
    public bool CanDestroy() => true;

    public bool DestroyConstructable()
    {
        QueueFree();
        return true;
    }

    #endregion

    public void SetBuildingDefinition(BuildingDefinition definition)
    {
        _buildingDefinition = definition;
        _constructionState = new ConstructionState(
            definition.WorkRequired,
            definition.RequiredResources
        );

        Name = definition.DisplayName ?? definition.IdName ?? "Building";

        if (!string.IsNullOrEmpty(definition.Visual?.ModelPath))
        {
            try
            {
                var scene = GD.Load<PackedScene>(definition.Visual.ModelPath);
                if (scene != null)
                {
                    var model = scene.Instantiate<Node3D>();
                    model.Scale = Vector3.One * definition.Visual.Scale;
                    model.RotationDegrees = definition.Visual.RotationOffset;
                    AddChild(model);

                    // Recursively search for MeshInstance3D
                    _meshInstance = FindMeshInstanceRecursive(model);
                    
                    if (_meshInstance == null)
                    {
                        GameLogger.Warning($"BuildingConstruction: No MeshInstance3D found in model '{definition.Visual.ModelPath}'. Using fallback.");
                    }
                    else
                    {
                        GameLogger.Debug($"BuildingConstruction: Found MeshInstance3D in model '{definition.Visual.ModelPath}'");
                    }
                }
                else
                {
                    GameLogger.Error($"BuildingConstruction: Failed to load scene from path '{definition.Visual.ModelPath}'");
                }
            }
            catch (System.Exception e)
            {
                GameLogger.Error($"BuildingConstruction: Exception loading model '{definition.Visual.ModelPath}': {e.Message}");
            }
        }
        else
        {
            GameLogger.Warning($"BuildingConstruction: No model path specified for building '{definition.IdName}'. Using fallback.");
        }

        if (_meshInstance == null)
        {
            _meshInstance = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.5f, 0.5f) },
                Name = "FallbackMesh"
            };
            AddChild(_meshInstance);
            GameLogger.Debug($"BuildingConstruction: Created fallback mesh for building '{definition.IdName}'");
        }
    }

    public void SetPlacement(VoronoiCell primaryCell, List<VoronoiCell>? additionalCells, Node3D parentBody)
    {
        PrimaryCell = primaryCell;
        OccupiedCells.Clear();
        OccupiedCells.Add(primaryCell);
        if (additionalCells != null)
            OccupiedCells.AddRange(additionalCells);

        // Calculate world position of cell center (same as ghost positioning logic)
        var cellWorldPos = parentBody.GlobalTransform * primaryCell.Center;
        
        // Calculate local position relative to parent body
        var localPosition = parentBody.GlobalTransform.AffineInverse() * cellWorldPos;
        
        // Orient along surface normal (same as ghost orientation logic)
        var up = (cellWorldPos - parentBody.GlobalPosition).Normalized();
        var forward = up.Cross(Vector3.Right).Normalized();
        if (forward.LengthSquared() < 0.001f)
            forward = up.Cross(Vector3.Forward).Normalized();
        var right = forward.Cross(up).Normalized();
        forward = up.Cross(right).Normalized();

        // Create transform with correct orientation and position
        var localBasis = parentBody.GlobalTransform.Basis.Inverse() * new Basis(right, up, forward);
        Transform = new Transform3D(localBasis, localPosition);
        
        GameLogger.Debug($"BuildingConstruction.SetPlacement: Building '{Name}' placed at local position {localPosition}, world position {cellWorldPos}, up vector {up}");
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
        if (_meshInstance?.MaterialOverride is StandardMaterial3D existingMat)
            _originalMaterial = existingMat;

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
        if (_meshInstance != null && _originalMaterial != null)
            _meshInstance.MaterialOverride = _originalMaterial;
    }

    /// <summary>
    /// Recursively searches for a MeshInstance3D in a node hierarchy.
    /// </summary>
    private MeshInstance3D? FindMeshInstanceRecursive(Node node)
    {
        // Check if this node is a MeshInstance3D
        if (node is MeshInstance3D meshInstance)
            return meshInstance;

        // Recursively search children
        foreach (var child in node.GetChildren())
        {
            var found = FindMeshInstanceRecursive(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
