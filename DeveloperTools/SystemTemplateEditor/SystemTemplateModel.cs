#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDict = Godot.Collections.Dictionary;

namespace DeveloperTools.SystemTemplateEditor;

/// <summary>
/// Which YAML section a node belongs to. Also drives hierarchy rules: only
/// <see cref="Dominant"/> nodes may be roots, and only non-dominant nodes may be reparented.
/// </summary>
public enum BodyCategory
{
    Dominant,
    Planetary,
    Belt,
    Satellite,
}

/// <summary>
/// One editable body / belt in the system template tree. Backed by the raw loader-shape
/// <see cref="GDict"/> so fields the editor never touches (mesh/noise/resource blocks) survive a
/// load → edit → save round-trip untouched; typed accessors read/write the subset the editor edits.
/// </summary>
public sealed class BodyNode
{
    /// <summary>Source-of-truth dict in the shape TemplateHelpers load/save expects.</summary>
    public GDict Raw { get; }

    public BodyCategory Category { get; set; }
    public BodyNode? Parent { get; set; }
    public List<BodyNode> Children { get; } = new();

    /// <summary>Transient editor flag: card expanded vs collapsed.</summary>
    public bool Expanded { get; set; }

    public BodyNode(GDict raw, BodyCategory category)
    {
        Raw = raw ?? new GDict();
        Category = category;
    }

    public bool IsDominant => Category == BodyCategory.Dominant;

    public string Type
    {
        get => Raw.TryGetValue("type", out var v) ? v.AsString() : "Star";
        set => Raw["type"] = value;
    }

    public string Name
    {
        get => Raw.TryGetValue("name", out var v) ? v.AsString() : Type;
        set => Raw["name"] = value;
    }

    // --- Orbital params (planetary read from orbital_parameters; satellite from template) ---

    private GDict OrbitalParams
    {
        get
        {
            string key = Category == BodyCategory.Satellite ? "template" : "orbital_parameters";
            if (!Raw.TryGetValue(key, out var v) || v.Obj is not GDict d)
            {
                d = new GDict();
                Raw[key] = d;
            }
            return d;
        }
    }

    public float Apogee
    {
        get => ReadFloat(OrbitalParams, "apogee", Category == BodyCategory.Belt ? RingApogee : 500f);
        set => OrbitalParams["apogee"] = value;
    }

    public float Perigee
    {
        get => ReadFloat(OrbitalParams, "perigee", Category == BodyCategory.Belt ? RingPerigee : 300f);
        set => OrbitalParams["perigee"] = value;
    }

    public float StartingAngle
    {
        get => ReadFloat(OrbitalParams, "starting_angle", 0f);
        set => OrbitalParams["starting_angle"] = value;
    }

    public float VerticalOffset
    {
        get => ReadFloat(OrbitalParams, "vertical_offset", 0f);
        set => OrbitalParams["vertical_offset"] = value;
    }

    // --- Dominant-only ---

    public float Mass
    {
        get => ReadFloat(Template, "mass", 1000f);
        set => Template["mass"] = value;
    }

    public float Size
    {
        get => ReadFloat(Template, "size", 150f);
        set => Template["size"] = value;
    }

    public Vector3 Position
    {
        get => Template.TryGetValue("position", out var v) ? v.AsVector3() : Vector3.Zero;
        set => Template["position"] = value;
    }

    private GDict Template
    {
        get
        {
            if (!Raw.TryGetValue("template", out var v) || v.Obj is not GDict d)
            {
                d = new GDict();
                Raw["template"] = d;
            }
            return d;
        }
    }

    // --- Belt-only ---

    public float RingApogee
    {
        get => ReadFloat(Raw, "ring_apogee", 1000f);
        set => Raw["ring_apogee"] = value;
    }

    public float RingPerigee
    {
        get => ReadFloat(Raw, "ring_perigee", 500f);
        set => Raw["ring_perigee"] = value;
    }

    public int LowerRange
    {
        get => (int)ReadFloat(Raw, "lower_range", 1f);
        set => Raw["lower_range"] = value;
    }

    public int UpperRange
    {
        get => (int)ReadFloat(Raw, "upper_range", 4f);
        set => Raw["upper_range"] = value;
    }

    // --- Subtype ---

    public string? ExplicitSubtype
    {
        get => Raw.TryGetValue("subtype", out var v) ? v.AsString() : null;
        set
        {
            if (string.IsNullOrEmpty(value))
                Raw.Remove("subtype");
            else
                Raw["subtype"] = value;
        }
    }

    /// <summary>id → weight map for <c>subtype_weights</c>. Returns a live copy; assign back via
    /// <see cref="SetSubtypeWeights"/> to persist.</summary>
    public Dictionary<string, float> GetSubtypeWeights() => ReadWeights("subtype_weights");

    public void SetSubtypeWeights(IReadOnlyDictionary<string, float> weights) =>
        WriteWeights("subtype_weights", weights);

    /// <summary>Per-member asteroid subtype weights (belts only).</summary>
    public Dictionary<string, float> GetMemberSubtypeWeights() => ReadWeights("member_subtype_weights");

    public void SetMemberSubtypeWeights(IReadOnlyDictionary<string, float> weights) =>
        WriteWeights("member_subtype_weights", weights);

