using Godot;
using Structures.GameState;
using UtilityLibrary;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Fires <see cref="SignalBus.GameStarted"/> exactly once per game instance, when
/// the owning building is registered. Attached only to the company headquarters
/// in YAML; placing the HQ is the commit point that "starts the game" — until
/// then, the procedurally generated system can be regenerated freely.
/// Self-guards via <see cref="SystemData.IsGameStarted"/>, so re-attachment or
/// late registrations cannot re-fire after the player has confirmed the
/// naming dialog.
/// </summary>
public partial class GameStartBehavior : IBuildingBehavior
{
    private Building? _owner;

    public Building? Owner => _owner;

    public bool WantsTick => false;

    public void OnAttach(Building owner) => _owner = owner;

    public void OnRegister()
    {
        if (_owner == null)
            return;

        var systemData = ResolveSystemData();
        if (systemData != null && systemData.IsGameStarted)
            return;

        SignalBus.Instance?.EmitGameStarted(_owner);
    }

    public void OnUnregister() { }

    public void OnDetach() { }

    public void OnManufactureTick(float delta, Building owner) { }

    private static SystemData? ResolveSystemData()
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
            return null;
        return tree.Root.GetNodeOrNull<SystemData>("GameScene/system_container/SystemData");
    }
}
