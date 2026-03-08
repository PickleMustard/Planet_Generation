# Runtime Settings System

## Overview

The Runtime Settings System provides a centralized, type-safe, and persistent configuration management solution for the project. It enables runtime modification of game settings with automatic persistence, validation, and change notification.

### Key Features

- **Centralized Management**: All settings managed through a single singleton (`RuntimeSettings`)
- **Type Safety**: Strong typing with automatic validation of setting values
- **Persistence**: Settings automatically saved to `user://settings.cfg`
- **Change Notification**: Signal-based notification when settings change
- **Editor Integration**: Settings registered with Godot's ProjectSettings at runtime, plus editor plugin for full dialog integration
- **Debug Tools**: Console commands and visual settings panel for runtime debugging

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     RuntimeSettings (Singleton)                  │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────────────┐   │
│  │ ConfigFile  │  │ SettingsCache│  │ Configurables Registry│   │
│  │ Persistence │  │ In-Memory    │  │ IConfigurable objects │   │
│  └─────────────┘  └──────────────┘  └───────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │              ProjectSettings Integration                     ││
│  │  (Registers settings with Godot Editor at runtime)          ││
│  └─────────────────────────────────────────────────────────────┘│
└───────────────────────────┬─────────────────────────────────────┘
                            │
           ┌────────────────┼────────────────┐
           │                │                │
           ▼                ▼                ▼
     ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
     │ ThreadPooler│  │ GameLogger  │  │  TaskTimer  │
     │IConfigurable│  │IConfigurable│  │IConfigurable│
     └─────────────┘  └─────────────┘  └─────────────┘
           
┌─────────────────────────────────────────────────────────────────┐
│             RuntimeSettingsPlugin (Editor Tools-Only)            │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  Provides additional editor integration for ProjectSettings ││
│  │  Visible in Godot Editor > Project Settings dialog          ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

## Key Concepts

### IConfigurable Interface

The `IConfigurable` interface defines the contract for any object that wishes to expose configurable settings:

```csharp
public interface IConfigurable
{
    /// <summary>
    /// Gets the category name for grouping this configurable's settings in the UI.
    /// </summary>
    string SettingsCategory { get; }

    /// <summary>
    /// Applies a setting value to this configurable object.
    /// Called when a setting value changes.
    /// </summary>
    void ApplySetting(string key, object value);

    /// <summary>
    /// Gets the default value for a specific setting key.
    /// </summary>
    object GetSettingDefault(string key);

    /// <summary>
    /// Gets all configuration entries for this configurable object.
    /// </summary>
    IEnumerable<ConfigEntry> GetConfigEntries();
}
```

### ConfigEntry Class

The `ConfigEntry` class defines metadata for a single setting:

| Property | Type | Description |
|----------|------|-------------|
| `Key` | `string` | Unique identifier for the setting |
| `ValueType` | `Type` | Expected type (int, float, bool, string) |
| `DefaultValue` | `object` | Default value when not set |
| `MinValue` | `object` | Minimum value for numeric types |
| `MaxValue` | `object` | Maximum value for numeric types |
| `ValidOptions` | `string[]` | Valid options for enum-like strings |
| `Description` | `string` | Human-readable description |
| `RequiresRestart` | `bool` | Whether changes require a restart |
| `PropertyHintString` | `string` | Custom hint string for ProjectSettings |
| `ShowInAdvanced` | `bool` | Show in advanced view only (default: false) |
| `IsInternal` | `bool` | Hide from Project Settings dialog (default: false) |
| `ProjectSettingsCategory` | `string` | Custom category in ProjectSettings |

### RuntimeSettings Singleton

The central manager for all settings. Access via `RuntimeSettings.Instance`.

## For Developers: Adding New Settings

### Step 1: Implement IConfigurable

