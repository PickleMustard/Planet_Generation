#if DEBUG
using System.Collections.Generic;
using Godot;
using Structures;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using GArr = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDict = Godot.Collections.Dictionary;

namespace DeveloperTools.SystemTemplateEditor;

/// <summary>
/// Bridges the editor's <see cref="SystemTemplateModel"/> tree to the on-disk YAML format via the
/// existing <see cref="TemplateHelpers"/> load/save path. Re-nests the flat loader sections into a
/// tree on load, and flattens the tree back into the four sections on save — keeping the exact YAML
/// shape the rest of the engine reads.
/// </summary>
public static class SystemTemplateEditorYamlIO
{
    private const string TemplateDir = "res://Configuration/SystemTemplate/";

    /// <summary>Builds an editable tree from a parsed system template.</summary>
    public static SystemTemplateModel FromTemplate(SystemTemplateData data, string? sourceFileName = null)
    {
        var model = new SystemTemplateModel { SourceFileName = sourceFileName };
        var byName = new Dictionary<string, BodyNode>();

        // Dominants become roots.
        foreach (var dict in data.Dominant)
        {
            var node = new BodyNode(dict, BodyCategory.Dominant);
            model.Roots.Add(node);
            byName[node.Name] = node;
        }

        if (model.Roots.Count == 0)
            return model; // nothing else can attach without a root

        // Planetary bodies attach to a dominant by name (parent_body), else by orbital_center_index.
        foreach (var dict in data.Planetary)
        {
            var node = new BodyNode(dict, BodyCategory.Planetary);
            var parent = ResolvePlanetaryParent(node, model, byName);
            parent.Children.Add(node);
            node.Parent = parent;
            byName[node.Name] = node;
        }

        // Belts attach under a dominant by orbital_center_index.
        foreach (var dict in data.Belts)
        {
            var node = new BodyNode(dict, BodyCategory.Belt);
            int idx = ReadInt(dict, "orbital_center_index", 0);
            var parent = (idx >= 0 && idx < model.Roots.Count) ? model.Roots[idx] : model.Roots[0];
            parent.Children.Add(node);
            node.Parent = parent;
        }

        // Satellites attach by `parent` name. Multi-pass so moon-of-a-moon chains resolve.
        var pending = new List<BodyNode>();
        foreach (var dict in data.Satellites)
            pending.Add(new BodyNode(dict, BodyCategory.Satellite));

        bool progress = true;
        while (pending.Count > 0 && progress)
        {
            progress = false;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var node = pending[i];
                string parentName = node.Raw.TryGetValue("parent", out var v) ? v.AsString() : "";
                if (string.IsNullOrEmpty(parentName) || !byName.TryGetValue(parentName, out var parent))
                    continue;
                parent.Children.Add(node);
                node.Parent = parent;
                byName[node.Name] = node;
                pending.RemoveAt(i);
                progress = true;
            }
        }
        foreach (var orphan in pending)
        {
            GameLogger.Warning(
                $"SystemTemplateEditor: satellite '{orphan.Name}' references an unknown parent — attaching to '{model.Roots[0].Name}'"
            );
            model.Roots[0].Children.Add(orphan);
            orphan.Parent = model.Roots[0];
        }

        return model;
    }

    private static BodyNode ResolvePlanetaryParent(
        BodyNode node,
        SystemTemplateModel model,
        Dictionary<string, BodyNode> byName
    )
    {
        if (node.Raw.TryGetValue("orbital_parameters", out var opVar) && opVar.Obj is GDict op)
        {
            if (op.TryGetValue("parent_body", out var pbVar))
            {
                string pb = pbVar.AsString();
                if (!string.IsNullOrEmpty(pb) && pb != "barycenter" && byName.TryGetValue(pb, out var byParent))
                    return byParent;
            }
            int idx = ReadInt(op, "orbital_center_index", 0);
            if (idx >= 0 && idx < model.Roots.Count)
                return model.Roots[idx];
        }
        return model.Roots[0];
    }

    /// <summary>Flattens the tree back into the four loader-shape sections.</summary>
    public static (GArr dominant, GArr belts, GArr planetary, GArr satellites) ToSections(
        SystemTemplateModel model
    )
    {
        var dominant = new GArr();
        var belts = new GArr();
        var planetary = new GArr();
        var satellites = new GArr();

        foreach (var node in model.AllNodes())
        {
            switch (node.Category)
            {
                case BodyCategory.Dominant:
                    dominant.Add(node.Raw);
                    break;

                case BodyCategory.Planetary:
                    SyncPlanetaryParentLinks(node, model);
                    planetary.Add(node.Raw);
                    break;

                case BodyCategory.Belt:
                    node.Raw["orbital_center_index"] = ParentRootIndex(node, model);
                    belts.Add(node.Raw);
                    break;

                case BodyCategory.Satellite:
                    if (node.Parent != null)
                        node.Raw["parent"] = node.Parent.Name;
                    satellites.Add(node.Raw);
                    break;
            }
        }

        return (dominant, belts, planetary, satellites);
    }

    private static void SyncPlanetaryParentLinks(BodyNode node, SystemTemplateModel model)
    {
        if (node.Parent == null)
            return;
        if (!node.Raw.TryGetValue("orbital_parameters", out var opVar) || opVar.Obj is not GDict op)
        {
            op = new GDict();
            node.Raw["orbital_parameters"] = op;
        }
        op["parent_body"] = node.Parent.Name;
        op["orbital_center_index"] = ParentRootIndex(node, model);
    }

    private static int ParentRootIndex(BodyNode node, SystemTemplateModel model)
    {
        // Belts/planetary that hang directly off a dominant use that dominant's root index.
        if (node.Parent != null && node.Parent.IsDominant)
            return model.Roots.IndexOf(node.Parent);
        return model.RootIndexOf(node);
    }

    /// <summary>Serializes the model to YAML text using the canonical writer.</summary>
    public static string ToYaml(SystemTemplateModel model)
    {
        var (dominant, belts, planetary, satellites) = ToSections(model);
        return TemplateHelpers.GenerateYamlContent(dominant, belts, planetary, satellites);
    }

    /// <summary>Writes the model to <c>res://Configuration/SystemTemplate/&lt;fileName&gt;</c>.</summary>
    public static bool Save(SystemTemplateModel model, string fileName, out string error)
    {
        error = "";
        if (!fileName.EndsWith(".yaml"))
            fileName += ".yaml";
        string path = TemplateDir + fileName;
        string yaml = ToYaml(model);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            error = $"Could not open {path} for writing ({FileAccess.GetOpenError()})";
            GameLogger.Error($"SystemTemplateEditor: {error}");
            return false;
        }
        file.StoreString(yaml);
        model.SourceFileName = fileName;
        model.IsDirty = false;
        GameLogger.Info($"SystemTemplateEditor: saved {path}");
        return true;
    }

    private static int ReadInt(GDict d, string key, int fallback) =>
        d.TryGetValue(key, out var v) ? (int)(float)v : fallback;
}
#endif
