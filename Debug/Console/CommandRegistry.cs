#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace Debug.Console;

/// <summary>
/// Metadata about a registered debug command.
/// </summary>
public class CommandInfo
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Usage { get; set; }
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string Category { get; set; } = "General";
    public bool RequiresTarget { get; set; }
    public MethodInfo? Method { get; set; }
    public Type? DeclaringType { get; set; }
    public bool IsStatic { get; set; }
    public object? SingletonInstance { get; set; }

    /// <summary>
    /// The type of instance this external command is called from.
    /// Null for commands that are members of the target class or are global.
    /// </summary>
    public object? ExternalCaller { get; set; }
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
                    ScanType(type!);
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
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
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
                    RequiresTarget = attr.RequiresTarget || attr.ExternalCaller != null,
                    Method = method,
                    DeclaringType = type,
                    IsStatic = method.IsStatic,
                    ExternalCaller = attr.ExternalCaller,
                };

                RegisterCommand(commandInfo);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CommandRegistry] Error scanning type {type.FullName}: {ex.Message}");
        }
    }

    public void ScanInstance(object instance)
    {
        try
        {
            GD.Print($"[CommandRegistry] Scanning instance {instance.GetType().FullName}");
            var methods = instance
                .GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<DebugCommandAttribute>();
                if (attr == null)
                {
                    continue;
                }
                var commandInfo = new CommandInfo
                {
                    Name = attr.Name,
                    Description = attr.Description,
                    Usage = attr.Usage,
                    Aliases = attr.Aliases,
                    Category = attr.Category,
                    RequiresTarget = attr.RequiresTarget || attr.ExternalCaller != null,
                    Method = method,
                    DeclaringType = instance.GetType(),
                    IsStatic = false,
                    ExternalCaller = instance,
                };
                RegisterCommand(commandInfo);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr(
                $"[CommandRegistry] Error scanning type {instance.GetType().FullName}: {ex.Message}"
            );
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
    public bool TryGetCommand(string name, out CommandInfo? command)
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

        try
        {
            if (parsed.HasNamespaces)
            {
                return ExecuteOnNamespaces(command!, parsed, context);
            }
            else if (command!.RequiresTarget)
            {
                // Command requires a target but no namespace provided
                if (command.SingletonInstance != null)
                {
                    return InvokeCommand(
                        command,
                        command.SingletonInstance,
                        command.SingletonInstance,
                        context,
                        parsed
                    );
                }
                context.WriteError(
                    $"Command '{command.Name}' requires a target. "
                        + $"Use: ({command.DeclaringType?.Name ?? "namespace"}) {command.Name}"
                );
                return 1;
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
    /// Executes a static (global) command.
    /// </summary>
    private int ExecuteStaticCommand(
        CommandInfo command,
        CommandParser.ParsedCommand parsed,
        CommandContext context
    )
    {
        var parameters = new object[] { context, parsed.Arguments };
        var result = command.Method!.Invoke(null, parameters);
        return result is int exitCode ? exitCode : 0;
    }

    /// <summary>
    /// Executes a command on one or more namespace-resolved instances.
    /// Handles both explicit namespaces and wildcard expansion.
    /// </summary>
    private int ExecuteOnNamespaces(
        CommandInfo command,
        CommandParser.ParsedCommand parsed,
        CommandContext context
    )
    {
        // Expand all namespaces (resolving wildcards)
        var expandedNamespaces = new List<string>();

        foreach (var ns in parsed.Namespaces)
        {
            if (ns.Contains('*'))
            {
                // Wildcard: extract prefix and expand
                var (typeName, _) = _parser.SplitNamespace(ns);
                if (string.IsNullOrEmpty(typeName))
                {
                    context.WriteError($"Invalid wildcard namespace: {ns}");
                    return 1;
                }

                string prefix = typeName.Replace("*", "").TrimEnd('.');
                var matchedNamespaces = InstanceRegistry.GetNamespacesByPrefix(prefix);
                expandedNamespaces.AddRange(matchedNamespaces);
            }
            else
            {
                expandedNamespaces.Add(ns);
            }
        }

        if (expandedNamespaces.Count == 0)
        {
            context.WriteError("No instances found matching the provided namespaces");
            return 1;
        }

        int successCount = 0;
        int failCount = 0;

        foreach (var ns in expandedNamespaces)
        {
            // Resolve instance
            if (!InstanceRegistry.TryGetInstance(ns, out var functionTarget))
            {
                context.WriteError($"No instance found with namespace: {ns}");
                failCount++;
                continue;
            }

            try
            {
                // Create a per-invocation context for multi-target execution
                if (expandedNamespaces.Count > 1)
                {
                    using var perInvocationContext = new CommandContext(
                        context.Registry,
                        callerNamespace: ns,
                        targetInstance: functionTarget,
                        callerInstance: command.ExternalCaller,
                        outputWriter: context.Output
                    );

                    int result = InvokeCommand(
                        command,
                        functionTarget!,
                        command.ExternalCaller!,
                        perInvocationContext,
                        parsed
                    );

                    // Forward the output to the main context, prefixed with namespace
                    var output = perInvocationContext.GetOutput();
                    if (!string.IsNullOrEmpty(output))
                    {
                        context.WriteLine($"[color=cyan][{ns}][/color]");
                        context.Write(output);
                    }

                    if (result == 0)
                        successCount++;
                    else
                        failCount++;
                }
                else
                {
                    CommandContext invocationContext = new CommandContext(
                        context.Registry,
                        callerNamespace: ns,
                        targetInstance: functionTarget,
                        callerInstance: command.ExternalCaller,
                        outputWriter: context.Output
                    );
                    // Single namespace — use the provided context directly
                    int result = InvokeCommand(
                        command,
                        functionTarget!,
                        command.ExternalCaller!,
                        invocationContext,
                        parsed
                    );
                    if (result == 0)
                        successCount++;
                    else
                        failCount++;
                }
            }
            catch (Exception ex)
            {
                context.WriteError($"Error executing on '{ns}': {ex.Message}\n{ex.StackTrace}");
                failCount++;
            }
        }

        if (expandedNamespaces.Count > 1)
        {
            context.WriteLine($"Executed on {successCount} instance(s) ({failCount} failed)");
        }

        return failCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Invokes a command method on a target instance.
    /// Handles both instance methods and static methods (ExternalCaller pattern).
    /// </summary>
    private int InvokeCommand(
        CommandInfo command,
        object target,
        object caller,
        CommandContext context,
        CommandParser.ParsedCommand parsed
    )
    {
        var parameters = new object[] { context, parsed.Arguments };

        if (command.IsStatic)
        {
            // Static method or ExternalCaller: populate context with target instance
            // Create a context with the target instance set
            using var targetContext = new CommandContext(
                context.Registry,
                callerNamespace: null,
                targetInstance: target,
                outputWriter: context.Output
            );

            var result = command.Method!.Invoke(
                null,
                new object[] { targetContext, parsed.Arguments }
            );
            return result is int exitCode ? exitCode : 0;
        }
        else if (caller is not null)
        {
            var result = command.Method!.Invoke(caller, parameters);
            return result is int exitCode ? exitCode : 0;
        }
        else
        {
            // Instance method: invoke on the target directly
            var result = command.Method!.Invoke(target, parameters);
            return result is int exitCode ? exitCode : 0;
        }
    }

    /// <summary>
    /// Gets autocomplete suggestions for the given input.
    /// </summary>
    /// <param name="input">The current input.</param>
    /// <returns>List of suggestions.</returns>
    public IEnumerable<string> GetSuggestions(string input)
    {
        var parsed = _parser.Parse(input);
        var suggestions = new List<string>();

        if (parsed.HasNamespaces)
        {
            // After a closing paren, suggest command names compatible with the namespace types
            if (!string.IsNullOrEmpty(parsed.CommandName))
            {
                foreach (var cmd in _commands.Values)
                {
                    if (
                        cmd.Name!.StartsWith(parsed.CommandName, StringComparison.OrdinalIgnoreCase)
                        && cmd.RequiresTarget
                    )
                    {
                        suggestions.Add(cmd.Name!);
                    }
                }
            }
            else
            {
                // No command name yet, suggest all target-requiring commands
                foreach (var cmd in _commands.Values.Where(c => c.RequiresTarget))
                {
                    suggestions.Add(cmd.Name!);
                }
            }
        }
        else if (!string.IsNullOrEmpty(parsed.CommandName))
        {
            // Global command suggestions
            foreach (var cmd in _commands.Values)
            {
                if (cmd.Name!.StartsWith(parsed.CommandName, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(cmd.Name!);
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

        return suggestions.Distinct().OrderBy(s => s);
    }

    /// <summary>
    /// Gets help text for a specific command.
    /// </summary>
    /// <param name="commandName">The command name.</param>
    /// <returns>Help text or null if not found.</returns>
    public string? GetHelp(string commandName)
    {
        if (!TryGetCommand(commandName, out var command))
        {
            return null;
        }

        var help = $"**{command!.Name!}** - {command!.Description!}";
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
            help +=
                $"\n  Requires target instance. Use: ({command.DeclaringType?.Name ?? "namespace"}) {command.Name}";
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
