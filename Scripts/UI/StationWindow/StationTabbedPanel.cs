using System.Collections.Generic;
using Constructables;
using Constructables.Stations;
using Constructables.Stations.Behaviors;
using Godot;

namespace UI.StationWindow;

/// <summary>
/// Behavior-driven tabbed panel. Builds one tab per composed <see cref="IStationBehavior"/> into a
/// native <see cref="TabContainer"/>, mirroring the building GUI
/// (<see cref="UI.BuildingInfo.Administration.AdministrationTabbedPanel"/>). The signature (★) tab is the
/// station's identity behavior (shipyard / architect, else an overview). Storage and Transfer tabs are
/// always present and pinned to the end, because every <see cref="StationSatellite"/> holds
/// <c>BulkStorage</c> and registers as a transfer endpoint regardless of attached behaviors.
/// </summary>
public partial class StationTabbedPanel : PanelContainer
{
    [Signal]
    public delegate void ItemSelectedEventHandler(string itemType, int itemIndex);

    [Export]
    public TabContainer? _tabContainer;

    private enum StationTabKind
    {
        Overview,
        Shipyard,
        Architect,
        Transfer,
        Storage,
    }

    private sealed class TabSpec
    {
        public StationTabKind Kind;
        public string Label = "";
        public IStationBehavior? Behavior;
        public bool ForceStorageGrid;
        public bool Signature;
    }

    private StationSatellite? _station;
    private readonly List<StationDetailPanel> _panels = new();

    public override void _Ready() { }

    public void Initialize(StationSatellite station)
    {
        Clear();
        _station = station;

        if (_tabContainer == null)
        {
            UtilityLibrary.GameLogger.Warning("StationTabbedPanel: no TabContainer configured");
            return;
        }

        foreach (var spec in BuildTabSpecs(station))
        {
            var panel = InstancePanel(spec);
            if (panel == null)
                continue;

            panel.Name = $"Tab_{spec.Kind}_{_panels.Count}";
            _tabContainer.AddChild(panel);
            int idx = _tabContainer.GetTabCount() - 1;
            _tabContainer.SetTabTitle(idx, (spec.Signature ? "★ " : "") + spec.Label);

            if (panel is StationArchitectPanel architect)
                architect.ItemSelected += OnNestedItemSelected;

            panel.SetStation(station, spec.Behavior);
            _panels.Add(panel);
        }

        if (_tabContainer.GetTabCount() > 0)
            _tabContainer.CurrentTab = 0;
    }

    public void Clear()
    {
        foreach (var panel in _panels)
        {
            if (!IsInstanceValid(panel))
                continue;
            if (panel is StationArchitectPanel architect)
                architect.ItemSelected -= OnNestedItemSelected;
            panel.Clear();
            if (panel.GetParent() != null)
                panel.GetParent().RemoveChild(panel);
            panel.QueueFree();
        }
        _panels.Clear();
        _station = null;
    }

    private static List<TabSpec> BuildTabSpecs(StationSatellite station)
    {
        var specs = new List<TabSpec>();
        var usedSpecial = new HashSet<StationTabKind>();

        // 1. Signature: the station's identity behavior (shipyard wins over architect).
        var shipyard = station.GetBehavior<ShipyardBehavior>();
        var architect = station.GetBehavior<OrbitalConstructorBehavior>();
        if (shipyard != null)
        {
            specs.Add(new TabSpec { Kind = StationTabKind.Shipyard, Label = "Shipyard", Behavior = shipyard, Signature = true });
            usedSpecial.Add(StationTabKind.Shipyard);
        }
        else if (architect != null)
        {
            specs.Add(new TabSpec { Kind = StationTabKind.Architect, Label = "Architect", Behavior = architect, Signature = true });
            usedSpecial.Add(StationTabKind.Architect);
        }

        // 2. Remaining behavior tabs, highest priority first. Storage/Transfer are deferred to the
        //    pinned end; shipyard/architect are deduped; everything else becomes a generic tab.
        var ordered = new List<IStationBehavior>(station.Behaviors);
        ordered.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        foreach (var behavior in ordered)
        {
            switch (MapBehaviorToTabKind(behavior))
            {
                case StationTabKind.Shipyard:
                    if (usedSpecial.Add(StationTabKind.Shipyard))
                        specs.Add(new TabSpec { Kind = StationTabKind.Shipyard, Label = "Shipyard", Behavior = behavior });
                    break;
                case StationTabKind.Architect:
                    if (usedSpecial.Add(StationTabKind.Architect))
                        specs.Add(new TabSpec { Kind = StationTabKind.Architect, Label = "Architect", Behavior = behavior });
                    break;
                case StationTabKind.Transfer:
                case StationTabKind.Storage:
                    break; // pinned at the end
                default:
                    // Generic / unknown behavior: one tab each when it opts into display.
                    if (behavior is IStationBehaviorDisplay display)
                        specs.Add(new TabSpec { Kind = StationTabKind.Overview, Label = display.TabLabel, Behavior = behavior });
                    break;
            }
        }

        // 3. Ensure a signature exists: synthetic Overview when nothing else featured.
        if (specs.Count == 0)
            specs.Add(new TabSpec { Kind = StationTabKind.Overview, Label = "Overview", Behavior = null, Signature = true });
        else if (!specs[0].Signature)
            specs[0].Signature = true;

        // 4. Always-present universal tabs, pinned in a consistent order at the end.
        specs.Add(new TabSpec
        {
            Kind = StationTabKind.Transfer,
            Label = "Transfers",
            Behavior = station.GetBehavior<TransferHubBehavior>(),
        });
        specs.Add(new TabSpec
        {
            Kind = StationTabKind.Storage,
            Label = "Storage",
            Behavior = station.GetBehavior<StorageHubBehavior>(),
            ForceStorageGrid = true,
        });

        return specs;
    }

    private static StationTabKind MapBehaviorToTabKind(IStationBehavior behavior) =>
        behavior switch
        {
            ShipyardBehavior => StationTabKind.Shipyard,
            OrbitalConstructorBehavior => StationTabKind.Architect,
            TransferHubBehavior => StationTabKind.Transfer,
            StorageHubBehavior => StationTabKind.Storage,
            _ => StationTabKind.Overview,
        };

    // Each tab's layout is authored as a .tscn; behavior lives in the matching script.
    private const string ShipyardScenePath = "res://UI/StationWindow/StationShipyardPanel.tscn";
    private const string ArchitectScenePath = "res://UI/StationWindow/StationArchitectPanel.tscn";
    private const string TransferScenePath = "res://UI/StationWindow/StationTransferPanel.tscn";
    private const string GenericScenePath = "res://UI/StationWindow/StationGenericPanel.tscn";

    private static StationDetailPanel? InstancePanel(TabSpec spec)
    {
        string path = spec.Kind switch
        {
            StationTabKind.Shipyard => ShipyardScenePath,
            StationTabKind.Architect => ArchitectScenePath,
            StationTabKind.Transfer => TransferScenePath,
            _ => GenericScenePath,
        };

        var scene = ResourceLoader.Load<PackedScene>(path);
        if (scene == null)
        {
            UtilityLibrary.GameLogger.Warning($"StationTabbedPanel: failed to load panel scene '{path}'");
            return null;
        }

        var panel = scene.Instantiate<StationDetailPanel>();
        if (panel is StationGenericPanel generic)
            generic.ForceStorageGrid = spec.ForceStorageGrid;
        return panel;
    }

    private void OnNestedItemSelected(string itemType, int itemIndex) =>
        EmitSignal(SignalName.ItemSelected, itemType, itemIndex);
}
