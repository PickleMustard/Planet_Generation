using System;
using Structures.Enums;

namespace Structures.Logistics;

/// <summary>
/// Tracks the application of a modifier to a value, including source ID, timestamp,
/// modifier type, and before/after state changes for audit and debugging purposes.
/// </summary>
public struct AppliedModifier
{
    /// <summary>
    /// Unique identifier for the source of this applied modifier.
    /// </summary>
    public string SourceId { get; private set; }

    /// <summary>
    /// Timestamp when this modifier was applied.
    /// </summary>
    public DateTime Timestamp { get; private set; }

    /// <summary>
    /// The type of modifier that was applied.
    /// </summary>
    public ModifierType ModifierType { get; private set; }

    /// <summary>
    /// The value before the modifier was applied.
    /// </summary>
    public float BeforeValue { get; private set; }

    /// <summary>
    /// The value after the modifier was applied.
    /// </summary>
    public float AfterValue { get; private set; }

    /// <summary>
    /// Creates a new AppliedModifier record.
    /// </summary>
    /// <param name="sourceId">Unique identifier for the modifier source.</param>
    /// <param name="modifierType">The type of modifier being applied.</param>
    /// <param name="beforeValue">The value before the modifier was applied.</param>
    /// <param name="afterValue">The value after the modifier was applied.</param>
    public AppliedModifier(string sourceId, ModifierType modifierType, float beforeValue, float afterValue)
    {
        SourceId = sourceId;
        Timestamp = DateTime.UtcNow;
        ModifierType = modifierType;
        BeforeValue = beforeValue;
        AfterValue = afterValue;
    }

    /// <summary>
    /// Creates a new AppliedModifier record with a specific timestamp.
    /// Useful for replaying or recreating modifier history.
    /// </summary>
    /// <param name="sourceId">Unique identifier for the modifier source.</param>
    /// <param name="modifierType">The type of modifier being applied.</param>
    /// <param name="beforeValue">The value before the modifier was applied.</param>
    /// <param name="afterValue">The value after the modifier was applied.</param>
    /// <param name="timestamp">Specific timestamp for the application.</param>
    public AppliedModifier(string sourceId, ModifierType modifierType, float beforeValue, float afterValue, DateTime timestamp)
    {
        SourceId = sourceId;
        Timestamp = timestamp;
        ModifierType = modifierType;
        BeforeValue = beforeValue;
        AfterValue = afterValue;
    }

    /// <summary>
    /// The difference between the after and before values.
    /// </summary>
    public float Delta => AfterValue - BeforeValue;

    /// <summary>
    /// The percentage change from before to after.
    /// Returns 0 if before value is 0 to avoid division by zero.
    /// </summary>
    public float PercentageChange => BeforeValue != 0 ? (Delta / BeforeValue) * 100f : 0f;
}
