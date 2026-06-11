#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DeveloperTools.Common;

/// <summary>
/// Large grid picker that replaces single-column ItemList/OptionButton selectors. Renders a
/// neutral <see cref="PickerItem"/> list as icon+label cells (configurable columns), grouped
/// by tier or category. Supports fuzzy name search and tier / category / tag refine filters.
/// Group modes and filters auto-hide when the items or <see cref="PickerConfig"/> don't supply
/// them. Emits <see cref="Picked"/> with the chosen item id.
/// Configure via <see cref="Configure"/> before adding to the tree; build types via
/// <see cref="EntityPickers"/>.
/// </summary>
public partial class EntityPickerPopup : PopupPanel
{
    [Signal]
    public delegate void PickedEventHandler(string id);

    /// <summary>Forward-compat surface for bulk selection; not yet implemented.</summary>
    [Signal]
    public delegate void MultiPickedEventHandler(string[] ids);

    private enum GroupMode
    {
        Tier,
        Category,
    }

    private readonly List<PickerItem> _all = new();
    private PickerConfig _config = new();

    private readonly List<int> _tierValues = new(); // distinct tiers, ascending
    private readonly List<string> _categoryValues = new(); // distinct categories, sorted
    private readonly List<string> _tagValues = new(); // distinct tags, sorted
    private readonly HashSet<string> _selectedTags = new();

    [Export]
    public LineEdit? _search;

    [Export]
    public HBoxContainer? _filtersBar;

    [Export]
    public VBoxContainer? _resultsBox;
    private OptionButton? _groupByButton;
    private OptionButton? _tierFilter;
    private OptionButton? _categoryFilter;
    private MenuButton? _tagFilter;

    private GroupMode _groupMode = GroupMode.Tier;
    private int _tierFilterValue = int.MinValue; // MinValue = all tiers
    private string _categoryFilterValue = ""; // "" = all categories

    private static PackedScene? _scene;