```csharp
using System.Collections.Generic;
using UtilityLibrary;

public partial class MySystem : Node, IConfigurable
{
    // IConfigurable implementation
    public string SettingsCategory => "mysystem";

    public IEnumerable<ConfigEntry> GetConfigEntries() => new[]
    {
        new ConfigEntry
        {
            Key = "max_items",
            ValueType = typeof(int),
            DefaultValue = 100,
            MinValue = 10,
            MaxValue = 1000,
            Description = "Maximum number of items to process",
            RequiresRestart = false
        },
        new ConfigEntry
        {
            Key = "processing_mode",
            ValueType = typeof(string),
            DefaultValue = "auto",
            ValidOptions = new[] { "auto", "manual", "batch" },
            Description = "Processing mode selection",
            RequiresRestart = true
        },
        new ConfigEntry
        {
            Key = "debug_enabled",
            ValueType = typeof(bool),
            DefaultValue = false,
            Description = "Enable debug output",
            RequiresRestart = false
        }
    };

    public void ApplySetting(string key, object value)
    {
        switch (key)
        {
            case "max_items":
                _maxItems = (int)value;
                break;
            case "processing_mode":
                // Requires restart - just store for next initialization
                break;
            case "debug_enabled":
                _DebugEnabled = (bool)value;
                OnDebugModeChanged();
                break;
        }
    }

    public object GetSettingDefault(string key) => key switch
    {
        "max_items" => 100,
        "processing_mode" => "auto",
        "debug_enabled" => false,
        _ => null
    };
}
```

### Step 2: Register with RuntimeSettings

Register your configurable in `_Ready()`:

```csharp
public override void _Ready()
{
    // Register with settings system
    RuntimeSettings.Instance?.RegisterConfigurable(this);
    
    // Load initial settings
    _maxItems = RuntimeSettings.Instance?.GetSetting<int>(SettingsCategory, "max_items") ?? 100;
    _debugEnabled = RuntimeSettings.Instance?.GetSetting<bool>(SettingsCategory, "debug_enabled") ?? false;
}
```

### Step 3: Listen for Setting Changes (Optional)

Connect to the `SettingChanged` signal to react to runtime changes:

```csharp
public override void _Ready()
{
    RuntimeSettings.Instance?.RegisterConfigurable(this);
    
    // Listen for setting changes
    if (RuntimeSettings.Instance != null)
    {
        RuntimeSettings.Instance.SettingChanged += OnSettingChanged;
    }
}

private void OnSettingChanged(string category, string key, Variant value)
{
    if (category != SettingsCategory) return;
    
    // Handle the change - ApplySetting is already called automatically
    GameLogger.Info($"Setting changed: {key}");
}

public override void _ExitTree()
{
    if (RuntimeSettings.Instance != null)
    {
        RuntimeSettings.Instance.SettingChanged -= OnSettingChanged;
    }
}
```

## Example Implementation

### ThreadPooler Configuration

```csharp
public partial class ThreadPooler : Node, IConfigurable
{
    public string SettingsCategory => "threading";

    public IEnumerable<ConfigEntry> GetConfigEntries() => new[]
    {
        new ConfigEntry
        {
            Key = "allocation_percentage",
            ValueType = typeof(float),
            DefaultValue = 0.75f,
            MinValue = 0.1f,
            MaxValue = 1.0f,
            Description = "Percentage of CPU cores to allocate for thread pool",
            RequiresRestart = true
        },
        new ConfigEntry
        {
            Key = "manual_thread_count",
            ValueType = typeof(int),
            DefaultValue = 0,
            MinValue = 0,
            MaxValue = 64,
            Description = "Override thread count (0 = auto-calculated)",
            RequiresRestart = true
        }
    };

    public void ApplySetting(string key, object value)
    {
        // Both settings require restart, so just store the value
        // The actual application happens during Initialize()
    }

    public object GetSettingDefault(string key) => key switch
    {
        "allocation_percentage" => 0.75f,
        "manual_thread_count" => 0,
        _ => null
    };

    private void Initialize()
    {
        // Read settings at initialization time
        int manualThreadCount = RuntimeSettings.Instance?.GetSetting<int>(
            SettingsCategory, "manual_thread_count") ?? 0;
        float allocationPercentage = RuntimeSettings.Instance?.GetSetting<float>(
            SettingsCategory, "allocation_percentage") ?? 0.75f;
        
        // Use settings to configure thread pool...
    }
}
```

### GameLogger Configuration

