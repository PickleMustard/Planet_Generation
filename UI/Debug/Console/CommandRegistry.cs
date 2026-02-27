#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace UI.Debug.Console;

/// <summary>
/// Metadata about a registered debug command.
/// </summary>
public class CommandInfo
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Usage { get; set; }
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string Category { get; set; } = "General";
    public bool RequiresTarget { get; set; }
    public MethodInfo Method { get; set; }
    public Type DeclaringType { get; set; }
    public bool IsStatic { get; set; }
    public object SingletonInstance { get; set; }
}

/// <summary>
/// Registry that discovers and manages all debug commands using reflection.
/// Supports both static global commands and instance-based commands.
/// </summary>
public class CommandRegistry
{
    private readonly Dictionary<string, CommandInfo> _commands = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<string, CommandInfo> _aliases = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly CommandParser _parser = new();

    /// <summary>
    /// Gets all registered commands.
    /// </summary>
    public IEnumerable<CommandInfo> GetAllCommands() => _commands.Values;

    /// <summary>
    /// Gets all command names.
    /// </summary>
    public IEnumerable<string> GetCommandNames() => _commands.Keys;

    /// <summary>
    /// Gets commands grouped by category.
    /// </summary>
    public ILookup<string, CommandInfo> GetCommandsByCategory() =>
        _commands.Values.ToLookup(c => c.Category);

    /// <summary>
    /// Gets the number of registered commands.
    /// </summary>
    public int CommandCount => _commands.Count;

    /// <summary>
    /// Initializes the registry by scanning all loaded assemblies for debug commands.
    /// </summary>
    public void Initialize()
    {
        DiscoverCommands();
        GD.Print($"[CommandRegistry] Discovered {_commands.Count} commands");
    }