    /// <summary>Instantiates the popup scene. Call <see cref="Configure"/> next, then add to the tree.</summary>
    public static EntityPickerPopup Create()
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/Common/EntityPickerPopup.tscn");
        return _scene.Instantiate<EntityPickerPopup>();
    }

    /// <summary>Inject the items + config. Call before adding the popup to the scene tree.</summary>
    public void Configure(IEnumerable<PickerItem> items, PickerConfig config)
    {
        _all.Clear();
        _all.AddRange(items);
        _config = config ?? new PickerConfig();
        RecomputeFacets();
    }

    public override void _Ready()
    {
        base._Ready();
        // Re-derive in case Configure ran before _Ready added nodes; harmless if already done.
        RecomputeFacets();
        Title = _config.Title;
        _search.PlaceholderText = _config.SearchPlaceholder;
        _search.TextChanged += _ => Rebuild();
        PopulateFilters();
        Rebuild();
    }

    private void RecomputeFacets()
    {
        _tierValues.Clear();
        _tierValues.AddRange(
            _all.Where(i => i.Tier.HasValue).Select(i => i.Tier!.Value).Distinct().OrderBy(t => t)
        );
        _categoryValues.Clear();
        _categoryValues.AddRange(
            _all.Select(i => i.Category ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s, StringComparer.Ordinal)
        );
        _tagValues.Clear();
        _tagValues.AddRange(
            _all.SelectMany(i => (IEnumerable<string>?)i.Tags ?? Array.Empty<string>())
                .Distinct()
                .OrderBy(s => s, StringComparer.Ordinal)
        );

        // Pick a sensible default group mode given availability.
        bool tierAvailable = _config.AllowGroupByTier && _tierValues.Count > 0;
        _groupMode = tierAvailable ? GroupMode.Tier : GroupMode.Category;
    }

    // ── Layout ───────────────────────────────────────────────────────────

    private void PopulateFilters()
    {
        var filters = _filtersBar;

        bool tierGroup = _config.AllowGroupByTier && _tierValues.Count > 0;
        bool categoryGroup = _config.AllowGroupByCategory;
        if (tierGroup && categoryGroup)
        {
            filters.AddChild(
                new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Group:" }
            );
            _groupByButton = new OptionButton();
            _groupByButton.AddItem("Tier", 0);
            _groupByButton.AddItem("Category", 1);
            _groupByButton.Select(_groupMode == GroupMode.Tier ? 0 : 1);
            _groupByButton.ItemSelected += idx =>
            {
                _groupMode = idx == 1 ? GroupMode.Category : GroupMode.Tier;
                Rebuild();
            };
            filters.AddChild(_groupByButton);
        }

        if (_config.ShowTierFilter && _tierValues.Count > 0)
        {
            filters.AddChild(
                new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Tier:" }
            );
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
        }

        if (_config.ShowCategoryFilter && _categoryValues.Count > 0)
        {
            filters.AddChild(
                new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Category:" }
            );
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
        }

        if (_config.ShowTagsFilter && _tagValues.Count > 0)
        {
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
        }

        filters.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += () =>
        {
            Hide();
            QueueFree();
        };
        filters.AddChild(cancel);
    }

    private void OnTagToggled(long id)
    {
        if (_tagFilter == null)
            return;
        var popup = _tagFilter.GetPopup();
        int idx = popup.GetItemIndex((int)id);
        bool nowChecked = !popup.IsItemChecked(idx);
        popup.SetItemChecked(idx, nowChecked);
        string tag = popup.GetItemText(idx);
        if (nowChecked)
            _selectedTags.Add(tag);
        else
            _selectedTags.Remove(tag);
        UpdateTagButtonText();
        Rebuild();
    }

    private void OnClearTags()
    {
        if (_tagFilter == null)
            return;
        _selectedTags.Clear();
        var popup = _tagFilter.GetPopup();
        for (int i = 0; i < popup.ItemCount; i++)
            popup.SetItemChecked(i, false);
        UpdateTagButtonText();
        Rebuild();
    }

    private void UpdateTagButtonText()
    {
        if (_tagFilter != null)
            _tagFilter.Text = _selectedTags.Count == 0 ? "Tags" : $"Tags ({_selectedTags.Count})";
    }

    // ── Results ──────────────────────────────────────────────────────────

    private void Rebuild()
    {
        foreach (var c in _resultsBox.GetChildren())
            c.QueueFree();

        IEnumerable<PickerItem> candidates = _all;
        if (_tierFilterValue != int.MinValue)
            candidates = candidates.Where(i => i.Tier == _tierFilterValue);
        if (!string.IsNullOrEmpty(_categoryFilterValue))
            candidates = candidates.Where(i => (i.Category ?? "") == _categoryFilterValue);
        if (_selectedTags.Count > 0)
            candidates = candidates.Where(i =>
                i.Tags != null && _selectedTags.All(i.Tags.Contains)
            );

        var list = candidates.ToList();
        string query = _search.Text?.Trim() ?? "";

        if (!string.IsNullOrEmpty(query))
        {
            var ranked = new List<(PickerItem item, int score)>();
            foreach (var i in list)
                if (FuzzyMatch.TryMatch(query, i.DisplayName ?? "", out int score))
                    ranked.Add((i, score));
            ranked.Sort(
                (a, b) =>
                    b.score != a.score
                        ? b.score.CompareTo(a.score)
                        : string.Compare(
                            a.item.DisplayName,
                            b.item.DisplayName,
                            StringComparison.Ordinal
                        )
            );
            AddGroup("Results", ranked.Select(x => x.item).ToList());
        }
        else if (_groupMode == GroupMode.Tier)
        {
            foreach (
                var tier in list.Where(i => i.Tier.HasValue)
                    .Select(i => i.Tier!.Value)
                    .Distinct()
                    .OrderBy(t => t)
            )
                AddGroup(
                    $"Tier {tier}",
                    list.Where(i => i.Tier == tier)
                        .OrderBy(i => i.DisplayName, StringComparer.Ordinal)
                        .ToList()
                );
            // Items without a tier still need to appear.
            var untiered = list.Where(i => !i.Tier.HasValue)
                .OrderBy(i => i.DisplayName, StringComparer.Ordinal)
                .ToList();
            if (untiered.Count > 0)
                AddGroup("(no tier)", untiered);
        }
        else
        {
            foreach (
                var cat in list.Select(i => i.Category ?? "(uncategorized)")
                    .Distinct()
                    .OrderBy(s => s, StringComparer.Ordinal)
            )
                AddGroup(
                    cat,
                    list.Where(i => (i.Category ?? "(uncategorized)") == cat)
                        .OrderBy(i => i.DisplayName, StringComparer.Ordinal)
                        .ToList()
                );
        }

        if (_resultsBox.GetChildCount() == 0)
            _resultsBox.AddChild(
                new Label
                {
                    ThemeTypeVariation = "LabelHighContrast",
                    Text = "No items match the current filters.",
                }
            );
    }

    private void AddGroup(string header, List<PickerItem> items)
    {
        if (items.Count == 0)
            return;

        var headerLabel = new Label { Text = $"{header}  ({items.Count})" };
        headerLabel.AddThemeColorOverride("font_color", new Color(0.7f, 1f, 0.85f));
        headerLabel.AddThemeFontSizeOverride("font_size", 14);
        _resultsBox.AddChild(headerLabel);

        var grid = new GridContainer
        {
            Columns = _config.Columns,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _resultsBox.AddChild(grid);
        foreach (var i in items)
            grid.AddChild(MakeCell(i));

        _resultsBox.AddChild(new HSeparator());
    }

    private Control MakeCell(PickerItem item)
    {
        string id = item.Id ?? "";
        var btn = new Button
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(135, 44),
            TooltipText = BuildTooltip(item),
        };
        btn.Pressed += () =>
        {
            EmitSignal(SignalName.Picked, id);
            Hide();
            QueueFree();
        };

        var hb = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        hb.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hb.OffsetLeft = 6;
        hb.OffsetRight = -6;
        hb.AddThemeConstantOverride("separation", 6);
        btn.AddChild(hb);

        if (item.IconTexture != null)
        {
            hb.AddChild(
                new TextureRect
                {
                    Texture = item.IconTexture,
                    CustomMinimumSize = new Vector2(32, 32),
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    SelfModulate = item.IconTint,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
        }

        hb.AddChild(
            new Label
            {
                ThemeTypeVariation = "LabelHighContrast",
                Text = item.DisplayName,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                ClipText = true,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );

        return btn;
    }

    private static string BuildTooltip(PickerItem item)
    {
        var lines = new List<string> { item.Id ?? "" };
        if (item.Tier.HasValue)
            lines.Add($"Tier: {item.Tier.Value}");
        lines.Add($"Category: {item.Category ?? "—"}");
        if (item.Tags != null && item.Tags.Count > 0)
            lines.Add($"Tags: {string.Join(", ", item.Tags)}");
        if (item.ExtraTooltipLines != null)
            lines.AddRange(item.ExtraTooltipLines);
        return string.Join("\n", lines);
    }
}
#endif
