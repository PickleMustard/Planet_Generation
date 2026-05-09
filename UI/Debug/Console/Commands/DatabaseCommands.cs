#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using UI.Debug.DatabaseViewer;

namespace UI.Debug.Console;

public static class DatabaseCommands
{
    [DebugCommand("db_list", "List all databases registered with the debug viewer", "db_list", Category = "Database")]
    public static int List(CommandContext ctx, string[] args)
    {
        var providers = DataProviderRegistry.Providers;
        if (providers.Count == 0)
        {
            ctx.WriteWarning("No data providers registered.");
            return 0;
        }

        ctx.WriteLine($"[color=yellow]Databases ({providers.Count}):[/color]");

        var byCategory = DataProviderRegistry.ProvidersByCategory;
        foreach (var category in byCategory.Keys.OrderBy(c => c))
        {
            ctx.WriteLine($"\n[color=cyan]{category}:[/color]");
            foreach (var p in byCategory[category].OrderBy(p => p.Name))
            {
                int total = 0;
                try
                {
                    var data = p.GetData();
                    total = data.TotalCount;
                }
                catch (Exception e)
                {
                    ctx.WriteLine($"  {p.Name} - [color=red]error: {e.Message}[/color]");
                    continue;
                }

                ctx.WriteLine($"  {p.Name} - {total} top-level items");
            }
        }
        return 0;
    }

    [DebugCommand("db_show", "Show top-level entries in a database (or search if pattern given)", "db_show <name> [pattern]", Category = "Database")]
    public static int Show(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: db_show <name> [pattern]");
            return 1;
        }

        var provider = DataProviderRegistry.GetProvider(args[0]);
        if (provider == null)
        {
            ctx.WriteError($"Database not found: {args[0]}");
            ctx.WriteLine("Run [color=cyan]db_list[/color] to see available databases.");
            return 1;
        }

        if (args.Length >= 2)
        {
            var pattern = args[1];
            var matches = provider.Search(pattern).ToList();
            if (matches.Count == 0)
            {
                ctx.WriteLine($"No matches for '{pattern}' in {provider.Name}.");
                return 0;
            }
            ctx.WriteLine($"[color=yellow]{provider.Name} matches for '{pattern}' ({matches.Count}):[/color]");
            foreach (var path in matches)
            {
                ctx.WriteLine($"  {path}");
            }
            return 0;
        }

        DebugDataNode root;
        try
        {
            root = provider.GetData();
        }
        catch (Exception e)
        {
            ctx.WriteError($"Failed to read {provider.Name}: {e.Message}");
            return 1;
        }

        ctx.WriteLine($"[color=yellow]{provider.Name}:[/color]");
        WriteNodeSummary(ctx, root, indent: "  ");
        ctx.WriteLine($"\nUse [color=cyan]db_inspect {provider.Name} <path>[/color] to drill into a node.");
        return 0;
    }

    [DebugCommand("db_inspect", "Inspect a node in a database by slash-separated path", "db_inspect <name> <path>", Category = "Database")]
    public static int Inspect(CommandContext ctx, string[] args)
    {
        if (args.Length < 2)
        {
            ctx.WriteError("Usage: db_inspect <name> <path>");
            ctx.WriteLine("Example: db_inspect Recipes \"By Category/headquarters/hq_all_in_one_operation\"");
            return 1;
        }

        var provider = DataProviderRegistry.GetProvider(args[0]);
        if (provider == null)
        {
            ctx.WriteError($"Database not found: {args[0]}");
            return 1;
        }

        DebugDataNode root;
        try
        {
            root = provider.GetData();
        }
        catch (Exception e)
        {
            ctx.WriteError($"Failed to read {provider.Name}: {e.Message}");
            return 1;
        }

        var pathTokens = args[1].Split('/', StringSplitOptions.RemoveEmptyEntries);
        var node = WalkPath(root, pathTokens, out var failedAt);
        if (node == null)
        {
            ctx.WriteError($"Path not found: '{args[1]}' (failed at '{failedAt}')");
            return 1;
        }

        ctx.WriteLine($"[color=yellow]{provider.Name}/{string.Join('/', pathTokens)}:[/color]");
        WriteNodeDetail(ctx, node, indent: "  ");
        return 0;
    }

    private static DebugDataNode? WalkPath(DebugDataNode root, string[] tokens, out string failedAt)
    {
        failedAt = "";
        DebugDataNode current = root;
        foreach (var token in tokens)
        {
            var match = current.Children.FirstOrDefault(c =>
                string.Equals(c.Name, token, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                failedAt = token;
                return null;
            }
            current = match;
        }
        return current;
    }

    private static void WriteNodeSummary(CommandContext ctx, DebugDataNode node, string indent)
    {
        if (node.Properties.Count > 0)
        {
            foreach (var kvp in node.Properties)
            {
                ctx.WriteLine($"{indent}{kvp.Key}: {kvp.Value.GetFormattedValue()}");
            }
        }

        if (node.Children.Count > 0)
        {
            foreach (var child in node.Children)
            {
                ctx.WriteLine($"{indent}[{child.Name}] ({child.TotalCount} items)");
            }
        }

        if (node.Properties.Count == 0 && node.Children.Count == 0)
        {
            ctx.WriteLine($"{indent}(empty)");
        }
    }

    private static void WriteNodeDetail(CommandContext ctx, DebugDataNode node, string indent)
    {
        if (node.HasValue)
        {
            ctx.WriteLine($"{indent}value: {node.GetFormattedValue()} ({node.ValueType})");
        }

        foreach (var kvp in node.Properties)
        {
            ctx.WriteLine($"{indent}{kvp.Key}: {kvp.Value.GetFormattedValue()}");
        }

        foreach (var child in node.Children)
        {
            ctx.WriteLine($"{indent}[{child.Name}] ({child.TotalCount} items)");
        }

        if (!node.HasValue && node.Properties.Count == 0 && node.Children.Count == 0)
        {
            ctx.WriteLine($"{indent}(empty)");
        }
    }
}
#endif