```csharp
// GameLogger uses an internal ConfigurableProvider to implement IConfigurable
public static class GameLogger
{
    private class ConfigurableProvider : IConfigurable
    {
        public string SettingsCategory => "logging";

        public IEnumerable<ConfigEntry> GetConfigEntries() => new[]
        {
            new ConfigEntry
            {
                Key = "level",
                ValueType = typeof(string),
                DefaultValue = "DEBUG",
                ValidOptions = new[] { "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL", "PROD" },
                Description = "Minimum log level to output",
                RequiresRestart = false
            },
            new ConfigEntry
            {
                Key = "log_to_file",
                ValueType = typeof(bool),
                DefaultValue = true,
                Description = "Write log messages to logs/debug.log",
                RequiresRestart = false
            },
            new ConfigEntry
            {
                Key = "log_to_console",
                ValueType = typeof(bool),
                DefaultValue = true,
                Description = "Print log messages to Godot console",
                RequiresRestart = false
            }
        };

        public void ApplySetting(string key, object value)
        {
            switch (key)
            {
                case "level":
                    if (Enum.TryParse<Mode>(value.ToString(), out var modeEnum))
                    {
                        logMode = modeEnum;
                    }
                    break;
                case "log_to_file":
                    _logToFile = (bool)value;
                    break;
                case "log_to_console":
                    _logToConsole = (bool)value;
                    break;
            }
        }
    }
}
```

## Console Commands Reference

Access the debug console with the backtick (`` ` ``) key in DEBUG builds.

### Listing and Viewing Settings

| Command | Description | Example |
|---------|-------------|---------|
| `settings` | List all registered settings | `settings` |
| `settings get <category.key>` | Get detailed info for a setting | `settings get threading.allocation_percentage` |

### Modifying Settings

| Command | Description | Example |
|---------|-------------|---------|
| `settings set <category.key> <value>` | Set a setting value | `settings set logging.level WARNING` |
| `settings reset <category.key>` | Reset a setting to default | `settings reset threading.allocation_percentage` |
| `settings reset_all` | Reset all settings to defaults | `settings reset_all` |

### Persistence Commands

| Command | Description | Example |
|---------|-------------|---------|
| `settings save` | Save current settings to disk | `settings save` |
| `settings reload` | Reload settings from disk (discards unsaved changes) | `settings reload` |

### Examples

```bash
# List all settings
settings

# View current log level
settings get logging.level

# Change log level
settings set logging.level DEBUG

# Adjust thread allocation
settings set threading.allocation_percentage 0.5

# Reset a single setting
settings reset threading.manual_thread_count

# Reset everything
settings reset_all

# Save changes
settings save
```

## Settings Panel Usage

The Settings Panel provides a visual interface for editing settings.

### Accessing the Panel

1. Open the Debug Menu (press `` ` ``)
2. Navigate to the Settings tab or use the command `settings_panel`

### Panel Features

- **Category Sections**: Settings grouped by configurable category
- **Type-Appropriate Controls**:
  - **Numeric (int/float)**: SpinBox with min/max range
  - **Boolean**: CheckBox
  - **String**: LineEdit
  - **Enum-like**: OptionButton dropdown
- **Tooltips**: Hover over settings to see descriptions
- **Restart Indicators**: Settings requiring restart show a warning icon

### Panel Buttons

| Button | Description |
|--------|-------------|
| **Reset All** | Reset all settings to their default values |
| **Reload** | Reload settings from the configuration file |
| **Save** | Save current settings to disk |

## Understanding Restart Requirements

Some settings cannot be applied immediately and require a game restart:

### Settings That Require Restart

- **Thread pool configuration**: Thread count cannot be changed at runtime
- **Graphics settings**: Renderer changes need reinitialization
- **Resource paths**: File path changes require reloading

### How It Works

1. When `RequiresRestart = true`, the setting stores the new value
2. The UI displays a warning indicator (⚠)
3. On next startup, the new value is read and applied during initialization

### Example

```csharp
new ConfigEntry
{
    Key = "manual_thread_count",
    ValueType = typeof(int),
    DefaultValue = 0,
    MinValue = 0,
    MaxValue = 64,
    Description = "Override thread count (0 = auto)",
    RequiresRestart = true  // ← Setting requires restart
}
```

