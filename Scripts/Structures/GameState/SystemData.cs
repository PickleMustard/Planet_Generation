using Godot;
using UtilityLibrary;

namespace Structures.GameState;

/// <summary>
/// Attached to the system_container Node to store system-level information.
/// This is the authoritative source for system-wide data.
/// </summary>
public partial class SystemData : Node
{
    [Export]
    public string SystemName { get; set; } = "Unnamed System";

    [Export]
    public string CompanyName { get; set; } = "Unnamed Company";

    [Export]
    public bool IsGameStarted { get; set; } = false;

    /// <summary>
    /// Called when the game officially starts (headquarters placed and named).
    /// </summary>
    public void StartGame(string companyName, string systemName)
    {
        CompanyName = companyName;
        SystemName = systemName;
        IsGameStarted = true;

        GameLogger.Info($"[SystemData] Game started: Company='{companyName}', System='{systemName}'");
    }
}
