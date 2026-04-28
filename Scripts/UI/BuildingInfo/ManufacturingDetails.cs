using Godot;
using Godot.Collections;
using Structures.Resources;
using UI.Components;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Detail view for manufacturing buildings.
/// Displays the recipe, input resources with consumption rates, output resources with production rates,
/// and storage fill charts for each output.
/// </summary>
public partial class ManufacturingDetails : BaseBuildingDetails
{
    private RecipeDisplay? _recipeDisplay;
    private Label? _descriptionLabel;
    private VBoxContainer? _inputsContainer;
    private VBoxContainer? _outputsContainer;
    private HBoxContainer? _donutChartsContainer;

    private Label? _manufacturingStateLabel;
    private ProgressBar? _manufacturingProgressBar;

    private PackedScene? _resourceRateItemScene;

    public override void _Ready()
    {
        _recipeDisplay = GetNodeOrNull<RecipeDisplay>("HBoxContainer/LeftPanel/RecipeDisplay");
        _descriptionLabel = GetNodeOrNull<Label>("HBoxContainer/LeftPanel/DescriptionLabel");
        _inputsContainer = GetNodeOrNull<VBoxContainer>("HBoxContainer/RightPanel/InputsSection/InputsList");
        _outputsContainer = GetNodeOrNull<VBoxContainer>("HBoxContainer/RightPanel/OutputsSection/OutputsList");
        _donutChartsContainer = GetNodeOrNull<HBoxContainer>("HBoxContainer/RightPanel/DonutChartsHBox");

        var rightPanel = GetNodeOrNull<Control>("HBoxContainer/RightPanel");
        if (rightPanel != null)
        {
            var mfgBox = new VBoxContainer();
            
            _manufacturingStateLabel = new Label();
            _manufacturingStateLabel.Text = "State: Idle";
            _manufacturingStateLabel.HorizontalAlignment = HorizontalAlignment.Center;
            
            _manufacturingProgressBar = new ProgressBar();
            _manufacturingProgressBar.MinValue = 0;
            _manufacturingProgressBar.MaxValue = 1;
            _manufacturingProgressBar.CustomMinimumSize = new Vector2(0, 20);
            
            mfgBox.AddChild(_manufacturingStateLabel);
            mfgBox.AddChild(_manufacturingProgressBar);
            
            rightPanel.AddChild(mfgBox);
            rightPanel.MoveChild(mfgBox, 0); // Move to top of right panel
        }

        // Load the ResourceRateItem scene
        _resourceRateItemScene = ResourceLoader.Load<PackedScene>("res://UI/BuildingInfo/ResourceRateItem.tscn");
    }

    protected override void UpdateDisplay()
    {
        if (_building == null || _economy == null)
        {
            Clear();
            return;
        }

        var recipe = GetActiveRecipe();
        var definition = _building.Definition;

        if (_manufacturingStateLabel != null && _building.Manufacturing != null)
        {
            string stateText = $"State: {_building.Manufacturing.State}";
            if (_registration?.IsPaused == true)
                stateText += " (Paused - Power Deficit)";
            _manufacturingStateLabel.Text = stateText;
        }

        if (_manufacturingProgressBar != null && _building.Manufacturing != null)
        {
            if (_building.Manufacturing.WorkRequired > 0)
            {
                _manufacturingProgressBar.MaxValue = _building.Manufacturing.WorkRequired;
                _manufacturingProgressBar.Value = _building.Manufacturing.WorkProgress;
            }
            else
            {
                _manufacturingProgressBar.Value = 0;
            }
        }

        // Update recipe display
        if (_recipeDisplay != null)
        {
            _recipeDisplay.SetRecipe(recipe);
        }

        // Update description
        if (_descriptionLabel != null)
        {
            _descriptionLabel.Text = recipe?.Description ?? definition?.Description ?? "";
        }

        // Update inputs section
        UpdateInputsSection(recipe);

        // Update outputs section
        UpdateOutputsSection(recipe);
    }

