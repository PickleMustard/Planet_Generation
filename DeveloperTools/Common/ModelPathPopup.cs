#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using Registries;
using UtilityLibrary;

namespace DeveloperTools.Common;

/// <summary>
/// Model picker popup. Presents the <see cref="ModelConfig"/> wrapper <em>objects</em> from
/// <see cref="ModelManifest"/> as a searchable list — the developer selects a Godot resource, not
/// a file path. A "Browse raw…" escape hatch still wraps a loose <c>.glb</c>/<c>.gltf</c>/<c>.tscn</c>
/// into a <c>ModelConfig</c> <c>.tres</c> (create-on-pick) and anchors it via
/// <see cref="AssetManifestBuilder.AddToManifest"/> so it survives export. Either way the chosen
/// wrapper's resource path is emitted through <see cref="ModelSelected"/>, so callers and the YAML
/// save format are unchanged.
/// </summary>
public partial class ModelPathPopup : PopupPanel
{
    [Signal]
    public delegate void ModelSelectedEventHandler(string resPath);

    private readonly List<ModelConfig> _models = new();

    [Export] private LineEdit _search = null!;
    [Export] private VBoxContainer _resultsBox = null!;
    [Export] private FileDialog _fileDialog = null!;

    private static PackedScene? _scene;

    /// <summary>Instantiates the popup scene. Add it to the tree, then PopupCentered.</summary>
    public static ModelPathPopup Create()
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/Common/ModelPathPopup.tscn");
        return _scene.Instantiate<ModelPathPopup>();
    }

    public override void _Ready()
    {
        base._Ready();
        LoadManifest();
        Rebuild();
    }

    private void OnSearchChanged(string _) => Rebuild();
    private void OnCancelPressed() { Hide(); QueueFree(); }

    private void LoadManifest()
    {
        _models.Clear();
        if (!Godot.FileAccess.FileExists(AssetManifests.ModelManifestPath))
        {
            GameLogger.Warning($"ModelPathPopup: model manifest missing at {AssetManifests.ModelManifestPath}; use Rebuild Asset Manifests.");
            return;
        }

        var manifest = GD.Load<ModelManifest>(AssetManifests.ModelManifestPath);
        if (manifest == null) return;
        foreach (var model in manifest.Models)
            if (model != null)
                _models.Add(model);
    }

    private void Rebuild()
    {
        foreach (var c in _resultsBox.GetChildren()) c.QueueFree();

        string query = _search.Text?.Trim() ?? "";
        var matches = new List<ModelConfig>();
        foreach (var model in _models)
        {
            string id = model.Id.ToString();
            if (query.Length == 0 || FuzzyMatch.TryMatch(query, id, out _))
                matches.Add(model);
        }
        matches.Sort((a, b) => string.Compare(a.Id.ToString(), b.Id.ToString(), StringComparison.Ordinal));

        if (matches.Count == 0)
        {
            _resultsBox.AddChild(new Label
            {
                ThemeTypeVariation = "LabelHighContrast",
                Text = _models.Count == 0
                    ? "No models in manifest. Run Settings → Rebuild Asset Manifests."
                    : "No models match the search.",
            });
            return;
        }

        foreach (var model in matches) _resultsBox.AddChild(MakeRow(model));
    }

    private Control MakeRow(ModelConfig model)
    {
        string id = model.Id.ToString();
        var btn = new Button
        {
            Text = id,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = model.ResourcePath,
        };
        btn.Pressed += () => Confirm(model.ResourcePath);
        return btn;
    }

    private void Confirm(string? resPath)
    {
        if (!string.IsNullOrEmpty(resPath))
            EmitSignal(SignalName.ModelSelected, resPath);
        Hide();
        QueueFree();
    }

    // ── Browse raw (create-on-pick) ──────────────────────────────────────

    private void OnBrowsePressed()
    {
        _fileDialog.PopupCentered(new Vector2I(800, 600));
    }

    private void OnRawFileSelected(string path)
    {
        string wrapperPath = path.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)
            ? path
            : CreateModelWrapper(path);

        if (!string.IsNullOrEmpty(wrapperPath))
            AssetManifestBuilder.AddToManifest(GD.Load<ModelConfig>(wrapperPath));

        Confirm(wrapperPath);
    }

    /// <summary>
    /// Creates a <c>ModelConfig</c> <c>.tres</c> wrapper next to a raw model and
    /// returns its path. Returns an existing wrapper untouched.
    /// </summary>
    private static string CreateModelWrapper(string modelPath)
    {
        string tresPath = modelPath + ".tres";
        if (Godot.FileAccess.FileExists(tresPath))
            return tresPath; // already wrapped

        var packed = GD.Load<PackedScene>(modelPath);
        if (packed == null)
        {
            GameLogger.Warning($"Cannot wrap model, scene failed to load: {modelPath}");
            return tresPath;
        }

        int slash = modelPath.LastIndexOf('/');
        int dot = modelPath.LastIndexOf('.');
        string stem = modelPath.Substring(slash + 1, (dot > slash ? dot : modelPath.Length) - slash - 1);

        var config = new ModelConfig { Id = stem, Model = packed };
        var err = ResourceSaver.Save(config, tresPath);
        if (err != Error.Ok)
            GameLogger.Warning($"Failed to save model wrapper '{tresPath}': {err}");
        else
            GameLogger.Info($"Created model wrapper '{tresPath}'");
        return tresPath;
    }
}
#endif
