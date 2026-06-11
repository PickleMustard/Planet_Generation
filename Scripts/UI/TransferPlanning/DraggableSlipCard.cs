using Godot;
using UI.Wireframe;

namespace UI.TransferPlanning;

/// <summary>
/// A <see cref="SlipCard"/> that can be dragged to reorder route priority.
/// Layout is the inherited scene <c>DraggableSlipCard.tscn</c> (a variant of
/// <c>SlipCard.tscn</c> with this script). Instantiate via <see cref="Create"/>.
/// </summary>
[GlobalClass]
public partial class DraggableSlipCard : SlipCard
{
    public string ScheduleId = string.Empty;
    public PriorityEditView? ParentView;

    private static PackedScene? _draggableScene;

    public static DraggableSlipCard Create()
    {
        _draggableScene ??= GD.Load<PackedScene>("res://UI/TransferPlanning/DraggableSlipCard.tscn");
        return _draggableScene.Instantiate<DraggableSlipCard>();
    }

    public override void _Ready()
    {
        ShowDragRail = true;
        base._Ready();
        MouseDefaultCursorShape = CursorShape.Drag;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new PanelContainer();
        preview.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.6f),
            BorderColor = WireColors.Ink,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        });
        var lbl = new Label { Text = $"RT · {ScheduleId[..System.Math.Min(6, ScheduleId.Length)]}", ThemeTypeVariation = "LabelHand" };
        preview.AddChild(lbl);
        SetDragPreview(preview);
        return ScheduleId;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.String;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.String) return;
        string draggedId = data.AsString();
        ParentView?.OnCardSwapRequested(draggedId, ScheduleId);
    }
}
