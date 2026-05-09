using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.Resources;

namespace UI.BuildingInfo;

/// <summary>
/// Modal popup that lists a building's defined recipes (default + alternatives) and
/// emits <see cref="RecipeSelected"/> when the player picks one. Mirrors the structure
/// of <see cref="UI.Construction.StationSelectionPopup"/>.
/// </summary>
public partial class RecipeSelectionPopup : PanelContainer
{
    [Signal]
    public delegate void RecipeSelectedEventHandler(string recipeId);

    [Signal]
    public delegate void PopupCancelledEventHandler();

    [Export]
    private ItemList _recipeList = null!;

    [Export]
    private Button _selectButton = null!;

    [Export]
    private Button _cancelButton = null!;

    [Export]
    private Label _emptyLabel = null!;

    private List<string> _recipeIds = new();
    private string? _activeRecipeId;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(400, 350);
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -200;
        OffsetRight = 200;
        OffsetTop = -175;
        OffsetBottom = 175;

        _recipeList.ItemSelected += OnItemSelected;
        _selectButton.Pressed += OnSelectPressed;
        _cancelButton.Pressed += OnCancelPressed;
    }

    /// <summary>
    /// Fills the list from the building's <see cref="BuildingDefinition.ProductionDefinition"/>.
    /// Each row stores its recipe id in metadata; the active recipe is marked and disabled.
    /// </summary>
    public void Populate(Building building)
    {
        _recipeList.Clear();
        _recipeIds.Clear();
        _selectButton.Disabled = true;
        _activeRecipeId = building.ActiveRecipeId;

        var production = building.Definition?.Production;
        if (production == null)
        {
            _emptyLabel.Visible = true;
            _recipeList.Visible = false;
            return;
        }

        var ordered = new List<string>();
        if (!string.IsNullOrEmpty(production.DefaultRecipe))
            ordered.Add(production.DefaultRecipe!);
        foreach (var alt in production.AlternativeRecipes)
        {
            if (!string.IsNullOrEmpty(alt) && !ordered.Contains(alt))
                ordered.Add(alt);
        }

        if (ordered.Count == 0)
        {
            _emptyLabel.Visible = true;
            _recipeList.Visible = false;
            return;
        }

        _emptyLabel.Visible = false;
        _recipeList.Visible = true;

        foreach (var recipeId in ordered)
        {
            RecipeDefinition? recipe = null;
            RecipeDatabase.Instance?.TryGetRecipe(recipeId, out recipe);

            string displayName = recipe?.DisplayName
                ?? recipe?.RecipeId
                ?? recipeId;
            bool isActive = recipeId == _activeRecipeId;
            string label = isActive ? $"[Active] {displayName}" : displayName;
            Texture2D? icon = recipe?.Icon?.SmallTexture;

            int idx = icon != null
                ? _recipeList.AddItem(label, icon)
                : _recipeList.AddItem(label);
            _recipeList.SetItemMetadata(idx, _recipeIds.Count);
            if (isActive)
                _recipeList.SetItemDisabled(idx, true);
            _recipeIds.Add(recipeId);
        }
    }

    private void OnItemSelected(long index)
    {
        _selectButton.Disabled = false;
    }

    private void OnSelectPressed()
    {
        var selected = _recipeList.GetSelectedItems();
        if (selected.Length == 0) return;

        int recipeIndex = _recipeList.GetItemMetadata(selected[0]).AsInt32();
        if (recipeIndex < 0 || recipeIndex >= _recipeIds.Count) return;

        EmitSignal(SignalName.RecipeSelected, _recipeIds[recipeIndex]);
    }

    private void OnCancelPressed()
    {
        EmitSignal(SignalName.PopupCancelled);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventKey keyEvent
            && keyEvent.Pressed
            && keyEvent.Keycode == Key.Escape)
        {
            OnCancelPressed();
            GetViewport().SetInputAsHandled();
        }
    }
}
