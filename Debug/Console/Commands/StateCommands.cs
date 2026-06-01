#if DEBUG
using System;
using System.Reflection;
using Godot;
using UtilityLibrary;

namespace Debug.Console;

public static class StateCommands
{
    [DebugCommand("set_log_level", "Change the Logger log level", "set_log_level <level>", Category = "State")]
    public static int SetLogLevel(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: set_log_level <level>");
            ctx.WriteLine("Available levels: DEBUG, INFO, WARNING, ERROR, CRITICAL, PROD");
            ctx.WriteLine($"Current level: {GameLogger.logMode}");
            return 1;
        }

        var levelStr = args[0].ToUpperInvariant();

        if (!Enum.TryParse<GameLogger.Mode>(levelStr, out var level))
        {
            ctx.WriteError($"Invalid log level: {levelStr}");
            ctx.WriteLine("Available levels: DEBUG, INFO, WARNING, ERROR, CRITICAL, PROD");
            return 1;
        }

        GameLogger.logMode = level;
        ctx.WriteLine($"[color=green]Log level set to: {level}[/color]");
        return 0;
    }

    [DebugCommand("toggle_wireframe", "Toggle wireframe rendering mode", "toggle_wireframe", Category = "State")]
    public static int ToggleWireframe(CommandContext ctx, string[] args)
    {
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        var viewport = sceneTree?.Root?.GetViewport();
        if (viewport == null)
        {
            ctx.WriteError("Could not get main viewport");
            return 1;
        }

        var debugDraw = viewport.DebugDraw;

        if (debugDraw == Viewport.DebugDrawEnum.Wireframe)
        {
            viewport.DebugDraw = Viewport.DebugDrawEnum.Disabled;
            ctx.WriteLine("[color=green]Wireframe mode: DISABLED[/color]");
        }
        else
        {
            viewport.DebugDraw = Viewport.DebugDrawEnum.Wireframe;
            ctx.WriteLine("[color=green]Wireframe mode: ENABLED[/color]");
        }

        return 0;
    }

    [DebugCommand("set", "Set a property value on an instance", "set <namespace>.<property> <value>", Category = "State")]
    public static int Set(CommandContext ctx, string[] args)
    {
        if (args.Length < 2)
        {
            ctx.WriteError("Usage: set <namespace>.<property> <value>");
            ctx.WriteLine("Example: set CelestialBody.Earth.Mass 1000");
            return 1;
        }

        var path = args[0];
        var valueStr = string.Join(" ", args, 1, args.Length - 1);
        var parts = path.Split('.', 2);

        if (parts.Length < 2)
        {
            ctx.WriteError("Invalid path format. Use: <namespace>.<property>");
            return 1;
        }

        var ns = parts[0];
        var propertyPath = parts[1];

        if (!InstanceRegistry.TryGetInstance(ns, out var instance))
        {
            ctx.WriteError($"Instance not found: {ns}");
            return 1;
        }

        return SetPropertyValue(ctx, instance!, propertyPath, valueStr);
    }

    private static int SetPropertyValue(CommandContext ctx, object instance, string propertyPath, string valueStr)
    {
        var parts = propertyPath.Split('.');
        object? current = instance;
        Type currentType = instance.GetType();
        PropertyInfo? property = null;
        FieldInfo? field = null;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (current == null)
            {
                ctx.WriteError($"Null reference at property: {part}");
                return 1;
            }

            property = currentType.GetProperty(part,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null)
            {
                field = currentType.GetField(part,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (field == null)
                {
                    ctx.WriteError($"Property/field not found: {part} on type {currentType.Name}");
                    return 1;
                }

                current = field.GetValue(current);
            }
            else
            {
                current = property.GetValue(current);
            }

            if (current != null)
            {
                currentType = current.GetType();
            }
        }

        var finalPart = parts[^1];
        property = currentType.GetProperty(finalPart,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        field = currentType.GetField(finalPart,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property == null && field == null)
        {
            ctx.WriteError($"Property/field not found: {finalPart} on type {currentType.Name}");
            return 1;
        }

        Type targetType = property?.PropertyType ?? field!.FieldType;

        if (!TryParseValue(valueStr, targetType, out var parsedValue))
        {
            ctx.WriteError($"Cannot convert '{valueStr}' to type {targetType.Name}");
            return 1;
        }

        try
        {
            if (property != null)
            {
                if (!property.CanWrite)
                {
                    ctx.WriteError($"Property {finalPart} is read-only");
                    return 1;
                }
                property.SetValue(current, parsedValue);
            }
            else
            {
                field!.SetValue(current, parsedValue);
            }

            ctx.WriteLine($"[color=green]Set {finalPart} = {FormatValue(parsedValue!)}[/color]");
            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to set value: {ex.Message}");
            return 1;
        }
    }

    private static bool TryParseValue(string valueStr, Type targetType, out object? value)
    {
        value = null;

        try
        {
            if (targetType == typeof(string))
            {
                value = valueStr;
                return true;
            }

            if (targetType == typeof(bool))
            {
                if (bool.TryParse(valueStr, out var b))
                {
                    value = b;
                    return true;
                }
                value = valueStr.ToLowerInvariant() is "1" or "yes" or "true" or "on";
                return true;
            }

            if (targetType == typeof(int))
            {
                if (int.TryParse(valueStr, out var i))
                {
                    value = i;
                    return true;
                }
                return false;
            }

            if (targetType == typeof(float))
            {
                if (float.TryParse(valueStr, out var f))
                {
                    value = f;
                    return true;
                }
                return false;
            }

            if (targetType == typeof(double))
            {
                if (double.TryParse(valueStr, out var d))
                {
                    value = d;
                    return true;
                }
                return false;
            }

            if (targetType == typeof(Vector2))
            {
                var parts = valueStr.Trim('(', ')').Split(',');
                if (parts.Length == 2 &&
                    float.TryParse(parts[0].Trim(), out var x) &&
                    float.TryParse(parts[1].Trim(), out var y))
                {
                    value = new Vector2(x, y);
                    return true;
                }
                return false;
            }

            if (targetType == typeof(Vector3))
            {
                var parts = valueStr.Trim('(', ')').Split(',');
                if (parts.Length == 3 &&
                    float.TryParse(parts[0].Trim(), out var x) &&
                    float.TryParse(parts[1].Trim(), out var y) &&
                    float.TryParse(parts[2].Trim(), out var z))
                {
                    value = new Vector3(x, y, z);
                    return true;
                }
                return false;
            }

            if (targetType.IsEnum)
            {
                value = Enum.Parse(targetType, valueStr, true);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatValue(object value)
    {
        if (value == null) return "null";

        if (value is Vector2 v2)
            return $"Vector2({v2.X:F2}, {v2.Y:F2})";
        if (value is Vector3 v3)
            return $"Vector3({v3.X:F2}, {v3.Y:F2}, {v3.Z:F2})";
        if (value is bool b)
            return b ? "true" : "false";
        if (value is string s)
            return $"\"{s}\"";

        return value.ToString() ?? "null";
    }
}
#endif
