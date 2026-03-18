namespace Structures.Enums;

/// <summary>
/// Defines how a modifier should be applied to a value.
/// </summary>
public enum ModifierType
{
    /// <summary>
    /// Adds or subtracts a flat value from the base value.
    /// </summary>
    Additive,

    /// <summary>
    /// Multiplies or divides the base value by a percentage.
    /// </summary>
    Multiplicative
}
