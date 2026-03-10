#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;

namespace UI.Debug.Console;

public enum SuggestionSource
{
    Command,
    Namespace,
    Alias,
    History,
    Argument,
    FilePath,
    PropertyPath
}

public class Suggestion : IComparable<Suggestion>
{
    public string Text { get; }
    public SuggestionSource Source { get; }
    public int Priority { get; }
    public string? Description { get; set; }

    public Suggestion(string text, SuggestionSource source, int priority = 0, string? description = null)
    {
        Text = text;
        Source = source;
        Priority = priority;
        Description = description ?? string.Empty;
    }

    public int CompareTo(Suggestion? other)
    {
        if (other is null) return 1;
        int priorityCompare = other.Priority.CompareTo(Priority);
        if (priorityCompare != 0) return priorityCompare;

        int sourceCompare = Source.CompareTo(other.Source);
        if (sourceCompare != 0) return sourceCompare;

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
        var parsed = _parser.Parse(input);

        if (string.IsNullOrEmpty(parsed.CommandName))
        {
            return Enumerable.Empty<Suggestion>();
        }

        bool hasPartialArg = input.EndsWith(" ") || parsed.Arguments.Length > 0;
        string lastToken = GetLastToken(input);

        if (hasPartialArg && !string.IsNullOrEmpty(parsed.CommandName) && _registry.HasCommand(parsed.CommandName))
        {
            AddArgumentSuggestions(suggestions, parsed, lastToken);
        }
        else if (!hasPartialArg)
        {
            AddCommandSuggestions(suggestions, parsed, input);
        }

        AddHistorySuggestions(suggestions, input);

        return RankSuggestions(suggestions, input);
    }

    private string GetLastToken(string input)
    {
        var trimmed = input.TrimEnd();
        var lastSpace = trimmed.LastIndexOf(' ');
        return lastSpace >= 0 ? trimmed.Substring(lastSpace + 1) : trimmed;
    }

    private void AddCommandSuggestions(List<Suggestion> suggestions, CommandParser.ParsedCommand parsed, string input)
    {
        string? searchTerm = string.IsNullOrEmpty(parsed.Namespace)
            ? parsed.CommandName
            : parsed.Namespace.Contains(".")
                ? parsed.Namespace
                : parsed.CommandName;

        foreach (var cmd in _registry.GetAllCommands())
        {
            if (cmd.Name!.StartsWith(parsed.CommandName!, StringComparison.OrdinalIgnoreCase))
            {
                int priority = cmd.Name.Equals(parsed.CommandName!, StringComparison.OrdinalIgnoreCase) ? 100 : 50;
                if (cmd.Name.StartsWith(parsed.CommandName!, StringComparison.OrdinalIgnoreCase) &&
                    cmd.Name.Length == parsed.CommandName!.Length)
                {
                    priority = 100;
                }
                else if (cmd.Name.StartsWith(parsed.CommandName!, StringComparison.OrdinalIgnoreCase))
                {
                    priority = 50 + (10 - Math.Min(10, cmd.Name.Length - parsed.CommandName!.Length));
                }

                suggestions.Add(new Suggestion(cmd.Name, SuggestionSource.Command, priority, cmd.Description!));
            }
        }

        AddNamespaceSuggestions(suggestions, searchTerm!, parsed);
    }

    private void AddNamespaceSuggestions(List<Suggestion> suggestions, string searchTerm, CommandParser.ParsedCommand parsed)
    {
        var namespaces = InstanceRegistry.GetNamespacesByPrefix(searchTerm);

        foreach (var ns in namespaces)
        {
            if (ns.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                int priority = ns.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ? 90 : 40;

                if (!string.IsNullOrEmpty(parsed.CommandName) && parsed.CommandName.Contains("."))
                {
                    var matchingCommands = _registry.GetAllCommands()
                        .Where(c => c.RequiresTarget)
                        .Select(c => $"{ns}.{c.Name}");

                    foreach (var fullCommand in matchingCommands)
                    {
                        suggestions.Add(new Suggestion(fullCommand, SuggestionSource.Namespace, priority));
                    }
                }
                else
                {
                    suggestions.Add(new Suggestion(ns, SuggestionSource.Namespace, priority));
                }
            }
        }
    }

    private void AddArgumentSuggestions(List<Suggestion> suggestions, CommandParser.ParsedCommand parsed, string lastToken)
    {
        if (!_registry.TryGetCommand(parsed.CommandName!, out var command))
        {
            return;
        }

        var commandSuggestions = GetCommandArgumentSuggestions(command!, parsed.Arguments, lastToken);
        foreach (var suggestion in commandSuggestions)
        {
            suggestions.Add(new Suggestion(suggestion, SuggestionSource.Argument, 30));
        }

        if (command!.Usage != null && command.Usage!.Contains("path", StringComparison.OrdinalIgnoreCase))
        {
            AddFilePathSuggestions(suggestions, lastToken);
        }

        if (command.Name == "get" || command.Name == "set")
        {
            AddPropertyPathSuggestions(suggestions, lastToken);
        }
    }

    private IEnumerable<string> GetCommandArgumentSuggestions(CommandInfo command, string[] args, string lastToken)
    {
        if (command.Name == "help")
        {
            return _registry.GetCommandNames()
                .Where(c => c.StartsWith(lastToken, StringComparison.OrdinalIgnoreCase));
        }

        if (command.Name == "list_namespaces")
        {
            var types = InstanceRegistry.GetAllInstances()
                .Select(i => i.GetType().Name)
                .Distinct();

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
                suggestions.Add(new Suggestion(ns, SuggestionSource.PropertyPath, 15, "Instance namespace"));
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
                var properties = instance!.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                foreach (var prop in properties)
                {
                    if (prop.Name.StartsWith(propertyPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        suggestions.Add(new Suggestion($"{namespacePart}.{prop.Name}", SuggestionSource.PropertyPath, 25));
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
