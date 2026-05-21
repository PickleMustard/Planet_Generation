#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using DeveloperTools.BiomeEditor.Popups;
using Structures.Enums;

namespace DeveloperTools.BiomeEditor.Cards;

/// <summary>
/// Per-rocky-subtype editor: baseResourceWeight, ResourceGroups multi-select,
/// AddResources / RemoveResources lists.
/// </summary>
public partial class SubtypeResourceCard : PanelContainer
{
    private BiomeEditorModel _model = null!;
    private RockyPlanetSubtype _subtype;

    private LineEdit _baseWeightEdit = null!;
    private HFlowContainer _groupFlow = null!;
    private VBoxContainer _addList = null!;
    private VBoxContainer _removeList = null!;

    public void Initialize(BiomeEditorModel model, RockyPlanetSubtype subtype)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _subtype = subtype;
        BuildLayout();
        Refresh();
        _model.SubtypeWeightsChanged += OnChanged;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _model.SubtypeWeightsChanged -= OnChanged;
    }

    private void OnChanged(RockyPlanetSubtype subtype)
    {
        if (subtype != _subtype) return;
        CallDeferred(nameof(Refresh));
    }

    private void BuildLayout()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.14f, 0.14f, 0.18f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6 });
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(root);

        var title = new Label { Text = _subtype.ToString() };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.5f));
        root.AddChild(title);

        var baseRow = new HBoxContainer();
        baseRow.AddChild(new Label { Text = "base_resource_weight:", CustomMinimumSize = new Vector2(180, 0) });
        _baseWeightEdit = new LineEdit { CustomMinimumSize = new Vector2(80, 0) };
        _baseWeightEdit.TextSubmitted += s => CommitBaseWeight(s);
        _baseWeightEdit.FocusExited += () => CommitBaseWeight(_baseWeightEdit.Text);
        baseRow.AddChild(_baseWeightEdit);
        root.AddChild(baseRow);

        root.AddChild(new Label { Text = "Resource Groups:" });
        _groupFlow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_groupFlow);

        root.AddChild(new Label { Text = "Add Resources:" });
        _addList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_addList);
        var addAddBtn = new Button { Text = "+ Add Resource" };
        addAddBtn.Pressed += () => OnPickResource(true);
        root.AddChild(addAddBtn);

        root.AddChild(new Label { Text = "Remove Resources:" });
        _removeList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_removeList);
        var addRemBtn = new Button { Text = "+ Remove Resource" };
        addRemBtn.Pressed += () => OnPickResource(false);
        root.AddChild(addRemBtn);
    }

    private void Refresh()
    {
        if (!_model.SubtypeResources.TryGetValue(_subtype, out var entry)) return;

        _baseWeightEdit.Text = entry.BaseResourceWeight.ToString("0.##", CultureInfo.InvariantCulture);

        foreach (var c in _groupFlow.GetChildren()) c.QueueFree();
        var allGroups = _model.ResourceGroups.Select(g => g.GroupName).OrderBy(s => s, StringComparer.Ordinal);
        foreach (var name in allGroups)
        {
            string captured = name;
            var cb = new CheckBox
            {
                Text = name,
                ButtonPressed = entry.ResourceGroups.Contains(name),
            };
            cb.Toggled += pressed => ToggleGroup(captured, pressed);
            _groupFlow.AddChild(cb);
        }

        BuildIdRows(_addList, entry.AddResources, true);
        BuildIdRows(_removeList, entry.RemoveResources, false);
    }

    private void BuildIdRows(VBoxContainer list, List<string> ids, bool isAdd)
    {
        foreach (var c in list.GetChildren()) c.QueueFree();
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = id, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var del = new Button { Text = "✕" };
            del.Pressed += () => RemoveIdAt(isAdd, id);
            row.AddChild(del);
            list.AddChild(row);
        }
    }

    private void RemoveIdAt(bool isAdd, string id)
    {
        if (!_model.SubtypeResources.TryGetValue(_subtype, out var entry)) return;
        var clone = CloneEntry(entry);
        if (isAdd) clone.AddResources.Remove(id);
        else clone.RemoveResources.Remove(id);
        _model.UpdateSubtypeResource(_subtype, clone);
    }

    private void ToggleGroup(string groupName, bool pressed)
    {
        if (!_model.SubtypeResources.TryGetValue(_subtype, out var entry)) return;
        var clone = CloneEntry(entry);
        if (pressed && !clone.ResourceGroups.Contains(groupName)) clone.ResourceGroups.Add(groupName);
        else if (!pressed) clone.ResourceGroups.Remove(groupName);
        _model.UpdateSubtypeResource(_subtype, clone);
    }

    private void CommitBaseWeight(string s)
    {
        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return;
        if (!_model.SubtypeResources.TryGetValue(_subtype, out var entry)) return;
        var clone = CloneEntry(entry);
        clone.BaseResourceWeight = v;
        _model.UpdateSubtypeResource(_subtype, clone);
    }

    private void OnPickResource(bool isAdd)
    {
        var popup = new ResourceIdPickerPopup();
        popup.ResourcePicked += id =>
        {
            if (!_model.SubtypeResources.TryGetValue(_subtype, out var entry)) return;
            var clone = CloneEntry(entry);
            if (isAdd && !clone.AddResources.Contains(id)) clone.AddResources.Add(id);
            else if (!isAdd && !clone.RemoveResources.Contains(id)) clone.RemoveResources.Add(id);
            _model.UpdateSubtypeResource(_subtype, clone);
        };
        GetTree().Root.AddChild(popup);
        popup.PopupCentered();
    }

    private static BiomeEditorModel.SubtypeResourceEdit CloneEntry(BiomeEditorModel.SubtypeResourceEdit e) => new()
    {
        Subtype = e.Subtype,
        BaseResourceWeight = e.BaseResourceWeight,
        ResourceGroups = new List<string>(e.ResourceGroups),
        AddResources = new List<string>(e.AddResources),
        RemoveResources = new List<string>(e.RemoveResources),
    };
}
#endif
