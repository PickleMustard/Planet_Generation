using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Detail view for power generation buildings.
/// Displays the recipe, power generation capacity, and fuel consumption (if applicable).
/// </summary>
public partial class PowerDetails : BaseBuildingDetails
{
    private RecipeDisplay? _recipeDisplay;
    private Label? _powerOutputLabel;
    private Label? _efficiencyLabel;
    private VBoxContainer? _inputsContainer;

    private PackedScene? _resourceRateItemScene;

    public override void _Ready()
    {
        _recipeDisplay = GetNodeOrNull<RecipeDisplay>("VBoxContainer/RecipeDisplay");
        _powerOutputLabel = GetNodeOrNull<Label>("VBoxContainer/PowerOutputLabel");
        _efficiencyLabel = GetNodeOrNull<Label>("VBoxContainer/EfficiencyLabel");
        _inputsContainer = GetNodeOrNull<VBoxContainer>("VBoxContainer/InputsSection/InputsList");

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

        // Update recipe display
        if (_recipeDisplay != null)
        {
            _recipeDisplay.SetRecipe(recipe);
        }

        // Update power output
        if (_powerOutputLabel != null && _registration != null)
        {
            if (_registration.TheoreticalPowerGeneration > 0)
            {
                _powerOutputLabel.Text = $"⚡ Generates: {_registration.TheoreticalPowerGeneration:F0} kW";
                _powerOutputLabel.Modulate = new Color(0.2f, 0.8f, 0.3f); // Green
            }
            else if (_registration.TheoreticalPowerConsumption > 0)
            {
                _powerOutputLabel.Text = $"⚡ Consumes: {_registration.TheoreticalPowerConsumption:F0} kW";
                _powerOutputLabel.Modulate = new Color(0.9f, 0.3f, 0.2f); // Red
            }
            else
            {
                _powerOutputLabel.Text = "⚡ No Power";
                _powerOutputLabel.Modulate = Colors.White;
            }
        }

        // Update efficiency label
        if (_efficiencyLabel != null && _registration != null)
        {
            float efficiency = CalculateEfficiency();
            if (efficiency > 0)
            {
                _efficiencyLabel.Text = $"Efficiency: {efficiency:P0}";
                _efficiencyLabel.Visible = true;
            }
            else
            {
                _efficiencyLabel.Visible = false;
            }
        }

        // Update inputs section (fuel/resources)
        UpdateInputsSection(recipe);
    }

    public override void Clear()
    {
        base.Clear();

        if (_recipeDisplay != null)
        {
            _recipeDisplay.Clear();
        }

        if (_powerOutputLabel != null)
        {
            _powerOutputLabel.Text = "⚡ No Power";
            _powerOutputLabel.Modulate = Colors.White;
        }

        if (_efficiencyLabel != null)
        {
            _efficiencyLabel.Visible = false;
        }

        ClearContainer(_inputsContainer);
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

        bool hasNonPowerInputs = false;

        foreach (var kvp in recipe.InputResources)
        {
            if (kvp.Key == "power")
            {
                continue; // Skip power input, we show generation instead
            }

            hasNonPowerInputs = true;

            var item = _resourceRateItemScene.Instantiate<ResourceRateItem>();
            if (item != null)
            {
                float rate = _registration?.TheoreticalInputRates.TryGetValue(kvp.Key, out float r) == true ? r : 0f;
                item.SetResourceRate(kvp.Key, -rate);
                _inputsContainer.AddChild(item);
            }
        }

        // Show/hide inputs section based on whether we have any
        var inputsSection = _inputsContainer?.GetParent() as Control;
        if (inputsSection != null)
        {
            inputsSection.Visible = hasNonPowerInputs;
        }
    }

    private float CalculateEfficiency()
    {
        if (_registration == null || _building?.Definition?.Production == null)
        {
            return 0f;
        }

        float productionSpeed = _building.Definition.Production.ProductionSpeed;
        return productionSpeed; // Production speed acts as efficiency multiplier
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
}
