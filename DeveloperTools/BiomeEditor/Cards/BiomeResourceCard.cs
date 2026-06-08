#if DEBUG
using System;
using System.Globalization;
using System.Linq;
using Godot;
using DeveloperTools.Common;
using Structures.Enums;

namespace DeveloperTools.BiomeEditor.Cards;

/// <summary>
/// Per-biome resource group availability editor. One row per known resource group with
/// an OptionButton choosing None / Abundant / Frequent / Normal / Scarce / Rare.
/// "None" removes the group from the biome (it will not generate there).
/// </summary>
public partial class BiomeResourceCard : PanelContainer
{
    private static readonly AvailabilityLevel[] Levels =
    {
        AvailabilityLevel.Abundant,
        AvailabilityLevel.Frequent,
        AvailabilityLevel.Normal,
        AvailabilityLevel.Scarce,
        AvailabilityLevel.Rare,
    };

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

        _model.BiomeResources.TryGetValue(_biome, out var entry);

        foreach (var group in _model.ResourceGroups.OrderBy(g => g.GroupName, StringComparer.Ordinal))
        {
            string groupName = group.GroupName;
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = groupName, SizeFlagsHorizontal = SizeFlags.ExpandFill });

            var opt = new OptionButton { CustomMinimumSize = new Vector2(110, 0) };
            opt.AddItem("None", 0); // id 0 = None / not generated
            for (int i = 0; i < Levels.Length; i++)
                opt.AddItem(Levels[i].ToString(), i + 1);

            int selected = 0;
            if (entry != null && entry.Groups.TryGetValue(groupName, out var level))
                selected = Array.IndexOf(Levels, level) + 1;
            opt.Select(selected);

            opt.ItemSelected += idx => OnLevelSelected(groupName, (int)idx);
            row.AddChild(opt);
            _rowList.AddChild(row);
        }
    }

    private void OnLevelSelected(string groupName, int itemId)
    {
        if (itemId <= 0)
            _model.RemoveBiomeGroup(_biome, groupName);
        else
            _model.SetBiomeGroupLevel(_biome, groupName, Levels[itemId - 1]);
    }
}
#endif
