using Structures.Enums;

namespace Structures.Logistics;

/// <summary>
/// Defines expenditure budget constraints for trajectory planning on a leg.
/// The executor will filter trajectory options to those within these bounds.
/// </summary>
public class DepartureConstraints
{
    /// <summary>
    /// Whether the budget bounds are expressed as time-of-flight or delta-v.
    /// </summary>
    public ExpenditureBudgetMode BudgetMode { get; set; } = ExpenditureBudgetMode.TimeOfFlight;

    /// <summary>
    /// Minimum expenditure budget. Null means no lower bound.
    /// Units depend on BudgetMode: seconds for TOF, m/s for DeltaV.
    /// </summary>
    public float? MinBudget { get; set; }

    /// <summary>
    /// Maximum expenditure budget. Null means no upper bound.
    /// Units depend on BudgetMode: seconds for TOF, m/s for DeltaV.
    /// </summary>
    public float? MaxBudget { get; set; }

    /// <summary>
    /// Number of trajectory options to generate from the planner before filtering.
    /// </summary>
    public int NumOptions { get; set; } = 10;

    /// <summary>
    /// Ranking criteria for selecting among valid trajectory options.
    /// </summary>
    public TrajectorySolution.RankingCriteria RankingCriteria { get; set; }
        = TrajectorySolution.RankingCriteria.MostEfficient;
}
