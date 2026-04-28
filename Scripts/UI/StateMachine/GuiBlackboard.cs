using Godot;
using ProceduralGeneration;
using Structures.GameState;

namespace UI.StateMachine;

/// <summary>
/// Blackboard class for shared GUI state data across state machine states.
/// Used by GuiHSM and GuiState to share contextual information about
/// the currently selected orbital body, cell, continent, and UI state.
/// </summary>
public partial class GuiBlackboard : RefCounted
{
    /// <summary>
    /// The currently selected orbital body (planet, moon, star, etc.)
    /// </summary>
    public IOrbitalBody? SelectedBody { get; set; }

    /// <summary>
    /// The currently selected Voronoi cell on the celestial body surface
    /// </summary>
    public VoronoiCell? SelectedCell { get; set; }

    /// <summary>
    /// The currently selected continent containing the selected cell
    /// </summary>
    public Continent? SelectedContinent { get; set; }

    /// <summary>
    /// The type of item currently being selected or manipulated in the UI
    /// </summary>
    public string CurrentItemType { get; set; } = string.Empty;

    /// <summary>
    /// The name of the current definition being used (building type, resource type, etc.)
    /// </summary>
    public string CurrentDefinitionName { get; set; } = string.Empty;

    /// <summary>
    /// The player's camera for 3D interactions (raycasts, orbital camera)
    /// </summary>
    public Camera3D? PlayerCamera { get; set; }

    /// <summary>
    /// The index of the currently selected continent (-1 if none)
    /// </summary>
    public int SelectedContinentIndex { get; set; } = -1;

    /// <summary>
    /// Clears all blackboard data, resetting to initial state.
    /// Called when deselecting or switching contexts.
    /// </summary>
    public void Clear()
    {
        SelectedBody = null;
        SelectedCell = null;
        SelectedContinent = null;
        CurrentItemType = string.Empty;
        CurrentDefinitionName = string.Empty;
        PlayerCamera = null;
        SelectedContinentIndex = -1;
    }
}
