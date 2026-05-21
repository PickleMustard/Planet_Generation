#if DEBUG
using System;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using UtilityLibrary;
using UtilityLibrary.GameMath.Orbital;

namespace Debug.Console;

/// <summary>
/// Debug console commands for simulation control.
/// </summary>
public static class SimulationCommands
{
    private const string SimulationCategory = "Simulation";

    [DebugCommand(
        "pause_simulation",
        "Pause the orbital simulation by setting time scale to 0",
        "pause_simulation",
        Category = SimulationCategory
    )]
    public static int PauseSimulation(CommandContext ctx, string[] args)
    {
        if (Godot.Engine.TimeScale <= 0)
        {
            ctx.WriteLine("Simulation is already paused");
            return 0;
        }

        float previousTimeScale = (float)Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 0.0;

        // Update runtime state if available
        RuntimeSettings.Instance?.SetSetting("Simulation", "TimeScale", 0f);
        RuntimeSettings.Instance?.SetSetting("Simulation", "IsPaused", true);

        GameLogger.Info($"Simulation paused (previous TimeScale: {previousTimeScale})");
        ctx.WriteLine(
            $"[color=green]Simulation paused[/color] (TimeScale: {previousTimeScale} -> 0)"
        );
        return 0;
    }

    [DebugCommand(
        "resume_simulation",
        "Resume the orbital simulation by setting time scale to 1",
        "resume_simulation",
        Category = SimulationCategory
    )]
    public static int ResumeSimulation(CommandContext ctx, string[] args)
    {
        if (Godot.Engine.TimeScale > 0)
        {
            ctx.WriteLine("Simulation is already running");
            return 0;
        }

        float previousTimeScale = (float)Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 1.0;

        // Update runtime state if available
        RuntimeSettings.Instance?.SetSetting("Simulation", "TimeScale", 1f);
        RuntimeSettings.Instance?.SetSetting("Simulation", "IsPaused", false);

        GameLogger.Info($"Simulation resumed (previous TimeScale: {previousTimeScale})");
        ctx.WriteLine(
            $"[color=green]Simulation resumed[/color] (TimeScale: {previousTimeScale} -> 1)"
        );
        return 0;
    }

    [DebugCommand(
        "sim_status",
        "Show the current simulation status (paused/running, time scale)",
        "sim_status",
        Category = SimulationCategory
    )]
    public static int SimStatus(CommandContext ctx, string[] args)
    {
        float timeScale = (float)Godot.Engine.TimeScale;
        bool isPaused = timeScale <= 0;

        string status = isPaused ? "[color=red]PAUSED[/color]" : "[color=green]RUNNING[/color]";
        ctx.WriteLine($"Simulation Status: {status}");
        ctx.WriteLine($"TimeScale: {timeScale}");

        // Also show the original time scale if paused (for resume reference)
        if (isPaused)
        {
            float? originalTimeScale = RuntimeSettings.Instance?.GetSetting<float>(
                "Simulation",
                "TimeScaleBeforePause"
            );
            if (originalTimeScale.HasValue)
            {
                ctx.WriteLine($"(was: {originalTimeScale})");
            }
        }

        return 0;
    }

    [DebugCommand(
        "set_time_scale",
        "Set the simulation time scale (0=paused, 1=normal, >1=fast, <1=slow)",
        "set_time_scale [value]",
        Category = SimulationCategory
    )]
    public static int SetTimeScale(CommandContext ctx, string[] args)
    {
        if (args.Length == 0 || !float.TryParse(args[0], out float newScale))
        {
            ctx.WriteError("Usage: set_time_scale <value>");
            ctx.WriteLine("  0   = paused");
            ctx.WriteLine("  0.5 = half speed");
            ctx.WriteLine("  1   = normal");
            ctx.WriteLine("  2   = double speed");
            return 1;
        }

        if (newScale < 0)
        {
            ctx.WriteError("Time scale cannot be negative");
            return 1;
        }

        float previousScale = (float)Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = (double)newScale;

        // Save the original scale before pausing
        if (newScale == 0 && previousScale > 0)
        {
            RuntimeSettings.Instance?.SetSetting(
                "Simulation",
                "TimeScaleBeforePause",
                previousScale
            );
        }

        // Update runtime state
        RuntimeSettings.Instance?.SetSetting("Simulation", "TimeScale", newScale);
        RuntimeSettings.Instance?.SetSetting("Simulation", "IsPaused", newScale <= 0);

        GameLogger.Info($"Time scale changed: {previousScale} -> {newScale}");
        ctx.WriteLine(
            $"TimeScale: [color=yellow]{previousScale}[/color] -> [color=yellow]{newScale}[/color]"
        );
        return 0;
    }

