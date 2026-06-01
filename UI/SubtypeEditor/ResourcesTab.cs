using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Resources;

namespace UI.SubtypeEditor;

/// <summary>
/// Resource configuration: resource_groups, add_resources, remove_resources as CSV LineEdits.
/// Layout defined in ResourcesTab.tscn.
/// </summary>
public partial class ResourcesTab : ScrollContainer
{
    private SubtypeEditorModel _model = null!;
    private string? _subtypeId;

    [Export] private LineEdit? _groupsEdit;
    [Export] private LineEdit? _addEdit;
    [Export] private LineEdit? _removeEdit;
    private bool _suppress;

    public void Initialize(SubtypeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    public override void _Ready()
    {
        if (_groupsEdit != null)
        {
            _groupsEdit.TextSubmitted += _ => CommitLists();
            _groupsEdit.FocusExited += () => CommitLists();
        }
        if (_addEdit != null)
        {
            _addEdit.TextSubmitted += _ => CommitLists();
            _addEdit.FocusExited += () => CommitLists();
        }
        if (_removeEdit != null)
        {
            _removeEdit.TextSubmitted += _ => CommitLists();
            _removeEdit.FocusExited += () => CommitLists();
        }
    }

    public void SetSubtype(string? subtypeId)
    {
        _subtypeId = subtypeId;
        _suppress = true;
        try
        {
            if (string.IsNullOrEmpty(subtypeId))
            {
                if (_groupsEdit != null) _groupsEdit.Text = "";
                if (_addEdit != null) _addEdit.Text = "";
                if (_removeEdit != null) _removeEdit.Text = "";
                return;
            }
            var def = _model.GetById(subtypeId);
            if (def == null) return;
            if (_groupsEdit != null) _groupsEdit.Text = string.Join(", ", def.ResourceGroups);
            if (_addEdit != null) _addEdit.Text = string.Join(", ", def.AddResources);
            if (_removeEdit != null) _removeEdit.Text = string.Join(", ", def.RemoveResources);
        }
        finally { _suppress = false; }
    }

    private void CommitLists()
    {
        if (_suppress) return;
        if (string.IsNullOrEmpty(_subtypeId)) return;
        var def = _model.GetById(_subtypeId);
        if (def == null) return;
        def.ResourceGroups = ParseCsv(_groupsEdit?.Text ?? "");
        def.AddResources = ParseCsv(_addEdit?.Text ?? "");
        def.RemoveResources = ParseCsv(_removeEdit?.Text ?? "");
        _model.MarkDirty(_subtypeId);
    }

    private static List<string> ParseCsv(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries)
         .Select(t => t.Trim())
         .Where(t => t.Length > 0)
         .ToList();
}