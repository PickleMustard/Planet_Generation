using Godot;
using Structures.Resources;
using UI.Components;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Detail view for extraction and agriculture buildings.
/// Displays the recipe, output resource, production rate, and storage fill.
/// </summary>
public partial class ExtractionDetails : BaseBuildingDetails
{
    private RecipeDisplay? _recipeDisplay;
    private Label? _descriptionLabel;
    private TextureRect? _outputIcon;
    private Label? _outputNameLabel;
    private Label? _rateLabel;
    private DonutChart? _storageChart;
    private Label? _yieldLabel;

    public override void _Ready()
    {
        _recipeDisplay = GetNodeOrNull<RecipeDisplay>("HBoxContainer/LeftPanel/RecipeDisplay");
        _descriptionLabel = GetNodeOrNull<Label>("HBoxContainer/LeftPanel/DescriptionLabel");
        _outputIcon = GetNodeOrNull<TextureRect>("HBoxContainer/RightPanel/OutputHBox/OutputIcon");
        _outputNameLabel = GetNodeOrNull<Label>("HBoxContainer/RightPanel/OutputHBox/OutputNameLabel");
        _rateLabel = GetNodeOrNull<Label>("HBoxContainer/RightPanel/RateLabel");
        _storageChart = GetNodeOrNull<DonutChart>("HBoxContainer/RightPanel/StorageChart");
        _yieldLabel = GetNodeOrNull<Label>("HBoxContainer/RightPanel/YieldLabel");
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

        // Update description
        if (_descriptionLabel != null)
        {
            _descriptionLabel.Text = recipe?.Description ?? definition?.Description ?? "";
        }

        // Get the primary output resource
        string? primaryOutputId = GetPrimaryOutputResource(recipe);

        if (primaryOutputId != null)
        {
            var resourceDef = GetResourceDefinition(primaryOutputId);

            // Update output resource display
            if (_outputNameLabel != null)
            {
                _outputNameLabel.Text = FormatResourceName(primaryOutputId);
            }

            if (_outputIcon != null)
            {
                _outputIcon.Texture = resourceDef?.Icon?.SmallTexture ?? resourceDef?.Icon?.MediumTexture;
            }

            // Update rate label
            if (_rateLabel != null && _registration != null)
            {
                float rate = _registration.TheoreticalOutputRates.TryGetValue(primaryOutputId, out float r) ? r : 0f;
                _rateLabel.Text = $"+{rate:F1} units/sec";
            }

            // Update storage fill chart
            UpdateStorageChart(primaryOutputId);
        }
        else
        {
            if (_outputNameLabel != null)
            {
                _outputNameLabel.Text = "No Output";
            }

            if (_rateLabel != null)
            {
                _rateLabel.Text = "-";
            }
        }

        // Update yield multiplier
        if (_yieldLabel != null && _registration != null)
        {
            if (_registration.DepositYieldMultiplier != 1.0f)
            {
                _yieldLabel.Text = $"Deposit Yield: {_registration.DepositYieldMultiplier:P0}";
                _yieldLabel.Visible = true;
            }
            else
            {
                _yieldLabel.Visible = false;
            }
        }
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

        if (_outputNameLabel != null)
        {
            _outputNameLabel.Text = "-";
        }

        if (_rateLabel != null)
        {
            _rateLabel.Text = "-";
        }

        if (_storageChart != null)
        {
            _storageChart.Value = 0f;
        }

        if (_yieldLabel != null)
        {
            _yieldLabel.Visible = false;
        }
    }

    private string? GetPrimaryOutputResource(RecipeDefinition? recipe)
    {
        if (recipe?.OutputResources == null || recipe.OutputResources.Count == 0)
        {
            return null;
        }

        // Return the first non-power output
        foreach (var kvp in recipe.OutputResources)
        {
            if (kvp.Key != "power")
            {
                return kvp.Key;
            }
        }

        return null;
    }

    private void UpdateStorageChart(string resourceId)
    {
        if (_storageChart == null)
        {
            return;
        }

        float amount = GetResourceStockpile(resourceId);
        string category = GetResourceCategory(resourceId);
        float categoryUsed = GetCategoryUsed(category);
        float categoryCapacity = GetCategoryCapacity(category);

        if (categoryCapacity > 0)
        {
            float fillRatio = Mathf.Clamp(categoryUsed / categoryCapacity, 0f, 1f);
            _storageChart.Value = fillRatio;
        }
        else
        {
            _storageChart.Value = 0f;
        }
    }

    private string GetResourceCategory(string resourceId)
    {
        var resourceDef = GetResourceDefinition(resourceId);
        if (resourceDef?.ResourceType != null)
        {
            return resourceDef.ResourceType;
        }

        // Fallback categorization
        if (resourceId.EndsWith("_ore"))
        {
            return "ore";
        }

        return "raw_material";
    }
}
