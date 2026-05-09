using Structures.Enums;

namespace UI.TransferPlanning;

/// <summary>
/// Concrete dispatch-condition presets exposed in the manifest editor's
/// condition dropdown. Mirrors the design's eight options across capacity-,
/// resource-, and time-scoped conditions.
/// </summary>
public sealed class ConditionOption
{
    public string Id = "";
    public string Label = "";
    public ConditionScope ScopeKind;
    public DepartureConditionMode Mode;
    public DepartureThreshold Threshold;
    public float? WaitSeconds;

    public enum ConditionScope { All, Some, Time }

    public static readonly ConditionOption[] All =
    [
        new() { Id = "cap-full", Label = "Full capacity",      ScopeKind = ConditionScope.All,  Mode = DepartureConditionMode.AllResources, Threshold = DepartureThreshold.Full },
        new() { Id = "cap-50",   Label = "At least 50% full",  ScopeKind = ConditionScope.All,  Mode = DepartureConditionMode.AllResources, Threshold = DepartureThreshold.Half },
        new() { Id = "cap-75",   Label = "At least 75% full",  ScopeKind = ConditionScope.All,  Mode = DepartureConditionMode.AllResources, Threshold = DepartureThreshold.ThreeQuarter },
        new() { Id = "res-50",   Label = "At least 50% of any resource", ScopeKind = ConditionScope.Some, Mode = DepartureConditionMode.AnyResource, Threshold = DepartureThreshold.Half },
        new() { Id = "res-75",   Label = "At least 75% of any resource", ScopeKind = ConditionScope.Some, Mode = DepartureConditionMode.AnyResource, Threshold = DepartureThreshold.ThreeQuarter },
        new() { Id = "wait-30",  Label = "Wait 30 seconds",    ScopeKind = ConditionScope.Time, Mode = DepartureConditionMode.AllResources, Threshold = DepartureThreshold.Full, WaitSeconds = 30f },
        new() { Id = "wait-60",  Label = "Wait 60 seconds",    ScopeKind = ConditionScope.Time, Mode = DepartureConditionMode.AllResources, Threshold = DepartureThreshold.Full, WaitSeconds = 60f },
        new() { Id = "wait-120", Label = "Wait 120 seconds",   ScopeKind = ConditionScope.Time, Mode = DepartureConditionMode.AllResources, Threshold = DepartureThreshold.Full, WaitSeconds = 120f },
    ];

    public static ConditionOption Default => All[0];
}
