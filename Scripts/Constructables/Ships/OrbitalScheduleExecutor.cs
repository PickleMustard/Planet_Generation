using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Drives the execution of an OrbitalTransferSchedule on a LogisticsUnit.
/// Added as a child Node of LogisticsUnit, ticks in _PhysicsProcess.
/// Handles cargo loading/unloading, refueling, trajectory planning with
/// constraint filtering, retry logic, and leg advancement.
/// </summary>
public partial class OrbitalScheduleExecutor : Node
{
    private LogisticsUnit? _unit;
    private LogisticsMovementController? _movementController;
    private OrbitalTransferSchedule? _activeSchedule;
    private float _waitTimer;
    private float _retryTimer;

    /// <summary>
    /// The currently assigned and potentially running schedule.
    /// </summary>
    public OrbitalTransferSchedule? ActiveSchedule => _activeSchedule;

    public override void _Ready()
    {
        _unit = GetParent<LogisticsUnit>();
        _movementController = _unit?.MovementController;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_unit == null || _activeSchedule == null)
            return;

        float dt = (float)delta;

        switch (_activeSchedule.State)
        {
            case OrbitalScheduleState.Running:
                var currentLeg = _activeSchedule.CurrentLeg;
                if (currentLeg != null)
                    ProcessLeg(currentLeg, dt);
                break;

            case OrbitalScheduleState.WaitingBetweenLegs:
                _waitTimer -= dt;
                if (_waitTimer <= 0f)
                {
                    TransitionScheduleState(OrbitalScheduleState.Running);
                }
                break;
        }
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Assigns a schedule to this executor. The schedule must be in Idle state.
    /// Does not start execution — call StartSchedule() after assigning.
    /// </summary>
    public bool AssignSchedule(OrbitalTransferSchedule schedule)
    {
        if (_unit == null)
        {
            GameLogger.Warning("OrbitalScheduleExecutor: Cannot assign schedule — no parent unit");
            return false;
        }

        if (schedule.Legs.Count == 0)
        {
            GameLogger.Warning($"OrbitalScheduleExecutor [{_unit.Name}]: Cannot assign empty schedule");
            return false;
        }

        if (_activeSchedule != null && _activeSchedule.State == OrbitalScheduleState.Running)
        {
            GameLogger.Warning($"OrbitalScheduleExecutor [{_unit.Name}]: Cannot assign schedule while another is running");
            return false;
        }

        _activeSchedule = schedule;
        _activeSchedule.State = OrbitalScheduleState.Idle;
        _activeSchedule.CurrentLegIndex = 0;

        GameLogger.Info($"OrbitalScheduleExecutor [{_unit.Name}]: Schedule {schedule.ScheduleId} assigned with {schedule.Legs.Count} leg(s)");
        return true;
    }

    /// <summary>
    /// Wraps a single leg in a non-repeating schedule and starts execution.
    /// </summary>
    public bool ExecuteSingleLeg(Leg leg)
    {
        var schedule = new OrbitalTransferSchedule
        {
            Legs = { leg },
            IsRepeating = false
        };

        if (!AssignSchedule(schedule))
            return false;

        return StartSchedule();
    }

    /// <summary>
    /// Starts or resumes execution of the assigned schedule.
    /// </summary>
    public bool StartSchedule()
    {
        if (_unit == null || _activeSchedule == null)
        {
            GameLogger.Warning("OrbitalScheduleExecutor: Cannot start — no schedule assigned");
            return false;
        }

        if (_activeSchedule.State != OrbitalScheduleState.Idle
            && _activeSchedule.State != OrbitalScheduleState.Stopped)
        {
            GameLogger.Warning($"OrbitalScheduleExecutor [{_unit.Name}]: Cannot start schedule in state {_activeSchedule.State}");
            return false;
        }

        TransitionScheduleState(OrbitalScheduleState.Running);
        GameLogger.Info($"OrbitalScheduleExecutor [{_unit.Name}]: Schedule started");
        return true;
    }

    /// <summary>
    /// Stops the schedule. Can be resumed with StartSchedule().
    /// </summary>
    public void StopSchedule()
    {
        if (_activeSchedule == null || _activeSchedule.State == OrbitalScheduleState.Stopped)
            return;

        TransitionScheduleState(OrbitalScheduleState.Stopped);
        GameLogger.Info($"OrbitalScheduleExecutor [{_unit.Name}]: Schedule stopped");
    }

