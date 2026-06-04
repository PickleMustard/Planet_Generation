#if DEBUG
using System.Collections.Generic;
using Godot;
using Debug;
using DeveloperTools.Common;
using Structures.Resources;
using UtilityLibrary;

namespace DeveloperTools.ProductionChainVisualizer;

/// <summary>
/// Debug module visualizing the full resource/recipe production graph. A bipartite
/// GraphEdit shows resources, recipes, and tag-group nodes with directed production
/// edges. The left panel lists resources that no recipe produces (verification).
/// Clicking a resource (graph or list) dims everything outside its connected chain.
/// UI built programmatically; matching .tscn is a minimal root Control.
/// </summary>
public partial class ProductionChainVisualizerModule : BaseDebugModule
{
    public override string ModuleName => "Production Chains";

    private readonly ProductionChainGraphModel _model = new();
    private readonly ProductionChainGraphBuilder _builder = new();

    private GraphEdit _graph = null!;
    private ItemList _orphanList = null!;
    private LineEdit _searchBox = null!;
    private Label _orphanHeader = null!;
    private Label _statsLabel = null!;
    private CheckBox _transitiveCheck = null!;
    private CheckBox _includeRawCheck = null!;

    private List<string> _orphans = new();

    public override void _Ready()
    {
        base._Ready();
        BuildLayout();
        RebuildGraph();
    }

    public override void OnModuleEnabled()
    {
        base.OnModuleEnabled();
        RebuildGraph();
    }

    private static StyleBoxFlat MakeStyleBox(Color color) => new() { BgColor = color };

    private void BuildLayout()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var rootVBox = new VBoxContainer();
        rootVBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        rootVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rootVBox.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(rootVBox);

        // Toolbar.
        var toolbar = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rootVBox.AddChild(toolbar);

        var rebuildButton = new Button { Text = "Rebuild" };
        rebuildButton.Pressed += RebuildGraph;
        toolbar.AddChild(rebuildButton);

        _transitiveCheck = new CheckBox { Text = "Transitive chains", ButtonPressed = true };
        toolbar.AddChild(_transitiveCheck);

        _includeRawCheck = new CheckBox { Text = "Count raw as produced", ButtonPressed = true };
        _includeRawCheck.Toggled += _ => RefreshOrphans();
        toolbar.AddChild(_includeRawCheck);

        var clearButton = new Button { Text = "Clear filter" };
        clearButton.Pressed += () => _builder.ClearHighlight();
        toolbar.AddChild(clearButton);

        _statsLabel = new Label { ThemeTypeVariation = "LabelHighContrast", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _statsLabel.HorizontalAlignment = HorizontalAlignment.Right;
        toolbar.AddChild(_statsLabel);

        // Body split: orphan panel | graph.
        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rootVBox.AddChild(split);
        split.SplitOffsets = new[] { (int)GetViewport().GetVisibleRect().Size.X / 5 };

        var leftPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        leftPanel.AddThemeStyleboxOverride("panel", MakeStyleBox(new Color(0.12f, 0.12f, 0.14f)));
        split.AddChild(leftPanel);
        var leftVBox = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        leftPanel.AddChild(leftVBox);

        _orphanHeader = new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Unproduced Resources" };
        leftVBox.AddChild(_orphanHeader);

        _searchBox = new LineEdit { PlaceholderText = "Filter…" };
        _searchBox.TextChanged += _ => RenderOrphanList();
        leftVBox.AddChild(_searchBox);

        _orphanList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _orphanList.ItemActivated += OnOrphanActivated;
        leftVBox.AddChild(_orphanList);

        _graph = new GraphEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ShowGrid = true,
            RightDisconnects = false
        };
        _graph.NodeSelected += OnNodeSelected;
        split.AddChild(_graph);
    }

    private void RebuildGraph()
    {
        EnsureDatabasesLoaded();
        _model.Build();
        _builder.Populate(_graph, _model);
        RefreshOrphans();

        int nodes = _model.Nodes.Count;
        int edges = _model.Edges.Count;
        _statsLabel.Text = $"{nodes} nodes · {edges} edges";
        GameLogger.Info($"Production Chain Visualizer rebuilt: {nodes} nodes, {edges} edges");
    }

    private static void EnsureDatabasesLoaded()
    {
        if (ResourceDatabase.Instance is { IsLoaded: false } resDb) resDb.LoadData();
        if (RecipeDatabase.Instance is { IsLoaded: false } recDb) recDb.LoadData();
    }

    private void RefreshOrphans()
    {
        _orphans = _model.ComputeOrphans(_includeRawCheck.ButtonPressed);
        _orphanHeader.Text = $"Unproduced Resources ({_orphans.Count})";
        RenderOrphanList();
    }

    private void RenderOrphanList()
    {
        _orphanList.Clear();
        string query = _searchBox.Text ?? string.Empty;
        foreach (var name in _orphans)
        {
            if (query.Length > 0 && !FuzzyMatch.TryMatch(query, name, out _)) continue;
            _orphanList.AddItem(name);
        }
    }

    private void OnOrphanActivated(long index)
    {
        if (index < 0 || index >= _orphanList.ItemCount) return;
        string name = _orphanList.GetItemText((int)index);
        string nodeId = ProductionChainGraphModel.ResourceId(name);
        FocusNode(nodeId);
        ApplyFilter(nodeId);
    }

    private void OnNodeSelected(Node node)
    {
        if (node is not GraphNode gnode) return;
        if (_builder.TryGetId(gnode.Name, out var id)) ApplyFilter(id);
    }

    private void ApplyFilter(string nodeId)
    {
        var set = _model.GetConnectedSet(nodeId, _transitiveCheck.ButtonPressed);
        _builder.ApplyHighlight(set);
    }

    private void FocusNode(string nodeId)
    {
        if (!_builder.IdToName.TryGetValue(nodeId, out var name)) return;
        if (_graph.GetNodeOrNull<GraphNode>((string)name) is not { } gnode) return;
        // Center the view on the node's offset.
        _graph.ScrollOffset = gnode.PositionOffset - _graph.Size / 2f;
    }
}
#endif
