using Constructables;
using Constructables.Buildings.Behaviors;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.GameState;
using Structures.Resources;
using Structures.Transfers;
using UI.TransferPlanning;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Bespoke panel for storage and transport hubs. Top: render thumbnail + capacity stats.
/// Body: extended bulk-slot grid on the left, links/transfers list on the right.
/// Transport hubs show an additional transfers section; storage hubs hide it.
/// </summary>
public partial class HubPanelDetails : BaseBuildingDetails
{
    private TextureRect? _renderIcon;
    private Label? _kindTag;
    private Label? _usedValue;
    private Label? _freeValue;
    private Label? _categoryValue;
    private ProgressBar? _fillBar;
    private Label? _fillPctLabel;

    private Label? _slotsHeaderLabel;
    private GridContainer? _bulkSlotsGrid;

    private Label? _linksHeader;
    private VBoxContainer? _depositsList;
    private VBoxContainer? _withdrawalsList;

    private VBoxContainer? _transfersSection;
    private Label? _transfersHeader;
    private VBoxContainer? _transfersList;
    private Button? _manageRoutesButton;

    private PackedScene? _resourceSlotItemScene;

    private int _refreshCounter;
    public override void _PhysicsProcess(double delta)
    {
        _refreshCounter++;
        if (_refreshCounter >= 10) { _refreshCounter = 0; UpdateDisplay(); }
    }

    public override void _Ready()
    {
        _renderIcon = GetNodeOrNull<TextureRect>("Layout/Header/RenderTile/RenderIcon");
        _kindTag = GetNodeOrNull<Label>("Layout/Header/Stats/KindTag");
        _usedValue = GetNodeOrNull<Label>("Layout/Header/Stats/Stats/UsedTile/Value");
        _freeValue = GetNodeOrNull<Label>("Layout/Header/Stats/Stats/FreeTile/Value");
        _categoryValue = GetNodeOrNull<Label>("Layout/Header/Stats/Stats/CategoryTile/Value");
        _fillBar = GetNodeOrNull<ProgressBar>("Layout/Header/Stats/FillRow/FillBar");
        _fillPctLabel = GetNodeOrNull<Label>("Layout/Header/Stats/FillRow/FillPct");

        _slotsHeaderLabel = GetNodeOrNull<Label>("Layout/Body/SlotsPane/SlotsHeader");
        _bulkSlotsGrid = GetNodeOrNull<GridContainer>("Layout/Body/SlotsPane/Scroll/BulkSlotsGrid");

        _linksHeader = GetNodeOrNull<Label>("Layout/Body/IOPane/LinksHeader");
        _depositsList = GetNodeOrNull<VBoxContainer>("Layout/Body/IOPane/Scroll/Sections/Deposits/List");
        _withdrawalsList = GetNodeOrNull<VBoxContainer>("Layout/Body/IOPane/Scroll/Sections/Withdrawals/List");

        _transfersSection = GetNodeOrNull<VBoxContainer>("Layout/Body/IOPane/Scroll/Sections/Transfers");
        _transfersHeader = GetNodeOrNull<Label>("Layout/Body/IOPane/Scroll/Sections/Transfers/HeaderRow/Header");
        _transfersList = GetNodeOrNull<VBoxContainer>("Layout/Body/IOPane/Scroll/Sections/Transfers/List");
        _manageRoutesButton = GetNodeOrNull<Button>("Layout/Body/IOPane/Scroll/Sections/Transfers/HeaderRow/ManageRoutesButton");
        if (_manageRoutesButton != null) _manageRoutesButton.Pressed += OnManageRoutesPressed;

        _resourceSlotItemScene = ResourceLoader.Load<PackedScene>("res://UI/BuildingInfo/ResourceSlotItem.tscn");
    }

    protected override void UpdateDisplay()
    {
        if (_building == null) { Clear(); return; }

        UpdateRender();
        bool isTransport = IsTransportHub(_building);
        UpdateHeader(isTransport);
        UpdateBulkSlots();
        UpdateLinks();
        UpdateTransfers(isTransport);
    }

