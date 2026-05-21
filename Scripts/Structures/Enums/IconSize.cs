using Godot;

namespace Structures.Enums;

/// <summary>
/// Standard icon sizes for UI display.
/// Icons are generated at the largest size (512) and downscaled as needed.
/// Large uses the base name with no suffix; Medium adds "_med"; Small adds "_small".
/// Off deactivates icon display entirely.
/// </summary>
public enum IconSize
{
    /// <summary>
    /// 64x64 pixels - UI lists, tooltips, compact displays. File suffix: "_small".
    /// </summary>
    Small = 64,

    /// <summary>
    /// 128x128 pixels - Standard UI panels, inventory slots. File suffix: "_med".
    /// </summary>
    Medium = 128,

    /// <summary>
    /// 512x512 pixels - Detail views, zoomed displays, high-DPI. No suffix (base name).
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
    /// Returns 0 for Off.
    /// </summary>
    /// <param name="size">The icon size.</param>
    /// <returns>The pixel dimension as an integer.</returns>
    public static int GetPixels(this IconSize size) => (int)size;

    /// <summary>
    /// Gets the default icon size for general UI use (Large / full).
    /// </summary>
    public static IconSize Default => IconSize.Large;

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
    /// Converts a string to an IconSize.
    /// "off" → Off, unknown → Large (full).
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The corresponding IconSize, or Large if parsing fails.</returns>
    public static IconSize Parse(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "small" => IconSize.Small,
            "medium" => IconSize.Medium,
            "large" => IconSize.Large,
            _ => IconSize.Large
        };
    }

    /// <summary>
    /// Gets the file suffix for an icon size.
    /// Large → "" (base name only), Medium → "_med", Small → "_small", Off → "".
    /// </summary>
    /// <param name="size">The icon size.</param>
    /// <returns>The file suffix string (empty for Large and Off).</returns>
    public static string GetSuffix(this IconSize size) => size switch
    {
        IconSize.Small => "_small",
        IconSize.Medium => "_med",
        IconSize.Large => "",
        _ => ""
    };
}
