# Godot 4.x ProjectSettings Programming Research

## Overview

This document summarizes how to programmatically add settings to Godot's ProjectSettings in Godot 4.x.

## 1. Registering Custom Settings with ProjectSettings

To add a custom setting that appears in the Editor's Project Settings dialog, you need to:

1. **Set the initial value** using `set_setting()`
2. **Add property info** using `add_property_info()` to define the type and editor hints
3. **Optionally configure visibility** using `set_as_basic()` or `set_as_internal()`
4. **Set the initial/reset value** using `set_initial_value()`

### Basic Example (GDScript)

```gdscript
# Set the value first
ProjectSettings.set_setting("my_game/difficulty_level", 1)

# Define property info for the editor
var property_info = {
    "name": "my_game/difficulty_level",
    "type": TYPE_INT,
    "hint": PROPERTY_HINT_ENUM,
    "hint_string": "Easy,Medium,Hard,Extreme"
}

ProjectSettings.add_property_info(property_info)

# Set the initial/reset value
ProjectSettings.set_initial_value("my_game/difficulty_level", 1)

# Make it a "basic" setting (always visible, not hidden behind "Advanced")
ProjectSettings.set_as_basic("my_game/difficulty_level", true)
```

### Basic Example (C#)

```csharp
// Set the value first
ProjectSettings.SetSetting("my_game/difficulty_level", 1);

// Define property info for the editor
var propertyInfo = new Godot.Collections.Dictionary
{
    {"name", "my_game/difficulty_level"},
    {"type", (int)Variant.Type.Int},
    {"hint", (int)PropertyHint.Enum},
    {"hint_string", "Easy,Medium,Hard,Extreme"}
};

ProjectSettings.AddPropertyInfo(propertyInfo);

// Set the initial/reset value
ProjectSettings.SetInitialValue("my_game/difficulty_level", 1);

// Make it a "basic" setting
ProjectSettings.SetAsBasic("my_game/difficulty_level", true);
```

### Complete Plugin Example

When adding settings from an editor plugin, typically do this in `_enter_tree()`:

```gdscript
@tool
extends EditorPlugin

func _enter_tree():
    # Define your custom setting
    var setting_name = "my_plugin/enable_feature"
    
    # Only add if it doesn't exist
    if not ProjectSettings.has_setting(setting_name):
        ProjectSettings.set_setting(setting_name, true)
        
        var property_info = {
            "name": setting_name,
            "type": TYPE_BOOL,
        }
        ProjectSettings.add_property_info(property_info)
        ProjectSettings.set_initial_value(setting_name, true)
        ProjectSettings.set_as_basic(setting_name, true)

func _exit_tree():
    # Optionally clean up when plugin is disabled
    # Note: This removes the setting entirely
    pass
    # ProjectSettings.set_setting("my_plugin/enable_feature", null)
```

## 2. ProjectSettings Class Methods

### Core Methods

| Method | Description |
|--------|-------------|
| `get_setting(name: String, default_value: Variant = null)` | Get a setting value |
| `get_setting_with_override(name: StringName)` | Get setting with feature tag overrides applied |
| `set_setting(name: String, value: Variant)` | Set a setting value (use `null` to delete) |
| `has_setting(name: String)` | Check if a setting exists |
| `clear(name: String)` | Clear a setting (not recommended) |

### Editor Integration Methods

| Method | Description |
|--------|-------------|
| `add_property_info(hint: Dictionary)` | Add editor property info for a setting |
| `set_initial_value(name: String, value: Variant)` | Set the default/reset value |
| `set_as_basic(name: String, basic: bool)` | Control basic vs advanced visibility |
| `set_as_internal(name: String, internal: bool)` | Hide from Project Settings dialog |
| `set_restart_if_changed(name: String, restart: bool)` | Mark if editor restart is needed |
| `set_order(name: String, position: int)` | Set display order |

### Persistence Methods

| Method | Description |
|--------|-------------|
| `save()` | Save to `project.godot` (editor plugins only) |
| `save_custom(file: String)` | Save to custom file (supports `.godot`, `.binary`, or `override.cfg`) |

### Utility Methods

| Method | Description |
|--------|-------------|
| `globalize_path(path: String)` | Convert `res://` or `user://` to OS-native path |
| `localize_path(path: String)` | Convert OS path to `res://` path |
| `get_order(name: String)` | Get the order of a setting |
| `load_resource_pack(pack: String, ...)` | Load a .pck or .zip file |
| `get_global_class_list()` | Get list of registered global classes |

### Property Info Dictionary Structure

