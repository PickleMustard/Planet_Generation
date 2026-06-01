using System.Collections.Generic;
using Constructables;
using Constructables.Tick;
using Godot;
using UtilityLibrary;

namespace Structures.GameState;

/// <summary>
/// Attached to the system_container Node to store system-level information.
/// This is the authoritative source for system-wide data and owns the lifetime of the
/// ManufactureTickEngine for the active game session.
/// </summary>
public partial class SystemData : Node
{
    [Export]
    public string SystemName { get; set; } = "Unnamed System";

    [Export]
    public string CompanyName { get; set; } = "Unnamed Company";

    [Export]
    public bool IsGameStarted { get; set; } = false;

    private ManufactureTickEngine? _engine;

    // Authoritative ship registry keyed by LogisticsUnit.Id. Replaces tree-walking
    // discovery (per-body SatellitesContainer scans, recursive system_container walks).
    // Single-threaded: mutations come only from LogisticsUnit._EnterTree/_ExitTree, which
    // Godot runs on the main thread. ThreadPooler never spawns or frees scene nodes.
    private readonly Dictionary<string, LogisticsUnit> _ships = new();

    /// <summary>
    /// Boots the ManufactureTickEngine as soon as SystemData enters the scene tree
    /// (during GameScene._Ready, before any building can be placed). This guarantees
    /// ManufactureTickEngine.Instance is non-null for every Building.Register() call.
    /// </summary>
    public override void _Ready()
    {
        if (_engine == null)
            _engine = ManufactureTickEngine.Start();

        // Backfill ships that were added to the system_container BEFORE this SystemData
        // attached (the save-load path adds ships under a LoadingScreen-owned staging
        // container; their _EnterTree register attempt finds no SystemData and silently
        // skips). Scan the parent recursively now that we're alive.
        BackfillShipRegistry();

        base._Ready();
    }

    private void BackfillShipRegistry()
    {
        var container = GetParent();
        if (container == null) return;
        int before = _ships.Count;
        BackfillRecursive(container);
        int added = _ships.Count - before;
        if (added > 0)
            GameLogger.Info($"[SystemData] Registry backfill: {added} ship(s) discovered (total: {_ships.Count}).");
    }

    private void BackfillRecursive(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is LogisticsUnit unit)
                RegisterShip(unit);
            BackfillRecursive(child);
        }
    }

    /// <summary>
    /// Flips the session into the "started" state once the player places and names
    /// the headquarters. Engine lifecycle is owned by _Ready / EndGame; this method
    /// only stamps the company/system names and the IsGameStarted flag.
    /// </summary>
    public void StartGame(string companyName, string systemName)
    {
        CompanyName = companyName;
        SystemName = systemName;
        IsGameStarted = true;

        GameLogger.Info($"[SystemData] Game started: Company='{companyName}', System='{systemName}'");
    }

    /// <summary>
    /// Tears down the manufacture tick engine. Call when leaving the game session
    /// (return to main menu, quit, etc.). _ExitTree provides a defensive backstop.
    /// </summary>
    public void EndGame()
    {
        _engine?.Stop();
        _engine = null;
        IsGameStarted = false;
    }

    /// <summary>
    /// TODO(save-load): On load completion, after rehydrating ContinentEconomy /
    /// StationEconomy instances and their Buildings, walk the loaded set and call
    /// engine.RegisterBatch(...) on both economies and buildings. Bypass
    /// ContinentEconomy.RegisterBuilding so storage capacity isn't double-added.
    /// </summary>
    public void RegisterLoadedTickables(IEnumerable<IManufactureTickable>? tickables)
    {
        if (tickables == null) return;
        if (_engine == null)
        {
            GameLogger.Warning("[SystemData] RegisterLoadedTickables called before _Ready; engine missing.");
            return;
        }
        _engine.RegisterBatch(tickables);
    }

    public override void _ExitTree()
    {
        EndGame();
        base._ExitTree();
    }

    // ---------------- Ship Registry ----------------

    /// <summary>Live count of registered LogisticsUnits.</summary>
    public int ShipCount => _ships.Count;

    /// <summary>
    /// Registers a LogisticsUnit by its <see cref="LogisticsUnit.Id"/>. Idempotent for the
    /// same instance. On Id collision with a different instance (save corruption), logs
    /// an error and replaces the existing entry — the displaced ship's later
    /// <c>_ExitTree</c> will not unregister the replacement (see <see cref="UnregisterShip"/>).
    /// </summary>
    public void RegisterShip(LogisticsUnit unit)
    {
        if (unit == null) return;
        if (string.IsNullOrEmpty(unit.Id))
        {
            GameLogger.Error($"[SystemData] RegisterShip rejected: ship '{unit.Name}' has empty Id.");
            return;
        }

        if (_ships.TryGetValue(unit.Id, out var existing))
        {
            if (ReferenceEquals(existing, unit))
                return; // idempotent
            GameLogger.Error($"[SystemData] Id collision on '{unit.Id}': replacing existing instance '{existing.Name}' with '{unit.Name}'.");
        }

        _ships[unit.Id] = unit;
    }

    /// <summary>
    /// Unregisters the given instance. Returns true when the entry was removed.
    /// Guards against ejecting a replacement entry: if the registry holds a different
    /// instance under the same Id, the call is a no-op.
    /// </summary>
    public bool UnregisterShip(LogisticsUnit unit)
    {
        if (unit == null || string.IsNullOrEmpty(unit.Id)) return false;
        if (_ships.TryGetValue(unit.Id, out var existing) && ReferenceEquals(existing, unit))
        {
            _ships.Remove(unit.Id);
            return true;
        }
        return false;
    }

    /// <summary>Tries to resolve a ship by its persisted Id.</summary>
    public bool TryGetShip(string id, out LogisticsUnit? unit)
    {
        if (string.IsNullOrEmpty(id))
        {
            unit = null;
            return false;
        }
        return _ships.TryGetValue(id, out unit);
    }

    /// <summary>
    /// Returns the live values collection. Callers iterating must not mutate the
    /// registry (Register/Unregister) during iteration; copy with <c>.ToArray()</c>
    /// when in doubt.
    /// </summary>
    public IReadOnlyCollection<LogisticsUnit> GetAllShips() => _ships.Values;

    /// <summary>
    /// Locates the SystemData attached to <c>system_container</c> by walking up from
    /// any Node already in the scene tree. Robust to scene-path differences; falls
    /// back to the absolute path when no <c>system_container</c> ancestor is found.
    /// </summary>
    public static SystemData? FindForNode(Node node)
    {
        if (node == null) return null;
        for (var n = node; n != null; n = n.GetParent())
        {
            if (n.Name == "system_container")
                return n.GetNodeOrNull<SystemData>("SystemData");
        }
        var tree = node.GetTree();
        return tree != null ? FindIn(tree) : null;
    }

    /// <summary>Resolves SystemData via the canonical absolute path.</summary>
    public static SystemData? FindIn(SceneTree tree)
    {
        return tree?.Root?.GetNodeOrNull<SystemData>("GameScene/system_container/SystemData");
    }
}
