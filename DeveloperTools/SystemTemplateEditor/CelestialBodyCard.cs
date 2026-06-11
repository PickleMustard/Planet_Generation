#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DeveloperTools.SystemTemplateEditor;

/// <summary>
/// One collapsible card binding a single <see cref="BodyNode"/>. The always-visible header shows
/// type / subtype-summary / editable name plus reorder, up-level, add-child and delete actions;
/// the expandable body is a 2-column <see cref="GridContainer"/> of the orbital/physical fields that
/// apply to this node's <see cref="BodyCategory"/>. Structural actions raise
/// <see cref="StructureChanged"/> so the module rebuilds the whole tree; field edits mutate the node
/// and mark the model dirty in place.
/// </summary>
public partial class CelestialBodyCard : PanelContainer
{
    [Signal]
    public delegate void StructureChangedEventHandler();

    private SystemTemplateModel _model = null!;
    private BodyNode _node = null!;

    [Export] private Button _toggleButton = null!;
    [Export] private LineEdit _nameEdit = null!;
    [Export] private Button _upButton = null!;
    [Export] private Button _downButton = null!;
    [Export] private Button _upLevelButton = null!;
    [Export] private Button _addChildButton = null!;
    [Export] private Button _deleteButton = null!;
    [Export] private VBoxContainer _body = null!;

    /// <summary>Spin controls paired with a getter, so board drags can push values back into the card
    /// without rebuilding it (which would steal focus mid-edit).</summary>
    private readonly List<(SpinBox spin, Func<float> get)> _valueRefreshers = new();

    /// <summary>The node currently being dragged across cards (drag data can't carry a C# object).</summary>
    private static BodyNode? _draggedNode;

    private static PackedScene? _scene;