## Settings File Format

Settings are persisted to `user://settings.cfg` using Godot's ConfigFile format:

```ini
[threading]

allocation_percentage=0.75
manual_thread_count=0

[logging]

level="DEBUG"
log_to_file=true
log_to_console=true

[tasktimer]

progress_panel_visible=true
auto_collapse_delay=3.0
```

## ProjectSettings vs ConfigFile

The RuntimeSettings system uses two different Godot APIs for settings management:

### ConfigFile (RuntimeSettings Default)

- **Storage**: `user://settings.cfg` (user-specific, writable at runtime)
- **Visibility**: Not visible in Editor's Project Settings dialog
- **Use Case**: Runtime-modifiable game settings, user preferences
- **Persistence**: Saved/loaded manually via `SaveToFile()` / `LoadFromFile()`

### ProjectSettings (Editor Integration)

- **Storage**: `project.godot` (project-wide, version controlled)
- **Visibility**: Visible in Editor's Project Settings dialog
- **Use Case**: Engine configuration, development settings
- **Persistence**: Loaded automatically at engine startup

### How They Work Together

When `RuntimeSettings` registers a configurable, it automatically registers the settings with both systems:

1. **ConfigFile**: Stores runtime values that can be modified while the game is running
2. **ProjectSettings**: Makes settings visible in the Editor for development/debugging

The values from ConfigFile take precedence over ProjectSettings at runtime, allowing:
- Default values from ProjectSettings when no user config exists
- User-customized values persisted in ConfigFile
- Editor-based configuration during development

To enable settings in ProjectSettings, use the `ConfigEntry` properties:
- `ShowInAdvanced = true` - Show only in advanced view
- `IsInternal = true` - Hide from Project Settings dialog entirely
- `ProjectSettingsCategory` - Custom category in the editor

## API Reference

### RuntimeSettings Methods

| Method | Description |
|--------|-------------|
| `RegisterConfigurable(IConfigurable)` | Register a configurable object |
| `GetSetting<T>(category, key)` | Get a typed setting value |
| `SetSetting(category, key, value)` | Set and validate a setting |
| `ResetSetting(category, key)` | Reset to default |
| `ResetAllSettings()` | Reset all settings |
| `SaveToFile()` | Persist to config file |
| `LoadFromFile()` | Load from config file |
| `HasSetting(category, key)` | Check if setting exists |
| `GetConfigurable(category)` | Get registered configurable |
| `GetAllEntries()` | Enumerate all config entries |
| `IsLoaded()` | Check if settings are loaded |

### RuntimeSettings Signals

| Signal | Parameters | Description |
|--------|------------|-------------|
| `SettingChanged` | `(category, key, value)` | Emitted when any setting changes |
| `SettingsLoaded` | `()` | Emitted after loading from file |

## Current Configurables

| Category | Class | Settings |
|----------|-------|----------|
| `threading` | `ThreadPooler` | `allocation_percentage`, `manual_thread_count` |
| `logging` | `GameLogger` | `level`, `log_to_file`, `log_to_console` |
| `tasktimer` | `TaskTimer` | `progress_panel_visible`, `auto_collapse_delay` |

## Testing

Tests for the settings system are located in `Tests/Settings/`:

- `ConfigEntryTest.cs` - Validation and range testing
- `RuntimeSettingsTest.cs` - Full system integration testing

Run tests with gdUnit4 in the Godot editor or via command line:

```bash
# Via Godot editor
# Right-click test file > Run Tests

# Via command line (headless)
godot --headless --path . -s Tests/Settings/RuntimeSettingsTest.cs
```

## Best Practices

1. **Use appropriate types**: Match ValueType to your actual data type
2. **Set reasonable ranges**: Use MinValue/MaxValue for numeric settings
3. **Provide descriptions**: Help users understand what each setting does
4. **Mark restart requirements**: Set `RequiresRestart = true` when needed
5. **Handle null RuntimeSettings**: Always use null-conditional operators
6. **Register early**: Register configurables in `_Ready()` before accessing settings
7. **Apply immediately when possible**: For non-restart settings, apply changes in `ApplySetting()`
