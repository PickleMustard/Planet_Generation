using System.Collections.Generic;

namespace UI.Wireframe;

public enum SlipStatus
{
    Waiting,
    Loading,
    Blocked,
    InTransit,
}

/// <summary>
/// View-model for a single dispatch slip card. Built by the slips list view
/// from a <c>TransferSchedule</c> + lookup data; the card never reaches into
/// game state directly.
/// </summary>
public sealed class SlipCardData
{
    public string ScheduleId = string.Empty;
    public int Priority = 1;
    public string DestinationName = string.Empty;
    public string DestinationCode = string.Empty;
    public string DestinationVia = string.Empty;
    public string DestinationDistance = string.Empty;
    public List<ManifestEntry> Manifest = new();
    public string ConditionLabel = string.Empty;
    public string ConditionShort = string.Empty;
    public List<string> WatchedResourceIcons = new();
    public float WeightTons;
    public string LastRun = "—";
    public StateDot.DotState State = StateDot.DotState.Idle;

    public SlipStatus Status = SlipStatus.Waiting;
    /// <summary>0..1 fill, or -1 for indeterminate.</summary>
    public float ProgressFraction = -1f;
    public string ProgressLabel = "—";
    public string StatusLabel = "idle";

    public bool IsOneTime;
    public bool IsCompleted;
    public string OrderId = string.Empty;

    public sealed class ManifestEntry
    {
        public string Icon = string.Empty;
        public string Label = string.Empty;
        public int Units;
    }
}
