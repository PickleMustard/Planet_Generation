using Godot;
using Structures.Resources;

namespace Structures.Logistics;

/// <summary>
/// Represents a single package of resources in transit between two <see cref="ResourceNode"/> instances.
/// Progress is tracked per-tick and deposit is delegated to the target node's owner when complete.
/// </summary>
public partial class ResourcePackage : Resource
{
    /// <summary>
    /// The unique identifier of the resource being transported.
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// The amount of resource in this package. Whole units only.
    /// </summary>
    public int Quantity { get; set; }

    private float _progress;

    /// <summary>
    /// Transport progress in the range 0.0..1.0.
    /// </summary>
    public float Progress
    {
        get => _progress;
        set => _progress = Mathf.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// The <see cref="ResourceLink"/> that owns this package.
    /// </summary>
    public ResourceLink? Link { get; set; }

    /// <summary>
    /// Whether this package is stuck and cannot proceed.
    /// </summary>
    public bool Stuck { get; set; }

    /// <summary>
    /// Returns true when <see cref="Progress"/> has reached 1.0.
    /// </summary>
    public bool IsComplete => _progress >= 1f;

    /// <summary>
    /// Advances transport progress by the specified amount, clamped to 0.0..1.0.
    /// </summary>
    /// <param name="amount">Amount to add to progress.</param>
    public void AdvanceProgress(float amount)
    {
        _progress = Mathf.Clamp(_progress + amount, 0f, 1f);
    }

    /// <summary>
    /// Attempts to deliver this package to the target node's owner.
    /// </summary>
    /// <returns>True if the owner accepted the deposit; otherwise false.</returns>
    public bool TryDeposit()
    {
        if (Link?.Target?.Owner is not { } owner)
        {
            return false;
        }

        return owner.OnDelivery(this);
    }
}
