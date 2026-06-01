using Godot;
using UtilityLibrary;

namespace UI.FarFieldIndicators;

/// <summary>
/// Individual far-field icon UI component that displays a body's billboard texture and name.
/// Attached to the FarFieldIcon.tscn scene.
/// </summary>
public partial class FarFieldIcon : Control
{
    [ExportCategory("UI References")]
    [Export]
    public TextureRect? IconTextureRect { get; set; }

    [Export]
    public Label? NameLabel { get; set; }

    private string _bodyName = string.Empty;

    public override void _Ready()
    {
        GameLogger.EnterFunction(nameof(_Ready), $"FarFieldIcon: {_bodyName}");

        // Ensure we have references
        if (IconTextureRect == null)
        {
            IconTextureRect = GetNode<TextureRect>("IconTextureRect");
        }

        if (NameLabel == null)
        {
            NameLabel = GetNode<Label>("NameLabel");
        }

        // Initial state - hidden until properly configured
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;

        GameLogger.ExitFunction(nameof(_Ready));
    }

    /// <summary>
    /// Configures the icon with the body's display name and initial texture.
    /// </summary>
    public void Setup(string bodyName, Texture2D? iconTexture)
    {
        _bodyName = bodyName;

        if (NameLabel != null)
        {
            NameLabel.Text = bodyName;
        }

        if (IconTextureRect != null && iconTexture != null)
        {
            IconTextureRect.Texture = iconTexture;
        }

        GameLogger.Debug($"FarFieldIcon.Setup: Configured for body '{bodyName}'");
    }

    /// <summary>
    /// Updates the displayed texture.
    /// </summary>
    public void UpdateTexture(Texture2D? texture)
    {
        if (IconTextureRect != null && texture != null)
        {
            IconTextureRect.Texture = texture;
        }
    }

    /// <summary>
    /// Updates the screen position of the icon.
    /// </summary>
    public void UpdatePosition(Vector2 screenPosition)
    {
        // Center the icon on the position
        Vector2 size = Size;
        Position = screenPosition - (size / 2);
    }

    /// <summary>
    /// Shows or hides the icon.
    /// </summary>
    public new void SetVisible(bool visible)
    {
        Visible = visible;
    }
}
