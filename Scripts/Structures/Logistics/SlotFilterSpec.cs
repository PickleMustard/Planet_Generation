namespace Structures.Logistics;

/// <summary>
/// Pairing of a <see cref="SlotFilter"/> with a slot count. Used by
/// <see cref="Constructables.Buildings.Behaviors.StorageHubBehavior"/> to allocate
/// the configured filter mix into BulkStorage at registration time.
/// </summary>
public sealed class SlotFilterSpec
{
    public SlotFilter Filter { get; set; } = SlotFilter.Any();
    public int Count { get; set; }

    public SlotFilterSpec() { }

    public SlotFilterSpec(SlotFilter filter, int count)
    {
        Filter = filter;
        Count = count;
    }
}
