using System.Collections.Generic;
using Constructables.Buildings.Behaviors;
using Godot;
using Structures.Resources;

namespace UI.BuildingInfo;

/// <summary>
/// Modal popup that lists the resources eligible for an extraction slot (the behavior's
/// <see cref="ExtractionBehavior.AvailableDeposits"/>, ordered by abundance) plus a "none"
/// option, and emits <see cref="ResourceSlotSelected"/> / <see cref="ResourceSlotCleared"/>
/// for the slot the player is editing. Mirrors <see cref="RecipeSelectionPopup"/>.
/// </summary>
public partial class ResourceSelectionPopup : PanelContainer
{
    [Signal]
    public delegate void ResourceSlotSelectedEventHandler(int slotIndex, string resourceId);

    [Signal]
    public delegate void ResourceSlotClearedEventHandler(int slotIndex);

    [Signal]
    public delegate void PopupCancelledEventHandler();

    [Export]
    private ItemList _resourceList = null!;

    [Export]
    private Button _selectButton = null!;

    [Export]
    private Button _cancelButton = null!;

    [Export]
    private Label _emptyLabel = null!;

    // Index 0 is always the "— None (empty) —" sentinel (null entry).
    private readonly List<string?> _resourceIds = new();
    private int _slotIndex = -1;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(400, 350);
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -200;
        OffsetRight = 200;
        OffsetTop = -175;
        OffsetBottom = 175;

        _resourceList.ItemSelected += OnItemSelected;
        _selectButton.Pressed += OnSelectPressed;
        _cancelButton.Pressed += OnCancelPressed;
    }

    /// <summary>
    /// Fills the list from <paramref name="ext"/>'s eligible deposits (abundance-descending),
    /// marking the slot's current resource. Each row stores its index into <see cref="_resourceIds"/>.
    /// </summary>
    public void Populate(ExtractionBehavior ext, int slotIndex)
    {
        _resourceList.Clear();
        _resourceIds.Clear();
        _selectButton.Disabled = true;
        _slotIndex = slotIndex;

        string? current = (slotIndex >= 0 && slotIndex < ext.Slots.Count)
            ? ext.Slots[slotIndex].ResourceId
            : null;

        // Sentinel "none" option first.
        _resourceList.AddItem("— None (empty) —");
        _resourceIds.Add(null);

        // Eligible deposits, ordered by abundance descending (alpha tiebreak).
        var ordered = new List<KeyValuePair<string, float>>(ext.AvailableDeposits);
        ordered.Sort((a, b) =>
        {
            int cmp = b.Value.CompareTo(a.Value);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.Key, b.Key);
        });

        var db = ResourceDatabase.Instance;
        foreach (var kvp in ordered)
        {
            ResourceDefinition? def = null;
            db?.TryGetResource(kvp.Key, out def);

            string display = ResourceNameFormatter.Prettify(def?.IdName ?? kvp.Key);
            bool isCurrent = kvp.Key == current;
            string label = isCurrent ? $"[Current] {display}  ·  {kvp.Value:P0}" : $"{display}  ·  {kvp.Value:P0}";
            Texture2D? icon = def?.Icon?.Texture;

            int idx = icon != null
                ? _resourceList.AddItem(label, icon)
                : _resourceList.AddItem(label);
            if (isCurrent)
                _resourceList.SetItemDisabled(idx, true);
            _resourceIds.Add(kvp.Key);
        }

        // The list is never truly empty (the "none" sentinel is always present), but show the
        // hint when there are no eligible deposits to choose from.
        bool hasDeposits = ext.AvailableDeposits.Count > 0;
        _emptyLabel.Visible = !hasDeposits;
    }

    private void OnItemSelected(long index)
    {
        _selectButton.Disabled = false;
    }

    private void OnSelectPressed()
    {
        var selected = _resourceList.GetSelectedItems();
        if (selected.Length == 0) return;

        int idx = selected[0];
        if (idx < 0 || idx >= _resourceIds.Count) return;

        string? resourceId = _resourceIds[idx];
        if (resourceId == null)
            EmitSignal(SignalName.ResourceSlotCleared, _slotIndex);
        else
            EmitSignal(SignalName.ResourceSlotSelected, _slotIndex, resourceId);
    }

    private void OnCancelPressed()
    {
        EmitSignal(SignalName.PopupCancelled);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventKey keyEvent
            && keyEvent.Pressed
            && keyEvent.Keycode == Key.Escape)
        {
            OnCancelPressed();
            GetViewport().SetInputAsHandled();
        }
    }
}
