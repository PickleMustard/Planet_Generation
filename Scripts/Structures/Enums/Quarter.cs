namespace Structures.Enums;

/// <summary>
/// The four in-game "months" of a year. A year is composed of the time span across all four
/// quarters in sequence (Q1 → Q2 → Q3 → Q4), after which the year increments and the quarter
/// wraps back to <see cref="Q1"/>. Tracked by <c>TimeKeeper</c>.
/// </summary>
public enum Quarter
{
    /// <summary>First quarter of the year.</summary>
    Q1,

    /// <summary>Second quarter of the year.</summary>
    Q2,

    /// <summary>Third quarter of the year.</summary>
    Q3,

    /// <summary>Fourth (final) quarter of the year.</summary>
    Q4
}
