using System.Collections.Generic;
using System.Linq;

namespace Structures.Logistics;

/// <summary>
/// Pure editing operations for an <see cref="OrbitalTransferSchedule"/>'s leg list.
/// Every operation preserves the chaining invariant
/// <c>leg[i].Origin == leg[i-1].Destination</c> with <c>leg[0].Origin</c> anchored
/// to the unit's current location, and keeps per-slot cargo / fuel / constraints
/// attached to their surviving legs.
///
/// The auto-generated closing leg (returns the unit to the start so a repeating
/// schedule can loop) is stripped before any structural edit and re-appended by
/// <see cref="Normalize"/> so it never participates in index-based operations.
/// </summary>
public static class OrbitalScheduleEditor
{
    /// <summary>
    /// Appends a new leg whose origin is the previous last leg's destination
    /// (or the unit location when the schedule is empty) and whose destination
    /// is <paramref name="destination"/>. Returns the created leg.
    /// </summary>
    public static Leg AppendLeg(OrbitalTransferSchedule schedule, LegEndpoint destination, LegEndpoint unitLocation)
    {
        StripClosingLeg(schedule);
        LegEndpoint origin = schedule.Legs.Count > 0
            ? Clone(schedule.Legs[^1].Destination)
            : Clone(unitLocation);

        var leg = new Leg
        {
            Origin = origin,
            Destination = Clone(destination),
        };
        schedule.Legs.Add(leg);
        Normalize(schedule, unitLocation);
        return leg;
    }

    /// <summary>
    /// Deletes the leg at <paramref name="index"/>. The preceding leg's
    /// destination is rewired to the deleted leg's destination (so
    /// A→B, B→C minus leg 2 becomes A→C); the deleted leg's manifest is
    /// discarded while survivors keep theirs. Deleting leg 0 promotes the next
    /// leg's origin to the unit location.
    /// </summary>
    public static void DeleteLeg(OrbitalTransferSchedule schedule, int index, LegEndpoint unitLocation)
    {
        StripClosingLeg(schedule);
        var legs = schedule.Legs;
        if (index < 0 || index >= legs.Count)
            return;

        if (index > 0)
            legs[index - 1].Destination = Clone(legs[index].Destination);
        legs.RemoveAt(index);

        Normalize(schedule, unitLocation);
    }

    /// <summary>
    /// Swaps the order of two legs. Only the destination endpoints are
    /// exchanged; origins are re-chained and per-slot manifests stay put.
    /// (A→B, B→C swapped becomes A→C, C→B.)
    /// </summary>
    public static void SwapLegs(OrbitalTransferSchedule schedule, int i, int j, LegEndpoint unitLocation)
    {
        StripClosingLeg(schedule);
        var legs = schedule.Legs;
        if (i < 0 || j < 0 || i >= legs.Count || j >= legs.Count || i == j)
            return;

        (legs[i].Destination, legs[j].Destination) = (legs[j].Destination, legs[i].Destination);
        Normalize(schedule, unitLocation);
    }

    /// <summary>
    /// Re-establishes the chaining invariant across all non-closing legs:
    /// leg 0's origin is the unit location and each subsequent leg's origin is
    /// the previous leg's destination. Endpoints are cloned so legs never share
    /// a mutable endpoint instance.
    /// </summary>
    public static void ReChain(OrbitalTransferSchedule schedule, LegEndpoint unitLocation)
    {
        StripClosingLeg(schedule);
        var legs = schedule.Legs;
        if (legs.Count == 0)
            return;

        legs[0].Origin = Clone(unitLocation);
        for (int i = 1; i < legs.Count; i++)
            legs[i].Origin = Clone(legs[i - 1].Destination);
    }

    /// <summary>
    /// Returns the closing leg required to loop a repeating schedule back to its
    /// start (final destination → first origin), or null when the schedule is
    /// not repeating, is empty, or already ends where it began. The returned leg
    /// is flagged <see cref="Leg.IsClosingLeg"/> and is not added to the schedule.
    /// </summary>
    public static Leg? ComputeClosingLeg(OrbitalTransferSchedule schedule)
    {
        var legs = schedule.Legs.Where(l => !l.IsClosingLeg).ToList();
        if (!schedule.IsRepeating || legs.Count == 0)
            return null;

        var first = legs[0];
        var last = legs[^1];
        if (SameEndpoint(last.Destination, first.Origin))
            return null;

        return new Leg
        {
            Origin = Clone(last.Destination),
            Destination = Clone(first.Origin),
            IsClosingLeg = true,
        };
    }

    /// <summary>
    /// Re-chains the schedule and (re)appends the closing leg if one is needed.
    /// Call after any structural edit.
    /// </summary>
    public static void Normalize(OrbitalTransferSchedule schedule, LegEndpoint unitLocation)
    {
        ReChain(schedule, unitLocation);
        var closing = ComputeClosingLeg(schedule);
        if (closing != null)
            schedule.Legs.Add(closing);
    }

    /// <summary>
    /// True when two endpoints refer to the same place: same station by id, or
    /// (for bare-body endpoints) the same orbital body instance.
    /// </summary>
    public static bool SameEndpoint(LegEndpoint? a, LegEndpoint? b)
    {
        if (a == null || b == null)
            return false;
        if (a.Station != null || b.Station != null)
            return a.Station != null && b.Station != null && a.Station.Id == b.Station.Id;
        return ReferenceEquals(a.Body, b.Body);
    }

    private static void StripClosingLeg(OrbitalTransferSchedule schedule)
        => schedule.Legs.RemoveAll(l => l.IsClosingLeg);

    private static LegEndpoint Clone(LegEndpoint e)
        => e.Station != null
            ? LegEndpoint.ForStation(e.Station, e.Body, e.BandIndex)
            : LegEndpoint.ForBody(e.Body, e.BandIndex);
}
