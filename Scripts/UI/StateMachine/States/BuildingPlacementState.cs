using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for placing buildings on a planet's surface.
/// Wraps the existing BuildingPlacementMode which handles raycasting,
/// ghost model display, cell validation, and multi-cell rotation.
/// </summary>
public partial class BuildingPlacementState : LimboState
{
    private Construction.BuildingPlacementMode? _placementMode;
    private BuildingDefinition? _buildingDef;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "BuildingPlacementState");

        var defName = Blackboard?.Top().GetVar("ConstructionDefinitionName").AsString() ?? "";
        BuildingDatabase.Instance.TryGetBuilding(defName, out _buildingDef);
        if (_buildingDef == null)
        {
            GameLogger.Error($"BuildingPlacementState: Building definition '{defName}' not found");
            Dispatch("placement_cancelled");
            return;
        }

        _placementMode = new Construction.BuildingPlacementMode();
        AddChild(_placementMode);
        _placementMode.Initialize(_buildingDef);

        _placementMode.PlacementConfirmed += OnPlacementConfirmed;
        _placementMode.PlacementCancelled += OnPlacementCancelled;

        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        GameLogger.Debug($"BuildingPlacementState: Placing '{_buildingDef.DisplayName ?? _buildingDef.IdName}'");
    }

    public override void _Exit()
    {
        base._Exit();

        if (_placementMode != null)
        {
            _placementMode.PlacementConfirmed -= OnPlacementConfirmed;
            _placementMode.PlacementCancelled -= OnPlacementCancelled;
            _placementMode.Cleanup();
            _placementMode.QueueFree();
            _placementMode = null;
        }

        _buildingDef = null;
    }

    private void OnPlacementConfirmed(
        VoronoiCell primaryCell,
        Node3D body,
        Godot.Collections.Array<VoronoiCell> additionalCells
    )
    {
        if (_buildingDef == null)
            return;

        try
        {
            List<VoronoiCell>? extraCells = null;
            if (additionalCells != null && additionalCells.Count > 0)
            {
                extraCells = new List<VoronoiCell>();
                foreach (var cell in additionalCells)
                    extraCells.Add(cell);
            }

            if (_buildingDef.IdName == "company_headquarters")
            {
                var hq = ConstructionManager.Instance.CreateHeadquarters(primaryCell, body, extraCells);
                if (hq == null)
                {
                    Dispatch("placement_cancelled");
                    return;
                }

                Dispatch("headquarters_placed");
                return;
            }

            ConstructionManager.Instance.CreateBuilding(
                primaryCell,
                body,
                _buildingDef,
                extraCells
            );

            ToastSystem.Instance?.Show(
                $"Construction started: {_buildingDef.DisplayName ?? _buildingDef.IdName}"
            );
            Dispatch("placement_confirmed");
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"BuildingPlacementState: Failed to create building — {e.Message}");
            ToastSystem.Instance?.Show($"Error: {e.Message}");
        }
    }

    private void OnPlacementCancelled()
    {
        Dispatch("placement_cancelled");
    }
}
