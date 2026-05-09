namespace Constructables.Power;

/// <summary>
/// Marker interface for behaviors that draw power from a <see cref="PowerGrid"/>.
/// </summary>
public interface IPowerConsumer
{
    /// <summary>
    /// Base draw per tick when the owner is running.
    /// </summary>
    float BaseDraw { get; }

    /// <summary>
    /// Grid this consumer is currently registered with. Set by
    /// <see cref="BodyPowerGridManager"/>; null when no covering grid exists.
    /// </summary>
    PowerGrid? Grid { get; set; }

    Constructables.Building? Owner { get; }

    /// <summary>
    /// Effective draw this tick, gated on owner state. Returns 0 when the owner is asleep,
    /// powered off, or otherwise not actively manufacturing.
    /// </summary>
    float GetCurrentDraw();
}
