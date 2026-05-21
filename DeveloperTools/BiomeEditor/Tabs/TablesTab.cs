#if DEBUG
using System;
using Godot;
using Structures.Enums;
using DeveloperTools.BiomeEditor.Cards;

namespace DeveloperTools.BiomeEditor.Tabs;

/// <summary>
/// Left ItemList of section keys, right ScrollContainer of cards for the selected section.
/// </summary>
public partial class TablesTab : Control
{
    private static readonly string[] Sections =
    {
        "Assigners",
        "Biome Resources",
        "Subtype Resources",
        "Atmosphere Templates",
        "Resource Groups",
    };

    private BiomeEditorModel? _model;
    private ItemList _sectionList = null!;
    private VBoxContainer _cardContainer = null!;
    private string _selectedSection = Sections[0];

    public override void _Ready()
    {
        base._Ready();
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLayout();
    }

    public void Initialize(BiomeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        Refresh();
    }

    private void BuildLayout()
    {
        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(split);

        _sectionList = new ItemList
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(180, 0),
        };
        foreach (var s in Sections) _sectionList.AddItem(s);
        _sectionList.ItemSelected += OnSectionSelected;
        split.AddChild(_sectionList);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        split.AddChild(scroll);

        _cardContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _cardContainer.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_cardContainer);

        _sectionList.Select(0);
    }

    private void OnSectionSelected(long index)
    {
        if (index < 0 || index >= Sections.Length) return;
        _selectedSection = Sections[index];
        Refresh();
    }

    public void Refresh()
    {
        if (_model == null) return;
        foreach (var c in _cardContainer.GetChildren()) c.QueueFree();

        switch (_selectedSection)
        {
            case "Assigners":
                foreach (var subtype in Enum.GetValues<RockyPlanetSubtype>())
                {
                    var card = new BiomeAssignerCard();
                    _cardContainer.AddChild(card);
                    card.Initialize(_model, subtype);
                }
                break;
            case "Biome Resources":
                foreach (var biome in Enum.GetValues<Biome.BiomeType>())
                {
                    var card = new BiomeResourceCard();
                    _cardContainer.AddChild(card);
                    card.Initialize(_model, biome);
                }
                break;
            case "Subtype Resources":
                foreach (var subtype in Enum.GetValues<RockyPlanetSubtype>())
                {
                    var card = new SubtypeResourceCard();
                    _cardContainer.AddChild(card);
                    card.Initialize(_model, subtype);
                }
                break;
            case "Atmosphere Templates":
                foreach (var typeName in _model.AtmosphereTemplates.Keys)
                {
                    var card = new AtmosphereTemplateCard();
                    _cardContainer.AddChild(card);
                    card.Initialize(_model, typeName);
                }
                break;
            case "Resource Groups":
                foreach (var g in _model.ResourceGroups)
                {
                    var card = new ResourceGroupCard();
                    _cardContainer.AddChild(card);
                    card.Initialize(_model, g.GroupName);
                }
                var addBtn = new Button { Text = "+ New Group" };
                addBtn.Pressed += OnNewGroup;
                _cardContainer.AddChild(addBtn);
                break;
        }
    }

    private void OnNewGroup()
    {
        if (_model == null) return;
        var lineEdit = new LineEdit
        {
            PlaceholderText = "groupName (camelCase)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var dialog = new ConfirmationDialog { Title = "New Group", DialogText = "Enter group name:" };
        dialog.AddChild(lineEdit);
        dialog.Confirmed += () =>
        {
            string name = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            _model.AddResourceGroup(name);
            CallDeferred(nameof(Refresh));
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }
}
#endif
