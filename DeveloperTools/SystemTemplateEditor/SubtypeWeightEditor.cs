#if DEBUG
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DeveloperTools.SystemTemplateEditor;

/// <summary>
/// Bottom details panel for the selected body. Edits the subtype distribution that drives generation:
/// an explicit single <c>subtype</c> or a weighted <c>subtype_weights</c> table (mutually exclusive,
/// mirroring the validator). Belts additionally expose a per-member asteroid-subtype table and the
/// member total-count range. Hidden when nothing is selected.
/// </summary>
public partial class SubtypeWeightEditor : PanelContainer
{
    private SystemTemplateModel _model = null!;
    private VBoxContainer _content = null!;

    public void Initialize(SystemTemplateModel model)
    {
        _model = model;
        _model.SelectionChanged += OnSelectionChanged;
    }

    public override void _Ready()
    {
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(_content);
        Rebuild();
    }

    public override void _ExitTree() => _model.SelectionChanged -= OnSelectionChanged;

    private void OnSelectionChanged(BodyNode? _) => Rebuild();

    private void Rebuild()
    {
        if (_content == null)
            return;
        foreach (var c in _content.GetChildren())
            c.QueueFree();

        var node = _model.Selected;
        Visible = node != null;
        if (node == null)
            return;

        _content.AddChild(new Label { Text = $"Subtypes — {node.Name}  [{node.Type}]" });

        // Explicit subtype toggle (mutually exclusive with the weight table).
        bool explicitOn = !string.IsNullOrEmpty(node.ExplicitSubtype);
        var toggle = new CheckBox { Text = "Explicit single subtype", ButtonPressed = explicitOn };
        toggle.Toggled += on => OnExplicitToggled(node, on);
        _content.AddChild(toggle);

        if (explicitOn)
        {
            BuildExplicitPicker(node);
        }
        else
        {
            BuildWeightTable(node, "Subtype weights",
                node.GetSubtypeWeights(), node.SetSubtypeWeights, SubtypeFamilies.IdsForBody(node));

            if (node.Category == BodyCategory.Belt)
            {
                BuildWeightTable(node, "Member subtype weights",
                    node.GetMemberSubtypeWeights(), node.SetMemberSubtypeWeights, SubtypeFamilies.MemberIds);
                BuildBeltCounts(node);
            }
        }
    }

    private void OnExplicitToggled(BodyNode node, bool on)
    {
        if (on)
        {
            node.SetSubtypeWeights(new Dictionary<string, float>());
            var ids = SubtypeFamilies.IdsForBody(node);
            node.ExplicitSubtype = ids.Count > 0 ? ids[0] : "";
        }
        else
        {
            node.ExplicitSubtype = null;
        }
        _model.MarkChanged();
        Rebuild();
    }

    private void BuildExplicitPicker(BodyNode node)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(new Label { Text = "Subtype" });
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var ids = SubtypeFamilies.IdsForBody(node);
        for (int i = 0; i < ids.Count; i++)
        {
            option.AddItem(ids[i], i);
            if (ids[i] == node.ExplicitSubtype)
                option.Select(i);
        }
        option.ItemSelected += idx =>
        {
            node.ExplicitSubtype = ids[(int)idx];
            _model.MarkChanged();
        };
        row.AddChild(option);
        _content.AddChild(row);
    }

    private void BuildWeightTable(
        BodyNode node,
        string title,
        Dictionary<string, float> weights,
        System.Action<IReadOnlyDictionary<string, float>> persist,
        IReadOnlyList<string> familyIds)
    {
        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var addButton = new Button { Text = "+ Subtype" };
        addButton.Pressed += () => OnAddSubtype(node, weights, persist, familyIds, addButton);
        header.AddChild(addButton);
        _content.AddChild(header);

        foreach (var id in weights.Keys.OrderBy(k => k).ToList())
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            var idLabel = new Label { Text = id, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(idLabel);

            var spin = new SpinBox
            {
                MinValue = 0,
                MaxValue = 1000,
                Step = 0.01,
                AllowGreater = true,
                CustomMinimumSize = new Vector2(90, 0),
            };
            spin.SetValueNoSignal(weights[id]);
            string capturedId = id;
            spin.ValueChanged += v =>
            {
                weights[capturedId] = (float)v;
                persist(weights);
                _model.MarkChanged();
            };
            row.AddChild(spin);

            var remove = new Button { Text = "✕", TooltipText = "Remove subtype" };
            remove.Pressed += () =>
            {
                weights.Remove(capturedId);
                persist(weights);
                _model.MarkChanged();
                Rebuild();
            };
            row.AddChild(remove);

            _content.AddChild(row);
        }
    }

    private void OnAddSubtype(
        BodyNode node,
        Dictionary<string, float> weights,
        System.Action<IReadOnlyDictionary<string, float>> persist,
        IReadOnlyList<string> familyIds,
        Control anchor)
    {
        var available = familyIds.Where(id => !weights.ContainsKey(id)).ToList();
        if (available.Count == 0)
            return;

        var popup = new PopupMenu();
        for (int i = 0; i < available.Count; i++)
            popup.AddItem(available[i], i);
        popup.IdPressed += id =>
        {
            weights[available[(int)id]] = 0.1f;
            persist(weights);
            _model.MarkChanged();
            Rebuild();
        };
        AddChild(popup);
        popup.Popup(new Rect2I((Vector2I)anchor.GlobalPosition, Vector2I.Zero));
    }

    private void BuildBeltCounts(BodyNode node)
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddChild(grid);

        grid.AddChild(new Label { Text = "Member count (min)" });
        var lower = new SpinBox { MinValue = 0, MaxValue = 10000, Step = 1, AllowGreater = true };
        lower.SetValueNoSignal(node.LowerRange);
        lower.ValueChanged += v => { node.LowerRange = (int)v; _model.MarkChanged(); };
        grid.AddChild(lower);

        grid.AddChild(new Label { Text = "Member count (max)" });
        var upper = new SpinBox { MinValue = 0, MaxValue = 10000, Step = 1, AllowGreater = true };
        upper.SetValueNoSignal(node.UpperRange);
        upper.ValueChanged += v => { node.UpperRange = (int)v; _model.MarkChanged(); };
        grid.AddChild(upper);
    }
}
#endif
