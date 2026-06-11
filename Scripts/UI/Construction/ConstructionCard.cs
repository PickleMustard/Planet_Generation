using System.Collections.Generic;
using Godot;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary.DataLoading;

namespace UI.Construction;

/// <summary>
/// A single clickable constructable entry in the docked construction menu.
/// Renders icon, display name, description, and build-cost rows. Layout lives in
/// <c>ConstructionCard.tscn</c>; build via the <see cref="CreateForBuilding"/> /
/// <see cref="CreateForStation"/> factories. Emits <see cref="Clicked"/> on
/// left-click unless disabled (e.g. a building that has hit its global build limit).
/// </summary>
public partial class ConstructionCard : PanelContainer
{
    [Signal]
    public delegate void ClickedEventHandler(string itemType, string definitionName);

    [Export] private TextureRect _icon = null!;
    [Export] private Label _nameLabel = null!;
    [Export] private Label _descLabel = null!;
    [Export] private HFlowContainer _costFlow = null!;

    private string _itemType = "";
    private string _definitionName = "";
    private bool _disabled;

    private static PackedScene? _scene;
    private static readonly PackedScene CostEntryScene =
        GD.Load<PackedScene>("res://UI/Construction/ConstructionCostEntry.tscn");

    private static ConstructionCard Instantiate(string itemType, string definitionName)
    {
        _scene ??= GD.Load<PackedScene>("res://UI/Construction/ConstructionCard.tscn");
        var card = _scene.Instantiate<ConstructionCard>();
        card._itemType = itemType;
        card._definitionName = definitionName;
        return card;
    }

    public static ConstructionCard CreateForBuilding(BuildingDefinition def)
    {
        var card = Instantiate("Building", def.IdName ?? "");

        bool enabled = BuildingDatabase.Instance.ValidateGlobalPlacement(def.IdName ?? "");
        string desc = def.Description ?? "";
        if (def.BuildingLimit > 0)
        {
            int placed = BuildingDatabase.Instance.GetGlobalPlacementCount(def.IdName ?? "");
            desc += $"\nBuilt: {placed} / {def.BuildingLimit}";
            if (placed >= def.BuildingLimit)
                desc += "  (limit reached)";
        }

        card.Populate(
            def.DisplayName ?? def.IdName ?? "Unknown",
            desc,
            ResolveIcon(def.Icon, def.Visual),
            def.Icon.IsValid ? def.Icon.Tint : Colors.White,
            def.RequiredResources,
            disabled: !enabled);
        return card;
    }

    public static ConstructionCard CreateForStation(StationDefinition def)
    {
        var card = Instantiate("Station", def.Name);

        string desc = $"{def.StationType}  •  Build time: {def.ConstructionTime:0}s";
        card.Populate(
            def.Name,
            desc,
            ResolveIcon(def.Icon, def.Visual),
            def.Icon.IsValid ? def.Icon.Tint : Colors.White,
            def.RequiredResources,
            disabled: false);
        return card;
    }

    private static Texture2D ResolveIcon(IconDefinition icon, VisualDefinition? visual)
    {
        if (icon.IsValid && icon.Texture != null)
            return icon.Texture;
        var fromVisual = visual?.GetIcon();
        return fromVisual ?? IconDataLoader.GetFallbackIcon();
    }

    public override void _Ready()
    {
        GuiInput += OnGuiInput;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    private void Populate(
        string displayName,
        string description,
        Texture2D icon,
        Color iconTint,
        Dictionary<string, int> costs,
        bool disabled)
    {
        _disabled = disabled;
        TooltipText = displayName;

        _icon.Texture = icon;
        _icon.Modulate = iconTint;

        _nameLabel.Text = displayName;

        _descLabel.Text = description;
        _descLabel.Visible = !string.IsNullOrWhiteSpace(description);

        if (costs != null)
        {
            foreach (var kvp in costs)
            {
                var entry = CostEntryScene.Instantiate<HBoxContainer>();
                var costIcon = entry.GetNode<TextureRect>("Icon");
                costIcon.Texture = ResourceDatabase.Instance.GetResourceIcon(kvp.Key);
                costIcon.Modulate = ResourceDatabase.Instance.GetResourceIconTint(kvp.Key);
                costIcon.TooltipText = kvp.Key;
                entry.GetNode<Label>("Amount").Text = $"x{kvp.Value}";
                _costFlow.AddChild(entry);
            }
        }
        _costFlow.Visible = costs != null && costs.Count > 0;

        if (_disabled)
            Modulate = new Color(1f, 1f, 1f, 0.45f);
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (_disabled)
            return;

        if (@event is InputEventMouseButton mb
            && mb.Pressed
            && mb.ButtonIndex == MouseButton.Left)
        {
            EmitSignal(SignalName.Clicked, _itemType, _definitionName);
            AcceptEvent();
        }
    }

    private void OnMouseEntered()
    {
        if (!_disabled)
            Modulate = new Color(1.15f, 1.15f, 1.15f, 1f);
    }

    private void OnMouseExited()
    {
        if (!_disabled)
            Modulate = Colors.White;
    }
}
