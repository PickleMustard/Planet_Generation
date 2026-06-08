using Godot;
using UtilityLibrary;

namespace Structures.Resources;

/// <summary>
/// Defines visual representation settings for game entities.
/// Shared between Buildings, Ships, Stations, Resources, and Recipes.
/// </summary>
public class VisualDefinition
{
    // ========== 3D Model Properties ==========

    /// <summary>Path to the model wrapper resource (<c>ModelConfig</c> <c>.tres</c>).</summary>
    public string? ModelResourcePath { get; set; }

    /// <summary>Pre-loaded PackedScene prototype (loaded during configuration).</summary>
    public PackedScene? ModelPrototype { get; set; }

    /// <summary>Path to material resource.</summary>
    public string? ModelMaterial { get; set; }

    /// <summary>Path to animation resource.</summary>
    public string? AnimationPath { get; set; }

    /// <summary>Name of a specific animation clip on the loaded model's AnimationPlayer.</summary>
    public string? AnimationName { get; set; }

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

    // ========== 2D Board Shape Properties ==========

    /// <summary>
    /// Identifier of the <see cref="BuildingShape2D"/> in
    /// <c>BuildingShape2DDatabase</c> used to render this entity on the 2D
    /// PlanetBoard. Default: "hexagon".
    /// </summary>
    public string ShapeId { get; set; } = "hexagon";

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
    /// <param name="scaleWithBody">When true (buildings), the model is sized proportionally to <paramref name="bodyRadius"/>. When false (ships, stations), a flat <see cref="Scale"/> multiplier is used regardless of orbit/body.</param>
    public Node3D? CreateModelInstance(float bodyRadius = 1.0f, bool scaleWithBody = true)
    {
        if (!HasValidPrototype)
        {
            GameLogger.Debug(
                $"VisualDefinition: No valid prototype available for model '{ModelResourcePath}'"
            );
            return null;
        }

        try
        {
            var instance = ModelPrototype!.Instantiate<Node3D>();
            // Buildings scale proportionally to the celestial body; ships/stations use a
            // flat Scale multiplier so they look the same wherever they orbit.
            instance.Scale = scaleWithBody
                ? Vector3.One * Scale * Mathf.Log(bodyRadius)
                : Vector3.One * Scale;
            instance.RotationDegrees = RotationOffset;

            GameLogger.Debug(
                $"VisualDefinition: Created model instance from prototype '{ModelResourcePath}' with body scale (radius: {bodyRadius})"
            );
            return instance;
        }
        catch (System.Exception ex)
        {
            GameLogger.Error(
                $"VisualDefinition: Failed to instantiate model '{ModelResourcePath}': {ex.Message}"
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
    /// Uses the source texture's pixel size when available; otherwise falls back
    /// to a default base size since Godot mipmaps handle runtime resizing.
    /// </summary>
    /// <returns>A Vector2 containing the scaled width and height.</returns>
    public Vector2 GetIconDimensions()
    {
        const float defaultBaseSize = 128f;
        Vector2 baseSize = IconTexture != null
            ? IconTexture.GetSize()
            : new Vector2(defaultBaseSize, defaultBaseSize);
        return baseSize * IconScale;
    }
}
