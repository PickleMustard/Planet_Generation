#if DEBUG
using System.Collections.Generic;
using System.Linq;
using Structures.Resources;
using UtilityLibrary;

namespace DeveloperTools.ProductionChainVisualizer;

/// <summary>
/// Pure-logic graph model for the Production Chain Visualizer. Reads
/// ResourceDatabase + RecipeDatabase and produces a bipartite graph of
/// resource / recipe / tag-group nodes plus directed production edges.
/// No Godot UI types — unit-testable in isolation.
/// </summary>
public class ProductionChainGraphModel
{
    public enum NodeKind { Resource, Recipe, Tag }

    public class Node
    {
        /// <summary>Unique node id, namespaced: "res:&lt;id&gt;", "rec:&lt;id&gt;", "tag:&lt;name&gt;".</summary>
        public string Id = string.Empty;
        public NodeKind Kind;
        public string DisplayName = string.Empty;
        /// <summary>Layer index for left-to-right layered layout.</summary>
        public int Layer;
        /// <summary>Raw resource (generatable, not produced by any recipe).</summary>
        public bool IsRaw;
        /// <summary>Referenced by a recipe but absent from ResourceDatabase (dangling ref).</summary>
        public bool IsMissing;

        // Backing definitions (null for tag/missing nodes).
        public ResourceDefinition? Resource;
        public RecipeDefinition? Recipe;
    }

    public class Edge
    {
        public string FromId = string.Empty;
        public string ToId = string.Empty;
        public float Amount;
        /// <summary>Edge connecting a tag-group node to one of its member resources.</summary>
        public bool IsTagFan;
        /// <summary>Edge produced by a recipe's conditional output.</summary>
        public bool IsConditional;
    }

    private readonly Dictionary<string, Node> _nodes = new();
    private readonly List<Edge> _edges = new();

    // Undirected adjacency for connected-set queries (built lazily after Build).
    private readonly Dictionary<string, HashSet<string>> _adjacency = new();

    public IReadOnlyDictionary<string, Node> Nodes => _nodes;
    public IReadOnlyList<Edge> Edges => _edges;

    public static string ResourceId(string idName) => "res:" + idName;
    public static string RecipeNodeId(string recipeId) => "rec:" + recipeId;
    public static string TagNodeId(string tagName) => "tag:" + tagName;

    /// <summary>Builds the full graph from the loaded databases.</summary>
    public void Build()
    {
        _nodes.Clear();
        _edges.Clear();
        _adjacency.Clear();

        var resourceDb = ResourceDatabase.Instance;
        var recipeDb = RecipeDatabase.Instance;
        if (resourceDb == null || recipeDb == null)
        {
            GameLogger.Warning("ProductionChainGraphModel.Build: databases unavailable");
            return;
        }

        // Resource nodes.
        foreach (var (id, def) in resourceDb.GetAllResources())
        {
            AddResourceNode(id, def);
        }

        // Recipe nodes + edges.
        foreach (var (recipeId, recipe) in recipeDb.GetAllRecipes())
        {
            string recNodeId = RecipeNodeId(recipeId);
            _nodes[recNodeId] = new Node
            {
                Id = recNodeId,
                Kind = NodeKind.Recipe,
                DisplayName = string.IsNullOrEmpty(recipe.DisplayName) ? recipeId : recipe.DisplayName!,
                Recipe = recipe
            };

            // Inputs: resource/tag -> recipe.
            foreach (var (key, amount) in recipe.InputResources)
            {
                string fromId = ResolvePortNode(key, resourceDb);
                AddEdge(fromId, recNodeId, amount, isTagFan: false, isConditional: false);
                FanTagMembers(key, fromId, towardTag: true, resourceDb);
            }

            // Outputs: recipe -> resource/tag.
            foreach (var (key, amount) in recipe.OutputResources)
            {
                string toId = ResolvePortNode(key, resourceDb);
                AddEdge(recNodeId, toId, amount, isTagFan: false, isConditional: false);
                FanTagMembers(key, toId, towardTag: false, resourceDb);
            }

            // Conditional outputs: recipe -> resource (plain id).
            foreach (var co in recipe.ConditionalOutputs)
            {
                if (string.IsNullOrEmpty(co.Resource)) continue;
                string toId = ResolvePortNode(co.Resource, resourceDb);
                AddEdge(recNodeId, toId, co.Amount, isTagFan: false, isConditional: true);
            }
        }

        ComputeLayers();
        BuildAdjacency();
    }

    private void AddResourceNode(string idName, ResourceDefinition? def)
    {
        string id = ResourceId(idName);
        if (_nodes.ContainsKey(id)) return;
        _nodes[id] = new Node
        {
            Id = id,
            Kind = NodeKind.Resource,
            DisplayName = idName,
            IsRaw = def?.IsGeneratable ?? false,
            IsMissing = def == null,
            Resource = def
        };
    }

    /// <summary>
    /// Resolves an input/output key to a port node id. Tag keys map to a (deduped)
    /// tag-group node; plain keys map to a resource node, creating a ghost node if
    /// the resource id is unknown.
    /// </summary>
    private string ResolvePortNode(string key, ResourceDatabase resourceDb)
    {
        if (RecipeDefinition.IsTagInput(key))
        {
            string tagName = RecipeDefinition.GetTagName(key);
            string tagId = TagNodeId(tagName);
            if (!_nodes.ContainsKey(tagId))
            {
                _nodes[tagId] = new Node
                {
                    Id = tagId,
                    Kind = NodeKind.Tag,
                    DisplayName = "#" + tagName
                };
            }
            return tagId;
        }

        string resId = ResourceId(key);
        if (!_nodes.ContainsKey(resId))
        {
            // Unknown resource referenced by a recipe — surface as a ghost node.
            resourceDb.TryGetResource(key, out var def);
            AddResourceNode(key, def);
        }
        return resId;
    }

