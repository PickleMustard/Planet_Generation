using System.Collections.Generic;
using System.Text;
using Constructables;
using Constructables.Stations.Behaviors;
using Godot;
using Logistics.Resources;
using Structures.Logistics;
using UtilityLibrary;

namespace UI.StationWindow;

/// <summary>
/// Full shipyard management panel hosted by <see cref="StationShipyardPanel"/> when
/// the station has a <see cref="ShipyardBehavior"/>. Shows active build slots,
/// build queue, a "Start Construction" button that opens the StationWindow's
/// sidebar (via <see cref="SignalBus.StationSidebarRequested"/>), and a 3D ghost
/// preview of the selected ship.
/// </summary>
public partial class ShipyardPanel : VBoxContainer
{
    [Export]
    private Label? _headerLabel;

    [Export]
    private HBoxContainer? _slotsContainer;

    [Export]
    private VBoxContainer? _queueSection;

    [Export]
    private VBoxContainer? _queueContainer;

    [Export]
    private Button? _startConstructionButton;

    [Export]
    private SubViewportContainer? _ghostViewportContainer;

    [Export]
    private SubViewport? _ghostViewport;

    [Export]
    private Camera3D? _ghostCamera;

    [Export]
    private Node3D? _ghostModelRoot;

    [Export]
    private PackedScene? _slotCardScene;
    private static readonly PackedScene QueueRowScene =
        GD.Load<PackedScene>("res://UI/StationWindow/ShipyardQueueRow.tscn");

    private StationSatellite? _station;
    private ShipyardBehavior? _shipyard;
    private LogisticsUnit? _selectedSlot;

    private readonly List<ShipyardSlotCard> _slotCards = new();
    private float _refreshTimer;
    private const float REFRESH_INTERVAL = 0.5f;

    public override void _Ready()
    {
        if (_startConstructionButton != null)
            _startConstructionButton.Pressed += OnStartConstructionPressed;

        if (SignalBus.Instance != null)
            SignalBus.Instance.StationSidebarItemSelected += OnSidebarItemSelected;
    }

    public override void _ExitTree()
    {
        if (_startConstructionButton != null)
            _startConstructionButton.Pressed -= OnStartConstructionPressed;

        if (SignalBus.Instance != null)
            SignalBus.Instance.StationSidebarItemSelected -= OnSidebarItemSelected;
    }

    public override void _Process(double delta)
    {
        if (_station == null || _shipyard == null)
            return;

        _refreshTimer += (float)delta;
        if (_refreshTimer >= REFRESH_INTERVAL)
        {
            _refreshTimer = 0f;
            RefreshDisplay();
        }
    }

    /// <summary>
    /// Binds this panel to a station's ShipyardBehavior and populates the UI.
    /// </summary>
    public void Bind(StationSatellite station)
    {
        _station = station;
        _shipyard = station.GetBehavior<ShipyardBehavior>();

        if (_shipyard == null)
        {
            GameLogger.Warning("ShipyardPanel: Station has no ShipyardBehavior");
            return;
        }

        UpdateHeader();
        UpdateSlotCards();
        UpdateQueue();
    }

    /// <summary>
    /// Clears all data and removes children.
    /// </summary>
    public void Clear()
    {
        _station = null;
        _shipyard = null;
        _selectedSlot = null;
        ClearSlotCards();
        ClearQueue();
        ClearGhostPreview();

        if (_headerLabel != null)
            _headerLabel.Text = "Shipyard";

        if (_startConstructionButton != null)
            _startConstructionButton.Disabled = true;
    }

    private void UpdateHeader()
    {
        if (_headerLabel != null && _shipyard != null)
        {
            int active = _shipyard.ActiveShipBuildCount;
            int max = _shipyard.MaxParallelShipBuilds;
            _headerLabel.Text = $"Shipyard - Slots: {active}/{max}";
        }
    }

    private void UpdateSlotCards()
    {
        if (_slotsContainer == null || _shipyard == null || _station == null)
            return;

        ClearSlotCards();

        var activeBuilds = _shipyard.GetActiveBuilds();
        int maxSlots = _shipyard.MaxParallelShipBuilds;

        for (int i = 0; i < maxSlots; i++)
        {
            ShipyardSlotCard card;
            if (_slotCardScene != null)
            {
                card = _slotCardScene.Instantiate<ShipyardSlotCard>();
            }
            else
            {
                card = new ShipyardSlotCard();
            }

            _slotsContainer.AddChild(card);

            LogisticsUnit? ship = i < activeBuilds.Count ? activeBuilds[i] : null;
            card.Bind(ship, _shipyard, i);
            card.SlotSelected += OnSlotCardSelected;

            _slotCards.Add(card);
        }

        // Enable start construction button only if there are available slots or queue room
        if (_startConstructionButton != null)
        {
            bool hasEmptySlot = activeBuilds.Count < maxSlots;
            _startConstructionButton.Disabled = !hasEmptySlot;
        }
    }

