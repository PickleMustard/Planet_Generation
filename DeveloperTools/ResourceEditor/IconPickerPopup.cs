#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using Registries;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using DeveloperTools.Common;

namespace DeveloperTools.ResourceEditor;

/// <summary>
/// Icon picker popup. Presents the <see cref="IconConfig"/> wrapper <em>objects</em> from
/// <see cref="IconManifest"/> as a searchable icon grid — the developer selects a Godot resource,
/// not a file path. A "Browse raw…" escape hatch still wraps a loose <c>.svg</c>/<c>.png</c> into
/// an <c>IconConfig</c> <c>.tres</c> (create-on-pick) and anchors it via
/// <see cref="AssetManifestBuilder.AddToManifest"/> so it survives export. Either way the chosen
/// wrapper's resource path is emitted through <see cref="IconSelected"/>, so callers and the YAML
/// save format are unchanged. The hosting scene (IconPickerPopup.tscn) is now a bare shell; the
/// content is built in code.
/// </summary>
public partial class IconPickerPopup : PopupPanel
{
    // --- Signal ---

    /// <summary>
    /// Emitted when the user confirms an icon selection.
    /// resourcePath points at the <c>IconConfig</c> <c>.tres</c> wrapper.
    /// </summary>
    [Signal]
    public delegate void IconSelectedEventHandler(string resourcePath);

    private const int Columns = 5;

    // --- State ---

    private readonly List<IconConfig> _icons = new();

    private LineEdit _search = null!;
    private VBoxContainer _resultsBox = null!;
    private FileDialog _fileDialog = null!;

    // ========================================================================
    // LIFECYCLE
    // ========================================================================

    public override void _Ready()
    {
        base._Ready();
        LoadManifest();
        BuildLayout();
        Rebuild();
    }

    /// <summary>Opens the popup. Layout is built in <see cref="_Ready"/>.</summary>
    public void OpenPicker()
    {
        base.Popup();
    }

    private void LoadManifest()
    {
        _icons.Clear();
        if (!Godot.FileAccess.FileExists(AssetManifests.IconManifestPath))
        {
            GameLogger.Warning($"IconPickerPopup: icon manifest missing at {AssetManifests.IconManifestPath}; use Rebuild Asset Manifests.");
            return;
        }

        var manifest = GD.Load<IconManifest>(AssetManifests.IconManifestPath);
        if (manifest == null) return;
        foreach (var icon in manifest.Icons)
            if (icon != null)
                _icons.Add(icon);
    }

    // ========================================================================
    // LAYOUT
    // ========================================================================

    private void BuildLayout()
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        root.AddChild(new Label
        {
            ThemeTypeVariation = "LabelHighContrast",
            Text = "Select Icon",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        _search = new LineEdit
        {
            PlaceholderText = "Search icons…",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _search.TextChanged += _ => Rebuild();
        root.AddChild(_search);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);
        _resultsBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_resultsBox);

        var actions = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddChild(actions);

        var browse = new Button { Text = "Browse raw…", TooltipText = "Wrap a loose .svg/.png into an IconConfig and anchor it" };
        browse.Pressed += OnBrowsePressed;
        actions.AddChild(browse);

        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += () => { Hide(); QueueFree(); };
        actions.AddChild(cancel);

        _fileDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Resources,
            CurrentDir = "res://Materials/Icons/",
            Filters = new[]
            {
                "*.tres ; Icon Wrapper (IconConfig)",
                "*.svg, *.png ; Image (creates wrapper)",
            },
        };
        _fileDialog.FileSelected += OnRawFileSelected;
        AddChild(_fileDialog);
    }

    // ========================================================================
    // GRID
    // ========================================================================

    private void Rebuild()
    {
        foreach (var c in _resultsBox.GetChildren()) c.QueueFree();

        string query = _search.Text?.Trim() ?? "";
        var matches = new List<IconConfig>();
        foreach (var icon in _icons)
        {
            string id = icon.Id.ToString();
            if (query.Length == 0 || FuzzyMatch.TryMatch(query, id, out _))
                matches.Add(icon);
        }
        matches.Sort((a, b) => string.Compare(a.Id.ToString(), b.Id.ToString(), StringComparison.Ordinal));

        if (matches.Count == 0)
        {
            _resultsBox.AddChild(new Label
            {
                ThemeTypeVariation = "LabelHighContrast",
                Text = _icons.Count == 0
                    ? "No icons in manifest. Run Settings → Rebuild Asset Manifests."
                    : "No icons match the search.",
            });
            return;
        }

        var grid = new GridContainer { Columns = Columns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _resultsBox.AddChild(grid);
        foreach (var icon in matches) grid.AddChild(MakeCell(icon));
    }

    private Control MakeCell(IconConfig icon)
    {
        string id = icon.Id.ToString();
        var btn = new Button
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(110, 96),
            TooltipText = $"{id}\n{icon.ResourcePath}",
        };
        btn.Pressed += () => Confirm(icon.ResourcePath);

        var vb = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        vb.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vb.AddChild(new TextureRect
        {
            Texture = icon.Texture ?? IconDataLoader.GetFallbackIcon(),
            CustomMinimumSize = new Vector2(64, 64),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SelfModulate = icon.Tint,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        vb.AddChild(new Label
        {
            ThemeTypeVariation = "LabelHighContrast",
            Text = id,
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        btn.AddChild(vb);
        return btn;
    }

    private void Confirm(string? resourcePath)
    {
        if (!string.IsNullOrEmpty(resourcePath))
            EmitSignal(SignalName.IconSelected, resourcePath);
        Hide();
        QueueFree();
    }

    // ========================================================================
    // BROWSE RAW (create-on-pick)
    // ========================================================================

    private void OnBrowsePressed()
    {
        _fileDialog.PopupCentered(new Vector2I(800, 600));
    }

    private void OnRawFileSelected(string path)
    {
        string? wrapperPath;
        if (path.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
        {
            wrapperPath = path;
        }
        else
        {
            var texture = GD.Load<Texture2D>(path);
            wrapperPath = CreateIconWrapper(path, texture);
        }

        // Anchor the (possibly new) wrapper so it survives export immediately.
        if (!string.IsNullOrEmpty(wrapperPath))
            AssetManifestBuilder.AddToManifest(GD.Load<IconConfig>(wrapperPath));

        Confirm(wrapperPath);
    }

    /// <summary>
    /// Creates an <c>IconConfig</c> <c>.tres</c> wrapper next to a raw image and
    /// returns its path. Returns an existing wrapper untouched.
    /// </summary>
    private static string CreateIconWrapper(string imagePath, Texture2D? texture)
    {
        string tresPath = StripExtension(imagePath) + ".tres";
        if (Godot.FileAccess.FileExists(tresPath))
            return tresPath; // already wrapped

        if (texture == null)
        {
            GameLogger.Warning($"Cannot wrap icon, texture failed to load: {imagePath}");
            return tresPath;
        }

        var config = new IconConfig
        {
            Id = StripExtension(FileName(imagePath)),
            Texture = texture,
        };
        var err = ResourceSaver.Save(config, tresPath);
        if (err != Error.Ok)
            GameLogger.Warning($"Failed to save icon wrapper '{tresPath}': {err}");
        else
            GameLogger.Info($"Created icon wrapper '{tresPath}'");
        return tresPath;
    }

    private static string StripExtension(string path)
    {
        int dot = path.LastIndexOf('.');
        return dot > path.LastIndexOf('/') ? path.Substring(0, dot) : path;
    }

    private static string FileName(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }
}
#endif
