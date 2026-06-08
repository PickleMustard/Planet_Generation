using System.Collections.Generic;
using Godot;

namespace UI.Construction;

/// <summary>
/// One row of a <see cref="PlacementCursorLabel"/>: an optional icon (tinted) and a text label.
/// </summary>
public readonly struct PlacementLabelRow
{
    public PlacementLabelRow(Texture2D? icon, Color tint, string text)
    {
        Icon = icon;
        Tint = tint;
        Text = text;
    }

    public Texture2D? Icon { get; }
    public Color Tint { get; }
    public string Text { get; }
}

/// <summary>
/// A behavior-agnostic, screen-space label panel that follows the cursor during building
/// placement. Always faces the user (it is a 2D Control) and shows an arbitrary list of
/// icon + text rows. Content is supplied by callers (see <see cref="PlacementLabelResolver"/>),
/// so any building behavior can drive it. Hidden automatically when given no rows.
/// </summary>
public partial class PlacementCursorLabel : PanelContainer
{
    private const float CursorOffsetX = 24f;
    private const float CursorOffsetY = 8f;
    private const float ScreenMargin = 8f;
    private const int IconSize = 24;

    private static readonly Theme WireframeTheme =
        ResourceLoader.Load<Theme>("res://UI/Theme/wireframe_paper/wireframe_paper.tres");

    private VBoxContainer _rows = null!;

    public override void _Ready()
    {
        if (WireframeTheme != null)
            Theme = WireframeTheme;

        // Float above everything else and never eat input — it only follows the cursor.
        MouseFilter = MouseFilterEnum.Ignore;
        TopLevel = true;
        ZIndex = 100;
        Visible = false;

        _rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_rows);
    }

    /// <summary>
    /// Replaces the panel contents with <paramref name="rows"/>. Hides the panel when the list
    /// is empty.
    /// </summary>
    public void SetRows(IReadOnlyList<PlacementLabelRow> rows)
    {
        foreach (var child in _rows.GetChildren())
            child.QueueFree();

        if (rows == null || rows.Count == 0)
        {
            Visible = false;
            return;
        }

        foreach (var row in rows)
            _rows.AddChild(BuildRow(row));

        Visible = true;
    }

    /// <summary>Hides the panel and clears its rows.</summary>
    public void HideLabel()
    {
        Visible = false;
        if (_rows != null)
            foreach (var child in _rows.GetChildren())
                child.QueueFree();
    }

    /// <summary>
    /// Positions the panel beside <paramref name="screenPos"/>, clamped to stay fully on-screen.
    /// </summary>
    public void UpdateAnchor(Vector2 screenPos)
    {
        if (!Visible)
            return;

        var viewport = GetViewportRect().Size;
        var panelSize = Size;
        float x = screenPos.X + CursorOffsetX;
        float y = screenPos.Y + CursorOffsetY;

        // Flip to the left of the cursor if it would spill off the right edge.
        if (x + panelSize.X + ScreenMargin > viewport.X)
            x = screenPos.X - CursorOffsetX - panelSize.X;

        x = Mathf.Clamp(x, ScreenMargin, Mathf.Max(ScreenMargin, viewport.X - panelSize.X - ScreenMargin));
        y = Mathf.Clamp(y, ScreenMargin, Mathf.Max(ScreenMargin, viewport.Y - panelSize.Y - ScreenMargin));
        Position = new Vector2(x, y);
    }

    private static Control BuildRow(PlacementLabelRow row)
    {
        var hbox = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };

        if (row.Icon != null)
        {
            var icon = new TextureRect
            {
                Texture = row.Icon,
                Modulate = row.Tint,
                CustomMinimumSize = new Vector2(IconSize, IconSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            hbox.AddChild(icon);
        }

        var label = new Label
        {
            Text = row.Text,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        hbox.AddChild(label);

        return hbox;
    }
}
