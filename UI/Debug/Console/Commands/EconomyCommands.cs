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

                string status = instance switch
                {
                    ContinentEconomy ce => $"Buildings: {ce.ActiveBuildingCount}, Power: {ce.PowerGeneration:F1}/{ce.PowerConsumption:F1}",
                    StationEconomy se => $"Buildings: {se.ActiveBuildingCount}, Power: {se.PowerGeneration:F1}/{se.PowerConsumption:F1}",
                    _ => "Unknown type"
                };

                string deficitWarning = instance switch
                {
                    ContinentEconomy ce => ce.IsPowerDeficit ? " [color=red][DEFICIT][/color]" : "",
                    StationEconomy se => se.IsPowerDeficit ? " [color=red][DEFICIT][/color]" : "",
                    _ => ""
                };

                ctx.WriteLine($"  {ns}: {status}{deficitWarning}");
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

            switch (instance)
            {
                case ContinentEconomy ce:
                    ShowContinentEconomyInfo(ctx, ce);
                    break;
                case StationEconomy se:
                    ShowStationEconomyInfo(ctx, se);
                    break;
                default:
                    ctx.WriteError("Unknown economy type");
                    return 1;
            }

            return 0;
        }

        private static void ShowContinentEconomyInfo(CommandContext ctx, ContinentEconomy eco)
        {
            ctx.WriteLine($"Type: Continent");
            ctx.WriteLine($"Continent Index: {eco.Continent.StartingIndex}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Power:[/color]");
            ctx.WriteLine($"  Generation: {eco.PowerGeneration:F1}/s");
            ctx.WriteLine($"  Consumption: {eco.PowerConsumption:F1}/s");
            ctx.WriteLine($"  Stored: {eco.PowerStored:F1} / {eco.PowerStorageCapacity:F1}");
            ctx.WriteLine($"  Deficit: {(eco.IsPowerDeficit ? "[color=red]YES[/color]" : "No")}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Buildings:[/color]");
            ctx.WriteLine($"  Active: {eco.ActiveBuildingCount}");
            ctx.WriteLine($"  Paused: {eco.ActiveBuildings.Count(b => b.IsPaused)}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Stockpiles:[/color]");
            foreach (var kvp in eco.GetAllStockpiles().Where(s => s.Value > 0.01f).OrderBy(s => s.Key))
            {
                float netRate = eco.GetNetRate(kvp.Key);
                string rateStr = $"({(netRate >= 0 ? "+" : "")}{netRate:F2}/s)";
                ctx.WriteLine($"  {kvp.Key}: {kvp.Value:F1} {rateStr}");
            }
        }

        private static void ShowStationEconomyInfo(CommandContext ctx, StationEconomy eco)
        {
            ctx.WriteLine($"Type: Station");
            ctx.WriteLine($"Station ID: {eco.StationId}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Power:[/color]");
            ctx.WriteLine($"  Generation: {eco.PowerGeneration:F1}/s");
            ctx.WriteLine($"  Consumption: {eco.PowerConsumption:F1}/s");
            ctx.WriteLine($"  Stored: {eco.PowerStored:F1} / {eco.PowerStorageCapacity:F1}");
            ctx.WriteLine($"  Deficit: {(eco.IsPowerDeficit ? "[color=red]YES[/color]" : "No")}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Buildings:[/color]");
            ctx.WriteLine($"  Active: {eco.ActiveBuildingCount}");
            ctx.WriteLine($"  Paused: {eco.ActiveBuildings.Count(b => b.IsPaused)}");
            ctx.WriteLine($"");
            ctx.WriteLine($"[color=yellow]Stockpiles:[/color]");
            foreach (var kvp in eco.GetAllStockpiles().Where(s => s.Value > 0.01f).OrderBy(s => s.Key))
            {
                float netRate = eco.GetNetRate(kvp.Key);
                string rateStr = $"({(netRate >= 0 ? "+" : "")}{netRate:F2}/s)";
                ctx.WriteLine($"  {kvp.Key}: {kvp.Value:F1} {rateStr}");
            }
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

            float deposited = instance switch
            {
                ContinentEconomy ce => ce.DepositResource(resourceId, amount),
                StationEconomy se => se.DepositResource(resourceId, amount),
                _ => 0f
            };

            ctx.WriteLine($"[color=green]Added {deposited:F1} {resourceId}[/color]");
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

            float withdrawn = instance switch
            {
                ContinentEconomy ce => ce.WithdrawResource(resourceId, amount),
                StationEconomy se => se.WithdrawResource(resourceId, amount),
                _ => 0f
            };

            ctx.WriteLine($"[color=green]Removed {withdrawn:F1} {resourceId}[/color]");
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
            switch (instance)
            {
                case ContinentEconomy ce:
                    foreach (var building in ce.ActiveBuildings.Where(b => b.TheoreticalPowerGeneration <= 0 && !b.IsPaused))
                    {
                        building.IsPaused = true;
                        pausedCount++;
                    }
                    break;
                case StationEconomy se:
                    foreach (var building in se.ActiveBuildings.Where(b => b.TheoreticalPowerGeneration <= 0 && !b.IsPaused))
                    {
                        building.IsPaused = true;
                        pausedCount++;
                    }
                    break;
            }

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
            switch (instance)
            {
                case ContinentEconomy ce:
                    foreach (var building in ce.ActiveBuildings.Where(b => b.IsPaused))
                    {
                        building.IsPaused = false;
                        unpausedCount++;
                    }
                    break;
                case StationEconomy se:
                    foreach (var building in se.ActiveBuildings.Where(b => b.IsPaused))
                    {
                        building.IsPaused = false;
                        unpausedCount++;
                    }
                    break;
            }

            ctx.WriteLine($"[color=green]Unpaused {unpausedCount} buildings[/color]");
            return 0;
        }
    }
}
#endif
