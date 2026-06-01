using System.Collections.Generic;
using Godot;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary.DataLoading;

namespace UI.Construction;

/// <summary>
/// A single clickable constructable entry in the docked construction menu.
/// Renders icon, display name, description, and build-cost rows. Built entirely
/// in code via the <see cref="CreateForBuilding"/> / <see cref="CreateForStation"/>
/// factories so it needs no .tscn. Emits <see cref="Clicked"/> on left-click
/// unless disabled (e.g. a building that has hit its global build limit).
/// </summary>
public partial class ConstructionCard : PanelContainer
{
    [Signal]
    public delegate void ClickedEventHandler(string itemType, string definitionName);

    private string _itemType = "";
    private string _definitionName = "";
    private bool _disabled;

    public static ConstructionCard CreateForBuilding(BuildingDefinition def)
    {
        var card = new ConstructionCard
        {
            _itemType = "Building",
            _definitionName = def.IdName ?? "",
        };

        bool enabled = BuildingDatabase.Instance.ValidateGlobalPlacement(def.IdName ?? "");
        string desc = def.Description ?? "";
        if (def.BuildingLimit > 0)
        {
            int placed = BuildingDatabase.Instance.GetGlobalPlacementCount(def.IdName ?? "");
            desc += $"\nBuilt: {placed} / {def.BuildingLimit}";
            if (placed >= def.BuildingLimit)
                desc += "  (limit reached)";
        }

        card.Build(
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
        var card = new ConstructionCard
        {
            _itemType = "Station",
            _definitionName = def.Name,
        };

        string desc = $"{def.StationType}  •  Build time: {def.ConstructionTime:0}s";
        card.Build(
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

    private void Build(
        string displayName,
        string description,
        Texture2D icon,
        Color iconTint,
        Dictionary<string, int> costs,
        bool disabled)
    {
        _disabled = disabled;

        CustomMinimumSize = new Vector2(0, 84);
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        TooltipText = displayName;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        margin.AddChild(row);

        var iconRect = new TextureRect
        {
            Texture = icon,
            CustomMinimumSize = new Vector2(48, 48),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Modulate = iconTint,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        row.AddChild(iconRect);

        var info = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        info.AddThemeConstantOverride("separation", 2);
        row.AddChild(info);

        var nameLabel = new Label
        {
            Text = displayName,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 15);
        info.AddChild(nameLabel);

        if (!string.IsNullOrWhiteSpace(description))
        {
            var descLabel = new Label
            {
                Text = description,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            descLabel.AddThemeFontSizeOverride("font_size", 10);
            info.AddChild(descLabel);
        }

        if (costs != null && costs.Count > 0)
            info.AddChild(BuildCostRow(costs));

        GuiInput += OnGuiInput;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;

        if (_disabled)
            Modulate = new Color(1f, 1f, 1f, 0.45f);
    }

    private static Control BuildCostRow(Dictionary<string, int> costs)
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 10);
        flow.AddThemeConstantOverride("v_separation", 2);

        foreach (var kvp in costs)
        {
            var entry = new HBoxContainer();
            entry.AddThemeConstantOverride("separation", 2);

            var costIcon = new TextureRect
            {
                Texture = ResourceDatabase.Instance.GetResourceIcon(kvp.Key),
                Modulate = ResourceDatabase.Instance.GetResourceIconTint(kvp.Key),
                CustomMinimumSize = new Vector2(18, 18),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                TooltipText = kvp.Key,
            };
            entry.AddChild(costIcon);

            var amount = new Label { Text = $"x{kvp.Value}" };
            amount.AddThemeFontSizeOverride("font_size", 11);
            entry.AddChild(amount);

            flow.AddChild(entry);
        }

        return flow;
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
