#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Resources;

namespace DeveloperTools.Common;

/// <summary>
/// Large grid resource picker that replaces the single-column ItemList/OptionButton
/// selectors. Resources are rendered as icon+label cells, six per row, grouped by
/// tier (default) or category. Supports fuzzy name search and tier / category / tag
/// refine filters. Emits <see cref="ResourcePicked"/> with the chosen resource id.
/// </summary>
public partial class ResourcePickerPopup : PopupPanel
{
    [Signal]
    public delegate void ResourcePickedEventHandler(string resourceId);

    private const int Columns = 6;

    private enum GroupMode { Tier, Category }

    private LineEdit _search = null!;
    private OptionButton _groupByButton = null!;
    private OptionButton _tierFilter = null!;
    private OptionButton _categoryFilter = null!;
    private MenuButton _tagFilter = null!;
    private VBoxContainer _resultsBox = null!;

    private readonly List<ResourceDefinition> _all = new();
    private readonly List<int> _tierValues = new();        // distinct tiers, ascending
    private readonly List<string> _categoryValues = new(); // distinct categories, sorted
    private readonly List<string> _tagValues = new();      // distinct tags, sorted
    private readonly HashSet<string> _selectedTags = new();

    private GroupMode _groupMode = GroupMode.Tier;
    private int _tierFilterValue = int.MinValue; // MinValue = all tiers
    private string _categoryFilterValue = "";    // "" = all categories

    public override void _Ready()
    {
        base._Ready();
        LoadResources();
        BuildLayout();
        Rebuild();
    }