    private Dictionary<string, float> ReadWeights(string key)
    {
        var result = new Dictionary<string, float>(StringComparer.Ordinal);
        if (Raw.TryGetValue(key, out var v) && v.Obj is GDict d)
        {
            foreach (var k in d.Keys)
            {
                string id = k.AsString();
                if (!string.IsNullOrEmpty(id))
                    result[id] = (float)d[k];
            }
        }
        return result;
    }

    private void WriteWeights(string key, IReadOnlyDictionary<string, float> weights)
    {
        if (weights == null || weights.Count == 0)
        {
            Raw.Remove(key);
            return;
        }
        var d = new GDict();
        foreach (var kvp in weights)
            d[kvp.Key] = kvp.Value;
        Raw[key] = d;
    }

    private static float ReadFloat(GDict d, string key, float fallback) =>
        d.TryGetValue(key, out var v) ? (float)v : fallback;
}

/// <summary>
/// In-memory editable tree for the System Template Editor. Both the card column and the 2D board
/// bind to this single model; mutations raise <see cref="Changed"/> / <see cref="SelectionChanged"/>.
/// </summary>
public sealed class SystemTemplateModel
{
    public string? SourceFileName { get; set; }
    public bool IsDirty { get; set; }

    /// <summary>Dominant bodies — one scroll column per root.</summary>
    public List<BodyNode> Roots { get; } = new();

    public BodyNode? Selected { get; private set; }

    /// <summary>Raised after any structural or field mutation.</summary>
    public event Action? Changed;

    /// <summary>Raised when the selected node changes (null = nothing selected).</summary>
    public event Action<BodyNode?>? SelectionChanged;

    public void MarkChanged()
    {
        IsDirty = true;
        Changed?.Invoke();
    }

    public void Select(BodyNode? node)
    {
        if (ReferenceEquals(Selected, node))
            return;
        Selected = node;
        SelectionChanged?.Invoke(node);
    }

    /// <summary>Depth-first enumeration of every node under the roots (roots included).</summary>
    public IEnumerable<BodyNode> AllNodes()
    {
        foreach (var root in Roots)
            foreach (var n in Descend(root))
                yield return n;
    }

    private static IEnumerable<BodyNode> Descend(BodyNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var n in Descend(child))
                yield return n;
    }

    public bool IsDescendantOf(BodyNode node, BodyNode possibleAncestor)
    {
        for (var p = node.Parent; p != null; p = p.Parent)
            if (ReferenceEquals(p, possibleAncestor))
                return true;
        return false;
    }

    /// <summary>
    /// True when <paramref name="dragged"/> may legally become a child of <paramref name="target"/>.
    /// Enforces: dominants stay roots (never nested); no self/descendant drops; a non-dominant
    /// never lands at root level (it always has a non-null parent here, so dropping onto any node
    /// keeps it non-root).
    /// </summary>
    public bool CanReparent(BodyNode dragged, BodyNode target)
    {
        if (ReferenceEquals(dragged, target))
            return false;
        if (dragged.IsDominant)
            return false; // forbid dominant nesting
        if (IsDescendantOf(target, dragged))
            return false; // no cycles
        if (ReferenceEquals(dragged.Parent, target))
            return false; // already there
        return true;
    }

    public void Reparent(BodyNode dragged, BodyNode target)
    {
        if (!CanReparent(dragged, target))
            return;
        dragged.Parent?.Children.Remove(dragged);
        dragged.Parent = target;
        target.Children.Add(dragged);
        MarkChanged();
    }

    /// <summary>
    /// Promote a node to be a sibling of its current parent. Rejected when the new parent would be
    /// null (root) — a non-dominant can never become a sibling of a dominant root.
    /// </summary>
    public bool CanUpLevel(BodyNode node)
    {
        if (node.IsDominant)
            return false;
        var grand = node.Parent?.Parent;
        return grand != null; // null grandparent ⇒ promoting to root, forbidden
    }

    public void UpLevel(BodyNode node)
    {
        if (!CanUpLevel(node))
            return;
        var grand = node.Parent!.Parent!;
        node.Parent!.Children.Remove(node);
        node.Parent = grand;
        grand.Children.Add(node);
        MarkChanged();
    }

    /// <summary>Move a node up/down among its siblings (or among roots for a dominant).</summary>
    public void Reorder(BodyNode node, int delta)
    {
        var siblings = node.Parent?.Children ?? Roots;
        int i = siblings.IndexOf(node);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= siblings.Count)
            return;
        (siblings[i], siblings[j]) = (siblings[j], siblings[i]);
        MarkChanged();
    }

    public void Remove(BodyNode node)
    {
        var siblings = node.Parent?.Children ?? Roots;
        siblings.Remove(node);
        if (ReferenceEquals(Selected, node))
            Select(null);
        MarkChanged();
    }

    public void AddRoot(BodyNode node)
    {
        node.Parent = null;
        node.Category = BodyCategory.Dominant;
        Roots.Add(node);
        MarkChanged();
    }

    public void AddChild(BodyNode parent, BodyNode child)
    {
        child.Parent = parent;
        parent.Children.Add(child);
        MarkChanged();
    }

    /// <summary>Index of the root dominant this node ultimately hangs under (for orbital_center_index).</summary>
    public int RootIndexOf(BodyNode node)
    {
        var p = node;
        while (p?.Parent != null)
            p = p.Parent;
        return p == null ? -1 : Roots.IndexOf(p);
    }
}
#endif
