using System.Collections.Generic;
using Constructables;
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
    public static SlipCardData BuildFromSchedule(
        TransferSchedule schedule,
        BodyTransferManager? mgr,
        ResourceDatabase? resources)
    {
        var data = new SlipCardData
        {
            ScheduleId = schedule.ScheduleId,
            Priority = schedule.Priority > 0 ? schedule.Priority : 1,
            DestinationName = DescribeDestination(schedule.Destination),
            DestinationCode = ShortDestinationCode(schedule.Destination),
            DestinationVia = "rail",
            DestinationDistance = mgr != null
                ? $"{mgr.ComputeTravelTime(schedule.OriginBuildingId, schedule.Destination):0.#}s ETA"
                : "—",
        };

        float totalCapacity = mgr?.GetCapacity(schedule.OriginBuildingId) ?? 0f;
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
        data.State = MapState(schedule.State);
        data.LastRun = "—";
        return data;
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

    public static StateDot.DotState MapState(TransferScheduleState state) => state switch
    {
        TransferScheduleState.Dispatched => StateDot.DotState.Run,
        TransferScheduleState.Accumulating => StateDot.DotState.Run,
        TransferScheduleState.Stopped => StateDot.DotState.Block,
        _ => StateDot.DotState.Idle,
    };

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
