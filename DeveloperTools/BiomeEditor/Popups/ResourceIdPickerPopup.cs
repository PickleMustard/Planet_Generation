#if DEBUG
using System;
using System.Linq;
using Godot;

namespace DeveloperTools.BiomeEditor.Popups;

/// <summary>
/// Single-select picker for resource IDs sourced from ResourceDatabase. Filters
/// live as the user types; double-click or Select button returns via ResourcePicked.
/// </summary>
public partial class ResourceIdPickerPopup : PopupPanel
{
    [Signal]
    public delegate void ResourcePickedEventHandler(string resourceId);

    private LineEdit _filter = null!;
    private ItemList _list = null!;
    private System.Collections.Generic.List<string> _allIds = new();

    public override void _Ready()
    {
        base._Ready();
        BuildLayout();
        Refresh("");
    }

    private void BuildLayout()
    {
        Size = new Vector2I(360, 480);
        var vbox = new VBoxContainer { CustomMinimumSize = new Vector2(340, 460) };
        AddChild(vbox);

        _filter = new LineEdit { PlaceholderText = "Filter…", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _filter.TextChanged += Refresh;
        vbox.AddChild(_filter);

        _list = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _list.ItemActivated += OnActivated;
        vbox.AddChild(_list);

        var hb = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        vbox.AddChild(hb);
        hb.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += () => { Hide(); QueueFree(); };
        hb.AddChild(cancel);
        var ok = new Button { Text = "Select" };
        ok.Pressed += OnOk;
        hb.AddChild(ok);

        _allIds = BiomeEditorModel.GetAllResourceIds();
    }

    private void Refresh(string filter)
    {
        _list.Clear();
        foreach (var id in _allIds)
        {
            if (string.IsNullOrEmpty(filter) || id.Contains(filter, StringComparison.OrdinalIgnoreCase))
                _list.AddItem(id);
        }
    }

    private void OnActivated(long index) => Pick((int)index);
    private void OnOk()
    {
        var sel = _list.GetSelectedItems();
        if (sel.Length > 0) Pick(sel[0]);
    }

    private void Pick(int index)
    {
        if (index < 0 || index >= _list.ItemCount) return;
        string id = _list.GetItemText(index);
        EmitSignal(SignalName.ResourcePicked, id);
        Hide();
        QueueFree();
    }
}
#endif
