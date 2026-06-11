using System.Collections.Generic;
using Constructables;
using Constructables.Stations.Behaviors;
using Godot;

namespace UI.StationWindow;

/// <summary>
/// Bespoke Architect tab for an <see cref="OrbitalConstructorBehavior"/>. Shows work budget, regular /
/// overtime slot occupancy and the build queue with live progress. Rows are clickable
/// (left-click selects → bubbles <see cref="ItemSelected"/>; right-click opens a transfer popup to move
/// a building to a sibling architect on the same body). Tick-driven, so it polls while visible.
/// Ported from the former StationBehaviorTab architect view.
/// </summary>
public partial class StationArchitectPanel : StationDetailPanel
{
    [Signal]
    public delegate void ItemSelectedEventHandler(string itemType, int itemIndex);

    [Export]
    private VBoxContainer? _content;

    private int _refreshCounter;

    public override void _Ready()
    {
        // Layout lives in StationArchitectPanel.tscn; this only handles the case where
        // SetStation ran before the node entered the tree.
        if (_station != null)
            UpdateDisplay();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsVisibleInTree() || _station == null)
            return;
        _refreshCounter++;
        if (_refreshCounter >= 10)
        {
            _refreshCounter = 0;
            UpdateDisplay();
        }
    }

    protected override void UpdateDisplay()
    {
        if (_content == null || _station == null)
            return;
        ClearChildren(_content);

        if (_behavior is not OrbitalConstructorBehavior architect)
        {
            AddEmptyLabel(_content, "No architect behavior.");
            return;
        }

        Populate(architect);
    }

    private void Populate(OrbitalConstructorBehavior architect)
    {
        AddSectionHeader(_content!, "Orbital Architect");
        AddInfoRow(_content!, "Work / Slot / Tick", $"{architect.WorkBudgetPerTick:F2}");
        AddInfoRow(
            _content!,
            "Regular Slots",
            $"{architect.OccupiedRegularCount} / {architect.RegularSlotsConfigured}"
        );
        AddInfoRow(
            _content!,
            "Overtime Slots",
            $"{architect.OccupiedOvertimeCount} / {architect.OvertimeSlotTarget}"
                + (architect.OvertimeSlotsConfigured > architect.OvertimeSlotTarget
                    ? $" (+{architect.OvertimeSlotsConfigured - architect.OvertimeSlotTarget} retiring)"
                    : "")
        );
        AddInfoRow(_content!, "Overtime Cost / Slot", $"{architect.OvertimeCostPercent * 100f:F0}%");
        AddInfoRow(_content!, "Queue Depth", architect.QueueCount.ToString());

        var mgr = _station?.ParentBody?.BuildingConstructionMgr;
        if (mgr != null)
            AddInfoRow(_content!, "Architects on Body", mgr.ArchitectCount.ToString());

        AddSectionHeader(_content!, "Regular Slots");
        AppendSlotList(architect, architect.RegularSlots, "regular");

        AddSectionHeader(_content!, "Overtime Slots");
        if (architect.OvertimeSlotsConfigured == 0)
            AddEmptyLabel(_content!, "No overtime slots configured");
        else
            AppendSlotList(architect, architect.OvertimeSlots, "overtime");

        AddSectionHeader(_content!, $"Queue ({architect.QueueCount})");
        if (architect.QueueCount == 0)
        {
            AddEmptyLabel(_content!, "Queue empty");
        }
        else
        {
            for (int i = 0; i < architect.Queue.Count; i++)
            {
                var building = architect.Queue[i];
                AppendArchitectRow(architect, building, i, "queued_building", $"{building.Name}  |  queued");
            }
        }

        if (mgr != null && mgr.PendingBuildingCount > 0)
        {
            AddSectionHeader(_content!, $"Pending — body-wide ({mgr.PendingBuildingCount})");
            AddEmptyLabel(_content!, $"{mgr.PendingBuildingCount} building(s) waiting for any architect");
        }
    }

    private void AppendSlotList(
        OrbitalConstructorBehavior architect,
        IReadOnlyList<OrbitalConstructorBehavior.SlotRecord> slots,
        string slotKind)
    {
        if (slots.Count == 0)
        {
            AddEmptyLabel(_content!, "(no slots)");
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            string label;
            if (slot.Building == null)
            {
                label = $"slot {i + 1}  |  empty" + (slot.IsRetiring ? "  (retiring)" : "");
                AppendStaticRow(label);
                continue;
            }

            label =
                $"slot {i + 1}  |  {slot.Building.Name}  |  {slot.Building.GetProgress() * 100:F0}%"
                + (slot.IsRetiring ? "  (retiring)" : "");
            AppendArchitectRow(architect, slot.Building, i, $"{slotKind}_slot", label);
        }
    }

    private void AppendStaticRow(string text)
    {
        if (_content == null)
            return;
        var row = new HBoxContainer();
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
        row.AddChild(label);
        _content.AddChild(row);
    }

    private void AppendArchitectRow(
        OrbitalConstructorBehavior architect,
        IArchitectConstructable building,
        int index,
        string itemType,
        string text)
    {
        if (_content == null)
            return;

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        row.AddThemeConstantOverride("separation", 4);
        row.GuiInput += (@event) =>
        {
            if (@event is not InputEventMouseButton btn || !btn.Pressed)
                return;
            if (btn.ButtonIndex == MouseButton.Left)
            {
                EmitSignal(SignalName.ItemSelected, itemType, index);
                row.GetViewport().SetInputAsHandled();
            }
            else if (btn.ButtonIndex == MouseButton.Right)
            {
                ShowTransferPopup(architect, building, btn.GlobalPosition);
                row.GetViewport().SetInputAsHandled();
            }
        };

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(label);

        _content.AddChild(row);
    }

    private void ShowTransferPopup(
        OrbitalConstructorBehavior source,
        IArchitectConstructable building,
        Vector2 globalMousePos)
    {
        var mgr = _station?.ParentBody?.BuildingConstructionMgr;
        if (mgr == null)
            return;

        var siblings = new List<OrbitalConstructorBehavior>();
        foreach (var a in mgr.GetArchitects())
            if (a != source)
                siblings.Add(a);

        var popup = new PopupMenu();

        if (siblings.Count == 0)
        {
            popup.AddItem("(no other architects on this body)");
            popup.SetItemDisabled(0, true);
        }
        else
        {
            for (int i = 0; i < siblings.Count; i++)
            {
                var sibling = siblings[i];
                string siblingName = sibling.Owner?.Name ?? "?";
                popup.AddItem($"Transfer to {siblingName}  (load {sibling.Load})", i);
            }
        }

        AddChild(popup);
        popup.IdPressed += (id) =>
        {
            int idx = (int)id;
            if (idx >= 0 && idx < siblings.Count)
                source.TransferBuildingTo(siblings[idx], building);
            popup.QueueFree();
        };
        popup.PopupHide += () => popup.QueueFree();
        popup.Position = (Vector2I)globalMousePos;
        popup.Popup();
    }
}
