#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using UtilityLibrary;

namespace UI.Debug.Console;

public static class SettingsCommands
{
    [DebugCommand("settings", "List all runtime settings", "settings", Category = "Settings")]
    public static int SettingsList(CommandContext ctx, string[] args)
    {
        var runtimeSettings = RuntimeSettings.Instance;
        if (runtimeSettings == null)
        {
            ctx.WriteError("RuntimeSettings not available");
            return 1;
        }

        var entries = runtimeSettings.GetAllEntries()?.ToList();
        if (entries == null || entries.Count == 0)
        {
            ctx.WriteLine("[color=yellow]No settings registered.[/color]");
            ctx.WriteLine("");
            PrintUsageHelp(ctx);
            return 0;
        }

        var configurables = GetConfigurablesByCategory(runtimeSettings, entries);

        ctx.WriteLine("[color=cyan]=== Runtime Settings ===[/color]");
        ctx.WriteLine("");

        foreach (var kvp in configurables.OrderBy(c => c.Key))
        {
            string category = kvp.Key;
            var categoryEntries = kvp.Value;

            ctx.WriteLine($"[color=yellow]{category}[/color]:");

            foreach (var entry in categoryEntries.OrderBy(e => e.Key))
            {
                object? currentValue = GetCurrentSettingValue(runtimeSettings, category, entry);
                string formattedValue = FormatSettingValue(currentValue!, entry!);
                string defaultIndicator = IsDefaultValue(currentValue!, entry) ? "" : " [color=gray]*(modified)[/color]";

                ctx.WriteLine($"  {entry.Key}: {formattedValue}{defaultIndicator}");
            }

            ctx.WriteLine("");
        }

        PrintUsageHelp(ctx);
        return 0;
    }

    [DebugCommand("settings get", "Get a specific setting value", "settings get <category.key>", Category = "Settings")]
    public static int SettingsGet(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: settings get <category.key>");
            ctx.WriteLine("Example: settings get Graphics.VSyncEnabled");
            return 1;
        }

        var path = args[0];
        var parts = path.Split('.', 2);

        if (parts.Length < 2)
        {
            ctx.WriteError("Invalid path format. Use: <category.key>");
            ctx.WriteLine("Example: settings get Graphics.VSyncEnabled");
            return 1;
        }

        var category = parts[0];
        var key = parts[1];

        var runtimeSettings = RuntimeSettings.Instance;
        if (runtimeSettings == null)
        {
            ctx.WriteError("RuntimeSettings not available");
            return 1;
        }

        if (!runtimeSettings.HasSetting(category, key))
        {
            ctx.WriteError($"Setting not found: {category}.{key}");
            return 1;
        }

        var configurable = runtimeSettings.GetConfigurable(category);
        if (configurable == null)
        {
            ctx.WriteError($"No configurable found for category: {category}");
            return 1;
        }

        ConfigEntry? entry = FindConfigEntry(configurable, key);
        object? value = GetCurrentSettingValue(runtimeSettings, category, entry);
        object? defaultValue = entry?.DefaultValue;

        ctx.WriteLine($"[color=cyan]{category}.{key}[/color]");
        ctx.WriteLine($"  Current:  {FormatSettingValue(value!, entry!)}");
        ctx.WriteLine($"  Default:  {FormatSettingValue(defaultValue!, entry!)}");

        if (entry != null)
        {
            if (!string.IsNullOrEmpty(entry.Description))
            {
                ctx.WriteLine($"  Description: {entry.Description}");
            }

            if (entry.RequiresRestart)
            {
                ctx.WriteLine("  [color=yellow]* Requires restart[/color]");
            }

            if (entry.MinValue != null && entry.MaxValue != null)
            {
                ctx.WriteLine($"  Range: {entry.MinValue} - {entry.MaxValue}");
            }

            if (entry.ValidOptions != null && entry.ValidOptions.Length > 0)
            {
                ctx.WriteLine($"  Options: {string.Join(", ", entry.ValidOptions)}");
            }
        }

