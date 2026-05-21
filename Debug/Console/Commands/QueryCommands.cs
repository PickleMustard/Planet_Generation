#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace Debug.Console;

public static class QueryCommands
{
    [DebugCommand("get", "Get a property value from an instance", "get <namespace>.<property>", Category = "Query")]
    public static int Get(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: get <namespace>.<property>");
            ctx.WriteLine("Example: get CelestialBody.Earth.Position");
            return 1;
        }

        var path = args[0];
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

        return GetPropertyValue(ctx, instance!, propertyPath);
    }

    private static int GetPropertyValue(CommandContext ctx, object instance, string propertyPath)
    {
        var parts = propertyPath.Split('.');
        object? current = instance;
        Type currentType = instance.GetType();

        foreach (var part in parts)
        {
            if (current == null)
            {
                ctx.WriteError($"Null reference at property: {part}");
                return 1;
            }

            var property = currentType.GetProperty(part,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null)
            {
                var field = currentType.GetField(part,
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

        ctx.WriteLine(FormatValue(current!));
        return 0;
    }

    [DebugCommand("dump", "Dump all properties of an instance or database", "dump <namespace>", Category = "Query")]
    public static int Dump(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: dump <namespace>");
            ctx.WriteLine("Example: dump CelestialBody.Earth");
            return 1;
        }

        var ns = args[0];

        if (!InstanceRegistry.TryGetInstance(ns, out var instance))
        {
            ctx.WriteError($"Instance not found: {ns}");
            return 1;
        }

        return DumpObject(ctx, instance!, instance!.GetType().Name, 0);
    }

    private static int DumpObject(CommandContext ctx, object obj, string name, int depth)
    {
        if (depth > 3)
        {
            ctx.WriteLine($"{new string(' ', depth * 2)}{name}: [max depth reached]");
            return 0;
        }

        if (obj == null)
        {
            ctx.WriteLine($"{new string(' ', depth * 2)}{name}: null");
            return 0;
        }

        var type = obj.GetType();

        if (IsSimpleType(type))
        {
            ctx.WriteLine($"{new string(' ', depth * 2)}{name}: {FormatValue(obj!)}");
            return 0;
        }

        ctx.WriteLine($"{new string(' ', depth * 2)}[color=cyan]{name}[/color] ({type.Name}):");

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name);

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(obj);
                if (IsSimpleType(prop.PropertyType))
                {
                    ctx.WriteLine($"{new string(' ', (depth + 1) * 2)}{prop.Name}: {FormatValue(value!)}");
                }
                else if (value != null)
                {
                    DumpObject(ctx, value, prop.Name, depth + 1);
                }
                else
                {
                    ctx.WriteLine($"{new string(' ', (depth + 1) * 2)}{prop.Name}: null");
                }
            }
            catch (Exception ex)
            {
                ctx.WriteLine($"{new string(' ', (depth + 1) * 2)}{prop.Name}: [error: {ex.Message}]");
            }
        }

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(f => f.Name);

        foreach (var field in fields)
        {
            try
            {
                var value = field.GetValue(obj);
                if (IsSimpleType(field.FieldType))
                {
                    ctx.WriteLine($"{new string(' ', (depth + 1) * 2)}{field.Name}: {FormatValue(value!)}");
                }
                else if (value != null)
                {
                    DumpObject(ctx, value, field.Name, depth + 1);
                }
                else
                {
                    ctx.WriteLine($"{new string(' ', (depth + 1) * 2)}{field.Name}: null");
                }
            }
            catch (Exception ex)
            {
                ctx.WriteLine($"{new string(' ', (depth + 1) * 2)}{field.Name}: [error: {ex.Message}]");
            }
        }

        return 0;
    }

    [DebugCommand("find", "Search all registered instances for a pattern", "find <pattern>", Category = "Query")]
    public static int Find(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: find <pattern>");
            ctx.WriteLine("Example: find *Earth*");
            return 1;
        }

        var pattern = args[0];
        var namespaces = InstanceRegistry.GetAllNamespaces().ToList();
        var matches = new List<string>();

        foreach (var ns in namespaces)
        {
            if (MatchesPattern(ns, pattern))
            {
                matches.Add(ns);
                continue;
            }

            if (InstanceRegistry.TryGetInstance(ns, out var instance))
            {
                if (SearchObject(instance!, pattern))
                {
                    matches.Add(ns);
                }
            }
        }

        if (matches.Count == 0)
        {
            ctx.WriteInfo($"No matches found for pattern: {pattern}");
            return 0;
        }

        ctx.WriteLine($"[color=yellow]Found {matches.Count} match(es):[/color]");
        foreach (var match in matches.OrderBy(m => m))
        {
            ctx.WriteLine($"  {match}");
        }
        return 0;
    }

    private static bool SearchObject(object obj, string pattern)
    {
        if (obj == null) return false;

        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0);

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(obj);
                if (value != null)
                {
                    var strValue = value.ToString();
                    if (strValue != null && MatchesPattern(strValue, pattern))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Skip properties that throw on access
            }
        }

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            try
            {
                var value = field.GetValue(obj);
                if (value != null)
                {
                    var strValue = value.ToString();
                    if (strValue != null && MatchesPattern(strValue, pattern))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Skip fields that throw on access
            }
        }

        return false;
    }

    private static bool MatchesPattern(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
            return false;

        pattern = pattern.ToLowerInvariant();
        text = text.ToLowerInvariant();

        if (pattern.Contains("*"))
        {
            var parts = pattern.Split('*');
            if (parts.Length == 2)
            {
                var startsWith = parts[0];
                var endsWith = parts[1];

                if (string.IsNullOrEmpty(startsWith))
                    return text.EndsWith(endsWith);
                if (string.IsNullOrEmpty(endsWith))
                    return text.StartsWith(startsWith);

                return text.StartsWith(startsWith) && text.EndsWith(endsWith);
            }
        }

        return text.Contains(pattern);
    }

    private static bool IsSimpleType(Type type)
    {
        if (type == null) return true;

        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type == typeof(Vector2) ||
               type == typeof(Vector3) ||
               type == typeof(Color) ||
               type.IsEnum;
    }

    private static string FormatValue(object value)
    {
        if (value == null) return "[color=gray]null[/color]";

        var type = value.GetType();

        if (value is Vector2 v2)
            return $"Vector2({v2.X:F2}, {v2.Y:F2})";
        if (value is Vector3 v3)
            return $"Vector3({v3.X:F2}, {v3.Y:F2}, {v3.Z:F2})";
        if (value is Color c)
            return $"Color({c.R:F2}, {c.G:F2}, {c.B:F2}, {c.A:F2})";
        if (value is IDictionary dict)
            return $"[Dictionary: {dict.Count} entries]";
        if (value is IEnumerable enumerable and not string)
            return $"[Collection]";

        if (type.IsEnum)
            return $"[color=green]{value}[/color]";

        if (value is bool b)
            return b ? "[color=green]true[/color]" : "[color=red]false[/color]";

        if (value is string s)
            return $"\"{s}\"";

        return value.ToString() ?? "[null]";
    }
}
#endif
