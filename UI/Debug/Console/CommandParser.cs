#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;

namespace UI.Debug.Console;

/// <summary>
/// Parses raw input strings into structured command data with
/// namespace and argument extraction.
/// </summary>
public class CommandParser
{
    /// <summary>
    /// Represents a parsed command with its components.
    /// </summary>
    public class ParsedCommand
    {
        /// <summary>
        /// The namespace prefix (e.g., "CelestialBody.Earth").
        /// Null for global commands.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// The command name (e.g., "regenerate").
        /// </summary>
        public string CommandName { get; set; }

        /// <summary>
        /// Whether the namespace contains a wildcard.
        /// </summary>
        public bool HasWildcard { get; set; }

        /// <summary>
        /// The arguments passed to the command.
        /// </summary>
        public string[] Arguments { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The full raw input string.
        /// </summary>
        public string RawInput { get; set; }

        /// <summary>
        /// Gets the full command path including namespace.
        /// </summary>
        public string FullPath => string.IsNullOrEmpty(Namespace)
            ? CommandName
            : $"{Namespace}.{CommandName}";
    }

    /// <summary>
    /// Parses a raw input string into a structured command.
    /// </summary>
    /// <param name="input">The raw input string.</param>
    /// <returns>The parsed command data.</returns>
    public ParsedCommand Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ParsedCommand { RawInput = input };
        }

        input = input.Trim();
        var tokens = Tokenize(input);

        if (tokens.Count == 0)
        {
            return new ParsedCommand { RawInput = input };
        }

        var result = new ParsedCommand { RawInput = input };

        string firstToken = tokens[0];
        int lastDotIndex = firstToken.LastIndexOf('.');

        if (lastDotIndex > 0)
        {
            result.Namespace = firstToken.Substring(0, lastDotIndex);
            result.CommandName = firstToken.Substring(lastDotIndex + 1);
            result.HasWildcard = result.Namespace.Contains("*");
        }
        else
        {
            result.CommandName = firstToken;
        }

        if (tokens.Count > 1)
        {
            var args = new string[tokens.Count - 1];
            for (int i = 1; i < tokens.Count; i++)
            {
                args[i - 1] = tokens[i];
            }
            result.Arguments = args;
        }

        return result;
    }

    /// <summary>
    /// Tokenizes the input string, handling quoted strings.
    /// </summary>
    /// <param name="input">The input string to tokenize.</param>
    /// <returns>List of tokens.</returns>
    private List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var currentToken = new StringBuilder();
        bool inQuotes = false;
        bool escapeNext = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (escapeNext)
            {
                currentToken.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                }
                continue;
            }

            currentToken.Append(c);
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens;
    }

    /// <summary>
    /// Checks if the input appears to be a valid command format.
    /// </summary>
    /// <param name="input">The input to check.</param>
    /// <returns>True if the input looks like a valid command.</returns>
    public bool IsValidCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parsed = Parse(input);
        return !string.IsNullOrEmpty(parsed.CommandName);
    }

    /// <summary>
    /// Splits a namespace into its type and identifier parts.
    /// </summary>
    /// <param name="namespace">The namespace to split.</param>
    /// <returns>A tuple of (typeName, identifier).</returns>
    public (string typeName, string identifier) SplitNamespace(string @namespace)
    {
        if (string.IsNullOrEmpty(@namespace))
        {
            return (null, null);
        }

        int dotIndex = @namespace.IndexOf('.');
        if (dotIndex > 0)
        {
            return (
                @namespace.Substring(0, dotIndex),
                @namespace.Substring(dotIndex + 1)
            );
        }

        return (@namespace, null);
    }
}
#endif
