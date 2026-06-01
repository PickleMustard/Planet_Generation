using Godot;
using UI.Construction;

namespace UI.CommandLayer;

/// <summary>
/// Root of the persistent command layer. Always visible (a sibling of the GUI
/// HSM, like ToastSystem), so the command panel — and the construction menu it
/// opens — stay on screen across HUD and Orbital Window states. Owns the
/// command panel buttons, the docked <see cref="ConstructionMenuController"/>,
/// and the <see cref="PlacementOverlayController"/>, which it creates in code.
/// </summary>
public partial class CommandLayerController : Control
{
    [Export]
    public Button? _constructButton;

    [Export]
    public ConstructionMenuController? _menu;

    [Export]
    public PlacementOverlayController? _overlay;

    public override void _Ready()
    {
        _menu!.CardActivated += OnCardActivated;

        _constructButton!.Pressed += OnConstructPressed;
    }

    private void OnConstructPressed() => _menu?.Toggle();

    private void OnCardActivated(string itemType, string definitionName)
    {
        _overlay?.Begin(itemType, definitionName);
    }
}