    public override void Clear()
    {
        base.Clear();
        if (_renderIcon != null) _renderIcon.Texture = null;
        if (_kindTag != null) _kindTag.Text = "";
        if (_usedValue != null) _usedValue.Text = "0/0";
        if (_freeValue != null) _freeValue.Text = "0";
        if (_categoryValue != null) _categoryValue.Text = "—";
        if (_fillBar != null) _fillBar.Value = 0;
        if (_fillPctLabel != null) _fillPctLabel.Text = "0%";
        ClearChildren(_bulkSlotsGrid);
        ClearChildren(_depositsList);
        ClearChildren(_withdrawalsList);
        ClearChildren(_transfersList);
        if (_transfersSection != null) _transfersSection.Visible = false;
    }

    private bool IsTransportHub(Building building)
        => building.Definition?.TransferStation != null
            || building.GetBehavior<TransportHubBehavior>() != null;

    private void UpdateRender()
    {
        if (_renderIcon == null || _building?.Definition == null) return;
        var iconDef = _building.Definition.Icon;
        Texture2D? tex = iconDef?.IsValid == true
            ? (iconDef.MediumTexture ?? iconDef.SmallTexture)
            : null;
        if (tex == null && !string.IsNullOrEmpty(iconDef?.BasePath))
        {
            try { tex = ResourceLoader.Load<Texture2D>(iconDef.BasePath + "_medium.png"); }
            catch { tex = null; }
        }
        _renderIcon.Texture = tex;
    }

    private void UpdateHeader(bool isTransport)
    {
        if (_kindTag != null)
            _kindTag.Text = isTransport ? "TRANSPORT HUB" : "STORAGE HUB";

        var bulk = _building?.BulkStorage;
        int total = bulk?.Slots.Count ?? 0;
        int occupied = 0;
        float usedQty = 0f, capacityQty = 0f;
        if (bulk != null)
        {
            foreach (var slot in bulk.Slots)
            {
                if (!slot.IsEmpty) occupied++;
                usedQty += slot.Quantity;
                capacityQty += slot.Capacity;
            }
        }
        int free = total - occupied;

        if (_usedValue != null) _usedValue.Text = $"{occupied} / {total}";
        if (_freeValue != null) _freeValue.Text = $"{free}";
        if (_categoryValue != null) _categoryValue.Text = GetStorageCategory(_building?.Definition);

        float frac = capacityQty > 0f ? Mathf.Clamp(usedQty / capacityQty, 0f, 1f) : 0f;
        if (_fillBar != null) { _fillBar.MinValue = 0; _fillBar.MaxValue = 1; _fillBar.Value = frac; }
        if (_fillPctLabel != null) _fillPctLabel.Text = $"{Mathf.RoundToInt(frac * 100)}%";

        if (_slotsHeaderLabel != null)
            _slotsHeaderLabel.Text = $"BULK SLOTS · {occupied} / {total}";
    }

    private void UpdateBulkSlots()
    {
        PopulateSlotGrid(_bulkSlotsGrid, _building?.BulkStorage, _resourceSlotItemScene);
    }

    private void UpdateLinks()
    {
        ClearChildren(_depositsList);
        ClearChildren(_withdrawalsList);
        if (_building == null) return;

        int depositCount = 0, withdrawCount = 0;
        foreach (var node in _building.Nodes)
        {
            var link = node.Link;
            string otherName = link == null ? "— empty port —"
                : (ReferenceEquals(link.Source, node) ? link.Target : link.Source)?.Owner?.Name ?? "(unknown)";
            string profileName = link?.Profile?.IdName ?? "—";
            string detail = link == null ? "" : $"  ·  {profileName}";

            if (node.Kind == Structures.Resources.ResourceNodeKind.Import)
            {
                AddLinkRow(_depositsList, $"← {otherName}{detail}", link != null);
                depositCount++;
            }
            else if (node.Kind == Structures.Resources.ResourceNodeKind.Export)
            {
                AddLinkRow(_withdrawalsList, $"→ {otherName}{detail}", link != null);
                withdrawCount++;
            }
            else
            {
                AddLinkRow(_depositsList, $"⇄ {otherName}{detail}", link != null);
                AddLinkRow(_withdrawalsList, $"⇄ {otherName}{detail}", link != null);
                depositCount++;
                withdrawCount++;
            }
        }

        if (_linksHeader != null)
            _linksHeader.Text = $"HUB I/O · {depositCount} in · {withdrawCount} out";
    }

