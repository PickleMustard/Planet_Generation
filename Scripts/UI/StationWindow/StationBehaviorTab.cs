using Constructables;
using Constructables.Stations;
using Constructables.Stations.Behaviors;
using Godot;

namespace UI.StationWindow;

/// <summary>
/// Generic tab content for a station behavior. The same scene serves every tab; populator
/// dispatch picks a behavior-specific view based on the runtime type. The signature tab
/// (top-priority behavior) also docks a <see cref="StationStorageSection"/> beneath the
/// behavior content; non-signature tabs hide the storage section.
/// </summary>
public partial class StationBehaviorTab : PanelContainer
{
    [Signal]
    public delegate void ItemSelectedEventHandler(string itemType, int itemIndex);

    private VBoxContainer? _behaviorContent;
    private StationStorageSection? _storageSection;

    private StationSatellite? _station;
    private IStationBehavior? _behavior;

    public override void _Ready()
    {
        _behaviorContent = GetNode<VBoxContainer>("Margin/VBox/BehaviorContent");
        _storageSection = GetNode<StationStorageSection>("Margin/VBox/StorageSection");
    }

    public void Initialize(StationSatellite station, IStationBehavior? behavior, bool showStorage)
    {
        _station = station;
        _behavior = behavior;

        if (_storageSection != null)
        {
            if (showStorage)
            {
                _storageSection.Visible = true;
                _storageSection.Initialize(station);
            }
            else
            {
                _storageSection.Visible = false;
                _storageSection.Clear();
            }
        }

        PopulateBehaviorContent();
    }

    public void Clear()
    {
        _station = null;
        _behavior = null;
        if (_behaviorContent != null)
        {
            foreach (var child in _behaviorContent.GetChildren())
                child.QueueFree();
        }
        _storageSection?.Clear();
    }

    private void PopulateBehaviorContent()
    {
        if (_behaviorContent == null || _station == null)
            return;

        foreach (var child in _behaviorContent.GetChildren())
            child.QueueFree();

        switch (_behavior)
        {
            case ShipyardBehavior shipyard:
                PopulateShipyard(shipyard);
                break;
            case OrbitalConstructorBehavior architect:
                PopulateOrbitalConstructor(architect);
                break;
            case TransferHubBehavior hub:
                PopulateTransferHub(hub);
                break;
            case StorageHubBehavior storage:
                PopulateStorage(storage);
                break;
            case null:
                PopulateOverview();
                break;
            default:
                AddSectionHeader(_behavior.GetType().Name);
                AddEmptyLabel("No detail view configured for this behavior.");
                break;
        }
    }

    private void PopulateOverview()
    {
        if (_station == null)
            return;

        AddSectionHeader("Station Overview");
        AddInfoRow("Name", _station.Name);
        AddInfoRow("Type", _station.StationType ?? "—");
        AddInfoRow("Band", _station.BandIndex >= 0 ? _station.BandIndex.ToString() : "—");
        AddInfoRow("Active", _station.IsActive ? "Yes" : "No");

        if (_station.IsUnderConstruction)
        {
            AddSectionHeader("Construction");
            AddInfoRow("Status", _station.GetStatus());
            AddInfoRow("Progress", $"{_station.GetProgress() * 100:F0}%");
            AddInfoRow("Work", $"{_station.workDone:F1} / {_station.workRequired:F1}");
        }
    }

