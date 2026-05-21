using System;
using Godot;
using UtilityLibrary;

namespace Constructables.Stations;

/// <summary>
/// Reflection loader for IStationBehavior implementations referenced by
/// StationDefinition.BehaviorRefs (parsed from YAML).
/// Accepts either a class name or a "res://...cs" script path.
/// </summary>
public static class StationBehaviorFactory
{
    public static IStationBehavior? Create(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
            return null;

        try
        {
            if (nameOrPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                return CreateFromScript(nameOrPath);
            return CreateByName(nameOrPath);
        }
        catch (System.Exception ex)
        {
            GameLogger.Error(
                $"StationBehaviorFactory: Failed to instantiate behavior '{nameOrPath}': {ex.Message}"
            );
            return null;
        }
    }

    private static IStationBehavior? CreateFromScript(string filePath)
    {
        var script = GD.Load<CSharpScript>(filePath);
        if (script == null)
        {
            GameLogger.Error($"StationBehaviorFactory: Could not load script at '{filePath}'");
            return null;
        }

        var godotObj = script.New().AsGodotObject();
        if (godotObj is not IStationBehavior behavior)
        {
            GameLogger.Error(
                $"StationBehaviorFactory: Script '{filePath}' does not implement IStationBehavior"
            );
            godotObj?.Free();
            return null;
        }
        return behavior;
    }

    private static IStationBehavior? CreateByName(string className)
    {
        var assembly = typeof(IStationBehavior).Assembly;

        var type = assembly.GetType(className)
            ?? assembly.GetType($"Constructables.Stations.Behaviors.{className}")
            ?? assembly.GetType($"Constructables.Stations.{className}");

        if (type == null)
        {
            GameLogger.Error(
                $"StationBehaviorFactory: Could not find type '{className}'"
            );
            return null;
        }

        if (!typeof(IStationBehavior).IsAssignableFrom(type))
        {
            GameLogger.Error(
                $"StationBehaviorFactory: Type '{className}' does not implement IStationBehavior"
            );
            return null;
        }

        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor == null)
        {
            GameLogger.Error(
                $"StationBehaviorFactory: Type '{className}' has no parameterless constructor"
            );
            return null;
        }
        return (IStationBehavior)ctor.Invoke(null);
    }
}