    /// <summary>
    /// Cancels the schedule entirely and clears it.
    /// </summary>
    public void CancelSchedule()
    {
        if (_activeSchedule == null)
            return;

        GameLogger.Info($"OrbitalScheduleExecutor [{_unit.Name}]: Schedule cancelled");
        _activeSchedule = null;

        if (_unit != null && _unit.State != LogisticsUnitState.InTransit)
        {
            _unit.TransitionTo(LogisticsUnitState.Idle);
        }
    }

    // =========================================================================
    // LEG PROCESSING
    // =========================================================================

    private void ProcessLeg(Leg leg, float delta)
    {
        switch (leg.State)
        {
            case LegState.Pending:
                BeginLeg(leg);
                break;

            case LegState.RefuelingAtOrigin:
                HandleRefueling(leg, atOrigin: true);
                break;

            case LegState.LoadingCargo:
                HandleLoadingCargo(leg);
                break;

            case LegState.PlanningTrajectory:
                HandleTrajectoryPlanning(leg);
                break;

            case LegState.WaitingForRetry:
                HandleWaitingForRetry(leg, delta);
                break;

            case LegState.InTransit:
                HandleInTransit(leg);
                break;

            case LegState.RefuelingAtDestination:
                HandleRefueling(leg, atOrigin: false);
                break;

            case LegState.UnloadingCargo:
                HandleUnloadingCargo(leg);
                break;

            case LegState.Complete:
                AdvanceToNextLeg();
                break;
        }
    }

    private void BeginLeg(Leg leg)
    {
        int legIndex = _activeSchedule!.CurrentLegIndex;
        GameLogger.Info($"OrbitalScheduleExecutor [{_unit!.Name}]: Beginning leg {legIndex} ({leg.LegId})");
        SignalBus.Instance?.EmitOrbitalLegStarted(_unit.Id, legIndex);

        if (leg.RefuelInstructions.RefuelAtOrigin && leg.Origin.IsStation)
        {
            leg.State = LegState.RefuelingAtOrigin;
            _unit.TransitionTo(LogisticsUnitState.Loading);
        }
        else if (leg.PickupOrder != null && leg.Origin.IsStation)
        {
            leg.State = LegState.LoadingCargo;
            _unit.TransitionTo(LogisticsUnitState.Loading);
        }
        else
        {
            leg.State = LegState.PlanningTrajectory;
        }
    }

    private void HandleRefueling(Leg leg, bool atOrigin)
    {
        var endpoint = atOrigin ? leg.Origin : leg.Destination;

        if (!endpoint.IsStation || endpoint.Station?.Economy == null)
        {
            GameLogger.Warning($"OrbitalScheduleExecutor [{_unit!.Name}]: Cannot refuel — endpoint is not a station with economy");
            AdvanceRefuelState(leg, atOrigin);
            return;
        }

        var economy = endpoint.Station.Economy;
        string fuelId = leg.RefuelInstructions.FuelResourceId;

        float fuelNeeded = leg.RefuelInstructions.Amount;
        if (fuelNeeded >= float.MaxValue)
        {
            fuelNeeded = _unit!.MaxFuel - _unit.Fuel;
        }

        if (fuelNeeded <= 0f)
        {
            AdvanceRefuelState(leg, atOrigin);
            return;
        }

        float withdrawn = economy.WithdrawResource(fuelId, fuelNeeded);
        if (withdrawn > 0f)
        {
            _unit!.Refuel(withdrawn);
            GameLogger.Info($"OrbitalScheduleExecutor [{_unit.Name}]: Refueled {withdrawn:F2} of {fuelId} at {endpoint.Station.Name}");
        }
        else
        {
            GameLogger.Warning($"OrbitalScheduleExecutor [{_unit!.Name}]: No {fuelId} available at {endpoint.Station.Name}");
        }

        AdvanceRefuelState(leg, atOrigin);
    }

    private void AdvanceRefuelState(Leg leg, bool atOrigin)
    {
        if (atOrigin)
        {
            if (leg.PickupOrder != null && leg.Origin.IsStation)
            {
                leg.State = LegState.LoadingCargo;
            }
            else
            {
                leg.State = LegState.PlanningTrajectory;
            }
        }
        else
        {
            // At destination, after refueling
            if (leg.DropoffOrder != null && leg.Destination.IsStation)
            {
                leg.State = LegState.UnloadingCargo;
            }
            else
            {
                leg.State = LegState.Complete;
                EmitLegCompleted();
            }
        }
    }

