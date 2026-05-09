using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Displays a recipe as a horizontal row with icon and name.
/// Used in building detail panels to show the active recipe.
/// </summary>
public partial class RecipeDisplay : HBoxContainer
{
    [Signal]
    public delegate void RecipeClickedEventHandler();

    private TextureRect? _iconRect;
    private Label? _nameLabel;
    private bool _enabled = true;

    public override void _Ready()
    {
        _iconRect = GetNodeOrNull<TextureRect>("RecipeIcon");
        _nameLabel = GetNodeOrNull<Label>("RecipeNameLabel");
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!_enabled) return;
        if (@event is InputEventMouseButton mb
            && mb.Pressed
            && mb.ButtonIndex == MouseButton.Left)
        {
            EmitSignal(SignalName.RecipeClicked);
            AcceptEvent();
        }
    }

    /// <summary>
    /// Toggles click handling and visually dims the row when disabled.
    /// Used to disable recipe-swap UI for buildings whose recipe is not user-selectable
    /// (e.g., extraction buildings using a synthetic cell-derived recipe).
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        Modulate = enabled ? Colors.White : new Color(1f, 1f, 1f, 0.5f);
    }

    /// <summary>
    /// Sets the recipe to display.
    /// </summary>
    public void SetRecipe(RecipeDefinition? recipe)
    {
        if (recipe == null)
        {
            Clear();
            return;
        }

        // Set recipe name
        if (_nameLabel != null)
        {
            _nameLabel.Text = recipe.DisplayName ?? FormatRecipeName(recipe.RecipeId ?? "Unknown Recipe");
        }

        // Load and set icon
        if (_iconRect != null)
        {
            Texture2D? icon = LoadRecipeIcon(recipe);
            _iconRect.Texture = icon;
        }
    }

    /// <summary>
    /// Clears the display to default state.
    /// </summary>
    public void Clear()
    {
        if (_nameLabel != null)
        {
            _nameLabel.Text = "No Recipe";
        }

        if (_iconRect != null)
        {
            _iconRect.Texture = null;
        }
    }

    private Texture2D? LoadRecipeIcon(RecipeDefinition recipe)
    {
        if (recipe.Icon?.IsValid == true)
        {
            return recipe.Icon.MediumTexture;
        }

        // Fallback: try to load from base path if defined
        if (!string.IsNullOrEmpty(recipe.Icon?.BasePath))
        {
            try
            {
                string mediumPath = recipe.Icon.BasePath + "_medium.png";
                return ResourceLoader.Load<Texture2D>(mediumPath);
            }
            catch
            {
                GameLogger.Debug($"RecipeDisplay: Failed to load icon for recipe '{recipe.RecipeId}'");
            }
        }

        return null;
    }

    private string FormatRecipeName(string? recipeId)
    {
        if (string.IsNullOrEmpty(recipeId))
        {
            return "Unknown Recipe";
        }

        // Convert "iron_mining" to "Iron Mining"
        var words = recipeId.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (!string.IsNullOrEmpty(words[i]) && words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
            }
        }
        return string.Join(" ", words);
    }
}
