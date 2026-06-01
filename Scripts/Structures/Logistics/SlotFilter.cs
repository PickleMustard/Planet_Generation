using System;
using Structures.Enums;
using Structures.Resources;

namespace Structures.Logistics;

/// <summary>
/// What a storage slot is allowed to accept. A slot may admit any resource,
/// only one specific resource, only resources in a category, or only resources
/// of a given matter state.
/// </summary>
public enum SlotFilterKind
{
    Any,
    Resource,
    Category,
    MatterState,
}

/// <summary>
/// Acceptance rule for a <see cref="StorageSlot"/>. A slot's filter is fixed at
/// creation; once a resource occupies the slot, the slot is further locked to
/// that resource until it empties.
/// </summary>
public sealed class SlotFilter
{
    public SlotFilterKind Kind { get; }
    public string? ResourceId { get; }
    public string? Category { get; }
    public StateOfMatter? State { get; }

    private SlotFilter(SlotFilterKind kind, string? resourceId, string? category, StateOfMatter? state)
    {
        Kind = kind;
        ResourceId = resourceId;
        Category = category;
        State = state;
    }

    public static SlotFilter Any() => new(SlotFilterKind.Any, null, null, null);

    public static SlotFilter ForResource(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource id cannot be empty", nameof(resourceId));
        return new SlotFilter(SlotFilterKind.Resource, resourceId, null, null);
    }

    public static SlotFilter ForCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be empty", nameof(category));
        return new SlotFilter(SlotFilterKind.Category, null, category, null);
    }

    public static SlotFilter ForState(StateOfMatter state) =>
        new(SlotFilterKind.MatterState, null, null, state);

    public bool Accepts(ResourceDefinition resource)
    {
        return Kind switch
        {
            SlotFilterKind.Any => true,
            SlotFilterKind.Resource => string.Equals(resource.IdName, ResourceId, StringComparison.Ordinal),
            SlotFilterKind.Category => string.Equals(resource.ResourceType, Category, StringComparison.Ordinal),
            SlotFilterKind.MatterState => State.HasValue && resource.StateOfMatter == State.Value,
            _ => false,
        };
    }

    public override string ToString() => Kind switch
    {
        SlotFilterKind.Any => "any",
        SlotFilterKind.Resource => $"resource:{ResourceId}",
        SlotFilterKind.Category => $"category:{Category}",
        SlotFilterKind.MatterState => $"state:{State}",
        _ => "unknown",
    };
}
