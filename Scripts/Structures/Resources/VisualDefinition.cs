using Godot;
using Structures.Enums;
using UtilityLibrary;

namespace Structures.Resources;

/// <summary>
/// Defines visual representation settings for game entities.
/// Shared between Buildings, Ships, Stations, Resources, and Recipes.
/// </summary>
public class VisualDefinition
{
    // ========== 3D Model Properties ==========

    /// <summary>Path to 3D model resource (for reference/debugging).</summary>
    public string? ModelPath { get; set; }

    /// <summary>Pre-loaded PackedScene prototype (loaded during configuration).</summary>
    public PackedScene? ModelPrototype { get; set; }

    /// <summary>Path to material resource.</summary>
    public string? ModelMaterial { get; set; }

    /// <summary>Path to animation resource.</summary>
    public string? AnimationPath { get; set; }

    /// <summary>Scale factor for the model.</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>Rotation offset in degrees (Euler angles).</summary>
    public Vector3 RotationOffset { get; set; } = Vector3.Zero;

    // ========== 2D Icon Properties ==========

    /// <summary>Path to 2D icon texture (for UI/inventory displays).</summary>
    public string? IconPath { get; set; }

    /// <summary>Pre-loaded icon texture (loaded during configuration).</summary>
    public Texture2D? IconTexture { get; set; }

    /// <summary>Size multiplier for UI icon display relative to standard size.</summary>
    public float IconScale { get; set; } = 1.0f;

    /// <summary>Tint color for the icon. White = no tint.</summary>
    public Color IconTint { get; set; } = Colors.White;

    /// <summary>Target display size category for this icon.</summary>
    public IconSize IconSize { get; set; } = IconSize.Medium;

    // ========== 2D Board Shape Properties ==========

    /// <summary>
    /// Polygon shape used to render this entity on the 2D PlanetBoard.
    /// One of: hexagon, square, rectangle, pentagon, triangle. Default: hexagon.
    /// </summary>
    public string Shape { get; set; } = "hexagon";

    /// <summary>Board-space radius (circumscribed) of the shape in pixels.</summary>
    public float ShapeSize { get; set; } = 64f;

    /// <summary>Fill color of the shape on the board.</summary>
    public Color ShapeColor { get; set; } = new Color(0.30f, 0.45f, 0.60f, 1f);

    // ========== Helper Properties ==========

    /// <summary>
    /// Returns true if a valid model prototype is available.
    /// </summary>
    public bool HasValidPrototype => ModelPrototype != null && ModelPrototype.CanInstantiate();

    /// <summary>
    /// Returns true if a valid icon texture is available.
    /// </summary>
    public bool HasValidIcon => IconTexture != null;

    /// <summary>
    /// Creates a new instance of the model from the prototype.
    /// Returns null if no prototype is available.
    /// Caller must add the returned node to the scene tree.
    /// </summary>
    /// <param name="bodyRadius">The radius of the parent body for scaling calculations. Default is 1.0 for no body-relative scaling.</param>
    public Node3D? CreateModelInstance(float bodyRadius = 1.0f)
    {
        if (!HasValidPrototype)
        {
            GameLogger.Debug(
                $"VisualDefinition: No valid prototype available for model '{ModelPath}'"
            );
            return null;
        }

        try
        {
            var instance = ModelPrototype!.Instantiate<Node3D>();
            // Apply body-relative scaling: yamlScale * bodyRadius * 0.5
            // This makes buildings proportionally sized to the celestial body
            instance.Scale = Vector3.One * Scale * Mathf.Log(bodyRadius);
            instance.RotationDegrees = RotationOffset;

            GameLogger.Debug(
                $"VisualDefinition: Created model instance from prototype '{ModelPath}' with body scale (radius: {bodyRadius})"
            );
            return instance;
        }
        catch (System.Exception ex)
        {
            GameLogger.Error(
                $"VisualDefinition: Failed to instantiate model '{ModelPath}': {ex.Message}"
            );
            return null;
        }
    }

    // ========== Icon Helper Methods ==========

    /// <summary>
    /// Gets the icon texture for UI display.
    /// Returns null if no icon is configured (use fallback).
    /// </summary>
    /// <returns>The icon texture, or null if not available.</returns>
    public Texture2D? GetIcon() => IconTexture;

    /// <summary>
    /// Gets the effective icon size in pixels, accounting for scale.
    /// </summary>
    /// <returns>A Vector2 containing the scaled width and height.</returns>
    public Vector2 GetIconDimensions()
    {
        int baseSize = IconSize.GetPixels();
        float scaled = baseSize * IconScale;
        return new Vector2(scaled, scaled);
    }
}
