using Godot;
using Structures.Logistics;
using Structures.Resources;

namespace UI.BuildingInfo;

public partial class ResourceSlotItem : PanelContainer
{
    private static readonly Color OccupiedModulate = Colors.White;
    private static readonly Color EmptyModulate = new(0.4f, 0.4f, 0.4f);

    private TextureRect? _iconRect;
    private Label? _amountLabel;

    public override void _Ready()
    {
        _iconRect = GetNodeOrNull<TextureRect>("VBoxContainer/ResourceIcon");
        _amountLabel = GetNodeOrNull<Label>("VBoxContainer/AmountLabel");
    }

    public void SetSlot(StorageSlot slot)
    {
        _iconRect ??= GetNodeOrNull<TextureRect>("VBoxContainer/ResourceIcon");
        _amountLabel ??= GetNodeOrNull<Label>("VBoxContainer/AmountLabel");

        if (slot == null || slot.IsEmpty)
        {
            ShowEmpty(slot?.Filter);
            return;
        }

        Modulate = OccupiedModulate;

        var resourceId = slot.OccupiedResourceId!;
        ResourceDefinition? definition = null;
        var resourceDb = ResourceDatabase.Instance;
        if (resourceDb?.IsLoaded == true)
        {
            resourceDb.TryGetResource(resourceId, out definition);
        }

        if (_iconRect != null)
        {
            _iconRect.Texture = LoadResourceIcon(definition);
        }

        if (_amountLabel != null)
        {
            _amountLabel.Text = $"{slot.Quantity:F0}/{slot.Capacity:F0}";
        }

        TooltipText = ResourceNameFormatter.Prettify(definition?.IdName ?? resourceId);
    }

    public void Clear()
    {
        ShowEmpty(null);
    }

    private void ShowEmpty(SlotFilter? filter)
    {
        Modulate = EmptyModulate;

        if (_iconRect != null)
        {
            _iconRect.Texture = null;
        }

        string hint = FormatFilterHint(filter);
        if (_amountLabel != null)
        {
            _amountLabel.Text = hint;
        }

        TooltipText = filter != null ? $"{hint} ({filter})" : hint;
    }

    private static Texture2D? LoadResourceIcon(ResourceDefinition? definition)
    {
        return definition?.Icon?.Texture;
    }

    private static string FormatFilterHint(SlotFilter? filter)
    {
        if (filter == null) return "any";
        return filter.Kind switch
        {
            SlotFilterKind.Any => "any",
            SlotFilterKind.Resource => ResourceNameFormatter.Prettify(filter.ResourceId ?? ""),
            SlotFilterKind.Category => ResourceNameFormatter.Prettify(filter.Category ?? ""),
            SlotFilterKind.MatterState => filter.State?.ToString() ?? "any",
            _ => "any",
        };
    }

}
