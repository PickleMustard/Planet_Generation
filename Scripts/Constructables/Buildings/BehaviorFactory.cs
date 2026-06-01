using System;
using System.Collections.Generic;
using Godot;

namespace Constructables.Buildings;

/// <summary>
/// Reflection loader for IBuildingBehavior implementations referenced by
/// BuildingDefinition.BehaviorEntries (parsed from YAML).
/// Mirrors BuildingConfigLoader.InstantiateBehaviorByName for IPlacementBehavior.
/// Accepts either a class name or a "res://...cs" script path.
/// </summary>
public static class BehaviorFactory
{
    /// <summary>
    /// Creates a behavior instance and, if it implements IBehaviorConfigurable,
    /// calls Configure() with the provided config dict before returning.
    /// </summary>
    public static IBuildingBehavior? Create(
        string nameOrPath,
        Dictionary<string, object>? config = null)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
            return null;

        try
        {
            IBuildingBehavior? behavior;
            if (nameOrPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                behavior = CreateFromScript(nameOrPath);
            else
                behavior = CreateByName(nameOrPath);

            if (behavior is IBehaviorConfigurable configurable && config != null)
                configurable.Configure(config);

            return behavior;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"BehaviorFactory: Failed to instantiate behavior '{nameOrPath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Legacy parameterless overload — callers without config dicts
    /// (StationSatellite, tests) continue to use this.
    /// </summary>
    public static IBuildingBehavior? Create(string nameOrPath)
    {
        return Create(nameOrPath, config: null);
    }

    private static IBuildingBehavior? CreateFromScript(string filePath)
    {
        var script = GD.Load<CSharpScript>(filePath);
        if (script == null)
        {
            GD.PrintErr($"BehaviorFactory: Could not load script at '{filePath}'");
            return null;
        }

        var godotObj = script.New().AsGodotObject();
        if (godotObj is not IBuildingBehavior behavior)
        {
            GD.PrintErr($"BehaviorFactory: Script '{filePath}' does not implement IBuildingBehavior");
            godotObj?.Free();
            return null;
        }
        return behavior;
    }

    private static IBuildingBehavior? CreateByName(string className)
    {
        var assembly = typeof(IBuildingBehavior).Assembly;

        var type = assembly.GetType(className)
            ?? assembly.GetType($"Constructables.Buildings.Behaviors.{className}")
            ?? assembly.GetType($"Constructables.Buildings.{className}");

        if (type == null)
        {
            GD.PrintErr($"BehaviorFactory: Could not find type '{className}'");
            return null;
        }

        if (!typeof(IBuildingBehavior).IsAssignableFrom(type))
        {
            GD.PrintErr($"BehaviorFactory: Type '{className}' does not implement IBuildingBehavior");
            return null;
        }

        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor == null)
        {
            GD.PrintErr($"BehaviorFactory: Type '{className}' has no parameterless constructor");
            return null;
        }
        return (IBuildingBehavior)ctor.Invoke(null);
    }
}
