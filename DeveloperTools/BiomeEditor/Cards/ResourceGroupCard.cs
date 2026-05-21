#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using DeveloperTools.BiomeEditor.Popups;

namespace DeveloperTools.BiomeEditor.Cards;

/// <summary>
/// Editor for a named resource group: list of resource IDs.
/// </summary>
public partial class ResourceGroupCard : PanelContainer
{
    private BiomeEditorModel _model = null!;
    private string _groupName = "";
    private VBoxContainer _idList = null!;

    public void Initialize(BiomeEditorModel model, string groupName)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _groupName = groupName;
        BuildLayout();
        Refresh();
    }

    private void BuildLayout()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.14f, 0.14f, 0.18f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6 });
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(root);

        var header = new HBoxContainer();
        var title = new Label { Text = _groupName, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color(0.85f, 0.7f, 1f));
        header.AddChild(title);
        var addBtn = new Button { Text = "+ Resource" };
        addBtn.Pressed += OnAddResource;
        header.AddChild(addBtn);
        var delBtn = new Button { Text = "✕ Delete Group" };
        delBtn.Pressed += () => _model.RemoveResourceGroup(_groupName);
        header.AddChild(delBtn);
        root.AddChild(header);

        _idList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_idList);
    }

    private void Refresh()
    {
        var group = _model.ResourceGroups.Find(g => g.GroupName == _groupName);
        if (group == null) return;
        foreach (var c in _idList.GetChildren()) c.QueueFree();
        foreach (var id in group.ResourceIds)
        {
            string captured = id;
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = id, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var del = new Button { Text = "✕" };
            del.Pressed += () =>
            {
                var g = _model.ResourceGroups.Find(x => x.GroupName == _groupName);
                if (g == null) return;
                var next = new List<string>(g.ResourceIds);
                next.Remove(captured);
                _model.UpdateResourceGroup(_groupName, next);
                CallDeferred(nameof(Refresh));
            };
            row.AddChild(del);
            _idList.AddChild(row);
        }
    }

    private void OnAddResource()
    {
        var popup = new ResourceIdPickerPopup();
        popup.ResourcePicked += id =>
        {
            var g = _model.ResourceGroups.Find(x => x.GroupName == _groupName);
            if (g == null) return;
            if (g.ResourceIds.Contains(id)) return;
            var next = new List<string>(g.ResourceIds) { id };
            _model.UpdateResourceGroup(_groupName, next);
            CallDeferred(nameof(Refresh));
        };
        GetTree().Root.AddChild(popup);
        popup.PopupCentered();
    }
}
#endif
