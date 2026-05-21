using System.Collections.Generic;
using Constructables;
using Constructables.Stations.Behaviors;
using Godot;
using Godot.Collections;
using Logistics.Resources;
using UtilityLibrary;

namespace UI.ConstructionYard;

/// <summary>
/// Bespoke sub-window for a station's ShipyardBehavior. Lists active builds and queued
/// ships, handles pause/cancel/reorder/start interactions, and stays in sync via
/// ConstructionManager signals. Instantiated by ShipQueueManagementState on enter
/// and freed on exit.
/// </summary>
public partial class ConstructionYardWindow : Control
{
    [Signal]
    public delegate void BackRequestedEventHandler();

    [Export]
    private Label? _titleLabel;

    [Export]
    private Label? _slotsLabel;

    [Export]
    private Button? _backButton;

    [Export]
    private Button? _startConstructionButton;

    [Export]
    private VBoxContainer? _activeList;

    [Export]
    private VBoxContainer? _queueList;

    [Export]
    private ShipPickerPanel? _shipPickerPanel;

    [Export]
    private PackedScene? _activeBuildRowScene;

    [Export]
    private PackedScene? _queuedShipRowScene;

    private StationSatellite? _station;
    private ShipyardBehavior? _yard;
    private readonly System.Collections.Generic.Dictionary<LogisticsUnit, ActiveBuildRow> _activeRows = new();
    private readonly System.Collections.Generic.Dictionary<LogisticsUnit, QueuedShipRow> _queuedRows = new();

    public override void _Ready()
    {
        if (_backButton != null)
            _backButton.Pressed += OnBackPressed;
        if (_startConstructionButton != null)
            _startConstructionButton.Pressed += OnStartConstructionPressed;
        if (_shipPickerPanel != null)
        {
            _shipPickerPanel.ShipSelected += OnShipPickerConfirmed;
            _shipPickerPanel.Cancelled += OnShipPickerCancelled;
            _shipPickerPanel.Visible = false;
        }
    }

    public override void _ExitTree()
    {
        if (_backButton != null)
            _backButton.Pressed -= OnBackPressed;
        if (_startConstructionButton != null)
            _startConstructionButton.Pressed -= OnStartConstructionPressed;
        if (_shipPickerPanel != null)
        {
            _shipPickerPanel.ShipSelected -= OnShipPickerConfirmed;
            _shipPickerPanel.Cancelled -= OnShipPickerCancelled;
        }
    }

    public void Bind(StationSatellite station)
    {
        _station = station;
        _yard = station.GetBehavior<ShipyardBehavior>();

        if (_titleLabel != null)
            _titleLabel.Text = $"Construction Yard — {station.Name}";

        var cm = ConstructionManager.Instance;
        if (cm != null)
        {
            cm.ShipConstructionInitialized += OnShipInitialized;
            cm.ShipConstructionCompleted += OnShipCompleted;
            cm.ShipConstructionCancelled += OnShipCancelled;
            cm.ConstructionProgressUpdated += OnProgressUpdated;
        }

        RefreshAll();
    }

    public void Unbind()
    {
        var cm = ConstructionManager.Instance;
        if (cm != null)
        {
            cm.ShipConstructionInitialized -= OnShipInitialized;
            cm.ShipConstructionCompleted -= OnShipCompleted;
            cm.ShipConstructionCancelled -= OnShipCancelled;
            cm.ConstructionProgressUpdated -= OnProgressUpdated;
        }

        ClearRows();
        _yard = null;
        _station = null;
    }

    private void ClearRows()
    {
        foreach (var row in _activeRows.Values)
            row.QueueFree();
        _activeRows.Clear();

        foreach (var row in _queuedRows.Values)
            row.QueueFree();
        _queuedRows.Clear();
    }

    private void RefreshAll()
    {
        if (_yard == null || _activeList == null || _queueList == null)
            return;

        var active = _yard.GetActiveBuilds();
        var queued = _yard.GetShipBuildQueue();

        ReconcileActive(active);
        ReconcileQueue(queued);

        if (_slotsLabel != null)
            _slotsLabel.Text = $"Slots: {active.Count}/{_yard?.MaxParallelBuilds ?? 0}";
    }

