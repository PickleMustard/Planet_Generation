#if DEBUG
using System.Collections.Generic;
using Godot;
using UtilityLibrary;

namespace DeveloperTools.ProductionChainVisualizer;

/// <summary>
/// Translates a <see cref="ProductionChainGraphModel"/> into Godot GraphNodes /
/// connections inside a GraphEdit. Each node uses a single left (input) + right
/// (output) port; multiple edges may share a port. Highlighting is done by
/// dimming node modulate and rebuilding the visible connection subset.
/// </summary>
public class ProductionChainGraphBuilder
{
    private const float ColumnWidth = 320f;
    private const float RowHeight = 110f;

    private static readonly Color ResourcePort = new(0.40f, 0.65f, 0.95f);
    private static readonly Color RecipePort = new(0.95f, 0.65f, 0.30f);
    private static readonly Color TagPort = new(0.70f, 0.45f, 0.90f);
    private static readonly Color DimModulate = new(1f, 1f, 1f, 0.18f);
    private static readonly Color FullModulate = new(1f, 1f, 1f, 1f);

    private record struct Conn(StringName From, int FromPort, StringName To, int ToPort);

    private readonly Dictionary<string, StringName> _idToName = new();
    private readonly Dictionary<StringName, string> _nameToId = new();
    private readonly List<Conn> _allConnections = new();

    private GraphEdit? _graph;
    private ProductionChainGraphModel? _model;

    /// <summary>Maps a node id to its GraphNode name (for click lookups).</summary>
    public IReadOnlyDictionary<string, StringName> IdToName => _idToName;
    public bool TryGetId(StringName nodeName, out string id) => _nameToId.TryGetValue(nodeName, out id!);

    public void Populate(GraphEdit graph, ProductionChainGraphModel model)
    {
        _graph = graph;
        _model = model;
        _idToName.Clear();
        _nameToId.Clear();
        _allConnections.Clear();

        graph.ClearConnections();
        foreach (var child in graph.GetChildren())
        {
            if (child is GraphNode) child.QueueFree();
        }

        // Track how many nodes are stacked in each layer for vertical placement.
        var rowsPerLayer = new Dictionary<int, int>();

        foreach (var node in model.Nodes.Values)
        {
            var gnode = CreateNode(node);
            int row = rowsPerLayer.TryGetValue(node.Layer, out var r) ? r : 0;
            rowsPerLayer[node.Layer] = row + 1;
            gnode.PositionOffset = new Vector2(node.Layer * ColumnWidth, row * RowHeight);
            graph.AddChild(gnode);

            _idToName[node.Id] = gnode.Name;
            _nameToId[gnode.Name] = node.Id;
        }

        foreach (var edge in model.Edges)
        {
            if (!_idToName.TryGetValue(edge.FromId, out var fromName)) continue;
            if (!_idToName.TryGetValue(edge.ToId, out var toName)) continue;
            graph.ConnectNode(fromName, 0, toName, 0);
            _allConnections.Add(new Conn(fromName, 0, toName, 0));
        }

        GameLogger.Debug($"ProductionChainGraphBuilder: {_idToName.Count} nodes, {_allConnections.Count} connections");
    }

    private GraphNode CreateNode(ProductionChainGraphModel.Node node)
    {
        var gnode = new GraphNode
        {
            Name = UniqueName(node.Id),
            Title = node.DisplayName,
            Draggable = true
        };

        Color portColor = node.Kind switch
        {
            ProductionChainGraphModel.NodeKind.Recipe => RecipePort,
            ProductionChainGraphModel.NodeKind.Tag => TagPort,
            _ => ResourcePort
        };

        // Body row (also the slot row). Resource nodes show their icon when available.
        var row = new HBoxContainer();
        if (node.Kind == ProductionChainGraphModel.NodeKind.Resource &&
            node.Resource?.Icon is { IsValid: true } icon && icon.Texture != null)
        {
            row.AddChild(new TextureRect
            {
                Texture = icon.Texture,
                CustomMinimumSize = new Vector2(24, 24),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            });
        }
        var kindLabel = new Label { ThemeTypeVariation = "LabelHighContrast", Text = KindTag(node) };
        kindLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
        row.AddChild(kindLabel);
        gnode.AddChild(row);

        // Single row: left (input) + right (output) ports enabled.
        gnode.SetSlot(0, true, 0, portColor, true, 0, portColor);

        if (node.IsMissing)
        {
            gnode.Modulate = new Color(1f, 0.5f, 0.5f);
            gnode.Title = node.DisplayName + " (missing)";
        }
        return gnode;
    }

    private static string KindTag(ProductionChainGraphModel.Node node) => node.Kind switch
    {
        ProductionChainGraphModel.NodeKind.Recipe => "recipe",
        ProductionChainGraphModel.NodeKind.Tag => "tag group",
        _ => node.IsRaw ? "raw resource" : "resource"
    };

    private StringName UniqueName(string id)
    {
        var sb = new System.Text.StringBuilder(id.Length);
        foreach (char c in id)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }
        return new StringName(sb.ToString());
    }

    /// <summary>Dims nodes/edges not in <paramref name="connectedIds"/>.</summary>
    public void ApplyHighlight(HashSet<string> connectedIds)
    {
        if (_graph == null) return;
        foreach (var child in _graph.GetChildren())
        {
            if (child is GraphNode gnode && _nameToId.TryGetValue(gnode.Name, out var id))
                gnode.Modulate = connectedIds.Contains(id) ? FullModulate : DimModulate;
        }

        _graph.ClearConnections();
        foreach (var c in _allConnections)
        {
            bool fromIn = _nameToId.TryGetValue(c.From, out var fid) && connectedIds.Contains(fid);
            bool toIn = _nameToId.TryGetValue(c.To, out var tid) && connectedIds.Contains(tid);
            if (fromIn && toIn) _graph.ConnectNode(c.From, c.FromPort, c.To, c.ToPort);
        }
    }

    /// <summary>Restores full visibility of all nodes and connections.</summary>
    public void ClearHighlight()
    {
        if (_graph == null) return;
        foreach (var child in _graph.GetChildren())
        {
            if (child is not GraphNode gnode) continue;
            bool missing = _nameToId.TryGetValue(gnode.Name, out var id) &&
                           _model?.Nodes.TryGetValue(id, out var n) == true && n.IsMissing;
            gnode.Modulate = missing ? new Color(1f, 0.5f, 0.5f) : FullModulate;
        }

        _graph.ClearConnections();
        foreach (var c in _allConnections) _graph.ConnectNode(c.From, c.FromPort, c.To, c.ToPort);
    }
}
#endif
