#if DEBUG
using System;
using System.Linq;
using UI.Debug;

namespace UI.Debug.Console;

public static class BuiltInCommands
{
    [DebugCommand("help", "Show available commands or help for a specific command", "help [command]", Category = "Built-in")]
    public static int Help(CommandContext ctx, string[] args)
    {
        if (args.Length > 0)
        {
            var commandName = args[0];
            var help = ctx.Registry.GetHelp(commandName);
            if (help != null)
            {
                ctx.WriteLine(help);
            }
            else
            {
                ctx.WriteError($"Command not found: {commandName}");
                return 1;
            }
        }
        else
        {
            var categories = ctx.Registry.GetCommandsByCategory();
            ctx.WriteLine("[color=yellow]Available Commands:[/color]");
            
            foreach (var category in categories.OrderBy(c => c.Key))
            {
                ctx.WriteLine($"\n[color=cyan]{category.Key}:[/color]");
                foreach (var cmd in category.OrderBy(c => c.Name))
                {
                    ctx.WriteLine($"  {cmd.Name} - {cmd.Description}");
                }
            }
            
            ctx.WriteLine("\nType [color=cyan]help <command>[/color] for detailed usage.");
        }
        return 0;
    }

    [DebugCommand("clear", "Clear the console output", "clear", Category = "Built-in")]
    public static int Clear(CommandContext ctx, string[] args)
    {
        var console = GetConsole(ctx);
        if (console != null)
        {
            console.ClearOutput();
        }
        return 0;
    }

    [DebugCommand("history", "Show command history", "history", Category = "Built-in")]
    public static int History(CommandContext ctx, string[] args)
    {
        var console = GetConsole(ctx);
        if (console == null)
        {
            ctx.WriteError("Console not available");
            return 1;
        }

        console.ShowHistory();
        return 0;
    }

    [DebugCommand("list_commands", "List all registered commands", "list_commands [category]", Category = "Built-in")]
    public static int ListCommands(CommandContext ctx, string[] args)
    {
        var commands = ctx.Registry.GetAllCommands().ToList();
        
        if (args.Length > 0)
        {
            var category = args[0];
            commands = commands.Where(c => 
                c.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (commands.Count == 0)
            {
                ctx.WriteError($"No commands found in category: {category}");
                return 1;
            }
        }

        ctx.WriteLine($"[color=yellow]Registered Commands ({commands.Count}):[/color]");
        foreach (var cmd in commands.OrderBy(c => c.Name))
        {
            var aliasStr = cmd.Aliases.Length > 0 
                ? $" [aliases: {string.Join(", ", cmd.Aliases)}]" 
                : "";
            ctx.WriteLine($"  {cmd.Name} - {cmd.Description}{aliasStr}");
        }
        return 0;
    }

    [DebugCommand("list_namespaces", "List registered instances", "list_namespaces [type]", Category = "Built-in")]
    public static int ListNamespaces(CommandContext ctx, string[] args)
    {
        var namespaces = InstanceRegistry.GetAllNamespaces().ToList();
        
        if (args.Length > 0)
        {
            var typeFilter = args[0];
            namespaces = namespaces.Where(ns => 
                ns.StartsWith(typeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (namespaces.Count == 0)
            {
                ctx.WriteError($"No namespaces found matching: {typeFilter}");
                return 1;
            }
        }

        ctx.WriteLine($"[color=yellow]Registered Namespaces ({namespaces.Count}):[/color]");
        foreach (var ns in namespaces.OrderBy(n => n))
        {
            if (InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteLine($"  {ns} ({instance.GetType().Name})");
            }
        }
        return 0;
    }

    private static DebugConsole GetConsole(CommandContext ctx)
    {
        if (ctx.TargetInstance is DebugConsole console)
        {
            return console;
        }
        
        var namespaces = InstanceRegistry.GetNamespaces<DebugConsole>();
        foreach (var ns in namespaces)
        {
            if (InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                return instance as DebugConsole;
            }
        }
        
        return null;
    }
}
#endif
