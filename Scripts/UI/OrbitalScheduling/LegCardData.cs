namespace UI.OrbitalScheduling;

/// <summary>
/// Immutable view-model snapshot for a single leg row in <see cref="LegListView"/>.
/// Built from a <see cref="Structures.Logistics.Leg"/> plus its validation result.
/// </summary>
public sealed class LegCardData
{
    public int Index;
    public string OriginName = "";
    public string DestName = "";
    public string StateText = "";
    public string ManifestSummary = "";
    public string FuelSummary = "";
    public string TimingSummary = "";

    /// <summary>True for the leg currently being executed (shown on top, marked ▶).</summary>
    public bool IsCurrent;

    /// <summary>True for the auto-generated return-to-start closing leg (not editable).</summary>
    public bool IsClosingLeg;

    public bool IsValid = true;
    public string InvalidReason = "";
}