    /// <summary>
    /// Scans all loaded assemblies for methods tagged with DebugCommandAttribute.
    /// </summary>
    private void DiscoverCommands()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                ScanAssembly(assembly);
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var type in ex.Types.Where(t => t != null))
                {
                    ScanType(type);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr(
                    $"[CommandRegistry] Error scanning assembly {assembly.FullName}: {ex.Message}"
                );
            }
        }
    }

    /// <summary>
    /// Scans a single assembly for debug commands.
    /// </summary>
    private void ScanAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            ScanType(type);
        }
    }

    /// <summary>
    /// Scans a single type for debug command methods.
    /// </summary>
    private void ScanType(Type type)
    {
        try
        {
            var methods = type.GetMethods(
                BindingFlags.Static
                    | BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
            );

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<DebugCommandAttribute>();
                if (attr == null)
                    continue;

                var commandInfo = new CommandInfo
                {
                    Name = attr.Name,
                    Description = attr.Description,
                    Usage = attr.Usage,
                    Aliases = attr.Aliases,
                    Category = attr.Category,
                    RequiresTarget = attr.RequiresTarget,
                    Method = method,
                    DeclaringType = type,
                    IsStatic = method.IsStatic,
                };

                RegisterCommand(commandInfo);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CommandRegistry] Error scanning type {type.FullName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers a command with the registry.
    /// </summary>
    /// <param name="command">The command to register.</param>
    public void RegisterCommand(CommandInfo command)
    {
        if (string.IsNullOrEmpty(command.Name))
        {
            GD.PrintErr("[CommandRegistry] Cannot register command with empty name");
            return;
        }

        if (_commands.ContainsKey(command.Name))
        {
            GD.PrintErr($"[CommandRegistry] Command '{command.Name}' is already registered");
            return;
        }

        _commands[command.Name] = command;

        foreach (var alias in command.Aliases)
        {
            if (!string.IsNullOrEmpty(alias))
            {
                if (_aliases.ContainsKey(alias))
                {
                    GD.PrintErr($"[CommandRegistry] Alias '{alias}' is already registered");
                }
                else
                {
                    _aliases[alias] = command;
                }
            }
        }
    }

    /// <summary>
    /// Registers a singleton instance for instance command execution.
    /// </summary>
    /// <param name="instance">The singleton instance.</param>
    public void RegisterSingletonInstance(object instance)
    {
        var type = instance.GetType();
        foreach (var command in _commands.Values.Where(c => c.DeclaringType == type && !c.IsStatic))
        {
            command.SingletonInstance = instance;
        }
    }

    /// <summary>
    /// Tries to get command info by name or alias.
    /// </summary>
    /// <param name="name">The command name or alias.</param>
    /// <param name="command">The command info if found.</param>
    /// <returns>True if the command was found.</returns>
    public bool TryGetCommand(string name, out CommandInfo command)
    {
        return _commands.TryGetValue(name, out command) || _aliases.TryGetValue(name, out command);
    }

    /// <summary>
    /// Checks if a command exists.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <returns>True if the command exists.</returns>
    public bool HasCommand(string name)
    {
        return _commands.ContainsKey(name) || _aliases.ContainsKey(name);
    }

    /// <summary>
    /// Executes a command by parsing the input string.
    /// </summary>
    /// <param name="input">The raw input string.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>The exit code (0 for success).</returns>
    public int Execute(string input, CommandContext context)
    {
        var parsed = _parser.Parse(input);

        if (string.IsNullOrEmpty(parsed.CommandName))
        {
            context.WriteError("Empty command");
            return 1;
        }

        if (!TryGetCommand(parsed.CommandName, out var command))
        {
            context.WriteError($"Unknown command: {parsed.CommandName}");
            return 1;
        }

        return ExecuteCommand(command, parsed, context);
    }

    /// <summary>
    /// Executes a command with the given parsed data.
    /// </summary>
    private int ExecuteCommand(
        CommandInfo command,
        CommandParser.ParsedCommand parsed,
        CommandContext context
    )
    {
        try
        {
            if (command.RequiresTarget)
            {
                return ExecuteInstanceCommand(command, parsed, context);
            }
            else
            {
                return ExecuteStaticCommand(command, parsed, context);
            }
        }
        catch (Exception ex)
        {
            context.WriteError($"Command execution failed: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Executes a static command.
    /// </summary>
    private int ExecuteStaticCommand(
        CommandInfo command,
        CommandParser.ParsedCommand parsed,
        CommandContext context
    )
    {
        var parameters = new object[] { context, parsed.Arguments };
        var result = command.Method.Invoke(null, parameters);
        return result is int exitCode ? exitCode : 0;
    }

    /// <summary>
    /// Executes an instance command, resolving the target from namespace.
    /// </summary>
    private int ExecuteInstanceCommand(
        CommandInfo command,
        CommandParser.ParsedCommand parsed,
        CommandContext context
    )
    {
        if (string.IsNullOrEmpty(parsed.Namespace))
        {
            if (command.SingletonInstance != null)
            {
                var parameters = new object[] { context, parsed.Arguments };
                var result = command.Method.Invoke(command.SingletonInstance, parameters);
                return result is int exitCode ? exitCode : 0;
            }

            context.WriteError(
                $"Command '{command.Name}' requires a target instance. Use: <namespace>.{command.Name}"
            );
            return 1;
        }

        if (parsed.HasWildcard)
        {
            return ExecuteWildcardCommand(command, parsed, context);
        }

        if (!InstanceRegistry.TryGetInstance(parsed.Namespace, out var target))
        {
            context.WriteError($"No instance found with namespace: {parsed.Namespace}");
            return 1;
        }

        if (!command.DeclaringType.IsInstanceOfType(target))
        {
            context.WriteError(
                $"Instance '{parsed.Namespace}' is not of type {command.DeclaringType.Name}"
            );
            return 1;
        }

        var targetParams = new object[] { context, parsed.Arguments };
        var targetResult = command.Method.Invoke(target, targetParams);
        return targetResult is int targetExitCode ? targetExitCode : 0;
    }

    /// <summary>
    /// Executes a command on all instances matching a wildcard namespace.
    /// </summary>
    private int ExecuteWildcardCommand(
        CommandInfo command,
        CommandParser.ParsedCommand parsed,
        CommandContext context
    )
    {
        var (typeName, _) = _parser.SplitNamespace(parsed.Namespace);
        if (string.IsNullOrEmpty(typeName))
        {
            context.WriteError("Invalid wildcard namespace");
            return 1;
        }

        typeName = typeName.Replace("*", "").TrimEnd('.');
        var namespaces = InstanceRegistry.GetNamespacesByPrefix(typeName);
        var successCount = 0;
        var failCount = 0;

        foreach (var ns in namespaces)
        {
            if (!InstanceRegistry.TryGetInstance(ns, out var target))
            {
                failCount++;
                continue;
            }

            if (!command.DeclaringType.IsInstanceOfType(target))
            {
                failCount++;
                continue;
            }

            try
            {
                var wildcardParams = new object[] { context, parsed.Arguments };
                var result = command.Method.Invoke(target, wildcardParams);
                if (result is int exitCode && exitCode == 0)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }
            catch
            {
                failCount++;
            }
        }

        context.WriteLine($"Executed on {successCount} instances ({failCount} failed)");
        return failCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Gets autocomplete suggestions for the given input.
    /// </summary>
    /// <param name="input">The current input.</param>
    /// <returns>List of suggestions.</returns>
    public IEnumerable<string> GetSuggestions(string input)
    {
        var parsed = _parser.Parse(input);

        if (string.IsNullOrEmpty(parsed.CommandName))
        {
            return Enumerable.Empty<string>();
        }

        var suggestions = new List<string>();

        if (string.IsNullOrEmpty(parsed.Namespace))
        {
            foreach (var cmd in _commands.Values)
            {
                if (cmd.Name.StartsWith(parsed.CommandName, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(cmd.Name);
                }
            }

            foreach (var alias in _aliases.Keys)
            {
                if (alias.StartsWith(parsed.CommandName, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(alias);
                }
            }
        }

        if (!string.IsNullOrEmpty(parsed.Namespace) && parsed.Namespace.Contains("."))
        {
            var namespaces = InstanceRegistry.GetNamespacesByPrefix(parsed.Namespace);
            suggestions.AddRange(namespaces.Select(ns => $"{ns}.{parsed.CommandName}"));
        }

        return suggestions.Distinct().OrderBy(s => s);
    }

    /// <summary>
    /// Gets help text for a specific command.
    /// </summary>
    /// <param name="commandName">The command name.</param>
    /// <returns>Help text or null if not found.</returns>
    public string GetHelp(string commandName)
    {
        if (!TryGetCommand(commandName, out var command))
        {
            return null;
        }

        var help = $"**{command.Name}** - {command.Description}";
        if (!string.IsNullOrEmpty(command.Usage))
        {
            help += $"\n  Usage: {command.Usage}";
        }
        if (command.Aliases.Length > 0)
        {
            help += $"\n  Aliases: {string.Join(", ", command.Aliases)}";
        }
        if (command.RequiresTarget)
        {
            help += "\n  Requires target instance (use namespace prefix)";
        }

        return help;
    }

    /// <summary>
    /// Clears all registered commands.
    /// </summary>
    public void Clear()
    {
        _commands.Clear();
        _aliases.Clear();
    }
}
#endif