    /// <summary>
    /// For a tag port, draws fan edges between the tag-group node and each member
    /// resource. towardTag=true (input tag): resource -> tag. false (output tag): tag -> resource.
    /// </summary>
    private void FanTagMembers(string key, string tagNodeId, bool towardTag, ResourceDatabase resourceDb)
    {
        if (!RecipeDefinition.IsTagInput(key)) return;
        string tagName = RecipeDefinition.GetTagName(key);
        foreach (var member in resourceDb.GetResourcesByTag(tagName))
        {
            if (member?.IdName == null) continue;
            string memberId = ResourceId(member.IdName);
            if (!_nodes.ContainsKey(memberId)) AddResourceNode(member.IdName, member);
            if (towardTag) AddEdge(memberId, tagNodeId, 0f, isTagFan: true, isConditional: false);
            else AddEdge(tagNodeId, memberId, 0f, isTagFan: true, isConditional: false);
        }
    }

    private void AddEdge(string fromId, string toId, float amount, bool isTagFan, bool isConditional)
    {
        _edges.Add(new Edge
        {
            FromId = fromId,
            ToId = toId,
            Amount = amount,
            IsTagFan = isTagFan,
            IsConditional = isConditional
        });
    }

    /// <summary>
    /// Longest-path layer relaxation. Nodes with no incoming edges sit at layer 0;
    /// every edge pushes its target at least one layer right. Iteration is bounded
    /// and layers clamped so cycles cannot grow unbounded.
    /// </summary>
    private void ComputeLayers()
    {
        const int MaxLayer = 64;
        foreach (var n in _nodes.Values) n.Layer = 0;

        int passes = System.Math.Min(_nodes.Count, 256);
        for (int i = 0; i < passes; i++)
        {
            bool changed = false;
            foreach (var e in _edges)
            {
                if (!_nodes.TryGetValue(e.FromId, out var from)) continue;
                if (!_nodes.TryGetValue(e.ToId, out var to)) continue;
                int want = System.Math.Min(from.Layer + 1, MaxLayer);
                if (want > to.Layer)
                {
                    to.Layer = want;
                    changed = true;
                }
            }
            if (!changed) break;
        }
    }

    private void BuildAdjacency()
    {
        foreach (var e in _edges)
        {
            if (!_adjacency.TryGetValue(e.FromId, out var a)) _adjacency[e.FromId] = a = new HashSet<string>();
            if (!_adjacency.TryGetValue(e.ToId, out var b)) _adjacency[e.ToId] = b = new HashSet<string>();
            a.Add(e.ToId);
            b.Add(e.FromId);
        }
    }

    /// <summary>
    /// Returns the set of node ids connected to <paramref name="nodeId"/> (inclusive).
    /// transitive=true walks the whole undirected component; false returns 1-hop neighbors.
    /// </summary>
    public HashSet<string> GetConnectedSet(string nodeId, bool transitive)
    {
        var result = new HashSet<string> { nodeId };
        if (!_adjacency.TryGetValue(nodeId, out var direct)) return result;

        if (!transitive)
        {
            foreach (var n in direct) result.Add(n);
            return result;
        }

        var queue = new Queue<string>();
        queue.Enqueue(nodeId);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!_adjacency.TryGetValue(cur, out var neighbors)) continue;
            foreach (var n in neighbors)
            {
                if (result.Add(n)) queue.Enqueue(n);
            }
        }
        return result;
    }

    /// <summary>
    /// Resource ids that no recipe produces. includeRawAsProduced=true excludes
    /// generatable (naturally-sourced) resources from the orphan list.
    /// </summary>
    public List<string> ComputeOrphans(bool includeRawAsProduced)
    {
        // A resource is "produced" if it has any incoming edge from a recipe node,
        // or from a tag-group node that is itself produced by a recipe.
        var producedResources = new HashSet<string>();
        var producedTags = new HashSet<string>();

        foreach (var e in _edges)
        {
            if (!_nodes.TryGetValue(e.FromId, out var from)) continue;
            if (from.Kind == NodeKind.Recipe && _nodes.TryGetValue(e.ToId, out var to))
            {
                if (to.Kind == NodeKind.Resource) producedResources.Add(to.Id);
                else if (to.Kind == NodeKind.Tag) producedTags.Add(to.Id);
            }
        }
        // Resources fanned out from a produced tag count as produced too.
        foreach (var e in _edges)
        {
            if (!e.IsTagFan) continue;
            if (producedTags.Contains(e.FromId) && _nodes.TryGetValue(e.ToId, out var to) && to.Kind == NodeKind.Resource)
                producedResources.Add(to.Id);
        }

        var orphans = new List<string>();
        foreach (var n in _nodes.Values)
        {
            if (n.Kind != NodeKind.Resource) continue;
            if (producedResources.Contains(n.Id)) continue;
            if (includeRawAsProduced && n.IsRaw) continue;
            orphans.Add(n.DisplayName);
        }
        orphans.Sort();
        return orphans;
    }
}
#endif