    private void AddLinkRow(VBoxContainer? list, string text, bool connected)
    {
        if (list == null) return;
        var lbl = new Label { Text = text };
        if (!connected) lbl.Modulate = new Color(0.6f, 0.6f, 0.65f);
        list.AddChild(lbl);
    }

    private void UpdateTransfers(bool isTransport)
    {
        if (_transfersSection != null) _transfersSection.Visible = isTransport;
        ClearChildren(_transfersList);
        if (!isTransport || _transfersList == null) return;

        var mgr = FindBodyTransferManager();
        if (_manageRoutesButton != null) _manageRoutesButton.Disabled = mgr == null;
        if (mgr == null)
        {
            if (_transfersHeader != null) _transfersHeader.Text = "TRANSFERS · no manager";
            return;
        }

        var schedules = mgr.GetAllSchedules();
        var active = mgr.GetActiveTransfers();
        if (_transfersHeader != null)
            _transfersHeader.Text = $"TRANSFERS · {active.Count} active · {schedules.Count} scheduled";

        foreach (var schedule in schedules)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            string originShort = schedule.OriginBuildingId.Length >= 6
                ? schedule.OriginBuildingId[..6]
                : schedule.OriginBuildingId;
            var name = new Label
            {
                Text = $"hub {originShort} → {schedule.Destination}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            row.AddChild(name);
            var state = new Label { Text = schedule.State.ToString() };
            state.Modulate = schedule.State == TransferScheduleState.Idle
                ? new Color(0.6f, 0.6f, 0.65f)
                : new Color(0.29f, 0.65f, 0.32f);
            row.AddChild(state);
            _transfersList.AddChild(row);
        }
    }

    private void OnManageRoutesPressed()
    {
        if (_building == null) return;
        if (string.IsNullOrEmpty(_building.Id))
        {
            GameLogger.Warning("HubPanelDetails: building has no Id; cannot manage routes");
            return;
        }

        int continentIndex = _building.PrimaryCell?.ContinentIndex ?? -1;
        var (body, continent) = ResolveBodyAndContinent(continentIndex);
        if (body == null)
        {
            GameLogger.Warning("HubPanelDetails: failed to resolve body for transfer routes");
            return;
        }

        DispatchSlipsWindow.Instance?.ShowWindow(_building, body, continent);
    }

    private (Node3D? body, Continent? continent) ResolveBodyAndContinent(int continentIndex)
    {
        Node? cursor = _building?.VisualNode;
        while (cursor != null)
        {
            switch (cursor)
            {
                case CelestialBody cb:
                    return (cb, cb.Mesh?.GetContinent(continentIndex));
                case SatelliteBody sb:
                    return (sb, sb.Mesh?.GetContinent(continentIndex));
            }
            cursor = cursor.GetParent();
        }
        return (null, null);
    }

    private BodyTransferManager? FindBodyTransferManager()
    {
        var tree = GetTree();
        if (tree == null) return null;
        foreach (var node in tree.Root.GetChildren())
            if (TryFindManager(node, out var mgr)) return mgr;
        return null;
    }

    private static bool TryFindManager(Node node, out BodyTransferManager? mgr)
    {
        if (node is BodyTransferManager m) { mgr = m; return true; }
        foreach (var child in node.GetChildren())
            if (TryFindManager(child, out mgr)) return true;
        mgr = null;
        return false;
    }

    private string GetStorageCategory(BuildingDefinition? definition)
    {
        if (definition?.SlotFilters != null && definition.SlotFilters.Count > 0)
        {
            foreach (var spec in definition.SlotFilters)
            {
                if (spec.Filter.Kind == Structures.Logistics.SlotFilterKind.Category && spec.Filter.Category != null)
                    return spec.Filter.Category;
            }
        }
        return "any";
    }

    private static void ClearChildren(Node? container)
    {
        if (container == null) return;
        foreach (var child in container.GetChildren()) child.QueueFree();
    }
}
