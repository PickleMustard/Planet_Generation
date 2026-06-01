namespace Structures.Enums;

/// <summary>
/// Defines whether departure constraints are expressed in time-of-flight or delta-v.
/// </summary>
public enum ExpenditureBudgetMode
{
    /// <summary>
    /// Budget constraints are expressed as time-of-flight bounds in seconds.
    /// </summary>
    TimeOfFlight,

    /// <summary>
    /// Budget constraints are expressed as delta-v bounds in m/s.
    /// </summary>
    DeltaV
}
