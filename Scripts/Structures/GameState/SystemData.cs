using System.Collections.Generic;
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

    /// <summary>
    /// Boots the ManufactureTickEngine as soon as SystemData enters the scene tree
    /// (during GameScene._Ready, before any building can be placed). This guarantees
    /// ManufactureTickEngine.Instance is non-null for every Building.Register() call.
    /// </summary>
    public override void _Ready()
    {
        if (_engine == null)
            _engine = ManufactureTickEngine.Start();
        base._Ready();
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
}
