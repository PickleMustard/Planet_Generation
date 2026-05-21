#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;

namespace Debug.Console;

public enum SuggestionSource
{
    Command,
    Namespace,
    Alias,
    History,
    Argument,
    FilePath,
    PropertyPath,
}

public class Suggestion : IComparable<Suggestion>
{
    public string Text { get; }
    public SuggestionSource Source { get; }
    public int Priority { get; }
    public string? Description { get; set; }

    public Suggestion(
        string text,
        SuggestionSource source,
        int priority = 0,
        string? description = null
    )
    {
        Text = text;
        Source = source;
        Priority = priority;
        Description = description ?? string.Empty;
    }

    public int CompareTo(Suggestion? other)
    {
        if (other is null)
            return 1;
        int priorityCompare = other.Priority.CompareTo(Priority);
        if (priorityCompare != 0)
            return priorityCompare;

        int sourceCompare = Source.CompareTo(other.Source);
        if (sourceCompare != 0)
            return sourceCompare;

        return string.Compare(Text, other.Text, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() => Text;
}

public class AutocompleteEngine
{
    private readonly CommandRegistry _registry;
    private readonly CommandParser _parser = new();
    private readonly List<string> _commandHistory = new();
    private const int MaxHistorySize = 100;

    public AutocompleteEngine(CommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IEnumerable<Suggestion> GetSuggestions(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Enumerable.Empty<Suggestion>();
        }

        var suggestions = new List<Suggestion>();

        // Detect parenthesis context for namespace suggestions
        if (IsInsideParens(input, out string partialNs))
        {
            // User is typing inside (...) — suggest namespaces
            AddNamespaceSuggestions(suggestions, partialNs);
            AddHistorySuggestions(suggestions, input);
            return RankSuggestions(suggestions, input);
        }

        var parsed = _parser.Parse(input);

        if (parsed.HasNamespaces)
        {
            // After closing paren — suggest commands compatible with the namespace types
            string commandPartial = parsed.CommandName ?? "";
            bool hasPartialArg =
                input.TrimEnd().EndsWith(" ") && !string.IsNullOrEmpty(parsed.CommandName);

            if (hasPartialArg || parsed.Arguments.Length > 0)
            {
                // Suggesting arguments for a namespaced command
                string lastToken = GetLastToken(input);
                AddArgumentSuggestions(suggestions, parsed, lastToken);
            }
            else
            {
                // Suggest command names (filtered to target-requiring commands)
                AddNamespacedCommandSuggestions(suggestions, commandPartial, parsed);
            }
        }
        else
        {
            // Global command context
            if (string.IsNullOrEmpty(parsed.CommandName))
            {
                AddHistorySuggestions(suggestions, input);
                return RankSuggestions(suggestions, input);
            }

            bool hasPartialArg = input.EndsWith(" ") || parsed.Arguments.Length > 0;
            string lastToken = GetLastToken(input);

            if (
                hasPartialArg
                && !string.IsNullOrEmpty(parsed.CommandName)
                && _registry.HasCommand(parsed.CommandName)
            )
            {
                AddArgumentSuggestions(suggestions, parsed, lastToken);
            }
            else if (!hasPartialArg)
            {
                AddCommandSuggestions(suggestions, parsed, input);
            }
        }

        AddHistorySuggestions(suggestions, input);

        return RankSuggestions(suggestions, input);
    }

    /// <summary>
    /// Detects whether the user is currently typing inside an open parenthesis group.
    /// </summary>
    /// <param name="input">The current input text.</param>
    /// <param name="partialNamespace">The partial namespace being typed (after last comma or open paren).</param>
    /// <returns>True if inside parens (no closing paren yet).</returns>
    private bool IsInsideParens(string input, out string partialNamespace)
    {
        partialNamespace = "";
        int openParen = input.IndexOf('(');
        int closeParen = input.IndexOf(')');

        if (openParen >= 0 && closeParen < 0)
        {
            // Inside open parens
            string insideParens = input.Substring(openParen + 1);
            int lastComma = insideParens.LastIndexOf(',');
            partialNamespace =
                lastComma >= 0 ? insideParens.Substring(lastComma + 1).Trim() : insideParens.Trim();
            return true;
        }

        return false;
    }

    private string GetLastToken(string input)
    {
        var trimmed = input.TrimEnd();
        var lastSpace = trimmed.LastIndexOf(' ');
        return lastSpace >= 0 ? trimmed.Substring(lastSpace + 1) : trimmed;
    }

    /// <summary>
    /// Suggests global commands (no namespace prefix).
    /// </summary>
    private void AddCommandSuggestions(
        List<Suggestion> suggestions,
        CommandParser.ParsedCommand parsed,
        string input
    )
    {
        foreach (var cmd in _registry.GetAllCommands())
        {
            if (cmd.Name!.StartsWith(parsed.CommandName!, StringComparison.OrdinalIgnoreCase))
            {
                int priority = cmd.Name.Equals(
                    parsed.CommandName!,
                    StringComparison.OrdinalIgnoreCase
                )
                    ? 100
                    : 50;
                if (cmd.Name.Length == parsed.CommandName!.Length)
                {
                    priority = 100;
                }
                else
                {
                    priority =
                        50 + (10 - Math.Min(10, cmd.Name.Length - parsed.CommandName!.Length));
                }

                suggestions.Add(
                    new Suggestion(cmd.Name, SuggestionSource.Command, priority, cmd.Description!)
                );
            }
        }

        // Also suggest starting a namespace group if the user typed something that looks
        // like a namespace prefix (e.g., "Cel" could become "(CelestialBody.Earth)")
        if (!string.IsNullOrEmpty(parsed.CommandName) && !_registry.HasCommand(parsed.CommandName))
        {
            var namespaces = InstanceRegistry.GetNamespacesByPrefix(parsed.CommandName);
            foreach (var ns in namespaces.Take(10))
            {
                suggestions.Add(
                    new Suggestion($"({ns}) ", SuggestionSource.Namespace, 40, "Target instance")
                );
            }
        }
    }

    /// <summary>
    /// Suggests commands after a closing paren, filtered to commands that require a target.
    /// </summary>
    private void AddNamespacedCommandSuggestions(
        List<Suggestion> suggestions,
        string commandPartial,
        CommandParser.ParsedCommand parsed
    )
    {
        // Resolve the type of the first namespace to filter compatible commands
        Type? resolvedType = null;
        if (parsed.Namespaces.Count > 0)
        {
            string firstNs = parsed.Namespaces[0];
            if (
                !firstNs.Contains('*') && InstanceRegistry.TryGetInstance(firstNs, out var instance)
            )
            {
                resolvedType = instance?.GetType();
            }
        }

        foreach (var cmd in _registry.GetAllCommands().Where(c => c.RequiresTarget))
        {
            if (
                string.IsNullOrEmpty(commandPartial)
                || cmd.Name!.StartsWith(commandPartial, StringComparison.OrdinalIgnoreCase)
            )
            {
                // If we resolved the type, filter to compatible commands
                if (
                    resolvedType != null
                    && cmd.DeclaringType != null
                    && !cmd.DeclaringType.IsAssignableFrom(resolvedType)
                )
                {
                    continue;
                }

                int priority =
                    string.IsNullOrEmpty(commandPartial) ? 50
                    : cmd.Name!.Equals(commandPartial, StringComparison.OrdinalIgnoreCase) ? 100
                    : 50 + (10 - Math.Min(10, cmd.Name!.Length - commandPartial.Length));

                suggestions.Add(
                    new Suggestion(cmd.Name!, SuggestionSource.Command, priority, cmd.Description!)
                );
            }
        }
    }

    /// <summary>
    /// Suggests namespaces for use inside parentheses.
    /// </summary>
    private void AddNamespaceSuggestions(List<Suggestion> suggestions, string partialNamespace)
    {
        IEnumerable<string> namespaces;

        if (string.IsNullOrEmpty(partialNamespace))
        {
            namespaces = InstanceRegistry.GetAllNamespaces().Take(20);
        }
        else
        {
            namespaces = InstanceRegistry.GetNamespacesByPrefix(partialNamespace);
        }

        foreach (var ns in namespaces)
        {
            int priority = ns.Equals(partialNamespace, StringComparison.OrdinalIgnoreCase)
                ? 90
                : 40;
            suggestions.Add(new Suggestion(ns, SuggestionSource.Namespace, priority));
        }

        // Also suggest wildcard patterns
        if (!string.IsNullOrEmpty(partialNamespace) && !partialNamespace.Contains('*'))
        {
            // Extract type prefix (before the first dot)
            var dotIndex = partialNamespace.IndexOf('.');
            string typePrefix =
                dotIndex > 0 ? partialNamespace.Substring(0, dotIndex) : partialNamespace;

            // Check if there are multiple instances with this prefix
            var matchingNamespaces = InstanceRegistry.GetNamespacesByPrefix(typePrefix).ToList();
            if (matchingNamespaces.Count > 1)
            {
                suggestions.Add(
                    new Suggestion(
                        $"{typePrefix}.*",
                        SuggestionSource.Namespace,
                        35,
                        $"All {typePrefix} instances ({matchingNamespaces.Count})"
                    )
                );
            }
        }
    }

    private void AddArgumentSuggestions(
        List<Suggestion> suggestions,
        CommandParser.ParsedCommand parsed,
        string lastToken
    )
    {
        if (!_registry.TryGetCommand(parsed.CommandName!, out var command))
        {
            return;
        }

        var commandSuggestions = GetCommandArgumentSuggestions(
            command!,
            parsed.Arguments,
            lastToken
        );
        foreach (var suggestion in commandSuggestions)
        {
            suggestions.Add(new Suggestion(suggestion, SuggestionSource.Argument, 30));
        }

        if (
            command!.Usage != null
            && command.Usage!.Contains("path", StringComparison.OrdinalIgnoreCase)
        )
        {
            AddFilePathSuggestions(suggestions, lastToken);
        }

        if (command.Name == "get" || command.Name == "set")
        {
            AddPropertyPathSuggestions(suggestions, lastToken);
        }
    }

    private IEnumerable<string> GetCommandArgumentSuggestions(
        CommandInfo command,
        string[] args,
        string lastToken
    )
    {
        if (command.Name == "help")
        {
            return _registry
                .GetCommandNames()
                .Where(c => c.StartsWith(lastToken, StringComparison.OrdinalIgnoreCase));
        }

        if (command.Name == "list_namespaces")
        {
            var types = InstanceRegistry.GetAllInstances().Select(i => i.GetType().Name).Distinct();

            return types.Where(t => t.StartsWith(lastToken, StringComparison.OrdinalIgnoreCase));
        }

        return Enumerable.Empty<string>();
    }

    private void AddFilePathSuggestions(List<Suggestion> suggestions, string partialPath)
    {
        if (string.IsNullOrEmpty(partialPath))
        {
            suggestions.Add(new Suggestion("res://", SuggestionSource.FilePath, 20));
            suggestions.Add(new Suggestion("user://", SuggestionSource.FilePath, 20));
            return;
        }

        if (partialPath.StartsWith("res://") || partialPath.StartsWith("user://"))
        {
            suggestions.Add(new Suggestion(partialPath, SuggestionSource.FilePath, 20));
        }
    }

    private void AddPropertyPathSuggestions(List<Suggestion> suggestions, string partialPath)
    {
        if (string.IsNullOrEmpty(partialPath))
        {
            foreach (var ns in InstanceRegistry.GetAllNamespaces().Take(10))
            {
                suggestions.Add(
                    new Suggestion(ns, SuggestionSource.PropertyPath, 15, "Instance namespace")
                );
            }
            return;
        }

        if (partialPath.Contains("."))
        {
            var lastDot = partialPath.LastIndexOf('.');
            var namespacePart = partialPath.Substring(0, lastDot);

            if (InstanceRegistry.TryGetInstance(namespacePart, out var instance))
            {
                var propertyPrefix = partialPath.Substring(lastDot + 1);
                var properties = instance!
                    .GetType()
                    .GetProperties(
                        System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.Instance
                    );

                foreach (var prop in properties)
                {
                    if (prop.Name.StartsWith(propertyPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        suggestions.Add(
                            new Suggestion(
                                $"{namespacePart}.{prop.Name}",
                                SuggestionSource.PropertyPath,
                                25
                            )
                        );
                    }
                }
            }
        }
        else
        {
            var namespaces = InstanceRegistry.GetNamespacesByPrefix(partialPath);
            foreach (var ns in namespaces.Take(10))
            {
                suggestions.Add(new Suggestion(ns, SuggestionSource.PropertyPath, 15));
            }
        }
    }

    private void AddHistorySuggestions(List<Suggestion> suggestions, string input)
    {
        if (_commandHistory.Count == 0 || input.Length < 2)
        {
            return;
        }

        var matches = _commandHistory
            .Where(h => h.StartsWith(input, StringComparison.OrdinalIgnoreCase) && h != input)
            .Distinct()
            .Take(5);

        int priority = 10;
        foreach (var match in matches)
        {
            suggestions.Add(new Suggestion(match, SuggestionSource.History, priority--));
        }
    }

    private IEnumerable<Suggestion> RankSuggestions(List<Suggestion> suggestions, string input)
    {
        return suggestions
            .Where(s => !string.IsNullOrEmpty(s.Text))
            .DistinctBy(s => s.Text)
            .OrderBy(s => s.Source == SuggestionSource.History ? 0 : 1)
            .ThenByDescending(s => s.Priority)
            .ThenBy(s => s.Text, StringComparer.OrdinalIgnoreCase);
    }

    public void AddToHistory(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        _commandHistory.Remove(command);
        _commandHistory.Add(command);

        if (_commandHistory.Count > MaxHistorySize)
        {
            _commandHistory.RemoveAt(0);
        }
    }

    public void ClearHistory()
    {
        _commandHistory.Clear();
    }

    public IEnumerable<string> GetHistory()
    {
        return _commandHistory.AsEnumerable().Reverse();
    }

    public int HistoryCount => _commandHistory.Count;
}
#endif