    private void PopulateShipyard(ShipyardBehavior shipyard)
    {
        var active = shipyard.GetActiveBuilds();
        var queued = shipyard.GetShipBuildQueue();

        AddSectionHeader("Shipyard");
        AddInfoRow("Parallel Slots", shipyard.MaxParallelBuilds.ToString());
        AddInfoRow("Active Builds", active.Count.ToString());
        AddInfoRow("Queued", queued.Count.ToString());

        if (active.Count == 0 && queued.Count == 0)
        {
            AddEmptyLabel("No ships in build queue");
            return;
        }

        if (active.Count > 0)
        {
            AddSectionHeader($"Building ({active.Count})");
            for (int i = 0; i < active.Count; i++)
            {
                var ship = active[i];
                AppendRow(i, "active_ship", $"{ship.Name}  |  {ship.GetProgress() * 100:F0}%  |  {ship.GetStatus()}");
            }
        }

        if (queued.Count > 0)
        {
            AddSectionHeader($"Queued ({queued.Count})");
            for (int i = 0; i < queued.Count; i++)
            {
                var ship = queued[i];
                AppendRow(i, "queued_ship", $"{ship.Name}  |  Waiting…");
            }
        }
    }

    private void PopulateOrbitalConstructor(OrbitalConstructorBehavior architect)
    {
        AddSectionHeader("Orbital Architect");
        AddInfoRow("Work / Slot / Tick", $"{architect.WorkBudgetPerTick:F2}");
        AddInfoRow(
            "Regular Slots",
            $"{architect.OccupiedRegularCount} / {architect.RegularSlotsConfigured}"
        );
        AddInfoRow(
            "Overtime Slots",
            $"{architect.OccupiedOvertimeCount} / {architect.OvertimeSlotTarget}"
                + (architect.OvertimeSlotsConfigured > architect.OvertimeSlotTarget
                    ? $" (+{architect.OvertimeSlotsConfigured - architect.OvertimeSlotTarget} retiring)"
                    : "")
        );
        AddInfoRow("Overtime Cost / Slot", $"{architect.OvertimeCostPercent * 100f:F0}%");
        AddInfoRow("Queue Depth", architect.QueueCount.ToString());

        var mgr = _station?.ParentBody?.BuildingConstructionMgr;
        if (mgr != null)
            AddInfoRow("Architects on Body", mgr.ArchitectCount.ToString());

        AddSectionHeader("Regular Slots");
        AppendSlotList(architect, architect.RegularSlots, "regular");

        AddSectionHeader("Overtime Slots");
        if (architect.OvertimeSlotsConfigured == 0)
            AddEmptyLabel("No overtime slots configured");
        else
            AppendSlotList(architect, architect.OvertimeSlots, "overtime");

        AddSectionHeader($"Queue ({architect.QueueCount})");
        if (architect.QueueCount == 0)
        {
            AddEmptyLabel("Queue empty");
        }
        else
        {
            for (int i = 0; i < architect.Queue.Count; i++)
            {
                var building = architect.Queue[i];
                AppendArchitectRow(
                    architect,
                    building,
                    i,
                    "queued_building",
                    $"{building.Name}  |  queued"
                );
            }
        }

        if (mgr != null && mgr.PendingBuildingCount > 0)
        {
            AddSectionHeader($"Pending — body-wide ({mgr.PendingBuildingCount})");
            AddEmptyLabel(
                $"{mgr.PendingBuildingCount} building(s) waiting for any architect"
            );
        }
    }

    private void AppendSlotList(
        OrbitalConstructorBehavior architect,
        System.Collections.Generic.IReadOnlyList<OrbitalConstructorBehavior.SlotRecord> slots,
        string slotKind)
    {
        if (slots.Count == 0)
        {
            AddEmptyLabel("(no slots)");
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            string label;
            if (slot.Building == null)
            {
                label = $"slot {i + 1}  |  empty"
                    + (slot.IsRetiring ? "  (retiring)" : "");
                AppendStaticRow(label);
                continue;
            }

            label = $"slot {i + 1}  |  {slot.Building.Name}  |  {slot.Building.GetProgress() * 100:F0}%"
                + (slot.IsRetiring ? "  (retiring)" : "");
            AppendArchitectRow(architect, slot.Building, i, $"{slotKind}_slot", label);
        }
    }

    private void AppendStaticRow(string text)
    {
        if (_behaviorContent == null)
            return;
        var row = new HBoxContainer();
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
        row.AddChild(label);
        _behaviorContent.AddChild(row);
    }