        return 0;
    }

    [DebugCommand("settings set", "Set a setting value", "settings set <category.key> <value>", Category = "Settings")]
    public static int SettingsSet(CommandContext ctx, string[] args)
    {
        if (args.Length < 2)
        {
            ctx.WriteError("Usage: settings set <category.key> <value>");
            ctx.WriteLine("Example: settings set Graphics.VSyncEnabled true");
            return 1;
        }

        var path = args[0];
        var valueStr = string.Join(" ", args, 1, args.Length - 1);
        var parts = path.Split('.', 2);

        if (parts.Length < 2)
        {
            ctx.WriteError("Invalid path format. Use: <category.key>");
            return 1;
        }

        var category = parts[0];
        var key = parts[1];

        var runtimeSettings = RuntimeSettings.Instance;
        if (runtimeSettings == null)
        {
            ctx.WriteError("RuntimeSettings not available");
            return 1;
        }

        if (!runtimeSettings.HasSetting(category, key))
        {
            ctx.WriteError($"Setting not found: {category}.{key}");
            return 1;
        }

        var configurable = runtimeSettings.GetConfigurable(category);
        if (configurable == null)
        {
            ctx.WriteError($"No configurable found for category: {category}");
            return 1;
        }

        ConfigEntry? entry = FindConfigEntry(configurable, key);
        if (entry == null)
        {
            ctx.WriteError($"Could not find config entry for: {key}");
            return 1;
        }

        if (!TryParseSettingValue(valueStr, entry!.ValueType!, out object? parsedValue))
        {
            ctx.WriteError($"Cannot convert '{valueStr}' to type {entry!.ValueType!.Name}");
            if (entry.ValidOptions != null && entry.ValidOptions.Length > 0)
            {
                ctx.WriteLine($"Valid options: {string.Join(", ", entry.ValidOptions)}");
            }
            return 1;
        }

        if (!entry!.IsValid(parsedValue!))
        {
            ctx.WriteError($"Invalid value for {category}.{key}: {parsedValue}");
            if (entry.MinValue != null && entry.MaxValue != null)
            {
                ctx.WriteLine($"Valid range: {entry.MinValue} - {entry.MaxValue}");
            }
            if (entry.ValidOptions != null && entry.ValidOptions.Length > 0)
            {
                ctx.WriteLine($"Valid options: {string.Join(", ", entry.ValidOptions)}");
            }
            return 1;
        }

        try
        {
            runtimeSettings.SetSetting(category, key, parsedValue!);
            ctx.WriteLine($"[color=green]Set {category}.{key} = {FormatSettingValue(parsedValue!, entry)}[/color]");

            if (entry.RequiresRestart)
            {
                ctx.WriteLine("[color=yellow]Note: This change requires a restart to take effect.[/color]");
            }
            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to set value: {ex.Message}");
            return 1;
        }
    }

    [DebugCommand("settings reset", "Reset a setting to its default value", "settings reset <category.key>", Category = "Settings")]
    public static int SettingsReset(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: settings reset <category.key>");
            ctx.WriteLine("Example: settings reset Graphics.VSyncEnabled");
            return 1;
        }

        var path = args[0];
        var parts = path.Split('.', 2);

        if (parts.Length < 2)
        {
            ctx.WriteError("Invalid path format. Use: <category.key>");
            return 1;
        }

        var category = parts[0];
        var key = parts[1];

        var runtimeSettings = RuntimeSettings.Instance;
        if (runtimeSettings == null)
        {
            ctx.WriteError("RuntimeSettings not available");
            return 1;
        }

        if (!runtimeSettings.HasSetting(category, key))
        {
            ctx.WriteError($"Setting not found: {category}.{key}");
            return 1;
        }

        try
        {
            runtimeSettings.ResetSetting(category, key);
            ctx.WriteLine($"[color=green]Reset {category}.{key} to default value[/color]");
            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to reset setting: {ex.Message}");
            return 1;
        }
    }

    [DebugCommand("settings reset_all", "Reset all settings to defaults", "settings reset_all", Category = "Settings")]
    public static int SettingsResetAll(CommandContext ctx, string[] args)
    {
        var runtimeSettings = RuntimeSettings.Instance;
        if (runtimeSettings == null)
        {
            ctx.WriteError("RuntimeSettings not available");
            return 1;
        }

        try
        {
            runtimeSettings.ResetAllSettings();
            ctx.WriteLine("[color=green]All settings reset to defaults.[/color]");
            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to reset all settings: {ex.Message}");
            return 1;
        }
    }

    [DebugCommand("settings save", "Save settings to disk", "settings save", Category = "Settings")]
    public static int SettingsSave(CommandContext ctx, string[] args)
    {
        var runtimeSettings = RuntimeSettings.Instance;
        if (runtimeSettings == null)
        {
            ctx.WriteError("RuntimeSettings not available");
            return 1;
        }

        try
        {
            // ProjectSettings auto-saves, no explicit save needed
            ctx.WriteLine("[color=green]Settings are auto-saved to ProjectSettings.[/color]");
            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to save settings: {ex.Message}");
            return 1;
        }
    }

    [DebugCommand("settings reload", "Reload settings from disk", "settings reload", Category = "Settings")]
    public static int SettingsReload(CommandContext ctx, string[] args)
    {
        var runtimeSettings = RuntimeSettings.Instance;
        if (runtimeSettings == null)
        {
            ctx.WriteError("RuntimeSettings not available");
            return 1;
        }

        try
        {
            runtimeSettings.LoadFromFile();
            ctx.WriteLine("[color=green]Settings reloaded from file.[/color]");
            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to reload settings: {ex.Message}");
            return 1;
        }
    }

    private static Dictionary<string, List<ConfigEntry>> GetConfigurablesByCategory(
        RuntimeSettings runtimeSettings,
        List<ConfigEntry> entries)
    {
        var result = new Dictionary<string, List<ConfigEntry>>();

        foreach (var entry in entries)
        {
            foreach (var configurable in GetAllConfigurables(runtimeSettings))
            {
                foreach (var configEntry in configurable.GetConfigEntries())
                {
                    if (configEntry.Key == entry.Key)
                    {
                        string category = configurable.SettingsCategory;
                        if (!result.ContainsKey(category))
                        {
                            result[category] = new List<ConfigEntry>();
                        }
                        result[category].Add(configEntry);
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<IConfigurable> GetAllConfigurables(RuntimeSettings runtimeSettings)
    {
        var entries = runtimeSettings.GetAllEntries();
        var categories = new HashSet<string>();

        foreach (var entry in entries)
        {
            var configurable = runtimeSettings.GetConfigurable(entry.Key!);
            if (configurable != null)
            {
                categories.Add(configurable.SettingsCategory);
            }
        }

        foreach (var category in categories)
        {
            var configurable = runtimeSettings.GetConfigurable(category);
            if (configurable != null)
            {
                yield return configurable;
            }
        }
    }

    private static object? GetCurrentSettingValue(RuntimeSettings runtimeSettings, string category, ConfigEntry? entry)
    {
        if (entry == null) return null;

        try
        {
            var type = entry.ValueType;
            if (type == typeof(bool))
                return runtimeSettings.GetSetting<bool>(category, entry.Key!);
            if (type == typeof(int))
                return runtimeSettings.GetSetting<int>(category, entry.Key!);
            if (type == typeof(float))
                return runtimeSettings.GetSetting<float>(category, entry.Key!);
            if (type == typeof(double))
                return runtimeSettings.GetSetting<double>(category, entry.Key!);
            if (type == typeof(string))
                return runtimeSettings.GetSetting<string>(category, entry.Key!);

            return entry.DefaultValue;
        }
        catch
        {
            return entry.DefaultValue;
        }
    }

    private static ConfigEntry? FindConfigEntry(IConfigurable configurable, string key)
    {
        foreach (var entry in configurable.GetConfigEntries())
        {
            if (entry.Key == key)
            {
                return entry;
            }
        }
        return null;
    }

    private static bool TryParseSettingValue(string valueStr, Type targetType, out object? value)
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

    private static string FormatSettingValue(object value, ConfigEntry entry)
    {
        if (value == null) return "[color=gray]null[/color]";

        if (value is bool b)
            return b ? "[color=green]true[/color]" : "[color=red]false[/color]";

        if (value is string s)
            return $"\"{s}\"";

        if (entry?.ValueType?.IsEnum == true)
            return $"[color=green]{value}[/color]";

        return value.ToString() ?? "[color=gray]null[/color]";
    }

    private static bool IsDefaultValue(object currentValue, ConfigEntry entry)
    {
        if (entry?.DefaultValue == null || currentValue == null)
            return true;

        return Equals(currentValue, entry.DefaultValue);
    }

    private static void PrintUsageHelp(CommandContext ctx)
    {
        ctx.WriteLine("[color=gray]Usage:[/color]");
        ctx.WriteLine("  settings                  - List all settings");
        ctx.WriteLine("  settings get <c.k>        - Get a specific setting");
        ctx.WriteLine("  settings set <c.k> <val>  - Change a setting");
        ctx.WriteLine("  settings reset <c.k>      - Reset to default");
        ctx.WriteLine("  settings reset_all        - Reset all to defaults");
        ctx.WriteLine("  settings save             - Save to disk");
        ctx.WriteLine("  settings reload           - Reload from disk");
    }
}
#endif
