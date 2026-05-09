using System;
using Godot;

namespace Constructables.Buildings;

/// <summary>
/// Reflection loader for IBuildingBehavior implementations referenced by
/// BuildingDefinition.BehaviorRefs (parsed from YAML).
/// Mirrors BuildingConfigLoader.InstantiateBehaviorByName for IPlacementBehavior.
/// Accepts either a class name or a "res://...cs" script path.
/// </summary>
public static class BehaviorFactory
{
    public static IBuildingBehavior? Create(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
            return null;

        try
        {
            if (nameOrPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                return CreateFromScript(nameOrPath);
            return CreateByName(nameOrPath);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"BehaviorFactory: Failed to instantiate behavior '{nameOrPath}': {ex.Message}");
            return null;
        }
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
