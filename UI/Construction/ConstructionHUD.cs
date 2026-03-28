using Godot;

namespace UI.Construction;

public partial class ConstructionHUD : CanvasLayer
{
    public static ConstructionHUD? Instance { get; private set; }

    private Button _buildButton = null!;
    private ConstructionMenu _constructionMenu = null!;
    private ConstructionPlacementController _placementController = null!;

    public bool IsModalOpen =>
        _constructionMenu?.Visible == true
        || (_placementController?.CurrentState != PlacementState.Idle
            && _placementController != null);

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
        BuildToolbar();
        BuildConstructionMenu();
        BuildPlacementController();
    }

    private void BuildToolbar()
    {
        var toolbar = new HBoxContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = -60,
            OffsetRight = 60,
            OffsetTop = -50,
            OffsetBottom = -10,
        };
        AddChild(toolbar);

        _buildButton = new Button { Text = "Build" };
        _buildButton.Pressed += OnBuildButtonPressed;
        toolbar.AddChild(_buildButton);
    }

    private void BuildConstructionMenu()
    {
        _constructionMenu = new ConstructionMenu { Visible = false };
        _constructionMenu.ItemSelectedForPlacement += OnItemSelectedForPlacement;
        _constructionMenu.MenuClosed += OnMenuClosed;
        AddChild(_constructionMenu);
    }

    private void BuildPlacementController()
    {
        _placementController = new ConstructionPlacementController();
        _placementController.PlacementFinished += OnPlacementFinished;
        AddChild(_placementController);
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
