using Constructables;
using Constructables.Stations.Behaviors;
using Godot;
using ProceduralGeneration;

namespace UI.StationWindow;

/// <summary>
/// Always-present Transfer tab. When the station has a <see cref="TransferHubBehavior"/> it renders the
/// outbound schedules / in-flight transfers it owns; otherwise the station is a destination-only
/// endpoint and the tab renders inbound transfers resolved from the parent body
/// (<c>GetInboundSchedulesForBuilding</c> / <c>GetInboundActiveTransfersForBuilding</c>, keyed by
/// <see cref="StationSatellite.Id"/>). Tick-driven, so it polls while visible.
/// </summary>
public partial class StationTransferPanel : StationDetailPanel
{
    [Export]
    private Label? _header;

    [Export]
    private VBoxContainer? _list;

    private int _refreshCounter;

    public override void _Ready()
    {
        // Layout lives in StationTransferPanel.tscn; this only handles the case where
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
        if (_list == null || _header == null || _station == null)
            return;
        ClearChildren(_list);

        var hub = _station.GetBehavior<TransferHubBehavior>();
        if (hub != null)
            RenderOutbound(hub);
        else
            RenderInbound();
    }

    private void RenderOutbound(TransferHubBehavior hub)
    {
        var schedules = hub.GetAllSchedules();
        var active = hub.GetActiveTransfers();
        _header!.Text = $"Transfers · {active.Count} active · {schedules.Count} scheduled";

        if (schedules.Count == 0 && active.Count == 0)
        {
            AddEmptyLabel(_list!, "No transfers scheduled");
            return;
        }

        if (active.Count > 0)
        {
            AddSectionHeader(_list!, "In Flight");
            foreach (var kvp in active)
            {
                var order = kvp.Value.Order;
                AddRow($"{Short(order.OrderId)}  |  {order.Manifest.TotalUnits:F0}u  |  {order.State}");
            }
        }

        if (schedules.Count > 0)
        {
            AddSectionHeader(_list!, "Schedules");
            foreach (var s in schedules)
                AddRow($"{Short(s.ScheduleId)}  |  {s.State}  |  → {s.Destination}");
        }
    }

    private void RenderInbound()
    {
        IOrbitalBody? body = _station!.ParentBody;
        if (body == null || string.IsNullOrEmpty(_station.Id))
        {
            _header!.Text = "Transfers";
            AddEmptyLabel(_list!, "Destination-only endpoint");
            return;
        }

        var schedules = body.GetInboundSchedulesForBuilding(_station.Id);
        var active = body.GetInboundActiveTransfersForBuilding(_station.Id);
        _header!.Text = $"Inbound · {active.Count} active · {schedules.Count} scheduled";

        if (schedules.Count == 0 && active.Count == 0)
        {
            AddEmptyLabel(_list!, "No inbound transfers");
            return;
        }

        foreach (var transfer in active)
        {
            var order = transfer.Order;
            if (order == null)
                continue;
            AddRow($"from {FormatOrigin(order.OriginBuildingId, body)}  |  {order.State}");
        }
        foreach (var s in schedules)
            AddRow($"from {FormatOrigin(s.OriginBuildingId, body)}  |  {s.State}");
    }

    private void AddRow(string text)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(label);
        _list!.AddChild(row);
    }

    private static string Short(string id) => id.Length >= 6 ? id[..6] : id;

    private static string FormatOrigin(string originId, IOrbitalBody body)
    {
        string shortId = Short(originId);
        var owner = body.GetTransferEndpointOwner(originId);
        if (owner is Building b && !string.IsNullOrEmpty(b.Name))
            return $"{b.Name} ({shortId})";
        if (owner is StationSatellite s && !string.IsNullOrEmpty(s.Name))
            return $"{s.Name} ({shortId})";
        return shortId;
    }
}
