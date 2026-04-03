using Godot;
using UI;

namespace UI.Construction;

public partial class ConstructionHUD : CanvasLayer
{
    public static ConstructionHUD? Instance { get; private set; }

    [Export]
    private Button _buildButton = null!;

    [Export]
    private ConstructionMenu _constructionMenu = null!;

    [Export]
    private ConstructionPlacementController _placementController = null!;

    public bool IsModalOpen =>
        _constructionMenu?.Visible == true
        || (_placementController != null
            && _placementController.CurrentState != PlacementState.Idle
            && _placementController.CurrentState != PlacementState.SelectingCell);

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        Layer = 10;
        
        // If ConstructionMenu is not set in the scene, instantiate it
        if (_constructionMenu == null)
        {
            var menuScene = GD.Load<PackedScene>("res://UI/Construction/ConstructionMenu.tscn");
            if (menuScene != null)
            {
                _constructionMenu = menuScene.Instantiate<ConstructionMenu>();
                AddChild(_constructionMenu);
            }
        }
        
        // Connect signals
        if (_buildButton != null)
            _buildButton.Pressed += OnBuildButtonPressed;
        
        if (_constructionMenu != null)
        {
            _constructionMenu.ItemSelectedForPlacement += OnItemSelectedForPlacement;
            _constructionMenu.MenuClosed += OnMenuClosed;
            _constructionMenu.Visible = false;
        }
        
        if (_placementController != null)
            _placementController.PlacementFinished += OnPlacementFinished;
    }

    private void OnBuildButtonPressed()
    {
        if (_placementController.CurrentState != PlacementState.Idle)
            return;

        _constructionMenu.Visible = !_constructionMenu.Visible;
        if (_constructionMenu.Visible)
        {
            Input.SetMouseMode(Input.MouseModeEnum.Visible);
        }
        else
        {
            Input.SetMouseMode(Input.MouseModeEnum.Captured);
        }
    }

    private void OnItemSelectedForPlacement(string itemType, string definitionName)
    {
        _constructionMenu.Visible = false;
        _placementController.BeginPlacement(itemType, definitionName);
    }

    private void OnMenuClosed()
    {
        if (_placementController.CurrentState == PlacementState.Idle)
        {
            Input.SetMouseMode(Input.MouseModeEnum.Captured);
        }
    }

    private void OnPlacementFinished()
    {
        Input.SetMouseMode(Input.MouseModeEnum.Captured);
    }
}