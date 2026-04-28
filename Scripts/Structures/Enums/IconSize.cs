using Godot;

namespace Structures.Enums;

/// <summary>
/// Standard icon sizes for UI display.
/// Icons are generated at the largest size (512) and downscaled as needed.
/// </summary>
public enum IconSize
{
    /// <summary>
    /// 64x64 pixels - UI lists, tooltips, compact displays.
    /// </summary>
    Small = 64,

    /// <summary>
    /// 128x128 pixels - Standard UI panels, inventory slots.
    /// </summary>
    Medium = 128,

    /// <summary>
    /// 512x512 pixels - Detail views, zoomed displays, high-DPI.
    /// </summary>
    Large = 512
}

/// <summary>
/// Extension methods for the IconSize enum.
/// </summary>
public static class IconSizeExtensions
{
    /// <summary>
    /// Gets the pixel dimension for an icon size.
    /// </summary>
    /// <param name="size">The icon size.</param>
    /// <returns>The pixel dimension as an integer.</returns>
    public static int GetPixels(this IconSize size) => (int)size;

    /// <summary>
    /// Gets the default icon size for general UI use.
    /// </summary>
    public static IconSize Default => IconSize.Medium;

    /// <summary>
    /// Converts an IconSize to a Godot Vector2 with equal width and height.
    /// </summary>
    /// <param name="size">The icon size.</param>
    /// <returns>A Vector2 where X and Y are both set to the pixel dimension.</returns>
    public static Vector2 ToVector2(this IconSize size)
    {
        float pixels = GetPixels(size);
        return new Vector2(pixels, pixels);
    }

    /// <summary>
    /// Converts a string to an IconSize. Returns Medium for unknown values.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The corresponding IconSize, or Medium if parsing fails.</returns>
    public static IconSize Parse(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "small" => IconSize.Small,
            "medium" => IconSize.Medium,
            "large" => IconSize.Large,
            _ => IconSize.Medium
        };
    }

    /// <summary>
    /// Gets the file suffix for an icon size (e.g., "_64", "_128", "_512").
    /// </summary>
    /// <param name="size">The icon size.</param>
    /// <returns>The file suffix string.</returns>
    public static string GetSuffix(this IconSize size) => $"_{(int)size}";
}
