using System.Collections.Generic;
using Constructables;
using Constructables.Buildings.Behaviors;
using Structures.Enums;
using Structures.Resources;
using Structures.Transfers;
using UI.Wireframe;

namespace UI.TransferPlanning;

/// <summary>
/// Translates a <see cref="TransferSchedule"/> + lookup data into a
/// <see cref="SlipCardData"/> view-model the UI can render directly.
/// </summary>
internal static class SlipDataBuilder
{
    /// <summary>Seconds without an accumulating-progress advance before the slip is marked Blocked.</summary>
    public const double BlockedStallSeconds = 5.0;

    public static SlipCardData BuildFromOrder(
        TransferOrder order,
        TransferStationBehavior? behavior,
        ResourceDatabase? resources,
        bool completed,
        int displayPriority)
    {
        var data = new SlipCardData
        {
            IsOneTime = true,
            IsCompleted = completed,
            OrderId = order.OrderId,
            ScheduleId = order.OrderId,
            Priority = displayPriority > 0 ? displayPriority : 1,
            DestinationName = DescribeDestination(order.Destination),
            DestinationCode = ShortDestinationCode(order.Destination),
            DestinationVia = "rail",
            DestinationDistance = order.TravelTimeSeconds > 0f
                ? $"{order.TravelTimeSeconds:0.#}s ETA"
                : "—",
            ConditionLabel = "One-time order",
            ConditionShort = "one-shot",
            LastRun = "—",
        };

        float weight = 0f;
        foreach (var kvp in order.Manifest.Resources)
        {
            string id = kvp.Key;
            int units = kvp.Value;
            float perUnitWeight = LookupTransportWeight(resources, id);
            data.Manifest.Add(new SlipCardData.ManifestEntry
            {
                Icon = ShortResourceIcon(id),
                Label = LookupResourceLabel(resources, id),
                Units = units,
            });
            weight += units * perUnitWeight;
        }
        data.WeightTons = weight;

        ApplyOrderRuntime(data, order, completed);
        return data;
    }

    public static void ApplyOrderRuntime(SlipCardData data, TransferOrder order, bool completed)
    {
        if (completed)
        {
            data.Status = SlipStatus.Waiting;
            data.ProgressFraction = 1f;
            data.ProgressLabel = "delivered";
            data.StatusLabel = "delivered";
            data.State = StateDot.DotState.Idle;
            return;
        }

        switch (order.State)
        {
            case SurfaceTransferState.InTransit:
                {
                    float remaining = System.Math.Max(0f, order.TravelTimeSeconds - order.ElapsedTimeSeconds);
                    data.Status = SlipStatus.InTransit;
                    data.ProgressFraction = order.Progress;
                    data.ProgressLabel = $"{remaining:0.#}s left";
                    data.StatusLabel = "in transit";
                    data.State = StateDot.DotState.Run;
                    break;
                }
            case SurfaceTransferState.Loading:
                data.Status = SlipStatus.Loading;
                data.ProgressFraction = -1f;
                data.ProgressLabel = "loading";
                data.StatusLabel = "loading";
                data.State = StateDot.DotState.Run;
                break;
            case SurfaceTransferState.Unloading:
                data.Status = SlipStatus.Loading;
                data.ProgressFraction = -1f;
                data.ProgressLabel = "unloading";
                data.StatusLabel = "unloading";
                data.State = StateDot.DotState.Run;
                break;
            case SurfaceTransferState.Reverting:
                data.Status = SlipStatus.Loading;
                data.ProgressFraction = -1f;
                data.ProgressLabel = "returning";
                data.StatusLabel = "returning";
                data.State = StateDot.DotState.Run;
                break;
            default:
                data.Status = SlipStatus.Waiting;
                data.ProgressFraction = 1f;
                data.ProgressLabel = "delivered";
                data.StatusLabel = "delivered";
                data.State = StateDot.DotState.Idle;
                break;
        }
    }

