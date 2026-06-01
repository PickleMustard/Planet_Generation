using Constructables;
using Constructables.Stations;
using Godot;
using UI.Shared;

namespace UI.StationWindow;

/// <summary>
/// Generic, interface-driven station tab. Renders key/value rows from any behavior implementing
/// <see cref="IStationBehaviorDisplay"/>; with no behavior it shows the station overview. When the
/// behavior opts in via <see cref="IStationBehaviorDisplay.ShowStorageGrid"/> (or
/// <see cref="ForceStorageGrid"/> is set for the always-present Storage tab) it also renders the
/// station's bulk-storage slot grid, reusing the building <c>ResourceSlotItem</c> prefab.
/// Rows poll on the manufacture cadence; the storage grid refreshes reactively on
/// <see cref="Structures.Logistics.Storage.StorageUpdated"/>.
/// </summary>
public partial class StationGenericPanel : StationDetailPanel
{
    [Export]
    private VBoxContainer? _rows;

    [Export]
    private VBoxContainer? _storageBlock;

    [Export]
    private Label? _storageHeader;

    [Export]
    private GridContainer? _slotsGrid;

    [Export]
    private PackedScene? _resourceSlotItemScene;

    private int _refreshCounter;
    private bool _storageSubscribed;

    /// <summary>
    /// Forces the bulk-storage grid on even when the bound behavior is null — used by the
    /// always-present synthetic Storage tab when no <see cref="Behaviors.StorageHubBehavior"/> is attached.
    /// </summary>
    public bool ForceStorageGrid { get; set; }

    public override void _Ready()
    {
        // Layout lives in StationGenericPanel.tscn; this only handles the case where
        // SetStation ran before the node entered the tree.
        if (_station != null)
            UpdateDisplay();
    }

    public override void SetStation(StationSatellite station, IStationBehavior? behavior)
    {
        DetachStorage();
        _station = station;
        _behavior = behavior;

        if (ShouldShowGrid())
        {
            station.BulkStorage.StorageUpdated += OnStorageUpdated;
            _storageSubscribed = true;
        }

        UpdateDisplay();
    }

    public override void Clear()
    {
        DetachStorage();
        ClearChildren(_rows);
        ClearChildren(_slotsGrid);
        if (_storageBlock != null)
            _storageBlock.Visible = false;
        base.Clear();
    }

    public override void _ExitTree() => DetachStorage();

    public override void _PhysicsProcess(double delta)
    {
        if (!IsVisibleInTree() || _station == null)
            return;
        // Storage rows are static; only poll for live data (overview construction progress
        // or a behavior that wants ticks). The slot grid updates reactively, not here.
        bool wantsPoll = _behavior == null || _behavior.WantsTick;
        if (!wantsPoll)
            return;
        _refreshCounter++;
        if (_refreshCounter >= 10)
        {
            _refreshCounter = 0;
            RebuildRows();
        }
    }

    protected override void UpdateDisplay()
    {
        RebuildRows();
        RebuildGrid();
    }

    private bool ShouldShowGrid()
        => ForceStorageGrid || (_behavior as IStationBehaviorDisplay)?.ShowStorageGrid == true;

    private void RebuildRows()
    {
        if (_rows == null || _station == null)
            return;
        ClearChildren(_rows);

        if (_behavior == null)
        {
            AddSectionHeader(_rows, "Station Overview");
            AddInfoRow(_rows, "Name", _station.Name);
            AddInfoRow(_rows, "Type", _station.StationType ?? "—");
            AddInfoRow(_rows, "Band", _station.BandIndex >= 0 ? _station.BandIndex.ToString() : "—");
            AddInfoRow(_rows, "Active", _station.IsActive ? "Yes" : "No");

            if (_station.IsUnderConstruction)
            {
                AddSectionHeader(_rows, "Construction");
                AddInfoRow(_rows, "Status", _station.GetStatus());
                AddInfoRow(_rows, "Progress", $"{_station.GetProgress() * 100:F0}%");
                AddInfoRow(_rows, "Work", $"{_station.workDone:F1} / {_station.workRequired:F1}");
            }
            return;
        }

        if (_behavior is IStationBehaviorDisplay display)
        {
            AddSectionHeader(_rows, display.TabLabel);
            bool any = false;
            foreach (var (key, value) in display.GetDisplayRows())
            {
                AddInfoRow(_rows, key, value);
                any = true;
            }
            if (!any)
                AddEmptyLabel(_rows, "No details to display.");
        }
        else
        {
            AddSectionHeader(_rows, _behavior.GetType().Name.Replace("Behavior", ""));
            AddEmptyLabel(_rows, "No detail view configured for this behavior.");
        }
    }

    private void RebuildGrid()
    {
        bool show = ShouldShowGrid();
        if (_storageBlock != null)
            _storageBlock.Visible = show;
        if (!show || _station == null)
            return;

        var (occupied, total, used, _) = SummarizeStorage(_station.BulkStorage);
        if (_storageHeader != null)
            _storageHeader.Text = $"BULK SLOTS · {occupied} / {total} · {Mathf.RoundToInt(used)}u";
        SlotGrid.Populate(_slotsGrid, _station.BulkStorage, _resourceSlotItemScene);
    }

    private void OnStorageUpdated(string resourceId, int delta)
    {
        if (Engine.IsEditorHint())
            return;
        // StorageUpdated may fire from the manufacture tick thread — marshal to the main thread.
        Callable.From(RebuildGrid).CallDeferred();
    }

    private void DetachStorage()
    {
        if (_storageSubscribed && _station != null)
            _station.BulkStorage.StorageUpdated -= OnStorageUpdated;
        _storageSubscribed = false;
    }
}
