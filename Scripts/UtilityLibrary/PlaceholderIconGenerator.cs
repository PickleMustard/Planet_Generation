using Godot;
using System.Collections.Generic;
using System.IO;

namespace UtilityLibrary;

/// <summary>
/// Generates placeholder SVG icons for development/testing.
/// Emits a single 512px SVG per entity; Godot mipmaps handle runtime scaling.
/// </summary>
public static class PlaceholderIconGenerator
{
    private const int PlaceholderPixels = 512;

    private static readonly Dictionary<string, Color> CategoryColors = new()
    {
        ["ore"] = new Color(0.54f, 0.27f, 0.07f),        // Brown
        ["raw_material"] = new Color(0.44f, 0.5f, 0.56f), // Slate
        ["fuel"] = new Color(0.9f, 0.1f, 0.1f),           // Red
        ["food"] = new Color(0.2f, 0.8f, 0.2f),           // Green
        ["electronic"] = new Color(0.1f, 0.6f, 0.9f),     // Blue
        ["industrial"] = new Color(0.6f, 0.6f, 0.6f),     // Gray
        ["construction"] = new Color(0.8f, 0.5f, 0.2f),   // Orange
        ["power"] = new Color(1.0f, 0.8f, 0.0f),          // Yellow
        ["extraction"] = new Color(0.4f, 0.2f, 0.1f),     // Dark brown
        ["agriculture"] = new Color(0.3f, 0.7f, 0.3f),    // Forest green
        ["headquarters"] = new Color(0.6f, 0.3f, 0.8f),   // Purple
        ["courier"] = new Color(0.2f, 0.6f, 0.8f),        // Sky blue
        ["transport"] = new Color(0.8f, 0.4f, 0.2f),      // Rust
        ["freighter"] = new Color(0.5f, 0.5f, 0.7f),      // Steel blue
        ["shipyard"] = new Color(0.7f, 0.5f, 0.3f),       // Bronze
        ["refinery"] = new Color(0.6f, 0.6f, 0.2f),       // Olive
        ["habitat"] = new Color(0.4f, 0.7f, 0.6f),        // Teal
        ["architect"] = new Color(0.8f, 0.3f, 0.5f),      // Magenta
    };

    /// <summary>
    /// Generates a placeholder icon at the given base path.
    /// </summary>
    /// <param name="entityName">Name of the entity (for initial and label)</param>
    /// <param name="category">Category for color selection</param>
    /// <param name="outputBasePath">Base path without extension (e.g., ".../iron_ore")</param>
    public static void GeneratePlaceholders(string entityName, string category, string outputBasePath)
    {
        string initial = string.IsNullOrEmpty(entityName) ? "?" : entityName[..1].ToUpper();
        Color bgColor = CategoryColors.GetValueOrDefault(category.ToLower(), new Color(0.5f, 0.5f, 0.5f));

        GeneratePlaceholderSvg(initial, bgColor, outputBasePath);

        GameLogger.Info($"Generated placeholder icon for {entityName}");
    }

    private static void GeneratePlaceholderSvg(string initial, Color bgColor, string outputBasePath)
    {
        const int pixels = PlaceholderPixels;
        string outputPath = $"{outputBasePath}.svg";

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string svg = $"<svg width=\"{pixels}\" height=\"{pixels}\" viewBox=\"0 0 {pixels} {pixels}\" xmlns=\"http://www.w3.org/2000/svg\">\n" +
            $"    <defs>\n" +
            $"        <linearGradient id=\"bg\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"100%\">\n" +
            $"            <stop offset=\"0%\" style=\"stop-color:{bgColor.Lightened(0.2f).ToHtml(false)};stop-opacity:1\" />\n" +
            $"            <stop offset=\"100%\" style=\"stop-color:{bgColor.ToHtml(false)};stop-opacity:1\" />\n" +
            $"        </linearGradient>\n" +
            $"    </defs>\n" +
            $"    <rect width=\"{pixels}\" height=\"{pixels}\" fill=\"url(#bg)\" rx=\"{pixels / 8}\" ry=\"{pixels / 8}\"/>\n" +
            $"    <rect x=\"{pixels / 32}\" y=\"{pixels / 32}\" width=\"{pixels - pixels / 16}\" height=\"{pixels - pixels / 16}\"\n" +
            $"          fill=\"none\" stroke=\"white\" stroke-width=\"{pixels / 64}\" rx=\"{pixels / 10}\" ry=\"{pixels / 10}\" opacity=\"0.3\"/>\n" +
            $"    <text x=\"{pixels / 2}\" y=\"{pixels / 2 + pixels / 6}\" font-family=\"Arial, sans-serif\" font-size=\"{pixels / 2}\"\n" +
            $"          font-weight=\"bold\" fill=\"white\" text-anchor=\"middle\" opacity=\"0.9\">{initial}</text>\n" +
            $"</svg>";

        File.WriteAllText(outputPath, svg);
    }

    /// <summary>
    /// Generates all placeholder icons for resources from YAML configs.
    /// </summary>
    public static void GenerateResourcePlaceholders(string categoriesDirectory)
    {
        // This would iterate through resource YAML files and generate icons
        // Implementation depends on specific project needs
        GameLogger.Info("Generating resource placeholders...");

        // Example generation:
        // GeneratePlaceholders("iron_ore", "ore", "res://Assets/Icons/Resources/ore/iron_ore");
        // GeneratePlaceholders("copper_ore", "ore", "res://Assets/Icons/Resources/ore/copper_ore");
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