    public static SlipCardData BuildFromSchedule(
        TransferSchedule schedule,
        TransferStationBehavior? behavior,
        ResourceDatabase? resources)
    {
        var data = new SlipCardData
        {
            ScheduleId = schedule.ScheduleId,
            Priority = schedule.Priority > 0 ? schedule.Priority : 1,
            DestinationName = DescribeDestination(schedule.Destination),
            DestinationCode = ShortDestinationCode(schedule.Destination),
            DestinationVia = "rail",
            DestinationDistance = behavior != null
                ? $"{behavior.ComputeTravelTime(schedule.OriginBuildingId, schedule.Destination):0.#}s ETA"
                : "—",
        };

        float totalCapacity = behavior?.GetCapacity(schedule.OriginBuildingId) ?? 0f;
        float weight = 0f;
        foreach (var kvp in schedule.ResourceProportions)
        {
            string id = kvp.Key;
            float proportion = kvp.Value;
            float perUnitWeight = LookupTransportWeight(resources, id);
            float capacityForResource = totalCapacity * proportion;
            int units = perUnitWeight > 0f ? (int)(capacityForResource / perUnitWeight) : 0;
            string label = LookupResourceLabel(resources, id);
            string icon = ShortResourceIcon(id);
            data.Manifest.Add(new SlipCardData.ManifestEntry
            {
                Icon = icon,
                Label = label,
                Units = units,
            });
            weight += capacityForResource;

            if (schedule.DepartureMode == DepartureConditionMode.AnyResource
                && schedule.Threshold != DepartureThreshold.Full
                && !schedule.WaitSeconds.HasValue)
            {
                data.WatchedResourceIcons.Add(icon);
            }
        }
        data.WeightTons = weight;
        (data.ConditionLabel, data.ConditionShort) = DescribeCondition(schedule);
        ApplyRuntime(data, schedule, behavior);
        data.LastRun = "—";
        return data;
    }

    /// <summary>
    /// Cheap update path used by the per-card tick: refreshes only Status,
    /// ProgressFraction, ProgressLabel, StatusLabel, and the legacy StateDot field.
    /// Does not touch manifest, destination, or condition labels.
    /// </summary>
    public static void ApplyRuntime(
        SlipCardData data,
        TransferSchedule schedule,
        TransferStationBehavior? behavior)
    {
        var order = behavior?.GetActiveOrder(schedule.ScheduleId);

        if (schedule.State == TransferScheduleState.Dispatched && order != null)
        {
            if (order.State == SurfaceTransferState.InTransit)
            {
                float remaining = System.Math.Max(0f, order.TravelTimeSeconds - order.ElapsedTimeSeconds);
                data.Status = SlipStatus.InTransit;
                data.ProgressFraction = order.Progress;
                data.ProgressLabel = $"{remaining:0.#}s left";
                data.StatusLabel = "in transit";
            }
            else
            {
                // Loading / Unloading / Reverting — transient at endpoints.
                data.Status = SlipStatus.Loading;
                data.ProgressFraction = -1f;
                data.ProgressLabel = order.State == SurfaceTransferState.Unloading ? "unloading" : "loading";
                data.StatusLabel = data.ProgressLabel;
            }
        }
        else if (schedule.State == TransferScheduleState.Stopped)
        {
            data.Status = SlipStatus.Blocked;
            data.ProgressFraction = 0f;
            data.ProgressLabel = "stopped";
            data.StatusLabel = "blocked";
        }
        else if (schedule.State == TransferScheduleState.Accumulating && behavior != null)
        {
            (float prog, string label) = ComputeAccumulatingProgress(schedule, behavior);
            data.ProgressFraction = prog;
            data.ProgressLabel = label;

            // Wait-seconds schedules advance with wall clock; never flag as Blocked.
            bool stalled = false;
            if (!schedule.WaitSeconds.HasValue)
            {
                double stall = behavior.TotalTime - behavior.GetLastProgressTime(schedule.ScheduleId);
                stalled = stall > BlockedStallSeconds;
            }

            if (stalled)
            {
                data.Status = SlipStatus.Blocked;
                data.StatusLabel = "blocked";
                data.ProgressLabel = "stalled";
            }
            else
            {
                data.Status = SlipStatus.Waiting;
                data.StatusLabel = "waiting";
            }
        }
        else
        {
            data.Status = SlipStatus.Waiting;
            data.ProgressFraction = 0f;
            data.ProgressLabel = "idle";
            data.StatusLabel = "idle";
        }

        data.State = MapDotState(data.Status, schedule.State);
    }