The `add_property_info()` method takes a Dictionary with these keys:

```gdscript
var property_info = {
    "name": "category/property_name",  # Required: Full setting path
    "type": TYPE_INT,                   # Required: Variant.Type
    "hint": PROPERTY_HINT_ENUM,         # Optional: PropertyHint
    "hint_string": "One,Two,Three"      # Optional: Hint configuration
}
```

### Common PropertyHint Values

| Hint | Use Case |
|------|----------|
| `PROPERTY_HINT_NONE` | Default, no special handling |
| `PROPERTY_HINT_RANGE` | Numeric range: `"0,100,1"` |
| `PROPERTY_HINT_ENUM` | Enumeration: `"Option1,Option2,Option3"` |
| `PROPERTY_HINT_FILE` | File path |
| `PROPERTY_HINT_DIR` | Directory path |
| `PROPERTY_HINT_COLOR_NO_ALPHA` | Color without alpha |
| `PROPERTY_HINT_FLAGS` | Bit flags: `"Bit0,Bit1,Bit2"` |

## 3. ProjectSettings vs ConfigFile

### ProjectSettings

**Purpose:** Global project configuration stored in `project.godot`

**Characteristics:**
- Singleton - automatically available globally
- Loaded at project startup
- Settings are accessible via `ProjectSettings.get_setting()`
- Changes persist in `project.godot` file
- Used for engine and project configuration
- Supports feature tag overrides (e.g., `.windows`, `.debug`)

**When to use:**
- Engine configuration (window size, physics settings, etc.)
- Project-wide settings that need to be in Project Settings dialog
- Settings that need feature tag overrides

**Example:**
```gdscript
# Reading
var max_fps = ProjectSettings.get_setting("application/run/max_fps")

# Writing (typically in editor plugins only)
ProjectSettings.set_setting("my_game/custom_setting", 42)
ProjectSettings.save()
```

### ConfigFile

**Purpose:** General-purpose INI-style configuration file handling

**Characteristics:**
- Must be instantiated: `ConfigFile.new()`
- Manual load/save required
- Can read/write any `.cfg` or INI-style file
- Supports sections (like `[section]`)
- Not tied to project settings

**When to use:**
- User preferences/saved games
- Custom configuration files
- Runtime-modifiable settings
- Modding support

**Example:**
```gdscript
var config = ConfigFile.new()

# Load existing file
var err = config.load("user://settings.cfg")
if err != OK:
    # Use defaults
    config.set_value("audio", "volume", 1.0)

# Read values
var volume = config.get_value("audio", "volume", 1.0)

# Write values
config.set_value("audio", "volume", 0.8)

# Save
config.save("user://settings.cfg")
```

### Key Differences Summary

| Aspect | ProjectSettings | ConfigFile |
|--------|----------------|------------|
| **Scope** | Global singleton | Instance-based |
| **Storage** | `project.godot` | Any `.cfg` file |
| **Auto-load** | Yes, at startup | No, manual |
| **Editor Integration** | Yes, appears in UI | No |
| **Feature Tags** | Yes | No |
| **Runtime Writable** | Limited (use `save_custom`) | Yes |
| **Primary Use** | Engine/project config | User data/mods |

## 4. Best Practices

### Naming Conventions
- Use category/subcategory format: `"category/subcategory/property_name"`
- Use snake_case for property names
- Group related settings under a common category prefix

### Editor Plugins
- Add settings in `_enter_tree()`
- Check `has_setting()` before adding to avoid duplicates
- Set initial values with `set_initial_value()` for proper reset behavior
- Consider using `set_as_internal()` for hidden settings

### Runtime vs Editor
- Project settings are read once at startup
- Many settings have runtime equivalents (e.g., `Engine.max_fps`)
- Use runtime APIs for dynamic changes, not `set_setting()`

### Persistence
- Use `save()` in editor plugins to persist changes
- Use `save_custom("user://override.cfg")` for exported projects
- Remember that `override.cfg` can override project settings at runtime

## Sources

- [Godot Documentation: ProjectSettings Class](https://docs.godotengine.org/en/stable/classes/class_projectsettings.html)
- [Godot Documentation: Project Settings Tutorial](https://docs.godotengine.org/en/stable/tutorials/editor/project_settings.html)
- [Godot Documentation: Making Plugins](https://docs.godotengine.org/en/stable/tutorials/plugins/editor/making_plugins.html)
- [Godot Source: ProjectSettings XML Documentation](https://github.com/godotengine/godot/blob/stable/doc/classes/ProjectSettings.xml)
