using System;
using Godot;

namespace UI.SubtypeEditor;

/// <summary>
/// Right-side detail panel: TabContainer with Identity, Mesh/Tectonics Ranges, Spherical Harmonics,
/// Biome Assignment, Resources. Driven by <see cref="SubtypeEditorModel"/>; updates whenever the
/// browser selects a subtype.
/// Layout defined in SubtypeDetailPanel.tscn; inner tab instances are loaded from their own scenes.
/// </summary>
public partial class SubtypeDetailPanel : VBoxContainer
{
    private SubtypeEditorModel _model = null!;
    private string? _subtypeId;

    [Export] private Label? _emptyHint;
    [Export] private TabContainer? _tabs;

    private IdentityTab _identityTab = null!;
    private MeshTectonicRangesTab _meshTectTab = null!;
    private SphericalHarmonicsTab _shTab = null!;
    private BiomeAssignmentTab _biomeTab = null!;
    private ResourcesTab _resourcesTab = null!;

    public void Initialize(SubtypeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        FindInnerTabs();
        InitInnerTabs();
        SetSubtype(null);
    }

    public override void _Ready()
    {
        FindInnerTabs();
    }

    private void FindInnerTabs()
    {
        if (_tabs == null) return;

        // Find tab instances by name (set in .tscn)
        for (int i = 0; i < _tabs.GetChildCount(); i++)
        {
            var child = _tabs.GetChild(i);
            switch (child)
            {
                case IdentityTab identity:
                    _identityTab = identity;
                    break;
                case MeshTectonicRangesTab meshTect:
                    _meshTectTab = meshTect;
                    break;
                case SphericalHarmonicsTab sh:
                    _shTab = sh;
                    break;
                case BiomeAssignmentTab biome:
                    _biomeTab = biome;
                    break;
                case ResourcesTab resources:
                    _resourcesTab = resources;
                    break;
            }
        }
    }

    private void InitInnerTabs()
    {
        _identityTab?.Initialize(_model);
        _meshTectTab?.Initialize(_model);
        _shTab?.Initialize(_model);
        _biomeTab?.Initialize(_model);
        _resourcesTab?.Initialize(_model);
    }

    public string? CurrentSubtypeId => _subtypeId;

    public void SetSubtype(string? subtypeId)
    {
        _subtypeId = subtypeId;
        bool hasSubtype = !string.IsNullOrEmpty(subtypeId) && _model.GetById(subtypeId) != null;
        if (_emptyHint != null) _emptyHint.Visible = !hasSubtype;
        if (_tabs != null) _tabs.Visible = hasSubtype;

        _identityTab?.SetSubtype(subtypeId);
        _meshTectTab?.SetSubtype(subtypeId);
        _shTab?.SetSubtype(subtypeId);
        _biomeTab?.SetSubtype(subtypeId);
        _resourcesTab?.SetSubtype(subtypeId);
    }
}