#if DEBUG
using System;
using Structures.Enums;
using UI.Debug;
using UI.Debug.Console;
using UtilityLibrary;

namespace Constructables.ArtificialSatellites;

public partial class LogisticsUnit
{
    /// <summary>
    /// Registers this logistics unit with the debug console's InstanceRegistry.
    /// </summary>
    public void RegisterWithDebug()
    {
        try
        {
            string ns = InstanceRegistry.RegisterShip(this);
            GameLogger.Debug($"LogisticsUnit '{Name}' registered with debug console as '{ns}'");
        }
        catch (Exception e)
        {
            GameLogger.Warning(
                $"Failed to register LogisticsUnit '{Name}' with debug console: {e.Message}"
            );
        }
    }

    // ==================== Debug Commands ====================

    [DebugCommand(
        "cargo_add",
        "Add cargo to this ship",
        "cargo_add <resource> <quantity>",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int CargoAdd(CommandContext ctx, string[] args)
    {
        if (args.Length < 2)
        {
            ctx.WriteError("Usage: cargo_add <resource> <quantity>");
            ctx.WriteLine("Example: cargo_add Iron 500");
            return 1;
        }

        string resourceId = args[0];
        if (!float.TryParse(args[1], out float quantity))
        {
            ctx.WriteError($"Invalid quantity: '{args[1]}'. Must be a number.");
            return 1;
        }

        if (LoadCargo(resourceId, quantity))
        {
            float currentQty = _cargo?.GetResourceQuantity(resourceId) ?? 0;
            ctx.WriteLine(
                $"[color=green]Added {quantity} {resourceId}. Current: {currentQty}[/color]"
            );
            return 0;
        }

        ctx.WriteError($"Failed to add cargo. Resource: {resourceId}, Quantity: {quantity}");
        return 1;
    }

    [DebugCommand(
        "cargo_remove",
        "Remove cargo from this ship",
        "cargo_remove <resource> [quantity]",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int CargoRemove(CommandContext ctx, string[] args)
    {
        if (args.Length < 1)
        {
            ctx.WriteError("Usage: cargo_remove <resource> [quantity]");
            ctx.WriteLine("Example: cargo_remove Iron (removes all)");
            ctx.WriteLine("Example: cargo_remove Iron 200");
            return 1;
        }

        string resourceId = args[0];
        float quantity = float.MaxValue; // Remove all by default

        if (args.Length > 1)
        {
            if (!float.TryParse(args[1], out quantity))
            {
                ctx.WriteError($"Invalid quantity: '{args[1]}'. Must be a number.");
                return 1;
            }
        }

        // Get current quantity first
        float currentQty = _cargo?.GetResourceQuantity(resourceId) ?? 0;
        if (currentQty <= 0)
        {
            ctx.WriteError($"Resource '{resourceId}' not found in cargo.");
            return 1;
        }

        // If quantity is MaxValue, remove all
        float removeQty = quantity == float.MaxValue ? currentQty : Math.Min(quantity, currentQty);

        if (UnloadCargo(resourceId, removeQty))
        {
            float remaining = _cargo?.GetResourceQuantity(resourceId) ?? 0;
            ctx.WriteLine(
                $"[color=green]Removed {removeQty} {resourceId}. Remaining: {remaining}[/color]"
            );
            return 0;
        }

        ctx.WriteError($"Failed to remove cargo.");
        return 1;
    }

    [DebugCommand(
        "cargo_list",
        "List all cargo on this ship",
        "cargo_list",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int CargoList(CommandContext ctx, string[] args)
    {
        if (_cargo == null || _cargo.ResourceCount == 0)
        {
            ctx.WriteLine("[color=yellow]Cargo manifest is empty.[/color]");
            return 0;
        }

        ctx.WriteLine($"[color=yellow]=== Cargo for {Name} ===[/color]");
        ctx.WriteLine($"Total Mass: {_cargo.TotalCargoMass:F2}");

        foreach (var kvp in _cargo.Resources)
        {
            ctx.WriteLine($"  {kvp.Key}: {kvp.Value:F2}");
        }

        return 0;
    }

    [DebugCommand(
        "fuel_add",
        "Add fuel to this ship",
        "fuel_add <amount>",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int FuelAdd(CommandContext ctx, string[] args)
    {
        if (args.Length < 1)
        {
            ctx.WriteError("Usage: fuel_add <amount>");
            ctx.WriteLine("Example: fuel_add 500");
            return 1;
        }

        if (!float.TryParse(args[0], out float amount))
        {
            ctx.WriteError($"Invalid amount: '{args[0]}'. Must be a number.");
            return 1;
        }

        Refuel(amount);
        ctx.WriteLine(
            $"[color=green]Fuel added. Current: {_fuel:F2}/{_maxFuel:F2} ({GetFuelPercentage():F1}%)[/color]"
        );
        return 0;
    }

    [DebugCommand(
        "fuel_remove",
        "Remove fuel from this ship",
        "fuel_remove <amount>",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int FuelRemove(CommandContext ctx, string[] args)
    {
        if (args.Length < 1)
        {
            ctx.WriteError("Usage: fuel_remove <amount>");
            ctx.WriteLine("Example: fuel_remove 100");
            return 1;
        }

        if (!float.TryParse(args[0], out float amount))
        {
            ctx.WriteError($"Invalid amount: '{args[0]}'. Must be a number.");
            return 1;
        }

        ConsumeFuel(amount);
        ctx.WriteLine(
            $"[color=green]Fuel removed. Current: {_fuel:F2}/{_maxFuel:F2} ({GetFuelPercentage():F1}%)[/color]"
        );
        return 0;
    }

    [DebugCommand(
        "fuel_status",
        "Show fuel status for this ship",
        "fuel_status",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int FuelStatus(CommandContext ctx, string[] args)
    {
        ctx.WriteLine($"[color=yellow]=== Fuel Status for {Name} ===[/color]");
        ctx.WriteLine($"Current Fuel: {_fuel:F2}");
        ctx.WriteLine($"Max Fuel: {_maxFuel:F2}");
        ctx.WriteLine($"Percentage: {GetFuelPercentage():F1}%");
        ctx.WriteLine($"Has Fuel: {(HasFuel() ? "Yes" : "No")}");

        return 0;
    }

    [DebugCommand(
        "delta_v",
        "Show remaining delta-V for this ship",
        "delta_v",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int DeltaV(CommandContext ctx, string[] args)
    {
        float deltaV = CalculateRemainingDeltaV();

        ctx.WriteLine($"[color=yellow]=== Delta-V for {Name} ===[/color]");

        if (_currentEngine != null)
        {
            ctx.WriteLine($"Engine Isp: {_currentEngine.BaseSpecificImpulse:F1}");
            ctx.WriteLine($"Exhaust Velocity: {_currentEngine.ExhaustVelocity:F1} m/s");
        }
        else
        {
            ctx.WriteLine("[color=red]No engine installed![/color]");
        }

        ctx.WriteLine($"Dry Mass: {DryMass:F2}");
        ctx.WriteLine($"Fuel Mass: {_fuel:F2}");
        ctx.WriteLine($"Total Mass: {GetTotalMass():F2}");
        ctx.WriteLine($"[color=cyan]Remaining Δv: {deltaV:F2} m/s[/color]");

        return 0;
    }

    [DebugCommand(
        "plan_route",
        "Plan a route to another celestial body. Use --list to see options or --auto to select best",
        "plan_route <destination_body> [--list|--auto] [departure_time] [num_options]",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int PlanRoute(CommandContext ctx, string[] args)
    {
        if (args.Length < 1)
        {
            ctx.WriteError(
                "Usage: (Ships.0) plan_route <destination_namespace> [--list|--auto] [departure_time] [num_options]"
            );
            ctx.WriteLine("Examples:");
            ctx.WriteLine("  (Ships.0) plan_route CelestialBody.Mars --auto");
            ctx.WriteLine("  (Ships.0) plan_route CelestialBody.Mars --list");
            ctx.WriteLine("  (Ships.0) plan_route CelestialBody.Mars 60 10");
            return 1;
        }

        string destNs = args[0];
        string mode = "--auto"; // default mode
        float departureTime = 0f;
        int numOptions = 25;

        // Parse arguments
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                mode = args[i];
            }
            else if (float.TryParse(args[i], out float parsed))
            {
                if (departureTime == 0f)
                    departureTime = parsed;
                else
                    numOptions = (int)parsed;
            }
        }

        bool listOptions = mode == "--list";
        bool autoSelect = mode == "--auto";

        // Find destination
        if (!InstanceRegistry.TryGetInstance(destNs, out var destInstance))
        {
            ctx.WriteError($"Destination not found: {destNs}");
            ctx.WriteLine("Use 'list_constructables' to see available targets.");
            return 1;
        }

        if (destInstance is not ProceduralGeneration.PlanetGeneration.CelestialBody destination)
        {
            ctx.WriteError($"Target '{destNs}' is not a CelestialBody.");
            return 1;
        }

        if (_state != LogisticsUnitState.Idle)
        {
            ctx.WriteError($"Cannot plan route - ship is in {_state} state.");
            ctx.WriteLine("Ship must be in Idle state to plan a new route.");
            return 1;
        }

        // Generate route options
        var options = GetRouteOptions(destination, departureTime, numOptions);

        if (options.Count == 0)
        {
            ctx.WriteError($"No viable trajectory options found to {destination.Name}.");
            ctx.WriteLine("This may be due to insufficient fuel or engine not installed.");
            return 1;
        }

        // Store options for later selection
        _availableRouteOptions = options;
        _routeOptionsDestination = destination;

        if (listOptions)
        {
            // List all options
            ctx.WriteLine($"[color=yellow]=== Route Options to {destination.Name} ===[/color]");
            ctx.WriteLine($"Available Δv: {GetAvailableDeltaV():F2} m/s");
            ctx.WriteLine($"Fuel available: {_fuel:F2}\n");

            for (int i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                float fuelNeeded = CalculateFuelForDeltaV(opt.DeltaVRequired);
                string fuelStatus =
                    fuelNeeded <= _fuel
                        ? "[color=green]OK[/color]"
                        : "[color=red]INSUFFICIENT[/color]";
                string typeStr = opt.TransferType.ToString();

                ctx.WriteLine($"[color=cyan]Option {i}:[/color]");
                ctx.WriteLine(
                    $"  Type: {typeStr}, TOF: {opt.TimeOfFlight:F1}s, Δv: {opt.DeltaVRequired:F2} m/s"
                );
                ctx.WriteLine($"  Fuel needed: {fuelNeeded:F2} {fuelStatus}");
                ctx.WriteLine($"  Efficiency Score: {opt.EfficiencyScore:P0}");
                if (opt.Revolutions > 0)
                    ctx.WriteLine($"  Revolutions: {opt.Revolutions}");
                ctx.WriteLine($"");
            }

            ctx.WriteLine(
                $"[color=yellow]Use '(Ships.N) select_route <index>' to choose an option[/color]"
            );
            return 0;
        }

        // Auto-select: choose the first (most efficient) option
        if (autoSelect || options.Count == 1)
        {
            var selected = options[0];
            float fuelNeeded = CalculateFuelForDeltaV(selected.DeltaVRequired);

            if (fuelNeeded > _fuel)
            {
                ctx.WriteError($"Insufficient fuel for most efficient option.");
                ctx.WriteLine($"Required: {fuelNeeded:F2}, Available: {_fuel:F2}");
                ctx.WriteLine($"Use '(Ships.N) plan_route {destNs} --list' to see other options.");
                return 1;
            }

            // Plan with the selected option
            if (PlanRoute(selected))
            {
                ctx.WriteLine($"[color=green]Route planned to {destination.Name}[/color]");
                ctx.WriteLine($"Time of Flight: {selected.TimeOfFlight:F1}s");
                ctx.WriteLine($"Delta-V Required: {selected.DeltaVRequired:F2} m/s");
                ctx.WriteLine($"Fuel Required: {fuelNeeded:F2}");
                return 0;
            }

            ctx.WriteError("Failed to plan route.");
            return 1;
        }

        // Default: if multiple options and no flag, show the best one
        ctx.WriteLine(
            $"[color=yellow]Multiple options available ({options.Count}). Using most efficient.[/color]"
        );
        var best = options[0];
        float bestFuelNeeded = CalculateFuelForDeltaV(best.DeltaVRequired);

        if (PlanRoute(best))
        {
            ctx.WriteLine($"[color=green]Route planned to {destination.Name}[/color]");
            ctx.WriteLine($"Time of Flight: {best.TimeOfFlight:F1}s");
            ctx.WriteLine($"Delta-V Required: {best.DeltaVRequired:F2} m/s");
            ctx.WriteLine($"Fuel Required: {bestFuelNeeded:F2}");
            return 0;
        }

        ctx.WriteError("Failed to plan route.");
        return 1;
    }

    [DebugCommand(
        "list_routes",
        "List available route options to a destination",
        "list_routes <destination_body> [departure_time] [num_options]",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int ListRoutes(CommandContext ctx, string[] args)
    {
        if (args.Length < 1)
        {
            ctx.WriteError(
                "Usage: (Ships.0) list_routes <destination_namespace> [departure_time] [num_options]"
            );
            ctx.WriteLine("Example: (Ships.0) list_routes CelestialBody.Mars 60 10");
            return 1;
        }

        string destNs = args[0];
        float departureTime = 0f;
        int numOptions = 25;

        if (args.Length > 1)
        {
            if (float.TryParse(args[1], out float parsed))
                departureTime = parsed;
        }
        if (args.Length > 2)
        {
            if (int.TryParse(args[2], out int parsed))
                numOptions = parsed;
        }

        // Find destination
        if (!InstanceRegistry.TryGetInstance(destNs, out var destInstance))
        {
            ctx.WriteError($"Destination not found: {destNs}");
            return 1;
        }

        if (destInstance is not ProceduralGeneration.PlanetGeneration.CelestialBody destination)
        {
            ctx.WriteError($"Target '{destNs}' is not a CelestialBody.");
            return 1;
        }

        // Generate route options
        var options = GetRouteOptions(destination, departureTime, numOptions);

        if (options.Count == 0)
        {
            ctx.WriteError($"No viable trajectory options found to {destination.Name}.");
            return 1;
        }

        // Store options for later selection
        _availableRouteOptions = options;
        _routeOptionsDestination = destination;

        // Display options
        ctx.WriteLine($"[color=yellow]=== Route Options to {destination.Name} ===[/color]");
        ctx.WriteLine($"Available Δv: {GetAvailableDeltaV():F2} m/s");
        ctx.WriteLine($"Fuel available: {_fuel:F2}\n");

        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            float fuelNeeded = CalculateFuelForDeltaV(opt.DeltaVRequired);
            string fuelStatus =
                fuelNeeded <= _fuel ? "[color=green]OK[/color]" : "[color=red]INSUFFICIENT[/color]";
            string typeStr = opt.TransferType.ToString();

            ctx.WriteLine($"[color=cyan]Option {i}:[/color]");
            ctx.WriteLine(
                $"  Type: {typeStr}, TOF: {opt.TimeOfFlight:F1}s, Δv: {opt.DeltaVRequired:F2} m/s"
            );
            ctx.WriteLine($"  Fuel needed: {fuelNeeded:F2} {fuelStatus}");
            ctx.WriteLine($"  Efficiency Score: {opt.EfficiencyScore:P0}");
            if (opt.Revolutions > 0)
                ctx.WriteLine($"  Revolutions: {opt.Revolutions}");
            ctx.WriteLine($"");
        }

        ctx.WriteLine(
            $"[color=yellow]Use '(Ships.N) select_route <index>' to choose an option[/color]"
        );
        return 0;
    }

    [DebugCommand(
        "select_route",
        "Select a route option from previously generated list",
        "select_route <index>",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int SelectRoute(CommandContext ctx, string[] args)
    {
        if (args.Length < 1)
        {
            ctx.WriteError("Usage: select_route <index>");
            ctx.WriteLine(
                "Use '(Ships.N) list_routes' or '(Ships.N) plan_route --list' first to see options."
            );
            return 1;
        }

        if (!int.TryParse(args[0], out int index))
        {
            ctx.WriteError($"Invalid index: '{args[0]}'. Must be a number.");
            return 1;
        }

        if (_availableRouteOptions == null || _availableRouteOptions.Count == 0)
        {
            ctx.WriteError("No route options available.");
            ctx.WriteLine(
                "Use '(Ships.N) list_routes <destination>' or '(Ships.N) plan_route <destination> --list' first."
            );
            return 1;
        }

        if (index < 0 || index >= _availableRouteOptions.Count)
        {
            ctx.WriteError(
                $"Index out of range. Valid range: 0-{_availableRouteOptions.Count - 1}"
            );
            return 1;
        }

        var selected = _availableRouteOptions[index];
        float fuelNeeded = CalculateFuelForDeltaV(selected.DeltaVRequired);

        if (fuelNeeded > _fuel)
        {
            ctx.WriteError($"Insufficient fuel for option {index}.");
            ctx.WriteLine($"Required: {fuelNeeded:F2}, Available: {_fuel:F2}");
            return 1;
        }

        if (PlanRoute(selected))
        {
            string destName = _routeOptionsDestination?.Name ?? "Unknown";
            ctx.WriteLine($"[color=green]Route {index} selected to {destName}[/color]");
            ctx.WriteLine($"Time of Flight: {selected.TimeOfFlight:F1}s");
            ctx.WriteLine($"Delta-V Required: {selected.DeltaVRequired:F2} m/s");
            ctx.WriteLine($"Fuel Required: {fuelNeeded:F2}");

            // Clear stored options after selection
            _availableRouteOptions = null;
            _routeOptionsDestination = null;

            return 0;
        }

        ctx.WriteError("Failed to plan route.");
        return 1;
    }

    [DebugCommand(
        "execute_transfer",
        "Execute the planned transfer",
        "execute_transfer",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int ExecuteTransfer(CommandContext ctx, string[] args)
    {
        if (_state != LogisticsUnitState.Planning)
        {
            ctx.WriteError($"Cannot execute transfer - ship is in {_state} state.");
            ctx.WriteLine("Use '(Ships.N) plan_route' first to plan a transfer.");
            return 1;
        }

        if (_plannedTrajectory == null)
        {
            ctx.WriteError("No planned trajectory. Use '(Ships.N) plan_route' first.");
            return 1;
        }

        if (ExecuteTrajectory())
        {
            string destName = _plannedTrajectory?.DestinationBody.ToString() ?? "Unknown";
            ctx.WriteLine($"[color=green]Transfer initiated![/color]");
            ctx.WriteLine($"Destination: {destName}");
            return 0;
        }

        ctx.WriteError("Failed to execute transfer.");
        return 1;
    }

    [DebugCommand(
        "status",
        "Show complete status of this ship",
        "status",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int Status(CommandContext ctx, string[] args)
    {
        string parentName = GetParent()?.Name ?? "None";

        ctx.WriteLine($"[color=yellow]=== Status for {Name} ===[/color]");
        ctx.WriteLine($"ID: {Id}");
        ctx.WriteLine($"State: {_state}");
        ctx.WriteLine($"Band Index: {BandIndex}");
        ctx.WriteLine($"Parent Body: {parentName}");
        ctx.WriteLine($"Active: {IsActive}");

        ctx.WriteLine($"\n[color=cyan]Fuel:[/color]");
        ctx.WriteLine($"  Current: {_fuel:F2} / {_maxFuel:F2} ({GetFuelPercentage():F1}%)");

        ctx.WriteLine($"\n[color=cyan]Cargo:[/color]");
        if (_cargo != null && _cargo.ResourceCount > 0)
        {
            ctx.WriteLine($"  Total Mass: {_cargo.TotalCargoMass:F2}");
            foreach (var kvp in _cargo.Resources)
            {
                ctx.WriteLine($"  {kvp.Key}: {kvp.Value:F2}");
            }
        }
        else
        {
            ctx.WriteLine("  (empty)");
        }

        ctx.WriteLine($"\n[color=cyan]Engine:[/color]");
        if (_currentEngine != null)
        {
            ctx.WriteLine($"  Isp: {_currentEngine.BaseSpecificImpulse:F1}");
            ctx.WriteLine($"  Thrust: {_currentEngine.BaseThrust:F1}");
            ctx.WriteLine($"  Δv: {CalculateRemainingDeltaV():F2} m/s");
        }
        else
        {
            ctx.WriteLine("  (none)");
        }

        ctx.WriteLine($"\n[color=cyan]Transfer:[/color]");
        if (_state == LogisticsUnitState.Planning && _plannedTrajectory != null)
        {
            ctx.WriteLine($"  Planned Destination: {_destinationBody!.ToString() ?? "Unknown"}");
            ctx.WriteLine($"  Time of Flight: {_plannedTrajectory.TimeOfFlight:F1}s");
            ctx.WriteLine($"  Delta-V: {_plannedTrajectory.DeltaVRequired:F2} m/s");
        }
        else if (_state == LogisticsUnitState.InTransit || _state == LogisticsUnitState.Stranded)
        {
            ctx.WriteLine($"  Status: {_state}");
            if (_movementController != null && _movementController.IsTransferring)
            {
                float progress = _movementController.TransferProgress * 100f;
                ctx.WriteLine($"  Progress: {progress:F1}%");
                ctx.WriteLine($"  Phase: {_movementController.CurrentTransitPhase}");
                var bp = _movementController.ActiveBurnProfile;
                if (bp != null)
                {
                    ctx.WriteLine($"  Burn Profile: {bp.GetDescription()}");
                    ctx.WriteLine(
                        $"  Fuel Consumed: {_movementController.FuelConsumedThisTransfer:F2}kg "
                            + $"/ {bp.TotalFuelBudget:F2}kg budgeted"
                    );
                    float estRemaining =
                        _fuel - (bp.TotalFuelBudget - _movementController.FuelConsumedThisTransfer);
                    ctx.WriteLine(
                        $"  Est. Fuel at Arrival: {Godot.Mathf.Max(0f, estRemaining):F2}"
                    );
                }
            }
            else
            {
                ctx.WriteLine($"  Progress: {(_timeInTransfer / _transferTime * 100):F1}%");
            }
        }
        else if (_availableRouteOptions != null && _availableRouteOptions.Count > 0)
        {
            ctx.WriteLine(
                $"  [color=yellow]Route Options Available ({_availableRouteOptions.Count}):[/color]"
            );
            ctx.WriteLine($"  Destination: {_routeOptionsDestination?.Name ?? "Unknown"}");
            for (int i = 0; i < _availableRouteOptions.Count; i++)
            {
                var opt = _availableRouteOptions[i];
                float fuelNeeded = CalculateFuelForDeltaV(opt.DeltaVRequired);
                string fuelStatus = fuelNeeded <= _fuel ? "[green]✓[/green]" : "[red]✗[/red]";
                ctx.WriteLine(
                    $"    Option {i}: {opt.TransferType}, TOF: {opt.TimeOfFlight:F1}s, "
                        + $"Δv: {opt.DeltaVRequired:F2}, Fuel: {fuelNeeded:F2} {fuelStatus}"
                );
            }
            ctx.WriteLine($"  [color=cyan]Use '(Ships.N) select_route <index>' to choose[/color]");
        }
        else
        {
            ctx.WriteLine("  (none planned)");
        }

        return 0;
    }

    [DebugCommand(
        "transfer_info",
        "Show details of the planned or active transfer",
        "transfer_info",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public int TransferInfo(CommandContext ctx, string[] args)
    {
        if (_state == LogisticsUnitState.Idle || _state == LogisticsUnitState.Disabled)
        {
            ctx.WriteLine("[color=yellow]No active or planned transfer.[/color]");
            return 0;
        }

        ctx.WriteLine($"[color=yellow]=== Transfer Info for {Name} ===[/color]");
        ctx.WriteLine($"State: {_state}");

        if (_plannedTrajectory != null)
        {
            ctx.WriteLine($"\n[color=cyan]Planned Trajectory:[/color]");
            ctx.WriteLine($"  Transfer Type: {_plannedTrajectory.TransferType}");
            ctx.WriteLine($"  Time of Flight: {_plannedTrajectory.TimeOfFlight:F1}s");
            ctx.WriteLine(
                $"  Delta-V Required: {_plannedTrajectory.DeltaVRequired:F2} m/s "
                    + $"(depart: {_plannedTrajectory.DepartureDeltaV:F2}, "
                    + $"arrive: {_plannedTrajectory.ArrivalDeltaV:F2})"
            );
            ctx.WriteLine($"  Semi-Major Axis: {_plannedTrajectory.SemiMajorAxis:F2}");
            ctx.WriteLine($"  Eccentricity: {_plannedTrajectory.Eccentricity:F2}");
            ctx.WriteLine($"  Revolutions: {_plannedTrajectory.Revolutions}");
        }

        if (_destinationBody != null)
        {
            ctx.WriteLine($"\n[color=cyan]Destination:[/color]");
            ctx.WriteLine($"  Body: {_destinationBody.ToString()}");
            ctx.WriteLine(
                $"  Distance: {GlobalPosition.DistanceTo(_destinationBody.BodyPosition):F2}"
            );
        }

        if (_state == LogisticsUnitState.InTransit || _state == LogisticsUnitState.Stranded)
        {
            ctx.WriteLine($"\n[color=cyan]Progress:[/color]");

            if (
                _movementController != null
                && (_movementController.IsTransferring || _state == LogisticsUnitState.Stranded)
            )
            {
                float progress = _movementController.TransferProgress;
                ctx.WriteLine($"  Progress: {progress * 100:F1}%");
                ctx.WriteLine($"  Phase: {_movementController.CurrentTransitPhase}");

                var bp = _movementController.ActiveBurnProfile;
                if (bp != null)
                {
                    ctx.WriteLine($"\n[color=cyan]Burn Profile:[/color]");
                    ctx.WriteLine(
                        $"  Accelerating: {bp.AccelBurnDuration:F1}s ({bp.AccelFuelBudget:F2}kg @ {bp.AccelFuelRate:F3}kg/s)"
                    );
                    ctx.WriteLine($"  Coasting:     {bp.CoastDuration:F1}s (no fuel)");
                    ctx.WriteLine(
                        $"  Decelerating: {bp.DecelBurnDuration:F1}s ({bp.DecelFuelBudget:F2}kg @ {bp.DecelFuelRate:F3}kg/s)"
                    );
                    ctx.WriteLine($"  Total Fuel Budget: {bp.TotalFuelBudget:F2}kg");
                }

                ctx.WriteLine($"\n[color=cyan]Fuel Consumption:[/color]");
                ctx.WriteLine(
                    $"  Consumed This Transfer: {_movementController.FuelConsumedThisTransfer:F2}kg"
                );
                ctx.WriteLine($"  Current Fuel: {_fuel:F2} / {_maxFuel:F2}");

                if (bp != null)
                {
                    float remaining =
                        bp.TotalFuelBudget - _movementController.FuelConsumedThisTransfer;
                    ctx.WriteLine($"  Remaining Budget: {Godot.Mathf.Max(0f, remaining):F2}kg");
                    float estArrival = _fuel - Godot.Mathf.Max(0f, remaining);
                    ctx.WriteLine(
                        $"  Est. Fuel at Arrival: {Godot.Mathf.Max(0f, estArrival):F2}kg"
                    );
                }
            }
            else
            {
                float progress = _transferTime > 0 ? _timeInTransfer / _transferTime : 0;
                ctx.WriteLine($"  Time Elapsed: {_timeInTransfer:F1}s / {_transferTime:F1}s");
                ctx.WriteLine($"  Progress: {progress * 100:F1}%");
            }

            if (_state == LogisticsUnitState.Stranded)
            {
                ctx.WriteLine(
                    $"\n[color=red]*** STRANDED — fuel exhausted mid-transit ***[/color]"
                );
                ctx.WriteLine(
                    $"[color=red]Ship is adrift and cannot complete arrival burn.[/color]"
                );
            }
        }

        return 0;
    }
}
#endif
