namespace Structures.Enums;

/// <summary>
/// Defines the possible states of a constructable entity.
/// </summary>
public enum ConstructionStatus
{
    /// <summary>
    /// Construction has been queued but not yet started.
    /// </summary>
    Pending,

    /// <summary>
    /// Construction is actively progressing.
    /// </summary>
    InProgress,

    /// <summary>
    /// Construction is paused due to missing resources.
    /// </summary>
    Blocked,

    /// <summary>
    /// Construction has finished successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// Construction was cancelled before completion.
    /// </summary>
    Cancelled
}
