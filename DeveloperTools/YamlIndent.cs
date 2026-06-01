#if DEBUG
using System.Text;

namespace DeveloperTools;

/// <summary>
/// Provides calculated space-based indentation for YAML emission.
/// YAML standard forbids tabs for indentation; this helper ensures
/// all output uses spaces computed from indent level and step size.
/// </summary>
public static class YamlIndent
{
    /// <summary>Number of spaces per indent level (YAML convention: 2).</summary>
    public const int Step = 2;

    private static readonly string[] _cache = new string[16];

    /// <summary>
    /// Returns a string of <paramref name="level"/> × <see cref="Step"/> spaces.
    /// Results are cached for repeated use.
    /// </summary>
    public static string Of(int level)
    {
        if (level < 0) level = 0;
        if (level < _cache.Length && _cache[level] != null)
            return _cache[level];

        string result = new string(' ', level * Step);

        if (level < _cache.Length)
            _cache[level] = result;

        return result;
    }

    /// <summary>
    /// Appends a line with <paramref name="level"/>-deep indentation.
    /// Equivalent to <c>sb.Append(Of(level)).AppendLine(content)</c>.
    /// </summary>
    public static void AppendLine(StringBuilder sb, int level, string content)
    {
        sb.Append(Of(level)).AppendLine(content);
    }

    /// <summary>
    /// Appends a line with <paramref name="level"/>-deep indentation
    /// and a single interpolated value. Avoids allocating a formatted
    /// string when only one substitution is needed.
    /// </summary>
    public static void AppendLine(StringBuilder sb, int level, string prefix, string value)
    {
        sb.Append(Of(level)).Append(prefix).AppendLine(value);
    }
}
#endif
