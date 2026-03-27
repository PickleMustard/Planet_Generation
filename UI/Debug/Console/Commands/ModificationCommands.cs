#if DEBUG
using System;
using System.Reflection;
using Godot;
using Structures.Enums;
using ProceduralGeneration.PlanetGeneration;
using ProceduralGeneration.MeshGeneration;
using Structures.Resources;

namespace UI.Debug.Console;

public static class ModificationCommands
{
    [DebugCommand("spawn", "Spawn a celestial body of the specified type", "spawn <type> [name] [position]", Category = "Modification")]
    public static int Spawn(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: spawn <type> [name] [position]");
            ctx.WriteLine("Types: Star, RockyPlanet, GasGiant, Moon, Asteroid, Comet, BlackHole");
            ctx.WriteLine("Example: spawn RockyPlanet NewPlanet (100,0,50)");
            return 1;
        }

        var typeStr = args[0];
        if (!Enum.TryParse<CelestialBodyType>(typeStr, true, out var bodyType))
        {
            ctx.WriteError($"Unknown celestial body type: {typeStr}");
            ctx.WriteLine("Available types: Star, RockyPlanet, GasGiant, Moon, Asteroid, Comet, BlackHole");
            return 1;
        }

        var name = args.Length > 1 ? args[1] : $"{bodyType}_{DateTime.Now.Ticks % 10000}";

        Vector3 position = Vector3.Zero;
        if (args.Length > 2)
        {
            var posStr = string.Join(" ", args, 2, args.Length - 2);
            if (!TryParseVector3(posStr, out position))
            {
                ctx.WriteWarning($"Could not parse position '{posStr}', using zero vector");
            }
        }

        var sceneTree = Engine.GetMainLoop() as SceneTree;
        var systemContainer = sceneTree?.Root?.FindChild("SystemContainer", true, false);

        if (systemContainer == null)
        {
            systemContainer = sceneTree?.Root?.FindChild("CelestialBodies", true, false);
        }

        if (systemContainer == null)
        {
            ctx.WriteError("Could not find SystemContainer or CelestialBodies node");
            ctx.WriteLine("Make sure the scene has a container for celestial bodies");
            return 1;
        }