    private static (float progress, string label) ComputeAccumulatingProgress(
        TransferSchedule schedule,
        TransferStationBehavior behavior)
    {
        if (schedule.WaitSeconds.HasValue)
        {
            float wait = schedule.WaitSeconds.Value;
            if (wait <= 0f) return (1f, "ready");
            double elapsed = behavior.TotalTime - schedule.LastDispatchTime;
            float frac = (float)System.Math.Clamp(elapsed / wait, 0.0, 1.0);
            float remaining = System.Math.Max(0f, wait - (float)elapsed);
            return (frac, $"{remaining:0.#}s to dispatch");
        }

        float totalCapacity = behavior.GetCapacity(schedule.OriginBuildingId);
        if (totalCapacity <= 0f || schedule.ResourceProportions.Count == 0)
            return (0f, "no capacity");

        float thresholdFraction = schedule.Threshold.ToFraction();
        bool anyMode = schedule.DepartureMode == DepartureConditionMode.AnyResource;
        float best = anyMode ? 0f : 1f;
        bool sawTarget = false;
        foreach (var kvp in schedule.ResourceProportions)
        {
            string id = kvp.Key;
            float perUnitWeight = LookupTransportWeight(ResourceDatabase.Instance, id);
            if (perUnitWeight <= 0f) continue;

            float capacityForResource = totalCapacity * kvp.Value;
            int targetUnits = (int)System.Math.Floor(capacityForResource / perUnitWeight);
            float required = targetUnits * thresholdFraction;
            if (required <= 0f) continue;

            int stockpile = behavior.GetCurrentStockpile(id);
            float ratio = System.Math.Clamp(stockpile / required, 0f, 1f);
            sawTarget = true;
            if (anyMode)
            {
                if (ratio > best) best = ratio;
            }
            else
            {
                if (ratio < best) best = ratio;
            }
        }

        if (!sawTarget) return (0f, "no target");
        int pct = (int)System.Math.Round(best * 100f);
        return (best, $"{pct}% filled");
    }

    private static StateDot.DotState MapDotState(SlipStatus status, TransferScheduleState scheduleState)
    {
        if (status == SlipStatus.Blocked) return StateDot.DotState.Block;
        if (scheduleState == TransferScheduleState.Idle) return StateDot.DotState.Idle;
        return StateDot.DotState.Run;
    }

    public static string DescribeDestination(TransferDestination dest)
    {
        if (dest.IsOrbitalStation)
            return $"Station {dest.StationSatelliteId?[..System.Math.Min(8, dest.StationSatelliteId.Length)]}";
        if (!string.IsNullOrEmpty(dest.BuildingId))
            return $"Hub {dest.BuildingId[..System.Math.Min(8, dest.BuildingId.Length)]}";
        return "Unknown";
    }

    public static string ShortDestinationCode(TransferDestination dest)
    {
        if (dest.IsOrbitalStation && dest.StationSatelliteId is { Length: > 0 } sid)
            return "ST-" + sid[..System.Math.Min(6, sid.Length)].ToUpperInvariant();
        if (!string.IsNullOrEmpty(dest.BuildingId))
            return "HB-" + dest.BuildingId[..System.Math.Min(6, dest.BuildingId.Length)].ToUpperInvariant();
        return "—";
    }

    public static (string longLabel, string shortLabel) DescribeCondition(TransferSchedule s)
    {
        if (s.WaitSeconds.HasValue)
        {
            int seconds = (int)s.WaitSeconds.Value;
            return ($"Wait {seconds}s", $"every {seconds}s");
        }
        string fraction = s.Threshold switch
        {
            DepartureThreshold.Quarter => "25%",
            DepartureThreshold.Half => "50%",
            DepartureThreshold.ThreeQuarter => "75%",
            DepartureThreshold.Full => "100%",
            _ => "—",
        };
        bool any = s.DepartureMode == DepartureConditionMode.AnyResource;
        string longLabel = any
            ? $"At least {fraction} of any tracked resource"
            : $"At least {fraction} of all resources";
        string shortLabel = (any ? "res " : "cap ") + (s.Threshold == DepartureThreshold.Full ? "= 100%" : "≥ " + fraction);
        return (longLabel, shortLabel);
    }

    public static string ShortResourceIcon(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return "—";
        var compact = resourceId.Replace("_", "").Replace("-", "").ToUpperInvariant();
        return compact[..System.Math.Min(3, compact.Length)];
    }

    public static string LookupResourceLabel(ResourceDatabase? db, string id)
    {
        if (db != null && db.IsLoaded && db.TryGetResource(id, out var def) && def != null)
            return def.IdName ?? id;
        return id;
    }

    public static float LookupTransportWeight(ResourceDatabase? db, string id)
    {
        if (db != null && db.IsLoaded && db.TryGetResource(id, out var def) && def != null)
            return def.TransportWeight > 0f ? def.TransportWeight : 1f;
        return 1f;
    }
}