    private void HandleLoadingCargo(Leg leg)
    {
        if (!leg.Origin.IsStation || leg.Origin.Station?.Economy == null || leg.PickupOrder == null)
        {
            leg.State = LegState.PlanningTrajectory;
            return;
        }

        var economy = leg.Origin.Station.Economy;

        foreach (var kvp in leg.PickupOrder.Resources)
        {
            string resourceId = kvp.Key;
            float requested = kvp.Value;

            float withdrawn = economy.WithdrawResource(resourceId, requested);
            if (withdrawn > 0f)
            {
                _unit!.LoadCargo(resourceId, withdrawn);
            }
        }

        GameLogger.Info($"OrbitalScheduleExecutor [{_unit!.Name}]: Loaded cargo at {leg.Origin.Station.Name}");
        SignalBus.Instance?.EmitOrbitalCargoTransferCompleted(_unit.Id, leg.Origin.Station.Id, true);

        leg.State = LegState.PlanningTrajectory;
    }

    private void HandleTrajectoryPlanning(Leg leg)
    {
        if (_unit == null || _movementController == null || _activeSchedule == null)
            return;

        var options = _unit.GetRouteOptions(
            leg.Destination.Body,
            0f,
            leg.DepartureConstraints.NumOptions,
            leg.Destination.BandIndex
        );

        var filtered = FilterByConstraints(options, leg.DepartureConstraints);

        if (filtered.Count > 0)
        {
            var ranked = TrajectorySolution.RankBy(filtered, leg.DepartureConstraints.RankingCriteria);
            var selected = ranked[0];
            leg.SelectedTrajectory = selected;

            // Plan the route on the unit (sets _plannedTrajectory, transitions to Planning)
            _unit.PlanRoute(selected);

            // Execute immediately
            if (_unit.ExecuteTrajectory())
            {
                leg.State = LegState.InTransit;
                GameLogger.Info(
                    $"OrbitalScheduleExecutor [{_unit.Name}]: Trajectory selected — "
                    + $"TOF: {selected.TimeOfFlight:F1}s, ΔV: {selected.DeltaVRequired:F1} m/s"
                );
            }
            else
            {
                GameLogger.Warning($"OrbitalScheduleExecutor [{_unit.Name}]: ExecuteTrajectory failed");
                HandleTrajectoryFailure(leg, "Trajectory execution failed");
            }
        }
        else
        {
            HandleTrajectoryFailure(leg, "No trajectories within budget constraints");
        }
    }

    private void HandleTrajectoryFailure(Leg leg, string reason)
    {
        leg.TrajectoryAttempts++;
        int legIndex = _activeSchedule!.CurrentLegIndex;

        if (leg.TrajectoryAttempts >= _activeSchedule.MaxRetries)
        {
            GameLogger.Warning(
                $"OrbitalScheduleExecutor [{_unit!.Name}]: Leg {legIndex} failed after {leg.TrajectoryAttempts} attempts — {reason}"
            );
            leg.State = LegState.Failed;
            TransitionScheduleState(OrbitalScheduleState.Stranded);
            _unit.TransitionTo(LogisticsUnitState.Stranded);
            SignalBus.Instance?.EmitOrbitalLegFailed(_unit.Id, legIndex, reason);
        }
        else
        {
            GameLogger.Info(
                $"OrbitalScheduleExecutor [{_unit!.Name}]: Trajectory attempt {leg.TrajectoryAttempts}/{_activeSchedule.MaxRetries} failed — retrying in {_activeSchedule.RetryPeriod}s"
            );
            leg.State = LegState.WaitingForRetry;
            _unit.TransitionTo(LogisticsUnitState.WaitingForTrajectory);
            _retryTimer = _activeSchedule.RetryPeriod;
            SignalBus.Instance?.EmitOrbitalTrajectoryRetry(_unit.Id, legIndex, leg.TrajectoryAttempts, _activeSchedule.MaxRetries);
        }
    }

    private void HandleWaitingForRetry(Leg leg, float delta)
    {
        _retryTimer -= delta;
        if (_retryTimer <= 0f)
        {
            leg.State = LegState.PlanningTrajectory;
            // Transition back to a state that allows Planning
            if (_unit!.State == LogisticsUnitState.WaitingForTrajectory)
            {
                _unit.TransitionTo(LogisticsUnitState.Planning);
                // Immediately go back to idle so we can re-enter Planning from the trajectory planning handler
                _unit.TransitionTo(LogisticsUnitState.Idle);
            }
        }
    }

