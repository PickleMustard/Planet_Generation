#if DEBUG
using System;
using System.Globalization;
using System.Linq;
using Godot;
using DeveloperTools.BiomeEditor.Popups;
using Structures.Enums;

namespace DeveloperTools.BiomeEditor.Cards;

/// <summary>
/// Per-biome resource weight modifier editor. List of (resourceId, weight) rows
/// plus an Add button that opens ResourceIdPickerPopup.
/// </summary>
public partial class BiomeResourceCard : PanelContainer
{
    private BiomeEditorModel _model = null!;
    private Biome.BiomeType _biome;
    private VBoxContainer _rowList = null!;

    public void Initialize(BiomeEditorModel model, Biome.BiomeType biome)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _biome = biome;
        BuildLayout();
        Refresh();
        _model.BiomeWeightsChanged += OnBiomeWeightsChanged;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _model.BiomeWeightsChanged -= OnBiomeWeightsChanged;
    }

    private void BuildLayout()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.14f, 0.14f, 0.18f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6 });
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(root);

        var header = new HBoxContainer();
        var title = new Label { Text = _biome.ToString(), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color(0.7f, 1f, 0.85f));
        header.AddChild(title);
        var addBtn = new Button { Text = "+ Resource" };
        addBtn.Pressed += OnAddResource;
        header.AddChild(addBtn);
        root.AddChild(header);

        _rowList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_rowList);
    }

    private void OnBiomeWeightsChanged(Biome.BiomeType biome)
    {
        if (biome != _biome) return;
        CallDeferred(nameof(Refresh));
    }

    private void Refresh()
    {
        foreach (var c in _rowList.GetChildren()) c.QueueFree();
        if (!_model.BiomeResources.TryGetValue(_biome, out var entry)) return;
        foreach (var kvp in entry.Weights.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            string id = kvp.Key;
            float weight = kvp.Value;
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = id, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var edit = new LineEdit
            {
                Text = weight.ToString("0.##", CultureInfo.InvariantCulture),
                CustomMinimumSize = new Vector2(72, 0),
            };
            edit.TextSubmitted += s => Commit(id, s);
            edit.FocusExited += () => Commit(id, edit.Text);
            row.AddChild(edit);
            var del = new Button { Text = "✕" };
            del.Pressed += () => _model.RemoveBiomeResourceWeight(_biome, id);
            row.AddChild(del);
            _rowList.AddChild(row);
        }
    }

    private void Commit(string id, string s)
    {
        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return;
        _model.SetBiomeResourceWeight(_biome, id, v);
    }

    private void OnAddResource()
    {
        var popup = new ResourceIdPickerPopup();
        popup.ResourcePicked += id => _model.SetBiomeResourceWeight(_biome, id, 1f);
        GetTree().Root.AddChild(popup);
        popup.PopupCentered();
    }
}
#endif
