#if DEBUG
using System;
using System.Linq;
using Godot;
using Structures.Enums;
using Structures.Resources;
using DeveloperTools.BiomeEditor.Cards;

namespace DeveloperTools.BiomeEditor.Tabs;

/// <summary>
/// Left ItemList of section keys, right ScrollContainer of cards for the selected section.
/// </summary>
public partial class TablesTab : Control
{
    private static readonly string[] Sections =
    {
        "Biomes",
        "Subtypes",
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
        _model.BiomeRegistryChanged += OnRegistryChanged;
        _model.SubtypeRegistryChanged += OnRegistryChanged;
        Refresh();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_model != null)
        {
            _model.BiomeRegistryChanged -= OnRegistryChanged;
            _model.SubtypeRegistryChanged -= OnRegistryChanged;
        }
    }

    private void OnRegistryChanged() => CallDeferred(nameof(Refresh));

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
            case "Biomes":
                foreach (var id in _model.Biomes.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    var card = BiomeCard.Create(_model, id);
                    _cardContainer.AddChild(card);
                }
                var addBiomeBtn = new Button { Text = "+ Add Biome" };
                addBiomeBtn.Pressed += OnNewBiome;
                _cardContainer.AddChild(addBiomeBtn);
                break;
            case "Subtypes":
                foreach (var family in Enum.GetValues<BodyFamily>())
                {
                    var subs = _model.Subtypes.Values
                        .Where(s => s.Family == family)
                        .OrderBy(s => s.Id, StringComparer.Ordinal)
                        .ToList();
                    if (subs.Count == 0) continue;
                    var header = new Label { Text = family.ToString() };
                    header.AddThemeFontSizeOverride("font_size", 16);
                    header.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.6f));
                    _cardContainer.AddChild(header);
                    foreach (var s in subs)
                    {
                        var card = SubtypeCard.Create(_model, s.Id);
                        _cardContainer.AddChild(card);
                    }
                }
                var addSubBtn = new Button { Text = "+ Add Subtype" };
                addSubBtn.Pressed += OnNewSubtype;
                _cardContainer.AddChild(addSubBtn);
                break;
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

    private void OnNewBiome()
    {
        if (_model == null) return;
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var idEdit = new LineEdit { PlaceholderText = "biome_<name>", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var nameEdit = new LineEdit { PlaceholderText = "display name", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "id:" });
        body.AddChild(idEdit);
        body.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "display_name:" });
        body.AddChild(nameEdit);

        var dialog = new ConfirmationDialog { Title = "Add Biome", DialogText = "New biome registry entry:" };
        dialog.AddChild(body);
        dialog.Confirmed += () =>
        {
            string id = idEdit.Text.Trim();
            if (string.IsNullOrEmpty(id)) return;
            if (!_model.AddBiome(id, nameEdit.Text.Trim())) return;
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(420, 220));
    }

    private void OnNewSubtype()
    {
        if (_model == null) return;
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var idEdit = new LineEdit { PlaceholderText = "subtype_<family>_<name>", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var nameEdit = new LineEdit { PlaceholderText = "display name", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var familyOpt = new OptionButton();
        var families = Enum.GetValues<BodyFamily>();
        foreach (var f in families) familyOpt.AddItem(f.ToString());
        familyOpt.Selected = 0;

        body.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "id:" });
        body.AddChild(idEdit);
        body.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "display_name:" });
        body.AddChild(nameEdit);
        body.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "family (locked after create):" });
        body.AddChild(familyOpt);

        var dialog = new ConfirmationDialog { Title = "Add Subtype", DialogText = "New subtype registry entry:" };
        dialog.AddChild(body);
        dialog.Confirmed += () =>
        {
            string id = idEdit.Text.Trim();
            if (string.IsNullOrEmpty(id)) return;
            int idx = familyOpt.Selected;
            if (idx < 0 || idx >= families.Length) return;
            if (!_model.AddSubtype(id, nameEdit.Text.Trim(), families[idx])) return;
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(440, 280));
    }

}
#endif