    private void AppendArchitectRow(
        OrbitalConstructorBehavior architect,
        Building building,
        int index,
        string itemType,
        string text)
    {
        if (_behaviorContent == null)
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

        _behaviorContent.AddChild(row);
    }

    private void ShowTransferPopup(
        OrbitalConstructorBehavior source,
        Building building,
        Vector2 globalMousePos)
    {
        var mgr = _station?.ParentBody?.BuildingConstructionMgr;
        if (mgr == null) return;

        var siblings = new System.Collections.Generic.List<OrbitalConstructorBehavior>();
        foreach (var a in mgr.GetArchitects())
            if (a != source) siblings.Add(a);

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
                popup.AddItem(
                    $"Transfer to {siblingName}  (load {sibling.Load})",
                    i
                );
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

    private void PopulateTransferHub(TransferHubBehavior hub)
    {
        AddSectionHeader("Transfer Hub");
        var schedules = hub.GetAllSchedules();
        var active = hub.GetActiveTransfers();

        AddInfoRow("Active Transfers", active.Count.ToString());
        AddInfoRow("Schedules", schedules.Count.ToString());

        if (schedules.Count == 0 && active.Count == 0)
        {
            AddEmptyLabel("No transfers scheduled");
            return;
        }

        if (active.Count > 0)
        {
            AddSectionHeader("In Flight");
            int i = 0;
            foreach (var kvp in active)
            {
                var order = kvp.Value.Order;
                string id = order.OrderId.Length >= 6 ? order.OrderId[..6] : order.OrderId;
                AppendRow(i++, "active_transfer", $"{id}  |  {order.Manifest.TotalUnits:F0}u  |  {order.State}");
            }
        }

        if (schedules.Count > 0)
        {
            AddSectionHeader("Schedules");
            for (int i = 0; i < schedules.Count; i++)
            {
                var s = schedules[i];
                string id = s.ScheduleId.Length >= 6 ? s.ScheduleId[..6] : s.ScheduleId;
                AppendRow(i, "schedule", $"{id}  |  {s.State}  |  → {s.Destination}");
            }
        }
    }

    private void PopulateStorage(StorageHubBehavior storage)
    {
        AddSectionHeader("Storage Depot");
        AddInfoRow("Slot Capacity", storage.StorageCapacity.ToString());
        AddInfoRow("Filter Specs", storage.SlotFilters.Count.ToString());
        AddEmptyLabel("Slot inventory shown below.");
    }

    private void AddSectionHeader(string text)
    {
        if (_behaviorContent == null)
            return;
        var header = new Label { Text = text };
        header.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.95f));
        header.AddThemeFontSizeOverride("font_size", 15);
        _behaviorContent.AddChild(header);
        _behaviorContent.AddChild(new HSeparator());
    }

    private void AddInfoRow(string key, string value)
    {
        if (_behaviorContent == null)
            return;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var keyLabel = new Label { Text = key + ":" };
        keyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        keyLabel.AddThemeFontSizeOverride("font_size", 13);
        keyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(keyLabel);

        var valLabel = new Label { Text = value };
        valLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(valLabel);

        _behaviorContent.AddChild(row);
    }

    private void AddEmptyLabel(string text)
    {
        if (_behaviorContent == null)
            return;
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        label.AddThemeFontSizeOverride("font_size", 13);
        _behaviorContent.AddChild(label);
    }

    private void AppendRow(int index, string itemType, string text)
    {
        if (_behaviorContent == null)
            return;

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        row.AddThemeConstantOverride("separation", 4);
        row.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton btn
                && btn.ButtonIndex == MouseButton.Left
                && btn.Pressed)
            {
                EmitSignal(SignalName.ItemSelected, itemType, index);
                row.GetViewport().SetInputAsHandled();
            }
        };

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(label);

        _behaviorContent.AddChild(row);
    }
}