    private void HandleInTransit(Leg leg)
    {
        if (_movementController == null || _unit == null)
            return;

        // Check if transfer completed
        if (!_movementController.IsTransferring)
        {
            if (_unit.State == LogisticsUnitState.Stranded)
            {
                // Unit ran out of fuel mid-transit
                leg.State = LegState.Failed;
                TransitionScheduleState(OrbitalScheduleState.Stranded);
                SignalBus.Instance?.EmitOrbitalLegFailed(
                    _unit.Id,
                    _activeSchedule!.CurrentLegIndex,
                    "Unit stranded — fuel exhausted mid-transit"
                );
                return;
            }

            // Transfer completed successfully — handle post-arrival operations
            if (leg.RefuelInstructions.RefuelAtDestination && leg.Destination.IsStation)
            {
                leg.State = LegState.RefuelingAtDestination;
                _unit.TransitionTo(LogisticsUnitState.Unloading);
            }
            else if (leg.DropoffOrder != null && leg.Destination.IsStation)
            {
                leg.State = LegState.UnloadingCargo;
                _unit.TransitionTo(LogisticsUnitState.Unloading);
            }
            else
            {
                leg.State = LegState.Complete;
                EmitLegCompleted();
            }
        }
    }

    private void HandleUnloadingCargo(Leg leg)
    {
        if (!leg.Destination.IsStation || leg.Destination.Station?.Economy == null || leg.DropoffOrder == null)
        {
            leg.State = LegState.Complete;
            EmitLegCompleted();
            return;
        }

        var economy = leg.Destination.Station.Economy;

        foreach (var kvp in leg.DropoffOrder.Resources)
        {
            string resourceId = kvp.Key;
            float requested = kvp.Value;

            float available = _unit!.Cargo.GetResourceQuantity(resourceId);
            float toUnload = Mathf.Min(requested, available);

            if (toUnload > 0f)
            {
                _unit.UnloadCargo(resourceId, toUnload);
                economy.DepositResource(resourceId, toUnload);
            }
        }

        GameLogger.Info($"OrbitalScheduleExecutor [{_unit!.Name}]: Unloaded cargo at {leg.Destination.Station.Name}");
        SignalBus.Instance?.EmitOrbitalCargoTransferCompleted(_unit.Id, leg.Destination.Station.Id, false);

        leg.State = LegState.Complete;
        EmitLegCompleted();
    }

    // =========================================================================
    // LEG ADVANCEMENT
    // =========================================================================

    private void AdvanceToNextLeg()
    {
        if (_activeSchedule == null || _unit == null)
            return;

        _activeSchedule.CurrentLegIndex++;

        if (_activeSchedule.CurrentLegIndex >= _activeSchedule.Legs.Count)
        {
            if (_activeSchedule.IsRepeating)
            {
                GameLogger.Info($"OrbitalScheduleExecutor [{_unit.Name}]: Schedule cycle complete — repeating");
                _activeSchedule.ResetForRepeat();

                if (_activeSchedule.WaitPeriodBetweenLegs > 0f)
                {
                    _waitTimer = _activeSchedule.WaitPeriodBetweenLegs;
                    TransitionScheduleState(OrbitalScheduleState.WaitingBetweenLegs);
                }
                // else stays Running, will pick up first leg next frame
            }
            else
            {
                GameLogger.Info($"OrbitalScheduleExecutor [{_unit.Name}]: Schedule complete");
                TransitionScheduleState(OrbitalScheduleState.Complete);
                _unit.TransitionTo(LogisticsUnitState.Idle);
            }
        }
        else if (_activeSchedule.WaitPeriodBetweenLegs > 0f)
        {
            _waitTimer = _activeSchedule.WaitPeriodBetweenLegs;
            TransitionScheduleState(OrbitalScheduleState.WaitingBetweenLegs);
            _unit.TransitionTo(LogisticsUnitState.Idle);
        }
        // else stays Running, will begin next leg next frame
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private List<TrajectorySolution> FilterByConstraints(
        List<TrajectorySolution> options,
        DepartureConstraints constraints)
    {
        return options.Where(t =>
        {
            float value = constraints.BudgetMode == ExpenditureBudgetMode.TimeOfFlight
                ? t.TimeOfFlight
                : t.DeltaVRequired;

            if (constraints.MinBudget.HasValue && value < constraints.MinBudget.Value)
                return false;

            if (constraints.MaxBudget.HasValue && value > constraints.MaxBudget.Value)
                return false;

            return true;
        }).ToList();
    }

    private void TransitionScheduleState(OrbitalScheduleState newState)
    {
        if (_activeSchedule == null || _unit == null)
            return;

        _activeSchedule.State = newState;
        SignalBus.Instance?.EmitOrbitalScheduleStateChanged(_unit.Id, newState);
    }

    private void EmitLegCompleted()
    {
        if (_unit == null || _activeSchedule == null)
            return;

        int legIndex = _activeSchedule.CurrentLegIndex;
        GameLogger.Info($"OrbitalScheduleExecutor [{_unit.Name}]: Leg {legIndex} complete");
        SignalBus.Instance?.EmitOrbitalLegCompleted(_unit.Id, legIndex);
    }
}