    public override void Clear()
    {
        base.Clear();

        if (_recipeDisplay != null)
        {
            _recipeDisplay.Clear();
        }

        if (_descriptionLabel != null)
        {
            _descriptionLabel.Text = "";
        }

        ClearContainer(_inputsContainer);
        ClearContainer(_outputsContainer);
        ClearContainer(_donutChartsContainer);
    }

    private void UpdateInputsSection(RecipeDefinition? recipe)
    {
        ClearContainer(_inputsContainer);

        if (recipe?.InputResources == null || recipe.InputResources.Count == 0)
        {
            return;
        }

        if (_inputsContainer == null || _resourceRateItemScene == null)
        {
            return;
        }

        foreach (var kvp in recipe.InputResources)
        {
            if (kvp.Key == "power")
            {
                continue; // Power is handled separately
            }

            var item = _resourceRateItemScene.Instantiate<ResourceRateItem>();
            if (item != null)
            {
                float rate = _registration?.TheoreticalInputRates.TryGetValue(kvp.Key, out float r) == true ? r : 0f;
                item.SetResourceRate(kvp.Key, -rate); // Negative for consumption
                _inputsContainer.AddChild(item);
            }
        }
    }

    private void UpdateOutputsSection(RecipeDefinition? recipe)
    {
        ClearContainer(_outputsContainer);
        ClearContainer(_donutChartsContainer);

        if (recipe?.OutputResources == null || recipe.OutputResources.Count == 0)
        {
            return;
        }

        if (_outputsContainer == null || _resourceRateItemScene == null)
        {
            return;
        }

        foreach (var kvp in recipe.OutputResources)
        {
            if (kvp.Key == "power")
            {
                continue; // Power is handled separately
            }

            // Add rate item
            var item = _resourceRateItemScene.Instantiate<ResourceRateItem>();
            if (item != null)
            {
                float rate = _registration?.TheoreticalOutputRates.TryGetValue(kvp.Key, out float r) == true ? r : 0f;
                item.SetResourceRate(kvp.Key, rate);
                _outputsContainer.AddChild(item);
            }

            // Add storage donut chart for this output
            AddStorageDonutChart(kvp.Key);
        }
    }

    private void AddStorageDonutChart(string resourceId)
    {
        if (_donutChartsContainer == null)
        {
            return;
        }

        // Create a container for this resource's chart
        var chartContainer = new VBoxContainer();
        chartContainer.AddThemeConstantOverride("separation", 4);

        // Create the donut chart
        var chart = new DonutChart();
        chart.CustomMinimumSize = new Vector2(48, 48);
        chart.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        float amount = GetResourceStockpile(resourceId);
        string category = GetResourceCategory(resourceId);
        float categoryUsed = GetCategoryUsed(category);
        float categoryCapacity = GetCategoryCapacity(category);

        if (categoryCapacity > 0)
        {
            float fillRatio = Mathf.Clamp(categoryUsed / categoryCapacity, 0f, 1f);
            chart.Value = fillRatio;
        }

        // Create label
        var label = new Label();
        label.Text = FormatResourceName(resourceId);
        label.HorizontalAlignment = HorizontalAlignment.Center;

        chartContainer.AddChild(chart);
        chartContainer.AddChild(label);
        _donutChartsContainer.AddChild(chartContainer);
    }

    private void ClearContainer(Container? container)
    {
        if (container == null)
        {
            return;
        }

        foreach (var child in container.GetChildren())
        {
            child.QueueFree();
        }
    }

    private string GetResourceCategory(string resourceId)
    {
        var resourceDef = GetResourceDefinition(resourceId);
        if (resourceDef?.ResourceType != null)
        {
            return resourceDef.ResourceType;
        }

        if (resourceId.EndsWith("_ore"))
        {
            return "ore";
        }

        return "raw_material";
    }
}
