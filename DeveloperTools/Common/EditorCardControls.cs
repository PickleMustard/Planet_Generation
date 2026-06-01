#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Resources;

namespace DeveloperTools.Common;

/// <summary>
/// Builds the shared card subsections (required resources, visual, icon) used by
/// the ship and station editors. Controls mutate the supplied edit POCOs in place
/// and invoke <paramref name="onChanged"/> so the owning card can flag the model
/// dirty and (where needed) rebuild rows.
/// </summary>
public static class EditorCardControls
{
    private static readonly Color SectionResources = new(1.0f, 0.85f, 0.6f);
    private static readonly Color SectionVisual = new(0.7f, 1.0f, 0.7f);
    private static readonly Color SectionIcon = new(0.9f, 0.9f, 0.7f);

    private static Label Header(string text, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    // ── Required resources ───────────────────────────────────────────────

    /// <summary>
    /// Builds a "Required Resources" subsection with add/remove rows.
    /// <paramref name="rebuild"/> is called after add/remove to re-render the list.
    /// </summary>
    public static void BuildRequiredResources(Control parent,
        List<EditorResourceAmount> resources, Action onChanged, Action rebuild)
    {
        var headerRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var label = Header("Required Resources", SectionResources);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        headerRow.AddChild(label);
        var addButton = new Button { Text = "+ Resource" };
        addButton.Pressed += () =>
        {
            resources.Add(new EditorResourceAmount { ResourceId = "", Amount = 1 });
            onChanged();
            rebuild();
        };
        headerRow.AddChild(addButton);
        parent.AddChild(headerRow);

        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(rows);

        var allResourceIds = GetAllResourceIds();

        for (int i = 0; i < resources.Count; i++)
        {
            int index = i;
            var slot = resources[index];
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

            var idOption = new OptionButton
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(220, 0)
            };
            PopulateResourceDropdown(idOption, slot.ResourceId, allResourceIds);
            idOption.ItemSelected += selectedIdx =>
            {
                slot.ResourceId = idOption.GetItemText((int)selectedIdx);
                onChanged();
            };
            row.AddChild(idOption);

            var amount = new SpinBox
            {
                MinValue = 1,
                MaxValue = 1000000,
                Step = 1,
                AllowGreater = true,
                Value = slot.Amount
            };
            amount.ValueChanged += v => { slot.Amount = (int)v; onChanged(); };
            row.AddChild(amount);

            var delete = new Button { Text = "✕", TooltipText = "Remove resource" };
            delete.Pressed += () =>
            {
                resources.RemoveAt(index);
                onChanged();
                rebuild();
            };
            row.AddChild(delete);

            rows.AddChild(row);
        }
    }

    // ── Visual ───────────────────────────────────────────────────────────

    /// <summary>Builds the optional "Visual" subsection (model/shape fields).</summary>
    public static void BuildVisual(Control parent, EditorVisual v, Action onChanged)
    {
        parent.AddChild(Header("Visual (optional)", SectionVisual));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(grid);

        AddLineEdit(grid, "Model Path", v.ModelPath, s => { v.ModelPath = Nullify(s); onChanged(); },
            "res:// path to a .tscn/.glb model (optional)");
        AddLineEdit(grid, "Model Material", v.ModelMaterial, s => { v.ModelMaterial = Nullify(s); onChanged(); });
        AddLineEdit(grid, "Animation Path", v.AnimationPath, s => { v.AnimationPath = Nullify(s); onChanged(); });

        grid.AddChild(new Label { Text = "Scale" });
        var scale = new SpinBox
        {
            MinValue = 0.01, MaxValue = 1000, Step = 0.01, AllowGreater = true,
            Value = v.Scale, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        scale.ValueChanged += val => { v.Scale = (float)val; onChanged(); };
        grid.AddChild(scale);

        AddLineEdit(grid, "Shape Id", v.ShapeId, s =>
        {
            v.ShapeId = string.IsNullOrWhiteSpace(s) ? "hexagon" : s.Trim();
            onChanged();
        }, "Fallback 2D shape when no model (default hexagon)");

        grid.AddChild(new Label { Text = "Shape Size" });
        var shapeSize = new SpinBox
        {
            MinValue = 1, MaxValue = 4096, Step = 1, AllowGreater = true,
            Value = v.ShapeSize, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        shapeSize.ValueChanged += val => { v.ShapeSize = (float)val; onChanged(); };
        grid.AddChild(shapeSize);
    }

    // ── Icon ─────────────────────────────────────────────────────────────

    /// <summary>Builds the "Icon" subsection (base_path / scale / tint).</summary>
    public static void BuildIcon(Control parent, EditorIcon icon, Action onChanged)
    {
        parent.AddChild(Header("Icon", SectionIcon));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(grid);

        AddLineEdit(grid, "Base Path", icon.BasePath, s => { icon.BasePath = Nullify(s); onChanged(); },
            "res:// path to the icon (without extension)");

        grid.AddChild(new Label { Text = "Scale" });
        var scale = new SpinBox
        {
            MinValue = 0.01, MaxValue = 100, Step = 0.01, AllowGreater = true,
            Value = icon.Scale, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        scale.ValueChanged += val => { icon.Scale = (float)val; onChanged(); };
        grid.AddChild(scale);

        grid.AddChild(new Label { Text = "Tint" });
        var tint = new ColorPickerButton
        {
            Color = icon.Tint,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 24)
        };
        tint.ColorChanged += c => { icon.Tint = c; onChanged(); };
        grid.AddChild(tint);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all known resource IDs from the ResourceDatabase, sorted.
    /// Mirrors <see cref="BuildingEditor.BuildingEditorModel.GetAllResourceIds"/>
    /// and <see cref="RecipeEditor.RecipeEditorModel.GetAllResourceIds"/>.
    /// </summary>
    public static List<string> GetAllResourceIds()
    {
        try
        {
            var db = ResourceDatabase.Instance;
            if (db != null && db.IsLoaded)
                return db.GetAllResources().Keys.OrderBy(s => s).ToList();
        }
        catch { }
        return new List<string>();
    }

    /// <summary>
    /// Populates an <see cref="OptionButton"/> with resource IDs, selecting the
    /// current value. Unknown IDs are shown with an "(unknown)" suffix so they
    /// are not silently lost. Mirrors <see cref="BuildingEditor.RequiredResourceRow.RebuildResourceOptions"/>
    /// and <see cref="RecipeEditor.InputSlotRow.RebuildKeyOptions"/>.
    /// </summary>
    private static void PopulateResourceDropdown(OptionButton button, string currentId,
        List<string> options)
    {
        button.Clear();
        int selectedIndex = -1;
        for (int i = 0; i < options.Count; i++)
        {
            button.AddItem(options[i], i);
            if (options[i] == currentId) selectedIndex = i;
        }
        if (selectedIndex < 0)
        {
            if (!string.IsNullOrEmpty(currentId))
            {
                button.AddItem($"{currentId} (unknown)", options.Count);
                button.Select(options.Count);
            }
            else if (options.Count > 0)
            {
                button.Select(0);
            }
        }
        else
        {
            button.Select(selectedIndex);
        }
    }

    private static void AddLineEdit(GridContainer grid, string label, string? value,
        Action<string> onText, string tooltip = "")
    {
        grid.AddChild(new Label { Text = label });
        var edit = new LineEdit
        {
            Text = value ?? "",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = tooltip
        };
        edit.TextChanged += t => onText(t);
        grid.AddChild(edit);
    }

    private static string? Nullify(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
#endif