    private void LoadResources()
    {
        _all.Clear();
        try
        {
            var db = ResourceDatabase.Instance;
            if (db != null && db.IsLoaded)
                _all.AddRange(db.GetAllResources().Values);
        }
        catch { }

        _tierValues.Clear();
        _tierValues.AddRange(_all.Select(r => r.ResourceTier).Distinct().OrderBy(t => t));
        _categoryValues.Clear();
        _categoryValues.AddRange(_all.Select(r => r.ResourceType ?? "")
            .Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s, StringComparer.Ordinal));
        _tagValues.Clear();
        _tagValues.AddRange(_all.SelectMany(r => (IEnumerable<string>)r.Tags ?? Array.Empty<string>())
            .Distinct().OrderBy(s => s, StringComparer.Ordinal));
    }

    // ── Layout ───────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        Size = new Vector2I(900, 600);
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(880, 580) };
        AddChild(root);

        _search = new LineEdit
        {
            PlaceholderText = "Search resources…",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _search.TextChanged += _ => Rebuild();
        root.AddChild(_search);

        var filters = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddChild(filters);

        filters.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Group:" });
        _groupByButton = new OptionButton();
        _groupByButton.AddItem("Tier", 0);
        _groupByButton.AddItem("Category", 1);
        _groupByButton.Select(0);
        _groupByButton.ItemSelected += idx =>
        {
            _groupMode = idx == 1 ? GroupMode.Category : GroupMode.Tier;
            Rebuild();
        };
        filters.AddChild(_groupByButton);

        filters.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Tier:" });
        _tierFilter = new OptionButton();
        _tierFilter.AddItem("All", 0);
        for (int i = 0; i < _tierValues.Count; i++)
            _tierFilter.AddItem($"Tier {_tierValues[i]}", i + 1);
        _tierFilter.Select(0);
        _tierFilter.ItemSelected += idx =>
        {
            _tierFilterValue = idx == 0 ? int.MinValue : _tierValues[(int)idx - 1];
            Rebuild();
        };
        filters.AddChild(_tierFilter);

        filters.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Category:" });
        _categoryFilter = new OptionButton();
        _categoryFilter.AddItem("All", 0);
        for (int i = 0; i < _categoryValues.Count; i++)
            _categoryFilter.AddItem(_categoryValues[i], i + 1);
        _categoryFilter.Select(0);
        _categoryFilter.ItemSelected += idx =>
        {
            _categoryFilterValue = idx == 0 ? "" : _categoryValues[(int)idx - 1];
            Rebuild();
        };
        filters.AddChild(_categoryFilter);

        _tagFilter = new MenuButton { Text = "Tags" };
        var tagPopup = _tagFilter.GetPopup();
        tagPopup.HideOnCheckableItemSelection = false;
        for (int i = 0; i < _tagValues.Count; i++)
            tagPopup.AddCheckItem(_tagValues[i], i);
        tagPopup.IdPressed += OnTagToggled;
        filters.AddChild(_tagFilter);

        var clearTags = new Button { Text = "Clear Tags" };
        clearTags.Pressed += OnClearTags;
        filters.AddChild(clearTags);

        filters.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += () => { Hide(); QueueFree(); };
        filters.AddChild(cancel);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);
        _resultsBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_resultsBox);
    }

    private void OnTagToggled(long id)
    {
        var popup = _tagFilter.GetPopup();
        int idx = popup.GetItemIndex((int)id);
        bool nowChecked = !popup.IsItemChecked(idx);
        popup.SetItemChecked(idx, nowChecked);
        string tag = popup.GetItemText(idx);
        if (nowChecked) _selectedTags.Add(tag);
        else _selectedTags.Remove(tag);
        UpdateTagButtonText();
        Rebuild();
    }

    private void OnClearTags()
    {
        _selectedTags.Clear();
        var popup = _tagFilter.GetPopup();
        for (int i = 0; i < popup.ItemCount; i++) popup.SetItemChecked(i, false);
        UpdateTagButtonText();
        Rebuild();
    }

    private void UpdateTagButtonText() =>
        _tagFilter.Text = _selectedTags.Count == 0 ? "Tags" : $"Tags ({_selectedTags.Count})";

    // ── Results ──────────────────────────────────────────────────────────

    private void Rebuild()
    {
        foreach (var c in _resultsBox.GetChildren()) c.QueueFree();

        IEnumerable<ResourceDefinition> candidates = _all;
        if (_tierFilterValue != int.MinValue)
            candidates = candidates.Where(r => r.ResourceTier == _tierFilterValue);
        if (!string.IsNullOrEmpty(_categoryFilterValue))
            candidates = candidates.Where(r => (r.ResourceType ?? "") == _categoryFilterValue);
        if (_selectedTags.Count > 0)
            candidates = candidates.Where(r => r.Tags != null && _selectedTags.All(r.Tags.Contains));

        var list = candidates.ToList();
        string query = _search.Text?.Trim() ?? "";

        if (!string.IsNullOrEmpty(query))
        {
            var ranked = new List<(ResourceDefinition def, int score)>();
            foreach (var r in list)
                if (FuzzyMatch.TryMatch(query, r.IdName ?? "", out int score))
                    ranked.Add((r, score));
            ranked.Sort((a, b) => b.score != a.score
                ? b.score.CompareTo(a.score)
                : string.Compare(a.def.IdName, b.def.IdName, StringComparison.Ordinal));
            AddGroup("Results", ranked.Select(x => x.def).ToList());
        }
        else if (_groupMode == GroupMode.Tier)
        {
            foreach (var tier in list.Select(r => r.ResourceTier).Distinct().OrderBy(t => t))
                AddGroup($"Tier {tier}", list.Where(r => r.ResourceTier == tier)
                    .OrderBy(r => r.IdName, StringComparer.Ordinal).ToList());
        }
        else
        {
            foreach (var cat in list.Select(r => r.ResourceType ?? "(uncategorized)")
                         .Distinct().OrderBy(s => s, StringComparer.Ordinal))
                AddGroup(cat, list.Where(r => (r.ResourceType ?? "(uncategorized)") == cat)
                    .OrderBy(r => r.IdName, StringComparer.Ordinal).ToList());
        }

        if (_resultsBox.GetChildCount() == 0)
            _resultsBox.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "No resources match the current filters." });
    }

    private void AddGroup(string header, List<ResourceDefinition> items)
    {
        if (items.Count == 0) return;

        var headerLabel = new Label { Text = $"{header}  ({items.Count})" };
        headerLabel.AddThemeColorOverride("font_color", new Color(0.7f, 1f, 0.85f));
        headerLabel.AddThemeFontSizeOverride("font_size", 14);
        _resultsBox.AddChild(headerLabel);

        var grid = new GridContainer { Columns = Columns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _resultsBox.AddChild(grid);
        foreach (var r in items) grid.AddChild(MakeCell(r));

        _resultsBox.AddChild(new HSeparator());
    }

    private Control MakeCell(ResourceDefinition r)
    {
        string id = r.IdName ?? "";
        var btn = new Button
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(135, 44),
            TooltipText = BuildTooltip(r),
        };
        btn.Pressed += () =>
        {
            EmitSignal(SignalName.ResourcePicked, id);
            Hide();
            QueueFree();
        };

        var hb = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        hb.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hb.OffsetLeft = 6;
        hb.OffsetRight = -6;
        hb.AddThemeConstantOverride("separation", 6);
        btn.AddChild(hb);

        if (r.Icon?.Texture != null)
        {
            hb.AddChild(new TextureRect
            {
                Texture = r.Icon.Texture,
                CustomMinimumSize = new Vector2(32, 32),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                SelfModulate = r.GetEffectiveIconTint(),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }

        hb.AddChild(new Label
        { ThemeTypeVariation = "LabelHighContrast",
            Text = id,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            ClipText = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        return btn;
    }

    private static string BuildTooltip(ResourceDefinition r)
    {
        string tags = (r.Tags != null && r.Tags.Count > 0) ? string.Join(", ", r.Tags) : "—";
        return $"{r.IdName}\nTier: {r.ResourceTier}\nCategory: {r.ResourceType ?? "—"}\nTags: {tags}";
    }
}
#endif
