using System.Collections.Generic;
using Godot;
using UtilityLibrary;

namespace UI.Components;

/// <summary>
/// Generic sidebar panel with a header (title + close button), a search box,
/// and a scrollable item list. Consumers add arbitrary <see cref="Control"/>
/// children via <see cref="AddItem"/> and the sidebar handles filtering,
/// layout, and close behaviour.
/// <para>
/// Items can carry a <c>filter_text</c> metadata string (set via
/// <see cref="AddItem"/> or manually with <c>SetMeta("filter_text", ...)</c>)
/// that the built-in search box filters against.
/// </para>
/// </summary>
public partial class FilterableSidebar : PanelContainer
{
    [Signal]
    public delegate void ClosedEventHandler();

    [Export]
    public Label? _titleLabel;

    [Export]
    public LineEdit? _searchEdit;

    [Export]
    public Button? _closeButton;

    [Export]
    public VBoxContainer? _itemList;

    private string _filterText = string.Empty;

    public override void _Ready()
    {
        if (_closeButton != null)
            _closeButton.Pressed += OnClosePressed;

        if (_searchEdit != null)
            _searchEdit.TextChanged += OnSearchChanged;
    }

    public override void _ExitTree()
    {
        if (_closeButton != null)
            _closeButton.Pressed -= OnClosePressed;

        if (_searchEdit != null)
            _searchEdit.TextChanged -= OnSearchChanged;
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Sets the header title text.</summary>
    public void SetTitle(string title)
    {
        if (_titleLabel != null)
            _titleLabel.Text = title;
    }

    /// <summary>Sets the search box placeholder text.</summary>
    public void SetSearchPlaceholder(string placeholder)
    {
        if (_searchEdit != null)
            _searchEdit.PlaceholderText = placeholder;
    }

    /// <summary>Removes all items from the list and resets the search filter.</summary>
    public void ClearItems()
    {
        if (_itemList == null)
        {
            GameLogger.Warning("FilterableSidebar: item list not configured");
            return;
        }

        foreach (var child in _itemList.GetChildren())
            child.QueueFree();

        _filterText = string.Empty;
        if (_searchEdit != null)
            _searchEdit.Text = string.Empty;
    }

    /// <summary>
    /// Adds a <see cref="Control"/> as an item row.
    /// <paramref name="filterText"/> is stored as metadata so the built-in
    /// search box can filter it. Pass <c>null</c> to exclude the item from
    /// text filtering (always visible).
    /// </summary>
    public void AddItem(Control item, string? filterText = null)
    {
        if (_itemList == null)
        {
            GameLogger.Warning("FilterableSidebar: Cannot add item — item list not configured");
            return;
        }

        if (filterText != null)
            item.SetMeta("filter_text", filterText);

        _itemList.AddChild(item);

        // Apply current filter to the new item
        if (!string.IsNullOrEmpty(_filterText) && filterText != null)
        {
            bool visible = filterText.ToLowerInvariant().Contains(_filterText);
            item.Visible = visible;
        }
    }

    /// <summary>Removes a specific item from the list.</summary>
    public bool RemoveItem(Control item)
    {
        if (_itemList == null || !item.IsInsideTree())
            return false;

        _itemList.RemoveChild(item);
        item.QueueFree();
        return true;
    }

    /// <summary>
    /// Returns a snapshot of current top-level item controls in the list.
    /// Excludes non-<see cref="Control"/> children.
    /// </summary>
    public IReadOnlyList<Control> GetItems()
    {
        var result = new List<Control>();
        if (_itemList == null)
            return result;

        foreach (var child in _itemList.GetChildren())
        {
            if (child is Control control)
                result.Add(control);
        }

        return result;
    }

    /// <summary>
    /// Programmatically applies a search filter. Items whose
    /// <c>filter_text</c> metadata does not contain <paramref name="search"/>
    /// (case-insensitive) are hidden.
    /// </summary>
    public void ApplyFilter(string search)
    {
        _filterText = search.ToLowerInvariant();
        if (_itemList == null)
            return;

        foreach (var child in _itemList.GetChildren())
        {
            if (child is Control item)
            {
                if (!item.HasMeta("filter_text"))
                {
                    // Items without filter_text are always visible
                    item.Visible = true;
                    continue;
                }

                string itemText = item.GetMeta("filter_text").AsString();
                bool visible =
                    string.IsNullOrEmpty(_filterText)
                    || itemText.ToLowerInvariant().Contains(_filterText);
                item.Visible = visible;
            }
        }
    }

    /// <summary>Shows an empty-state placeholder message in the list area.</summary>
    public void ShowEmptyMessage(string message)
    {
        if (_itemList == null)
            return;

        ClearItems();
        var label = new Label { Text = message };
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        _itemList.AddChild(label);
    }

    // ── Internals ───────────────────────────────────────────────────────

    private void OnSearchChanged(string newText)
    {
        ApplyFilter(newText);
    }

    private void OnClosePressed()
    {
        EmitSignal(SignalName.Closed);
        Visible = false;
    }
}