    private void ReconcileActive(IReadOnlyList<LogisticsUnit> ships)
    {
        if (_activeList == null || _activeBuildRowScene == null)
            return;

        var live = new HashSet<LogisticsUnit>(ships);
        var toRemove = new System.Collections.Generic.List<LogisticsUnit>();
        foreach (var kvp in _activeRows)
            if (!live.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        foreach (var ship in toRemove)
        {
            _activeRows[ship].QueueFree();
            _activeRows.Remove(ship);
        }

        for (int i = 0; i < ships.Count; i++)
        {
            var ship = ships[i];
            if (!_activeRows.TryGetValue(ship, out var row))
            {
                row = _activeBuildRowScene.Instantiate<ActiveBuildRow>();
                _activeList.AddChild(row);
                row.Bind(ship);
                row.PauseToggled += OnActivePauseToggled;
                row.CancelRequested += OnActiveCancelRequested;
                _activeRows[ship] = row;
            }
            else
            {
                row.Refresh();
            }
            _activeList.MoveChild(row, i);
        }
    }

    private void ReconcileQueue(IReadOnlyList<LogisticsUnit> ships)
    {
        if (_queueList == null || _queuedShipRowScene == null)
            return;

        var live = new HashSet<LogisticsUnit>(ships);
        var toRemove = new System.Collections.Generic.List<LogisticsUnit>();
        foreach (var kvp in _queuedRows)
            if (!live.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        foreach (var ship in toRemove)
        {
            _queuedRows[ship].QueueFree();
            _queuedRows.Remove(ship);
        }

        for (int i = 0; i < ships.Count; i++)
        {
            var ship = ships[i];
            if (!_queuedRows.TryGetValue(ship, out var row))
            {
                row = _queuedShipRowScene.Instantiate<QueuedShipRow>();
                _queueList.AddChild(row);
                row.Bind(ship, i);
                row.MoveRequested += OnQueueMoveRequested;
                row.CancelRequested += OnQueueCancelRequested;
                _queuedRows[ship] = row;
            }
            else
            {
                row.Bind(ship, i);
            }
            _queueList.MoveChild(row, i);
        }
    }

    private void OnShipInitialized(Dictionary details)
    {
        if (!IsMyShip(details, "ship"))
            return;
        RefreshAll();
    }

    private void OnShipCompleted(Dictionary details)
    {
        if (!IsMyShip(details, "ship"))
            return;
        RefreshAll();
    }

    private void OnShipCancelled(Dictionary details)
    {
        if (!IsMyShip(details, "ship"))
            return;
        RefreshAll();
    }

    private void OnProgressUpdated(string entityName, float progress, string status)
    {
        if (_station == null)
            return;
        foreach (var kvp in _activeRows)
        {
            if (kvp.Key.Name.ToString() == entityName)
            {
                kvp.Value.Refresh();
                return;
            }
        }
    }

    private bool IsMyShip(Dictionary details, string key)
    {
        if (_station == null)
            return false;
        if (!details.ContainsKey(key))
            return false;
        var node = details[key].As<Node>();
        return node is LogisticsUnit ship && ship.ConstructingStation == _station;
    }

    private void OnActivePauseToggled(LogisticsUnit ship)
    {
        if (_yard == null)
            return;
        _yard.SetShipPaused(ship, !ship.IsManuallyPaused);
        RefreshAll();
    }

    private void OnActiveCancelRequested(LogisticsUnit ship)
    {
        _yard?.CancelShipConstruction(ship);
    }

    private void OnQueueMoveRequested(LogisticsUnit moved, LogisticsUnit target, bool insertAfter)
    {
        if (_yard == null)
            return;

        var queue = _yard.GetShipBuildQueue();
        LogisticsUnit? before = null;
        int targetIdx = -1;
        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i] == target)
            {
                targetIdx = i;
                break;
            }
        }
        if (targetIdx < 0)
            return;

        if (insertAfter)
        {
            int nextIdx = targetIdx + 1;
            if (queue[targetIdx] == moved)
                return;
            if (nextIdx < queue.Count && queue[nextIdx] != moved)
                before = queue[nextIdx];
            else
                before = null;
        }
        else
        {
            before = target;
        }

        _yard.ReorderQueue(moved, before);
        RefreshAll();
    }

    private void OnQueueCancelRequested(LogisticsUnit ship)
    {
        _yard?.CancelShipConstruction(ship);
    }

    private void OnBackPressed()
    {
        EmitSignal(SignalName.BackRequested);
    }

    private void OnStartConstructionPressed()
    {
        if (_shipPickerPanel == null)
            return;
        _shipPickerPanel.Populate();
        _shipPickerPanel.Visible = true;
    }

    private void OnShipPickerConfirmed(string shipDefinitionName, string shipName)
    {
        if (_shipPickerPanel != null)
            _shipPickerPanel.Visible = false;

        if (_yard == null || _station == null)
            return;

        var db = ShipDatabase.Instance;
        if (db == null || !db.TryGetShip(shipDefinitionName, out var def) || def == null)
        {
            GameLogger.Warning($"ConstructionYardWindow: ship definition '{shipDefinitionName}' not found");
            return;
        }

        var targetBody = _station.GetParent()?.GetParent() as IOrbitalBody;
        if (targetBody == null)
        {
            GameLogger.Warning($"ConstructionYardWindow: could not resolve parent orbital body for station '{_station.Name}'");
            return;
        }

        try
        {
            ConstructionManager.Instance?.CreateLogisticsUnit(
                targetBody,
                _station.BandIndex,
                string.IsNullOrWhiteSpace(shipName) ? null : shipName,
                def,
                _station);
        }
        catch (System.Exception ex)
        {
            GameLogger.Warning($"ConstructionYardWindow: ship enqueue failed - {ex.Message}");
        }

        RefreshAll();
    }

    private void OnShipPickerCancelled()
    {
        if (_shipPickerPanel != null)
            _shipPickerPanel.Visible = false;
    }
}
