#if DEBUG
using System.Globalization;
using Godot;

namespace DeveloperTools.Common;

/// <summary>
/// Shared YAML scalar formatting helpers for developer editor writers
/// (ship / station / engine). Mirrors the float/quote/color conventions used by
/// ResourceEditorYamlIO and BuildingEditorYamlIO so output diffs stay consistent
/// across editors. All callers indent via <see cref="YamlIndent"/>.
/// </summary>
public static class EditorYamlScalar
{
    /// <summary>Whole numbers emit as integers; otherwise minimal decimals.</summary>
    public static string FormatFloat(float value)
    {
        if (Mathf.IsEqualApprox(value, Mathf.Round(value)))
            return ((int)value).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    public static string Escape(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// Quotes a scalar when it contains characters YAML would interpret, looks
    /// boolean/null, or starts with a digit/sigil. Bare alphanumerics stay unquoted.
    /// </summary>
    public static string QuoteIfNeeded(string s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        if (NeedsQuoting(s)) return $"\"{Escape(s)}\"";
        return s;
    }

    private static bool NeedsQuoting(string s)
    {
        char c0 = s[0];
        if (char.IsDigit(c0) || c0 == '-' || c0 == '?' || c0 == '*' || c0 == '&'
            || c0 == '[' || c0 == ']' || c0 == '{' || c0 == '}' || c0 == '#'
            || c0 == '|' || c0 == '>' || c0 == '!' || c0 == '%' || c0 == '@' || c0 == '`')
            return true;
        if (s is "true" or "false" or "null" or "~" or "True" or "False" or "Null"
            or "yes" or "no" or "on" or "off")
            return true;
        foreach (var c in s)
        {
            if (c == ':' || c == '#' || c == '"' || c == '\n' || c == '\r' || c == '\t')
                return true;
        }
        return false;
    }

    /// <summary>Emits an RGBA color as a flow-style float array "[r, g, b, a]".</summary>
    public static string Color(Color c) =>
        $"[{FormatFloat(c.R)}, {FormatFloat(c.G)}, {FormatFloat(c.B)}, {FormatFloat(c.A)}]";

    /// <summary>Emits a Vector3 as a flow-style float array "[x, y, z]".</summary>
    public static string Vector3(Vector3 v) =>
        $"[{FormatFloat(v.X)}, {FormatFloat(v.Y)}, {FormatFloat(v.Z)}]";
}
#endif
