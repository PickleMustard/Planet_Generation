#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace UI.Debug.DatabaseViewer;

public partial class DatabaseViewer : BaseDebugModule
{
    private HSplitContainer? _mainSplit;
    private ItemList? _categoryList;
    private Tree? _dataTree;
    private LineEdit? _searchBox;
    private RichTextLabel? _detailPanel;
    private HBoxContainer? _actionButtons;
    private Button? _refreshButton;
    private Button? _copyPathButton;
    private Button? _watchButton;

    private string? _selectedCategory;
    private IDataProvider? _selectedProvider;
    private TreeItem? _selectedItem;
    private readonly HashSet<string> _watchedPaths = new();
    private double _refreshTimer;
    private const double RefreshInterval = 0.5;

    public override string ModuleName => "Database";

    public override void _Ready()
    {
        base._Ready();
        BuildUI();
        InitializeRegistry();
    }

    private void BuildUI()
    {
        Name = "DatabaseViewer";
        AnchorRight = 1f;
        AnchorBottom = 1f;
        OffsetRight = 0;
        OffsetBottom = 0;

        var mainContainer = new VBoxContainer
        {
            Name = "MainContainer",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        _mainSplit = new HSplitContainer
        {
            Name = "MainSplit",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SplitOffsets = new[] { 200 }
        };

        BuildCategoryPanel();
        BuildDataPanel();

        mainContainer.AddChild(_mainSplit);
        BuildDetailPanel(mainContainer);

        AddChild(mainContainer);
    }

    private void BuildCategoryPanel()
    {
        var categoryPanel = new PanelContainer
        {
            Name = "CategoryPanel",
            CustomMinimumSize = new Vector2(150, 0)
        };

        var categoryContainer = new VBoxContainer
        {
            Name = "CategoryContainer"
        };

        var categoryLabel = new Label
        {
            Name = "CategoryLabel",
            Text = "Categories",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        categoryLabel.AddThemeFontSizeOverride("font_size", 14);

        _categoryList = new ItemList
        {
            Name = "CategoryList",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AllowReselect = true
        };
        _categoryList.ItemSelected += OnCategorySelected;

        categoryContainer.AddChild(categoryLabel);
        categoryContainer.AddChild(_categoryList);
        categoryPanel.AddChild(categoryContainer);
        _mainSplit!.AddChild(categoryPanel);
    }

    private void BuildDataPanel()
    {
        var dataPanel = new PanelContainer
        {
            Name = "DataPanel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var dataContainer = new VBoxContainer
        {
            Name = "DataContainer"
        };

        _dataTree = new Tree
        {
            Name = "DataTree",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Columns = 3,
            AllowRmbSelect = true
        };
        _dataTree.SetColumnTitle(0, "Name");
        _dataTree.SetColumnTitle(1, "Value");
        _dataTree.SetColumnTitle(2, "Type");
        _dataTree.SetColumnExpand(0, true);
        _dataTree.SetColumnExpand(1, true);
        _dataTree.SetColumnExpand(2, false);
        _dataTree.SetColumnCustomMinimumWidth(2, 80);
        _dataTree.HideRoot = true;
        _dataTree.ItemSelected += OnTreeItemSelected;

        dataContainer.AddChild(_dataTree);
        dataPanel.AddChild(dataContainer);
        _mainSplit!.AddChild(dataPanel);
    }

    private void BuildDetailPanel(VBoxContainer mainContainer)
    {
        var detailContainer = new VBoxContainer
        {
            Name = "DetailContainer",
            CustomMinimumSize = new Vector2(0, 120)
        };

        var searchContainer = new HBoxContainer
        {
            Name = "SearchContainer"
        };

        var searchLabel = new Label
        {
            Text = "Filter:"
        };

        _searchBox = new LineEdit
        {
            Name = "SearchBox",
            PlaceholderText = "Search...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClearButtonEnabled = true
        };
        _searchBox.TextChanged += OnSearchChanged;

        searchContainer.AddChild(searchLabel);
        searchContainer.AddChild(_searchBox);
        detailContainer.AddChild(searchContainer);

        _detailPanel = new RichTextLabel
        {
            Name = "DetailPanel",
            BbcodeEnabled = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 60)
        };
        detailContainer.AddChild(_detailPanel);

        _actionButtons = new HBoxContainer
        {
            Name = "ActionButtons"
        };

        _refreshButton = new Button
        {
            Name = "RefreshButton",
            Text = "Refresh"
        };
        _refreshButton.Pressed += OnRefreshPressed;

        _copyPathButton = new Button
        {
            Name = "CopyPathButton",
            Text = "Copy Path"
        };
        _copyPathButton.Pressed += OnCopyPathPressed;

        _watchButton = new Button
        {
            Name = "WatchButton",
            Text = "Watch",
            ToggleMode = true
        };
        _watchButton.Toggled += OnWatchToggled;

        _actionButtons.AddChild(_refreshButton);
        _actionButtons.AddChild(_copyPathButton);
        _actionButtons.AddChild(_watchButton);
        detailContainer.AddChild(_actionButtons);

        mainContainer.AddChild(detailContainer);
    }

    private void InitializeRegistry()
    {
        DataProviderRegistry.Initialize();
        PopulateCategories();

        if (_categoryList!.ItemCount > 0)
        {
            _categoryList.Select(0);
            OnCategorySelected(0);
        }
    }

    private void PopulateCategories()
    {
        _categoryList!.Clear();

        var categories = DataProviderRegistry.Categories.OrderBy(c => c).ToList();
        foreach (var category in categories)
        {
            _categoryList.AddItem(category);
        }

        if (categories.Count == 0)
        {
            SetDetailText("[color=yellow]No data providers registered.[/color]\n\nData providers are discovered automatically from classes tagged with [DebugData] attribute.");
        }
    }

    private void OnCategorySelected(long index)
    {
        if (index < 0 || index >= _categoryList!.ItemCount)        {
            return;
        }

        _selectedCategory = _categoryList!.GetItemText((int)index);
        _selectedProvider = null;

        var providers = DataProviderRegistry.GetProvidersByCategory(_selectedCategory);
        if (providers.Count == 0)
        {
            _dataTree!.Clear();
            SetDetailText($"[color=yellow]No providers in category: {_selectedCategory}[/color]");
            return;
        }

        if (providers.Count == 1)
        {
            _selectedProvider = providers[0];
            RefreshDataTree();
        }
        else
        {
            PopulateAllProvidersData(providers);
        }
    }

    private void PopulateAllProvidersData(List<IDataProvider> providers)
    {
        _dataTree!.Clear();
        var root = _dataTree!.CreateItem();

        foreach (var provider in providers.OrderBy(p => p.Name))
        {
            if (provider.NeedsRefresh)
            {
                provider.Refresh();
            }

            var providerRoot = _dataTree!.CreateItem(root);
            providerRoot.SetText(0, provider.Name);
            providerRoot.SetMetadata(0, Variant.From($"__provider__:{provider.Name}"));
            providerRoot.SetText(1, $"[{provider.Category}]");
            providerRoot.Collapsed = true;

            var data = provider.GetData();
            if (data != null)
            {
                foreach (var property in data.Properties.Values)
                {
                    DataTreeBuilder.BuildTree(_dataTree!, property, providerRoot);
                }

                foreach (var child in data.Children)
                {
                    DataTreeBuilder.BuildTree(_dataTree!, child, providerRoot);
                }
            }
        }

        SetDetailText($"[color=cyan]{providers.Count}[/color] databases in [color=yellow]{_selectedCategory}[/color]\nExpand a database section to view its data.");
    }

    private void OnTreeItemSelected()
    {
        _selectedItem = _dataTree!.GetSelected();
        if (_selectedItem == null)
        {
            return;
        }

        var providerForItem = GetProviderForItem(_selectedItem);
        if (providerForItem != null && providerForItem != _selectedProvider)
        {
            _selectedProvider = providerForItem;
        }

        UpdateDetailPanel();
        UpdateWatchButton();
    }

    private IDataProvider? GetProviderForItem(TreeItem item)
    {
        if (item == null)
        {
            return null;
        }

        var current = item;
        while (current != null)
        {
            var metadata = current.GetMetadata(0);
            if (metadata.VariantType == Variant.Type.String)
            {
                var metadataStr = metadata.AsString();
                if (metadataStr.StartsWith("__provider__:"))
                {
                    var providerName = metadataStr.Substring("__provider__:".Length);
                    return DataProviderRegistry.GetProvider(providerName);
                }
            }
            current = current.GetParent();
        }

        return _selectedProvider;
    }

    private void OnContextMenuPressed(long id)
    {
        switch (id)
        {
            case 0:
                CopyValueToClipboard();
                break;
            case 1:
                CopyPathToClipboard();
                break;
            case 2:
                ToggleWatch();
                break;
        }
    }

    private void UpdateDetailPanel()
    {
        if (_selectedItem == null)
        {
            SetDetailText("");
            return;
        }

        var name = _selectedItem.GetText(0);
        var type = _selectedItem.GetText(2);

        var path = GetItemPath(_selectedItem);
        var metadata = _selectedItem.GetMetadata(0);

        var text = "";
        if (_selectedProvider != null)
        {
            text += $"[color=cyan]Database:[/color] {_selectedProvider.Name}\n";
        }
        text += $"[color=cyan]Path:[/color] {path}\n";
        text += $"[color=cyan]Type:[/color] {type}\n";

        if (metadata.VariantType == Variant.Type.Object)
        {
            var node = metadata.As<DebugDataNode>();
            if (node != null)
            {
                if (node.HasValue && node.Value != null)
                {
                    var formattedValue = DataTreeBuilder.FormatValue(node.Value);
                    text += $"[color=cyan]Value:[/color] {formattedValue}";
                }
                text += $"\n[color=cyan]Children:[/color] {node.Children.Count}";
                text += $" | [color=cyan]Properties:[/color] {node.Properties.Count}";
            }
        }

        SetDetailText(text);
    }

    private void SetDetailText(string bbcode)
    {
        _detailPanel!.Clear();
        _detailPanel!.ParseBbcode(bbcode);
    }

    private void UpdateWatchButton()
    {
        var path = GetItemPath(_selectedItem!);
        _watchButton!.ButtonPressed = _watchedPaths.Contains(path);
    }

    private string GetItemPath(TreeItem item)
    {
        if (item == null)
        {
            return "";
        }

        var parts = new List<string>();
        var current = item;

        while (current != null)
        {
            var text = current.GetText(0);
            if (!string.IsNullOrEmpty(text))
            {
                parts.Insert(0, text);
            }
            current = current.GetParent();
        }

        return string.Join(".", parts);
    }

    private void RefreshDataTree()
    {
        if (_selectedProvider == null)
        {
            return;
        }

        if (_selectedProvider.NeedsRefresh)
        {
            _selectedProvider.Refresh();
        }

        _dataTree!.Clear();

        var data = _selectedProvider.GetData();
        if (data == null)
        {
            SetDetailText("[color=red]Provider returned null data.[/color]");
            return;
        }

        DataTreeBuilder.BuildTree(_dataTree!, data);
        ApplySearchFilter();
    }

    private void OnSearchChanged(string newText)
    {
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        var searchText = _searchBox!.Text;
        if (string.IsNullOrEmpty(searchText))
        {
            SetAllItemsVisible(_dataTree!.GetRoot(), true);
            return;
        }

        FilterTreeItems(_dataTree!.GetRoot(), searchText);
    }

    private bool FilterTreeItems(TreeItem item, string searchText)
    {
        if (item == null)
        {
            return false;
        }

        var visible = false;
        var child = item.GetFirstChild();

        while (child != null)
        {
            if (FilterTreeItems(child, searchText))
            {
                visible = true;
            }
            child = child.GetNext();
        }

        var name = item.GetText(0);
        var value = item.GetText(1);

        if (name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            value.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            visible = true;
        }

        item.Visible = visible;
        return visible;
    }

    private void SetAllItemsVisible(TreeItem item, bool visible)
    {
        if (item == null)
        {
            return;
        }

        item.Visible = visible;

        var child = item.GetFirstChild();
        while (child != null)
        {
            SetAllItemsVisible(child, visible);
            child = child.GetNext();
        }
    }

    private void OnRefreshPressed()
    {
        if (!string.IsNullOrEmpty(_selectedCategory))
        {
            var providers = DataProviderRegistry.GetProvidersByCategory(_selectedCategory);
            if (providers.Count > 1)
            {
                foreach (var provider in providers)
                {
                    if (provider.NeedsRefresh)
                    {
                        provider.Refresh();
                    }
                }
                PopulateAllProvidersData(providers);
                return;
            }
        }

        if (_selectedProvider != null)
        {
            _selectedProvider.Refresh();
            RefreshDataTree();
        }
    }

    private void OnCopyPathPressed()
    {
        CopyPathToClipboard();
    }

    private void CopyPathToClipboard()
    {
        var path = GetItemPath(_selectedItem!);
        if (!string.IsNullOrEmpty(path))
        {
            DisplayServer.ClipboardSet(path);
            SetDetailText($"[color=green]Copied to clipboard:[/color] {path}");
        }
    }

    private void CopyValueToClipboard()
    {
        if (_selectedItem == null)
        {
            return;
        }

        var value = _selectedItem.GetText(1);
        if (!string.IsNullOrEmpty(value))
        {
            var cleanValue = StripBBCode(value);
            DisplayServer.ClipboardSet(cleanValue);
            SetDetailText("[color=green]Copied value to clipboard[/color]");
        }
    }

    private string StripBBCode(string text)
    {
        var result = System.Text.RegularExpressions.Regex.Replace(text, @"\[/?[^\]]*\]", "");
        return result;
    }

    private void OnWatchToggled(bool pressed)
    {
        ToggleWatch();
    }

    private void ToggleWatch()
    {
        if (_selectedItem == null)
        {
            return;
        }

        var path = GetItemPath(_selectedItem);

        if (_watchedPaths.Contains(path))
        {
            _watchedPaths.Remove(path);
            _watchButton!.ButtonPressed = false;
            SetDetailText($"[color=yellow]Stopped watching:[/color] {path}");
        }
        else
        {
            _watchedPaths.Add(path);
            _watchButton!.ButtonPressed = true;
            SetDetailText($"[color=green]Watching:[/color] {path}");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsVisible)
        {
            return;
        }

        _refreshTimer += delta;

        if (_refreshTimer >= RefreshInterval)
        {
            _refreshTimer = 0;
            RefreshWatchedItems();
        }
    }

    private void RefreshWatchedItems()
    {
        if (_watchedPaths.Count == 0 || _selectedProvider == null)
        {
            return;
        }

        if (_selectedProvider.NeedsRefresh)
        {
            _selectedProvider.Refresh();
            RefreshDataTree();
        }
    }

    public override void OnModuleEnabled()
    {
        base.OnModuleEnabled();

        if (_categoryList!.ItemCount == 0)
        {
            PopulateCategories();
        }

        if (_categoryList!.ItemCount > 0 && _categoryList.GetSelectedItems().Length == 0)
        {
            _categoryList.Select(0);
            OnCategorySelected(0);
        }
    }

    public override void OnModuleDisabled()
    {
        base.OnModuleDisabled();
    }

    public void SelectProvider(string providerName)
    {
        var provider = DataProviderRegistry.GetProvider(providerName);
        if (provider == null)
        {
            return;
        }

        var categoryIndex = 0;
        for (int i = 0; i < _categoryList!.ItemCount; i++)
        {
            if (_categoryList!.GetItemText(i) == provider.Category)
            {
                categoryIndex = i;
                break;
            }
        }

        _categoryList!.Select(categoryIndex);
        _selectedCategory = provider.Category;
        _selectedProvider = provider;
        RefreshDataTree();
    }

    public List<string> GetWatchedPaths()
    {
        return _watchedPaths.ToList();
    }

    public void ClearWatches()
    {
        _watchedPaths.Clear();
        _watchButton!.ButtonPressed = false;
    }
}
#endif
