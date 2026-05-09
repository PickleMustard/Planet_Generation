#if DEBUG
using System;
using System.Linq;
using Structures.GameState;
using UI.Debug.Console;
using UtilityLibrary;

namespace UI.Debug.Console.Commands
{
    public static class EconomyCommands
    {
        [DebugCommand(
            "economy_list",
            "List all registered economies",
            "economy_list",
            Category = "Economy"
        )]
        public static int EconomyList(CommandContext ctx, string[] args)
        {
            var namespaces = InstanceRegistry.GetAllEconomyNamespaces().ToList();

            if (namespaces.Count == 0)
            {
                ctx.WriteLine("[color=yellow]No economies registered.[/color]");
                return 0;
            }

            ctx.WriteLine($"[color=cyan]=== Registered Economies ({namespaces.Count}) ===[/color]");

            foreach (var ns in namespaces)
            {
                if (!InstanceRegistry.TryGetInstance(ns, out var instance))
                    continue;
            }

            return 0;
        }

        [DebugCommand(
            "economy_info",
            "Show detailed info for an economy",
            "economy_info <namespace>",
            Category = "Economy"
        )]
        public static int EconomyInfo(CommandContext ctx, string[] args)
        {
            if (args.Length < 1)
            {
                ctx.WriteError("Usage: economy_info <namespace>");
                ctx.WriteLine("Example: economy_info ContinentEconomy.0");
                return 1;
            }

            string ns = args[0];
            if (!InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteError($"Economy not found: {ns}");
                return 1;
            }

            ctx.WriteLine($"[color=cyan]=== {ns} ===[/color]");

            return 0;
        }

        [DebugCommand(
            "economy_add_resource",
            "Add resource to economy stockpile",
            "economy_add_resource <namespace> <resource> <amount>",
            Category = "Economy"
        )]
        public static int EconomyAddResource(CommandContext ctx, string[] args)
        {
            if (args.Length < 3)
            {
                ctx.WriteError("Usage: economy_add_resource <namespace> <resource> <amount>");
                return 1;
            }

            string ns = args[0];
            string resourceId = args[1];
            if (!float.TryParse(args[2], out float amount))
            {
                ctx.WriteError($"Invalid amount: {args[2]}");
                return 1;
            }

            if (!InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteError($"Economy not found: {ns}");
                return 1;
            }

            return 0;
        }

        [DebugCommand(
            "economy_remove_resource",
            "Remove resource from economy stockpile",
            "economy_remove_resource <namespace> <resource> <amount>",
            Category = "Economy"
        )]
        public static int EconomyRemoveResource(CommandContext ctx, string[] args)
        {
            if (args.Length < 3)
            {
                ctx.WriteError("Usage: economy_remove_resource <namespace> <resource> <amount>");
                return 1;
            }

            string ns = args[0];
            string resourceId = args[1];
            if (!float.TryParse(args[2], out float amount))
            {
                ctx.WriteError($"Invalid amount: {args[2]}");
                return 1;
            }

            if (!InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteError($"Economy not found: {ns}");
                return 1;
            }

            return 0;
        }

        [DebugCommand(
            "economy_set_power",
            "Set power stored in economy",
            "economy_set_power <namespace> <amount>",
            Category = "Economy"
        )]
        public static int EconomySetPower(CommandContext ctx, string[] args)
        {
            ctx.WriteLine("[color=yellow]Note: Direct power manipulation not implemented.[/color]");
            ctx.WriteLine("Power is calculated from building recipes.");
            ctx.WriteLine("Use building construction/demolition to change power.");
            return 0;
        }

        [DebugCommand(
            "economy_pause_buildings",
            "Pause all non-power-generating buildings",
            "economy_pause_buildings <namespace>",
            Category = "Economy"
        )]
        public static int EconomyPauseBuildings(CommandContext ctx, string[] args)
        {
            if (args.Length < 1)
            {
                ctx.WriteError("Usage: economy_pause_buildings <namespace>");
                return 1;
            }

            string ns = args[0];
            if (!InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteError($"Economy not found: {ns}");
                return 1;
            }

            int pausedCount = 0;
            ctx.WriteLine($"[color=green]Paused {pausedCount} buildings[/color]");
            return 0;
        }

        [DebugCommand(
            "economy_unpause_buildings",
            "Unpause all buildings",
            "economy_unpause_buildings <namespace>",
            Category = "Economy"
        )]
        public static int EconomyUnpauseBuildings(CommandContext ctx, string[] args)
        {
            if (args.Length < 1)
            {
                ctx.WriteError("Usage: economy_unpause_buildings <namespace>");
                return 1;
            }

            string ns = args[0];
            if (!InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                ctx.WriteError($"Economy not found: {ns}");
                return 1;
            }

            int unpausedCount = 0;
            ctx.WriteLine($"[color=green]Unpaused {unpausedCount} buildings[/color]");
            return 0;
        }
    }
}
#endif
