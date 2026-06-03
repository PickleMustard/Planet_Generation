using System.Collections.Generic;
using System.Linq;
using Structures.Enums;
using Structures.Logistics;

namespace Constructables;

/// <summary>
/// Per-leg validation result for an orbital schedule.
/// </summary>
public sealed class LegValidation
{
    /// <summary>Index of the leg in the schedule's leg list.</summary>
    public int LegIndex { get; set; }

    /// <summary>True when the leg is feasible (legal endpoints, a trajectory within
    /// constraints, and enough fuel).</summary>
    public bool IsValid { get; set; }

    /// <summary>Human-readable reason when invalid; empty when valid.</summary>
    public string Reason { get; set; } = "";

    /// <summary>True when this entry describes the auto-generated closing leg.</summary>
    public bool IsClosingLeg { get; set; }
}

/// <summary>
/// Aggregate validation result for a whole schedule.
/// </summary>
public sealed class ScheduleValidationResult
{
    public List<LegValidation> Legs { get; } = new();

    /// <summary>True only when every leg is valid and the schedule is non-empty.</summary>
    public bool IsValid => Legs.Count > 0 && Legs.All(l => l.IsValid);

    public LegValidation? ForLeg(int index) => Legs.FirstOrDefault(l => l.LegIndex == index);
}

/// <summary>
/// Validates an <see cref="OrbitalTransferSchedule"/> for a logistics unit: legal
/// (station-only) endpoints, a trajectory within each leg's
/// <see cref="DepartureConstraints"/>, and sufficient fuel. Runs synchronously on
/// the main thread (the trajectory planner reads live node positions).
///
/// Fuel/cargo state is approximated using the unit's CURRENT state for every leg
/// — it does not simulate cargo/fuel changes between legs, so it is a best-effort
/// pre-flight check rather than an exact forward simulation.
/// </summary>
public static class OrbitalScheduleValidator
{
    public static ScheduleValidationResult Validate(LogisticsUnit unit, OrbitalTransferSchedule schedule)
    {
        var result = new ScheduleValidationResult();
        if (unit == null || schedule == null)
            return result;

        for (int i = 0; i < schedule.Legs.Count; i++)
        {
            var leg = schedule.Legs[i];
            var v = new LegValidation { LegIndex = i, IsClosingLeg = leg.IsClosingLeg };
            ValidateLeg(unit, schedule, leg, v);
            result.Legs.Add(v);
        }

        return result;
    }

    private static void ValidateLeg(
        LogisticsUnit unit,
        OrbitalTransferSchedule schedule,
        Leg leg,
        LegValidation v)
    {
        // --- Endpoint legality: orbital transfers only touch satellites/stations. ---
        if (leg.Origin?.Body == null || leg.Destination?.Body == null)
        {
            v.Reason = "Leg is missing an endpoint.";
            return;
        }

        if (OrbitalScheduleEditor.SameEndpoint(leg.Origin, leg.Destination))
        {
            v.Reason = "Origin and destination are the same place.";
            return;
        }

        if (!leg.Destination.IsStation)
        {
            v.Reason = "Destination must be an orbital station (cannot use surface containers).";
            return;
        }

        // Pickup requires a station at the origin.
        if (leg.PickupOrder != null && leg.PickupOrder.ResourceCount > 0 && !leg.Origin.IsStation)
        {
            v.Reason = "Cargo pickup requires an origin station.";
            return;
        }

        // --- Trajectory feasibility within the leg's budget constraints. ---
        if (unit.CurrentEngine == null)
        {
            v.Reason = "Unit has no engine installed.";
            return;
        }

        var options = TrajectoryPlanner.Instance.GetOptions(
            unit,
            leg.Origin.Body,
            leg.Destination.Body,
            0f,
            leg.DepartureConstraints.NumOptions,
            leg.DepartureConstraints.RankingCriteria,
            leg.Destination.BandIndex);

        var filtered = FilterByConstraints(options, leg.DepartureConstraints);
        if (filtered.Count == 0)
        {
            v.Reason = options.Count == 0
                ? "No trajectory found between endpoints."
                : "No trajectory within the configured time/Δv range.";
            return;
        }

        var best = TrajectorySolution.RankBy(filtered, leg.DepartureConstraints.RankingCriteria)[0];

        // --- Fuel feasibility (approximate; uses current unit mass/fuel). ---
        float fuelNeeded = unit.CalculateFuelForDeltaV(best.DeltaVRequired);
        bool refuelsBefore = leg.RefuelInstructions.RefuelAtOrigin && leg.Origin.IsStation;
        float availableFuel = refuelsBefore ? unit.MaxFuel : unit.Fuel;
        if (fuelNeeded > availableFuel)
        {
            v.Reason = $"Not enough fuel: needs {fuelNeeded:F0} kg, has {availableFuel:F0} kg.";
            return;
        }

        v.IsValid = true;
    }

    private static List<TrajectorySolution> FilterByConstraints(
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
}
