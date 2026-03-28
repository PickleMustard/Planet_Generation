using System.Collections.Generic;
using Structures.Enums;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Holds all construction progress logic. Both StationSatellite and LogisticsUnit
/// delegate their IConstructable methods to an instance of this class.
/// </summary>
public class ConstructionState
{
    public float WorkRequired { get; set; }
    public float WorkDone { get; set; }
    public ConstructionStatus Status { get; private set; } = ConstructionStatus.Pending;
    public Dictionary<string, int> RequiredResources { get; set; } = new();
    public Dictionary<string, int> AvailableResources { get; set; } = new();

    private ConstructionStatus _previousStatus = ConstructionStatus.Pending;

    public ConstructionState(float workRequired, Dictionary<string, int>? requiredResources = null)
    {
        WorkRequired = workRequired;
        WorkDone = 0f;
        if (requiredResources != null)
        {
            RequiredResources = new Dictionary<string, int>(requiredResources);
        }
    }

    /// <summary>
    /// Returns normalized progress [0, 1].
    /// </summary>
    public float GetProgress()
    {
        if (WorkRequired <= 0f) return 1f;
        return WorkDone / WorkRequired;
    }

    /// <summary>
    /// Returns true if all required resources have been delivered.
    /// </summary>
    public bool CheckRequiredResourcesAvailable()
    {
        foreach (var kvp in RequiredResources)
        {
            if (!AvailableResources.TryGetValue(kvp.Key, out int available) || available < kvp.Value)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Starts construction. Immediately enters InProgress if resources are available,
    /// otherwise enters Blocked and waits for delivery.
    /// </summary>
    public void TryStart()
    {
        if (Status == ConstructionStatus.Complete || Status == ConstructionStatus.Cancelled)
            return;

        if (CheckRequiredResourcesAvailable())
        {
            SetStatus(ConstructionStatus.InProgress);
        }
        else
        {
            SetStatus(ConstructionStatus.Blocked);
        }
    }

    /// <summary>
    /// Advances construction by delta seconds. Returns the new status.
    /// Transitions: InProgress → Complete when done, InProgress → Blocked when resources depleted,
    /// Blocked → InProgress when resources become available.
    /// </summary>
    public ConstructionStatus UpdateProgress(float delta)
    {
        if (Status == ConstructionStatus.Complete || Status == ConstructionStatus.Cancelled)
            return Status;

        // Check if blocked status should be lifted
        if (Status == ConstructionStatus.Blocked)
        {
            if (CheckRequiredResourcesAvailable())
            {
                SetStatus(ConstructionStatus.InProgress);
            }
            else
            {
                return Status;
            }
        }

        // Only tick work when InProgress
        if (Status == ConstructionStatus.InProgress)
        {
            // Check resources are still available
            if (!CheckRequiredResourcesAvailable())
            {
                SetStatus(ConstructionStatus.Blocked);
                return Status;
            }

            WorkDone += delta;

            if (WorkDone >= WorkRequired)
            {
                WorkDone = WorkRequired;
                SetStatus(ConstructionStatus.Complete);
            }
        }

        return Status;
    }

    /// <summary>
    /// Delivers resources to this construction site.
    /// </summary>
    public void DeliverResources(string resourceId, int amount)
    {
        if (string.IsNullOrEmpty(resourceId) || amount <= 0) return;

        if (!AvailableResources.ContainsKey(resourceId))
            AvailableResources[resourceId] = 0;

        AvailableResources[resourceId] += amount;

        GameLogger.Debug($"ConstructionState: Delivered {amount}x {resourceId} (now {AvailableResources[resourceId]})");
    }

    /// <summary>
    /// Cancels construction.
    /// </summary>
    public void Cancel()
    {
        if (Status == ConstructionStatus.Complete) return;
        SetStatus(ConstructionStatus.Cancelled);
    }

    public bool IsComplete() => Status == ConstructionStatus.Complete;

    /// <summary>
    /// Whether the status changed on the last UpdateProgress call.
    /// </summary>
    public bool StatusChanged => Status != _previousStatus;

    /// <summary>
    /// The status before the last transition.
    /// </summary>
    public ConstructionStatus PreviousStatus => _previousStatus;

    private void SetStatus(ConstructionStatus newStatus)
    {
        _previousStatus = Status;
        Status = newStatus;
    }
}