    public static CelestialBodyCard Create(SystemTemplateModel model, BodyNode node)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/SystemTemplateEditor/CelestialBodyCard.tscn");
        var card = _scene.Instantiate<CelestialBodyCard>();
        card.Initialize(model, node);
        return card;
    }

    public void Initialize(SystemTemplateModel model, BodyNode node)
    {
        _model = model;
        _node = node;
        Name = $"Card_{node.Name}";
    }

    public override void _Ready()
    {
        base._Ready();
        _body.Visible = _node.Expanded;
        BuildFields(_body);
        RefreshHeader();
        _model.Changed += OnModelChanged;
        _model.SelectionChanged += OnSelectionChanged;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _model.Changed -= OnModelChanged;
        _model.SelectionChanged -= OnSelectionChanged;
    }

    private void OnModelChanged()
    {
        // Pull node values back into the controls (e.g. after a board drag). SetValueNoSignal avoids
        // re-entrancy and doesn't steal focus.
        foreach (var (spin, get) in _valueRefreshers)
            spin.SetValueNoSignal(get());
        RefreshHeader();
    }

    private void OnSelectionChanged(BodyNode? _) => RefreshHeader();

    private void BuildFields(VBoxContainer body)
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(grid);

        switch (_node.Category)
        {
            case BodyCategory.Dominant:
                AddSpin(grid, "Mass", () => _node.Mass, 0, 1e9f, 100f, v => _node.Mass = v);
                AddSpin(grid, "Size", () => _node.Size, 1, 100000f, 1f, v => _node.Size = v);
                AddVector3(grid, "Position", () => _node.Position, v => _node.Position = v);
                break;

            case BodyCategory.Belt:
                AddSpin(grid, "Ring Apogee", () => _node.RingApogee, 0, 1e6f, 10f, v => _node.RingApogee = v);
                AddSpin(grid, "Ring Perigee", () => _node.RingPerigee, 0, 1e6f, 10f, v => _node.RingPerigee = v);
                AddSpin(grid, "Lower Count", () => _node.LowerRange, 0, 10000, 1f, v => _node.LowerRange = (int)v);
                AddSpin(grid, "Upper Count", () => _node.UpperRange, 0, 10000, 1f, v => _node.UpperRange = (int)v);
                break;

            default: // Planetary / Satellite
                AddSpin(grid, "Apogee", () => _node.Apogee, 0, 1e6f, 10f, v => _node.Apogee = v);
                AddSpin(grid, "Perigee", () => _node.Perigee, 0, 1e6f, 10f, v => _node.Perigee = v);
                AddSpin(grid, "Start Angle", () => _node.StartingAngle, 0, 360, 1f, v => _node.StartingAngle = v);
                AddSpin(grid, "Vert Offset", () => _node.VerticalOffset, -1000, 1000, 1f, v => _node.VerticalOffset = v);
                if (_node.Category == BodyCategory.Satellite)
                {
                    AddSpin(grid, "Mass", () => _node.Mass, 0, 1e9f, 10f, v => _node.Mass = v);
                    AddSpin(grid, "Size", () => _node.Size, 1, 100000f, 1f, v => _node.Size = v);
                }
                break;
        }
    }

    private void AddSpin(GridContainer grid, string label, Func<float> getter,
        float min, float max, float step, Action<float> setter)
    {
        grid.AddChild(new Label { Text = label });
        var spin = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            AllowGreater = true,
            AllowLesser = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        spin.SetValueNoSignal(getter());
        spin.ValueChanged += v =>
        {
            setter((float)v);
            _model.MarkChanged();
        };
        grid.AddChild(spin);
        _valueRefreshers.Add((spin, getter));
    }

    private void AddVector3(GridContainer grid, string label, Func<Vector3> getter, Action<Vector3> setter)
    {
        grid.AddChild(new Label { Text = label });
        var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(hbox);
        for (int axis = 0; axis < 3; axis++)
        {
            int a = axis;
            var spin = new SpinBox
            {
                MinValue = -1e6f,
                MaxValue = 1e6f,
                Step = 1f,
                AllowGreater = true,
                AllowLesser = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(60, 0),
            };
            spin.SetValueNoSignal(getter()[a]);
            spin.ValueChanged += v =>
            {
                Vector3 cur = getter();
                cur[a] = (float)v;
                setter(cur);
                _model.MarkChanged();
            };
            hbox.AddChild(spin);
            _valueRefreshers.Add((spin, () => getter()[a]));
        }
    }

    private void RefreshHeader()
    {
        string arrow = _node.Expanded ? "▼" : "▶";
        string sel = ReferenceEquals(_model.Selected, _node) ? "● " : "";
        _toggleButton.Text = $"{sel}{arrow}  [{_node.Type}]  {SubtypeSummary()}";

        if (_nameEdit.Text != _node.Name)
            _nameEdit.Text = _node.Name;

        var siblings = _node.Parent?.Children ?? _model.Roots;
        int i = siblings.IndexOf(_node);
        _upButton.Disabled = i <= 0;
        _downButton.Disabled = i < 0 || i >= siblings.Count - 1;
        _upLevelButton.Disabled = !_model.CanUpLevel(_node);
        // Belts and dominants don't take children in this editor.
        _addChildButton.Disabled = _node.Category == BodyCategory.Belt;
    }

    private string SubtypeSummary()
    {
        if (!string.IsNullOrEmpty(_node.ExplicitSubtype))
            return _node.ExplicitSubtype!;
        int n = _node.GetSubtypeWeights().Count;
        return n > 0 ? $"weighted ({n})" : "default";
    }

    // ─── Header actions ───────────────────────────────────────────────────

    private void OnHeaderPressed()
    {
        _node.Expanded = !_node.Expanded;
        _body.Visible = _node.Expanded;
        _model.Select(_node);
        RefreshHeader();
    }

    private void OnNameChanged(string text)
    {
        _node.Name = text;
        _model.MarkChanged();
        RefreshHeader();
    }

    private void OnMoveUp()
    {
        _model.Reorder(_node, -1);
        EmitSignal(SignalName.StructureChanged);
    }

    private void OnMoveDown()
    {
        _model.Reorder(_node, +1);
        EmitSignal(SignalName.StructureChanged);
    }

    private void OnUpLevel()
    {
        _model.UpLevel(_node);
        EmitSignal(SignalName.StructureChanged);
    }

    private void OnAddChild()
    {
        var child = SystemTemplateFactory.NewChildOf(_node);
        if (child == null)
            return;
        _model.AddChild(_node, child);
        _node.Expanded = true;
        EmitSignal(SignalName.StructureChanged);
    }

    private void OnDelete()
    {
        var dialog = new ConfirmationDialog
        {
            Title = "Delete Body",
            DialogText = $"Delete '{_node.Name}' and all its children?",
        };
        dialog.Confirmed += () =>
        {
            _model.Remove(_node);
            EmitSignal(SignalName.StructureChanged);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(380, 120));
    }

    // ─── Drag-drop reparent (M4) ──────────────────────────────────────────

    public override Variant _GetDragData(Vector2 atPosition)
    {
        // Dominants stay roots — never draggable.
        if (_node.IsDominant)
            return default;

        _draggedNode = _node;

        SetDragPreview(new Label { Text = $"⇄ {_node.Type}: {_node.Name}" });

        return new Godot.Collections.Dictionary { ["system_template_card"] = true };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (_draggedNode == null)
            return false;
        if (data.VariantType != Variant.Type.Dictionary)
            return false;
        var dict = data.AsGodotDictionary();
        if (!dict.ContainsKey("system_template_card"))
            return false;
        return _model.CanReparent(_draggedNode, _node);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_draggedNode == null)
            return;
        _model.Reparent(_draggedNode, _node);
        _draggedNode = null;
        EmitSignal(SignalName.StructureChanged);
    }
}
#endif
