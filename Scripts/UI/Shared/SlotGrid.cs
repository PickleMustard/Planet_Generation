using Godot;
using Structures.Logistics;
using UI.BuildingInfo;

namespace UI.Shared;

/// <summary>
/// Fills a <see cref="GridContainer"/> with one <see cref="ResourceSlotItem"/> per storage slot
/// (resource icon + stack size). Shared by the building detail views and the station storage panel so
/// the slot prefab and population logic live in exactly one place.
/// </summary>
public static class SlotGrid
{
    public static void Populate(GridContainer? grid, Storage? storage, PackedScene? slotScene)
    {
        if (grid == null)
            return;

        foreach (var child in grid.GetChildren())
            child.QueueFree();

        if (storage == null || slotScene == null)
            return;

        foreach (var slot in storage.Slots)
        {
            var item = slotScene.Instantiate<ResourceSlotItem>();
            if (item == null)
                continue;
            grid.AddChild(item);
            item.SetSlot(slot);
        }
    }
}
