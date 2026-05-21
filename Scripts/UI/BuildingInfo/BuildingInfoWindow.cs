using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Modal frame that hosts a per-building-type detail panel routed by
/// <see cref="BuildingInfoPanel"/>. Provides backdrop dismissal, close/back chrome,
/// an action bar, and a shared recipe-selection popup.
/// </summary>
public partial class BuildingInfoWindow : Control
{
    public static BuildingInfoWindow? Instance { get; private set; }

    [Export] private Control? _blockInput;
    [Export] private Button? _closeButton;
    [Export] private Button? _backButton;
    [Export] private Button? _upgradeButton;
    [Export] private Button? _reconfigureButton;
    [Export] private Button? _planetBoardButton;
    [Export] private Button? _demolishButton;
    [Export] private BuildingInfoPanel? _panel;
    [Export] private RecipeSelectionPopup? _recipeSelectionPopup;

    private Building? _currentBuilding;
    private Building? _signalSubscribedBuilding;
    private RecipeDisplay? _connectedRecipeDisplay;

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void ManageRoutesRequestedEventHandler();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Ready()
    {
        Hide();

        if (_closeButton != null) _closeButton.Pressed += OnClosePressed;
        if (_backButton != null) _backButton.Pressed += OnBackPressed;
        if (_upgradeButton != null) _upgradeButton.Pressed += OnUpgradePressed;
        if (_reconfigureButton != null) _reconfigureButton.Pressed += OnReconfigurePressed;
        if (_planetBoardButton != null) _planetBoardButton.Pressed += OnPlanetBoardPressed;
        if (_demolishButton != null) _demolishButton.Pressed += OnDemolishPressed;

        if (_blockInput != null) _blockInput.GuiInput += OnBackdropInput;

        if (_recipeSelectionPopup != null)
        {
            _recipeSelectionPopup.RecipeSelected += OnRecipeSelected;
            _recipeSelectionPopup.PopupCancelled += OnRecipePopupCancelled;
            _recipeSelectionPopup.Hide();
        }

        GameLogger.Info("BuildingInfoWindow initialized");
    }

    public void ShowWindow(Building building)
    {
        UnsubscribeFromBuildingSignals();
        _currentBuilding = building;
        SubscribeToBuildingSignals(building);

        Show();
        Input.SetMouseMode(Input.MouseModeEnum.Visible);

        _panel?.SetBuilding(building);
        ConnectRecipeDisplayInPanel();

        GameLogger.Info($"BuildingInfoWindow shown for '{building.Name}'");
    }

    public void HideWindow()
    {
        Hide();
        GameLogger.Info("BuildingInfoWindow hidden");
    }

    public void Clear()
    {
        UnsubscribeFromBuildingSignals();
        DisconnectRecipeDisplay();
        _currentBuilding = null;
        _recipeSelectionPopup?.Hide();
        _panel?.Clear();
    }

    private void SubscribeToBuildingSignals(Building building)
    {
        building.OnRecipeChanged += OnBuildingRecipeChanged;
        _signalSubscribedBuilding = building;
    }

    private void UnsubscribeFromBuildingSignals()
    {
        if (_signalSubscribedBuilding != null)
        {
            _signalSubscribedBuilding.OnRecipeChanged -= OnBuildingRecipeChanged;
            _signalSubscribedBuilding = null;
        }
    }

    private void OnBuildingRecipeChanged(string newRecipeId)
    {
        if (_currentBuilding != null)
            _panel?.SetBuilding(_currentBuilding);
        ConnectRecipeDisplayInPanel();
    }

    private void ConnectRecipeDisplayInPanel()
    {
        DisconnectRecipeDisplay();
        if (_panel == null) return;
        _connectedRecipeDisplay = FindRecipeDisplay(_panel);
        if (_connectedRecipeDisplay != null)
            _connectedRecipeDisplay.RecipeClicked += OnRecipeClicked;
    }

    private void DisconnectRecipeDisplay()
    {
        if (_connectedRecipeDisplay != null)
        {
            _connectedRecipeDisplay.RecipeClicked -= OnRecipeClicked;
            _connectedRecipeDisplay = null;
        }
    }

    private static RecipeDisplay? FindRecipeDisplay(Node node)
    {
        if (node is RecipeDisplay rd) return rd;
        foreach (var child in node.GetChildren())
        {
            var found = FindRecipeDisplay(child);
            if (found != null) return found;
        }
        return null;
    }

    private void OnRecipeClicked()
    {
        if (_currentBuilding == null || _recipeSelectionPopup == null) return;
        if (_currentBuilding.GetBehavior<Constructables.Buildings.Behaviors.ExtractionBehavior>() != null)
            return;

        var mfg = _currentBuilding.GetBehavior<Constructables.Buildings.Behaviors.ManufacturingBehavior>();
        if (mfg == null) return;

        bool hasAny = !string.IsNullOrEmpty(mfg.DefaultRecipe)
            || mfg.AlternativeRecipes.Count > 0;
        if (!hasAny) return;

        _recipeSelectionPopup.Populate(_currentBuilding);
        _recipeSelectionPopup.Show();
    }

    private void OnRecipeSelected(string recipeId)
    {
        _currentBuilding?.SwapRecipe(recipeId);
        _recipeSelectionPopup?.Hide();
        if (_currentBuilding != null) _panel?.SetBuilding(_currentBuilding);
        ConnectRecipeDisplayInPanel();
    }

    private void OnRecipePopupCancelled()
    {
        _recipeSelectionPopup?.Hide();
    }

    private void OnClosePressed()
    {
        EmitSignal(SignalName.WindowCloseRequested);
    }

    private void OnBackPressed()
    {
        EmitSignal(SignalName.BackRequested);
    }

    private void OnUpgradePressed()
    {
        if (_currentBuilding == null) return;
        SignalBus.Instance?.EmitBuildingUpgradeRequested(0);
        GameLogger.Info($"BuildingInfoWindow: Upgrade requested for '{_currentBuilding.Name}'");
    }

    private void OnReconfigurePressed()
    {
        if (_currentBuilding == null) return;
        // Reuse the recipe-swap popup as the reconfigure entry point.
        OnRecipeClicked();
    }

    private void OnPlanetBoardPressed()
    {
        if (_currentBuilding?.VisualNode == null) return;
        Node? n = _currentBuilding.VisualNode;
        while (n != null && n is not IOrbitalBody)
            n = n.GetParentOrNull<Node>();
        if (n is CelestialBody body)
            SignalBus.Instance?.EmitOpenPlanetBoardRequested(body, "ResourceLink");
    }

    private void OnDemolishPressed()
    {
        if (_currentBuilding == null) return;
        SignalBus.Instance?.EmitBuildingDemolishRequested(0);
        GameLogger.Info($"BuildingInfoWindow: Demolish requested for '{_currentBuilding.Name}'");
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent
            && mouseEvent.ButtonIndex == MouseButton.Left
            && mouseEvent.Pressed)
        {
            EmitSignal(SignalName.WindowCloseRequested);
        }
    }
}
