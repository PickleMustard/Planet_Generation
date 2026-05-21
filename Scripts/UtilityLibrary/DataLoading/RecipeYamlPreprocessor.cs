using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Rewrites recipe YAML text so empty-key slot entries like `- : 5` survive
/// YamlDotNet parsing. Each empty key is swapped for a unique placeholder
/// (<see cref="EmptyKeyPrefix"/>{n}), and the original line numbers are
/// returned for downstream validator messaging. Callers strip the placeholder
/// back to "" via <see cref="IsEmptyKeyPlaceholder(string)"/>.
/// </summary>
internal static class RecipeYamlPreprocessor
{
    public const string EmptyKeyPrefix = "__empty_key_";

    private static readonly Regex _emptyKeyLine = new(
        @"^(?<indent>\s*-\s*):(?<rest>\s+.*)$",
        RegexOptions.Compiled);

    public readonly record struct Result(string Text, List<int> EmptyKeyLineNumbers);

    public static Result Preprocess(string yamlText)
    {
        var lineNumbers = new List<int>();
        if (string.IsNullOrEmpty(yamlText))
            return new Result(yamlText ?? "", lineNumbers);

        var sb = new StringBuilder(yamlText.Length + 32);
        var lines = yamlText.Split('\n');
        int counter = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            var match = _emptyKeyLine.Match(line);
            if (match.Success)
            {
                counter++;
                lineNumbers.Add(i + 1);
                sb.Append(match.Groups["indent"].Value)
                  .Append('"').Append(EmptyKeyPrefix).Append(counter).Append("\":")
                  .Append(match.Groups["rest"].Value);
            }
            else
            {
                sb.Append(line);
            }
            if (i < lines.Length - 1)
                sb.Append('\n');
        }

        return new Result(sb.ToString(), lineNumbers);
    }

    public static bool IsEmptyKeyPlaceholder(string? key)
        => !string.IsNullOrEmpty(key) && key!.StartsWith(EmptyKeyPrefix);
}