    [DebugCommand(
        "fast_forward",
        "Propagate all orbital bodies to their positions after N seconds using Keplerian propagation. Works regardless of pause state.",
        "fast_forward <seconds>",
        Category = SimulationCategory
    )]
    public static int FastForward(CommandContext ctx, string[] args)
    {
        if (args.Length == 0 || !float.TryParse(args[0], out float seconds))
        {
            ctx.WriteError("Usage: fast_forward <seconds>");
            ctx.WriteLine("  Example: fast_forward 3600  (1 hour)");
            ctx.WriteLine("  Example: fast_forward 86400 (1 day)");
            return 1;
        }

        if (seconds <= 0)
        {
            ctx.WriteError("Seconds must be positive");
            return 1;
        }

        ctx.WriteLine($"[color=yellow]Fast-forwarding {seconds} seconds...[/color]");
        GameLogger.Info($"Fast-forward command invoked: {seconds} seconds");

        // Get all celestial bodies from the scene tree
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree == null)
        {
            ctx.WriteError("Could not get scene tree");
            return 1;
        }
        var celestialBodies = sceneTree.GetNodesInGroup("CelestialBody");
        if (celestialBodies.Count == 0)
        {
            ctx.WriteLine("No celestial bodies found in scene");
            return 0;
        }

        int celestialCount = 0;
        int satelliteCount = 0;

        foreach (CelestialBody body in celestialBodies)
        {
            if (body.Mass <= 0)
            {
                ctx.WriteLine($"  Skipping {body.Name} (mass = 0)");
                continue;
            }

            // For N-body simulation, each body is affected by all others
            // We'll use a simplified approach: propagate based on the most massive body (central star)
            // or use the body's own mass for self-orbiting
            var centralBody = OrbitalMath.FindCentralBody(body);
            float mu = OrbitalMath.GetGravitationalParameter(centralBody);

            Vector3 currentPos = body.GlobalPosition;
            Vector3 currentVel = body.Velocity;

            // Propagate using Keplerian mechanics
            try
            {
                (Vector3 newPos, Vector3 newVel) = KeplerianMechanics.PropagateKepler(
                    currentPos,
                    currentVel,
                    mu,
                    seconds
                );

                if (Single.IsNaN(newPos.X) || Single.IsNaN(newPos.Y) || Single.IsNaN(newPos.Z))
                {
                    newPos = currentPos;
                }
                body.GlobalPosition = newPos;
                if (Single.IsNaN(newVel.X) || Single.IsNaN(newVel.Y) || Single.IsNaN(newVel.Z))
                {
                    newVel = currentVel;
                }
                body.Velocity = newVel;
            }
            catch (Exception e)
            {
                ctx.WriteError($"Propagation failed: {e.Message}\n{e.StackTrace}");
                GameLogger.Error($"Propagation failed: {e.Message}\n{e.StackTrace}");
            }

            //ctx.WriteLine($"  {body.Name}: pos({newPos.X:F1}, {newPos.Y:F1}, {newPos.Z:F1})");
            celestialCount++;

            // Also update any SatelliteBody children
            var children = body.GetChildren();
            foreach (var child in children)
            {
                if (child is SatelliteBody satellite)
                {
                    if (satellite.Mass <= 0)
                        continue;

                    // Satellites orbit their parent body
                    float satelliteMu = OrbitalMath.GRAVITATIONAL_CONSTANT * body.Mass;

                    Vector3 satPos = satellite.GlobalPosition;
                    Vector3 satVel = satellite.Velocity;

                    (Vector3 newSatPos, Vector3 newSatVel) = KeplerianMechanics.PropagateKepler(
                        satPos,
                        satVel,
                        satelliteMu,
                        seconds
                    );

                    satellite.GlobalPosition = newSatPos;

                    satelliteCount++;
                }
            }
        }

        ctx.WriteLine(
            $"[color=green]Updated {celestialCount} celestial bodies, {satelliteCount} satellites[/color]"
        );
        return 0;
    }
}
#endif
