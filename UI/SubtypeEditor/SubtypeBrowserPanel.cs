using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Resources;

namespace UI.SubtypeEditor;

/// <summary>
/// Left sidebar tree grouped by <see cref="BodyFamily"/>. Selecting a leaf raises
/// <see cref="SubtypeSelected"/>. Layout defined in SubtypeBrowserPanel.tscn;
/// popup dialogs (add/rename/delete) remain code-created (ephemeral).
/// </summary>
public partial class SubtypeBrowserPanel : VBoxContainer
{
    private SubtypeEditorModel _model = null!;
    private Dictionary<TreeItem, string> _itemToId = new();
    private string? _selectedId;

    [Export] private Button? _addButton;
    [Export] private Button? _renameButton;
    [Export] private Button? _deleteButton;
    [Export] private Tree? _tree;

    public event Action<string>? SubtypeSelected;

    public string? SelectedSubtypeId => _selectedId;

    public void Initialize(SubtypeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        Refresh();
        _model.RegistryChanged += OnRegistryChanged;
        _model.DefinitionChanged += OnDefinitionChanged;
    }

    public override void _Ready()
    {
        if (_addButton != null)
            _addButton.Pressed += OnAddPressed;
        if (_renameButton != null)
            _renameButton.Pressed += OnRenamePressed;
        if (_deleteButton != null)
            _deleteButton.Pressed += OnDeletePressed;
        if (_tree != null)
            _tree.ItemSelected += OnTreeItemSelected;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_model != null)
        {
            _model.RegistryChanged -= OnRegistryChanged;
            _model.DefinitionChanged -= OnDefinitionChanged;
        }
    }

    private void OnRegistryChanged() => CallDeferred(nameof(Refresh));
    private void OnDefinitionChanged(string id) => CallDeferred(nameof(Refresh));

    public void Refresh()
    {
        if (_tree == null) return;
        _tree.Clear();
        _itemToId.Clear();
        var root = _tree.CreateItem();
        root.SetText(0, "Subtypes");

        foreach (BodyFamily family in Enum.GetValues<BodyFamily>())
        {
            var familyDefs = _model.ByFamily(family)
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .ToList();
            if (familyDefs.Count == 0) continue;

            var familyItem = _tree.CreateItem(root);
            familyItem.SetText(0, family.ToString());
            familyItem.SetSelectable(0, false);
            familyItem.SetCustomColor(0, new Color(0.6f, 0.8f, 1f));

            foreach (var def in familyDefs)
            {
                var leaf = _tree.CreateItem(familyItem);
                string label = string.IsNullOrEmpty(def.DisplayName) ? def.Id : $"{def.DisplayName}  ({def.Id})";
                if (_model.IsDirty(def.Id)) label = "* " + label;
                leaf.SetText(0, label);
                _itemToId[leaf] = def.Id;
                if (_selectedId == def.Id)
                {
                    leaf.Select(0);
                }
            }
        }
    }

    private void OnTreeItemSelected()
    {
        if (_tree == null) return;
        var item = _tree.GetSelected();
        if (item == null) return;
        if (!_itemToId.TryGetValue(item, out var id)) return;
        if (_selectedId == id) return;
        _selectedId = id;
        SubtypeSelected?.Invoke(id);
    }

    private void OnAddPressed()
    {
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var idEdit = new LineEdit { PlaceholderText = "subtype_<family>_<name>", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var nameEdit = new LineEdit { PlaceholderText = "display name", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var familyOpt = new OptionButton();
        foreach (BodyFamily f in Enum.GetValues<BodyFamily>()) familyOpt.AddItem(f.ToString());
        body.AddChild(new Label { Text = "id:" });
        body.AddChild(idEdit);
        body.AddChild(new Label { Text = "display_name:" });
        body.AddChild(nameEdit);
        body.AddChild(new Label { Text = "family:" });
        body.AddChild(familyOpt);

        var dialog = new ConfirmationDialog { Title = "Add Subtype", DialogText = "New subtype:" };
        dialog.AddChild(body);
        dialog.Confirmed += () =>
        {
            string id = idEdit.Text.Trim();
            if (string.IsNullOrEmpty(id)) return;
            BodyFamily family = (BodyFamily)familyOpt.Selected;
            if (!_model.Add(id, nameEdit.Text.Trim(), family)) return;
            _selectedId = id;
            SubtypeSelected?.Invoke(id);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(440, 280));
    }

    private void OnRenamePressed()
    {
        if (string.IsNullOrEmpty(_selectedId)) return;
        string oldId = _selectedId;
        var lineEdit = new LineEdit
        {
            Text = oldId,
            PlaceholderText = "new subtype id",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var dialog = new ConfirmationDialog
        {
            Title = "Rename subtype",
            DialogText = $"Rename '{oldId}'.",
        };
        dialog.AddChild(lineEdit);
        dialog.Confirmed += () =>
        {
            string newId = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(newId) || newId == oldId) return;
            if (!_model.Rename(oldId, newId)) return;
            _selectedId = newId;
            SubtypeSelected?.Invoke(newId);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(440, 180));
    }

    private void OnDeletePressed()
    {
        if (string.IsNullOrEmpty(_selectedId)) return;
        string id = _selectedId;
        var refs = _model.FindReferences(id);
        string text = refs.Count == 0
            ? $"Delete subtype '{id}'?"
            : $"'{id}' is referenced by:\n  - " + string.Join("\n  - ", refs);
        var dialog = new ConfirmationDialog { Title = "Delete subtype", DialogText = text };
        dialog.Confirmed += () =>
        {
            _model.Remove(id);
            _selectedId = null;
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(480, 200));
    }
}