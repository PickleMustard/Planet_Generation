namespace Structures.Resources;

/// <summary>
/// Shared formatting helpers for resource display strings. Centralizes the snake_case →
/// Title Case conversion that was previously duplicated across UI components.
/// </summary>
public static class ResourceNameFormatter
{
    /// <summary>Turns a snake_case resource id into a title-cased display label.</summary>
    public static string Prettify(string id)
    {
        if (string.IsNullOrEmpty(id)) return "Unknown";
        var words = id.Split('_');
        for (int i = 0; i < words.Length; i++)
            if (words[i].Length > 0)
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
        return string.Join(" ", words);
    }
}