    private void UpdateQueue()
    {
        if (_queueContainer == null || _queueSection == null || _shipyard == null)
            return;

        ClearQueue();

        var queued = _shipyard.GetShipBuildQueue();

        if (queued.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "No ships in queue",
            };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            emptyLabel.AddThemeFontSizeOverride("font_size", 13);
            _queueContainer.AddChild(emptyLabel);
            return;
        }

        for (int i = 0; i < queued.Count; i++)
        {
            var ship = queued[i];
            var row = CreateQueueEntry(ship, i);
            _queueContainer.AddChild(row);
        }
    }

    private HBoxContainer CreateQueueEntry(LogisticsUnit ship, int index)
    {
        var row = QueueRowScene.Instantiate<HBoxContainer>();
        row.GetNode<Label>("NameLabel").Text = ship.ShipDef?.Name ?? ship.Name;
        row.GetNode<Label>("StatusLabel").Text = _shipyard?.GetBuildStatus(ship) ?? "Queued";

        var upButton = row.GetNode<Button>("UpButton");
        upButton.Visible = index > 0;
        upButton.Pressed += () =>
        {
            if (_shipyard == null || !IsInstanceValid(ship)) return;
            var queue = _shipyard.GetShipBuildQueue();
            if (index > 0 && index < queue.Count)
                _shipyard.ReorderQueue(ship, queue[index - 1]);
            RefreshDisplay();
        };

        row.GetNode<Button>("DownButton").Pressed += () =>
        {
            if (_shipyard == null || !IsInstanceValid(ship)) return;
            _shipyard.ReorderQueue(ship, null); // null = move to end
            RefreshDisplay();
        };

        row.GetNode<Button>("CancelButton").Pressed += () =>
        {
            if (_shipyard == null || !IsInstanceValid(ship)) return;
            _shipyard.CancelShipConstruction(ship);
            RefreshDisplay();
        };

        return row;
    }

    private void RefreshDisplay()
    {
        if (_station == null || _shipyard == null) return;
        UpdateHeader();

        // Refresh slot card data without rebuilding
        var activeBuilds = _shipyard.GetActiveBuilds();
        int maxSlots = _shipyard.MaxParallelShipBuilds;

        // If slot count changed, rebuild
        if (_slotCards.Count != maxSlots)
        {
            UpdateSlotCards();
        }
        else
        {
            for (int i = 0; i < _slotCards.Count; i++)
            {
                LogisticsUnit? ship = i < activeBuilds.Count ? activeBuilds[i] : null;
                _slotCards[i].Bind(ship, _shipyard, i);
            }
        }

        UpdateQueue();
    }

    private void OnStartConstructionPressed()
    {
        EmitShipyardSidebarData();
    }

    /// <summary>
    /// Builds the list of constructable ships from ShipDatabase and emits
    /// <see cref="SignalBus.StationSidebarRequested"/> so the StationWindow
    /// shows its sidebar independently.
    /// </summary>
    private void EmitShipyardSidebarData()
    {
        var db = ShipDatabase.Instance;
        if (db == null || !db.IsLoaded)
        {
            GameLogger.Warning("ShipyardPanel: ShipDatabase not loaded — cannot open sidebar");
            return;
        }

        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var kvp in db.GetAllShips())
        {
            var ship = kvp.Value;
            var resBuilder = new StringBuilder();
            bool first = true;
            foreach (var res in ship.RequiredResources)
            {
                if (!first) resBuilder.Append(", ");
                resBuilder.Append($"{res.Value} {res.Key}");
                first = false;
            }

            var dict = new Godot.Collections.Dictionary
            {
                { "id", ship.Name },
                { "name", ship.Name },
                { "mass", $"{ship.DryMass:F0} kg" },
                { "time", $"{ship.WorkRequired:F0} work" },
                { "resources", resBuilder.Length > 0 ? resBuilder.ToString() : "(none)" },
            };
            items.Add(dict);
        }

        SignalBus.Instance?.EmitStationSidebarRequested("Construct Ship", items);
    }

    /// <summary>
    /// Handles <see cref="SignalBus.StationSidebarItemSelected"/> when the user
    /// picks a ship from the sidebar. Enqueues construction via ShipyardBehavior
    /// and dismisses the sidebar.
    /// </summary>
    private void OnSidebarItemSelected(string shipDefinitionName)
    {
        if (_station == null || _shipyard == null) return;

        var db = ShipDatabase.Instance;
        if (db == null || !db.TryGetShip(shipDefinitionName, out var definition) || definition == null)
        {
            GameLogger.Warning($"ShipyardPanel: Ship definition '{shipDefinitionName}' not found");
            return;
        }

        IOrbitalBody? orbitalBody = null;
        var parentNode = _station.GetParent();
        if (parentNode != null)
        {
            var bodyNode = parentNode.GetParent();
            if (bodyNode is IOrbitalBody ob)
                orbitalBody = ob;
            else if (parentNode is IOrbitalBody ob2)
                orbitalBody = ob2;
        }

        if (orbitalBody == null)
        {
            GameLogger.Warning("ShipyardPanel: Cannot determine orbital body for ship construction");
            return;
        }

        GameLogger.Info($"[ShipyardPanel] Enqueuing ship '{shipDefinitionName}' at station '{_station.Name}'");

        _shipyard.CreateAndEnqueueShip(orbitalBody, _station.BandIndex, definition);

        // Dismiss the sidebar after selection
        SignalBus.Instance?.EmitStationSidebarDismissed();

        // Schedule a refresh so the new ship appears in slots/queue
        GetTree().CreateTimer(0.5).Timeout += RefreshDisplay;
    }

    private void OnSlotCardSelected(int slotIndex)
    {
        if (_shipyard == null) return;
        var activeBuilds = _shipyard.GetActiveBuilds();
        if (slotIndex < activeBuilds.Count && IsInstanceValid(activeBuilds[slotIndex]))
        {
            _selectedSlot = activeBuilds[slotIndex];
            UpdateGhostPreview(_selectedSlot.ShipDef);
        }
        else
        {
            _selectedSlot = null;
            ClearGhostPreview();
        }
    }

    private void UpdateGhostPreview(ShipDefinition? definition)
    {
        if (_ghostModelRoot == null || _ghostCamera == null)
            return;

        // Clear existing model
        foreach (var child in _ghostModelRoot.GetChildren())
            child.QueueFree();

        if (definition?.Visual == null)
            return;

        var modelInstance = definition.Visual.CreateModelInstance(1.0f);
        if (modelInstance == null)
            return;

        // Apply translucent material to all mesh instances
        modelInstance.Scale = Vector3.One * 0.8f;
        ApplyGhostMaterial(modelInstance);

        _ghostModelRoot.AddChild(modelInstance);

        // Frame camera on model
        FrameGhostCamera(modelInstance);
    }

    private void ClearGhostPreview()
    {
        if (_ghostModelRoot != null)
        {
            foreach (var child in _ghostModelRoot.GetChildren())
                child.QueueFree();
        }
    }

    private void ApplyGhostMaterial(Node3D model)
    {
        var ghostMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.8f, 1.0f, 0.5f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        foreach (var child in model.GetChildren())
        {
            if (child is MeshInstance3D meshInstance)
            {
                int surfaceCount = meshInstance.GetSurfaceOverrideMaterialCount();
                for (int i = 0; i < surfaceCount; i++)
                    meshInstance.SetSurfaceOverrideMaterial(i, ghostMat);
            }
            else if (child is Node3D node3D)
            {
                ApplyGhostMaterial(node3D);
            }
        }

        // Also apply to the model itself if it's a MeshInstance3D
        if (model is MeshInstance3D mesh)
        {
            int surfaceCount = mesh.GetSurfaceOverrideMaterialCount();
            for (int i = 0; i < surfaceCount; i++)
                mesh.SetSurfaceOverrideMaterial(i, ghostMat);
        }
    }

    private void FrameGhostCamera(Node3D model)
    {
        if (_ghostCamera == null) return;

        // Position camera to look at the model from above-front
        _ghostCamera.Position = new Vector3(0, 1.5f, 2.5f);
        _ghostCamera.LookAt(Vector3.Zero, Vector3.Up);
    }

    private void ClearSlotCards()
    {
        foreach (var card in _slotCards)
        {
            if (IsInstanceValid(card))
            {
                card.SlotSelected -= OnSlotCardSelected;
                card.QueueFree();
            }
        }
        _slotCards.Clear();
    }

    private void ClearQueue()
    {
        if (_queueContainer == null) return;
        foreach (var child in _queueContainer.GetChildren())
            child.QueueFree();
    }
}