        try
        {
            var mesh = new UnifiedCelestialMesh();
            var builder = new CelestialBody.Builder();
            builder.WithType(bodyType);
            builder.WithMesh(mesh);

            var celestialBody = builder.Build();
            celestialBody.Name = name;

            systemContainer.AddChild(celestialBody);
            celestialBody.Position = position;

            InstanceRegistry.RegisterNode(celestialBody);

            ctx.WriteLine($"[color=green]Spawned {bodyType} '{name}' at position {position}[/color]");
            ctx.WriteLine($"Namespace: {InstanceRegistry.GetNamespace(celestialBody)}");

            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to spawn celestial body: {ex.Message}");
            return 1;
        }
    }

    [DebugCommand("reload_resources", "Reload resource configurations from disk", "reload_resources", Category = "Modification")]
    public static int ReloadResources(CommandContext ctx, string[] args)
    {
        try
        {
            var database = ResourceDatabase.Instance;
            if (database == null)
            {
                ctx.WriteError("ResourceDatabase not available. Ensure it is registered as an autoload.");
                return 1;
            }

            var reloadMethod = typeof(ResourceDatabase).GetMethod("Reload",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (reloadMethod != null)
            {
                reloadMethod.Invoke(database, null);
                ctx.WriteLine("[color=green]Resource configurations reloaded successfully[/color]");
            }
            else
            {
                var loadMethod = typeof(ResourceDatabase).GetMethod("LoadResources",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (loadMethod != null)
                {
                    loadMethod.Invoke(database, null);
                    ctx.WriteLine("[color=green]Resource configurations reloaded successfully[/color]");
                }
                else
                {
                    ctx.WriteError("Could not find reload method on ResourceDatabase");
                    ctx.WriteLine("You may need to restart the game to reload resources");
                    return 1;
                }
            }

            var resources = database.GetAllResources();
            ctx.WriteLine($"Loaded {resources.Count} resource definitions");

            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to reload resources: {ex.Message}");
            return 1;
        }
    }

    [DebugCommand("set_param", "Set a generation parameter for an instance", "set_param <namespace> <param> <value>", Category = "Modification")]
    public static int SetParam(CommandContext ctx, string[] args)
    {
        if (args.Length < 3)
        {
            ctx.WriteError("Usage: set_param <namespace> <param> <value>");
            ctx.WriteLine("Example: set_param CelestialBody.Earth size 64");
            return 1;
        }

        var ns = args[0];
        var paramName = args[1];
        var valueStr = string.Join(" ", args, 2, args.Length - 2);

        if (!InstanceRegistry.TryGetInstance(ns, out var instance))
        {
            ctx.WriteError($"Instance not found: {ns}");
            return 1;
        }

        if (instance is CelestialBody body)
        {
            return SetCelestialBodyParam(ctx, body, paramName, valueStr);
        }

        if (instance is UnifiedCelestialMesh mesh)
        {
            return SetMeshParam(ctx, mesh, paramName, valueStr);
        }

        ctx.WriteError($"Unknown instance type for set_param: {instance!.GetType().Name}");
        return 1;
    }

    private static int SetCelestialBodyParam(CommandContext ctx, CelestialBody body, string paramName, string valueStr)
    {
        try
        {
            switch (paramName.ToLowerInvariant())
            {
                case "mass":
                    if (float.TryParse(valueStr, out var mass))
                    {
                        body.Mass = mass;
                        ctx.WriteLine($"[color=green]Set mass = {mass}[/color]");
                        return 0;
                    }
                    ctx.WriteError($"Invalid float value: {valueStr}");
                    return 1;

                case "velocity":
                    if (TryParseVector3(valueStr, out var velocity))
                    {
                        body.Velocity = velocity;
                        ctx.WriteLine($"[color=green]Set velocity = {velocity}[/color]");
                        return 0;
                    }
                    ctx.WriteError($"Invalid Vector3 value: {valueStr}. Use format: (x,y,z)");
                    return 1;

                case "position":
                    if (TryParseVector3(valueStr, out var position))
                    {
                        body.Position = position;
                        ctx.WriteLine($"[color=green]Set position = {position}[/color]");
                        return 0;
                    }
                    ctx.WriteError($"Invalid Vector3 value: {valueStr}. Use format: (x,y,z)");
                    return 1;

                default:
                    ctx.WriteError($"Unknown parameter: {paramName}");
                    ctx.WriteLine("Available parameters: mass, velocity, position");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to set parameter: {ex.Message}");
            return 1;
        }
    }

    private static int SetMeshParam(CommandContext ctx, UnifiedCelestialMesh mesh, string paramName, string valueStr)
    {
        try
        {
            var type = mesh.GetType();
            var property = type.GetProperty(paramName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null)
            {
                ctx.WriteError($"Unknown mesh parameter: {paramName}");
                return 1;
            }

            if (!property.CanWrite)
            {
                ctx.WriteError($"Parameter {paramName} is read-only");
                return 1;
            }

            object value;
            var propType = property.PropertyType;

            if (propType == typeof(int))
            {
                if (!int.TryParse(valueStr, out var intVal))
                {
                    ctx.WriteError($"Invalid integer value: {valueStr}");
                    return 1;
                }
                value = intVal;
            }
            else if (propType == typeof(float))
            {
                if (!float.TryParse(valueStr, out var floatVal))
                {
                    ctx.WriteError($"Invalid float value: {valueStr}");
                    return 1;
                }
                value = floatVal;
            }
            else if (propType == typeof(bool))
            {
                value = valueStr.ToLowerInvariant() is "true" or "1" or "yes";
            }
            else
            {
                ctx.WriteError($"Unsupported parameter type: {propType.Name}");
                return 1;
            }

            property.SetValue(mesh, value);
            ctx.WriteLine($"[color=green]Set {paramName} = {value}[/color]");
            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to set mesh parameter: {ex.Message}");
            return 1;
        }
    }

    private static bool TryParseVector3(string input, out Vector3 result)
    {
        result = Vector3.Zero;

        var trimmed = input.Trim('(', ')', ' ');
        var parts = trimmed.Split(',');

        if (parts.Length != 3)
            return false;

        if (!float.TryParse(parts[0].Trim(), out var x))
            return false;
        if (!float.TryParse(parts[1].Trim(), out var y))
            return false;
        if (!float.TryParse(parts[2].Trim(), out var z))
            return false;

        result = new Vector3(x, y, z);
        return true;
    }
}
#endif
