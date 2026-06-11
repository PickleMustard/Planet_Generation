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

        for (int i = 0; i < resources.Count; i++)
        {
            int index = i;
            rows.AddChild(EditorResourceRow.Create(
                resources[index],
                onChanged,
                () => { resources.RemoveAt(index); onChanged(); rebuild(); }));
        }
    }

    // ── Icon ─────────────────────────────────────────────────────────────

    /// <summary>Builds the "Icon" subsection (resource / scale / tint).</summary>
    public static void BuildIcon(Control parent, EditorIcon icon, Action onChanged)
    {
        parent.AddChild(Header("Icon", SectionIcon));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(grid);

        AddLineEdit(grid, "Resource", icon.ResourcePath, s => { icon.ResourcePath = Nullify(s); onChanged(); },
            "res:// path to the icon wrapper (IconConfig .tres)");

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Scale" });
        var scale = new SpinBox
        {
            MinValue = 0.01, MaxValue = 100, Step = 0.01, AllowGreater = true,
            Value = icon.Scale, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        scale.ValueChanged += val => { icon.Scale = (float)val; onChanged(); };
        grid.AddChild(scale);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Tint" });
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

    private static void AddLineEdit(GridContainer grid, string label, string? value,
        Action<string> onText, string tooltip = "")
    {
        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = label });
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
