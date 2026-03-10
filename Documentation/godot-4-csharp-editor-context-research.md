# Godot 4.x C# Editor Context Research

## Executive Summary

This document provides a comprehensive overview of making C# scripts available in the Godot 4.x editor context, including approaches, limitations, and best practices.

## Key Finding: C# Does NOT Support [Tool] Mode

**CRITICAL LIMITATION:** Unlike GDScript, C# scripts do **not** support the `[Tool]` annotation for running code in the editor context. This is a fundamental limitation of the current Godot 4.x C# implementation.

### Why No [Tool] Mode in C#?

The lack of `[Tool]` mode in C# is due to architectural differences:

1. **GDScript** is interpreted at runtime by the Godot engine itself
2. **C#/.NET** is compiled to IL bytecode and runs on the .NET runtime
3. **Editor Context**: The editor is a separate Godot process with its own scene tree
4. **Compilation**: C# scripts require compilation before the editor can execute them

### Official Documentation Sources

Based on research from official Godot 4.x documentation:

- **C# Basics**: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html
- **Editor Plugins**: https://docs.godotengine.org/en/stable/tutorials/plugins/editor/index.html
- **C# Differences from GDScript**: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_differences.html

---

## 1. Autoload Singletons in Editor Context

### Problem Statement
Autoload singletons registered in `project.godot` are **not automatically available** in the editor context for C# scripts.

### Why This Happens

1. **Runtime vs Editor Separation**: The editor runs scripts in a separate context from the game runtime
2. **Mono Initialization**: The .NET/Mono runtime is initialized differently in the editor
3. **Instance Availability**: Autoload instances may not be instantiated when the editor loads C# scripts

### Workarounds and Approaches

#### Approach 1: Use EditorPlugin (Recommended)

Create an EditorPlugin in C# that manages singleton-like behavior:

```csharp
#if TOOLS
using Godot;

[Tool]
public partial class MyEditorPlugin : EditorPlugin
{
    private static MyEditorSingleton _instance;

    public static MyEditorSingleton Instance => _instance;

    public override void _EnterTree()
    {
        _instance = this;
        // Initialize editor-specific singleton
    }

    public override void _ExitTree()
    {
        _instance = null;
        // Cleanup
    }

    // Provide methods that other scripts can call
    public void DoEditorThing()
    {
        GD.Print("Doing editor thing from C#!");
    }
}
#endif
```

**Pros:**
- Runs in actual editor context
- Can access editor APIs
- Properly initialized in editor
- Can be accessed by other editor scripts

**Cons:**
- Requires `#if TOOLS` directive
- Not accessible at runtime (only editor)
- Must be enabled as a plugin
- Separate from runtime autoloads

#### Approach 2: Static Class Pattern

Create a static utility class with conditional compilation:

```csharp
public static class EditorUtilities
{
#if TOOLS
    public static void LogEditorMessage(string message)
    {
        GD.Print($"[EDITOR] {message}");
    }

    public static bool IsInEditor => true;
#else
    public static void LogEditorMessage(string message)
    {
        // No-op or use GD.Print with prefix
    }

    public static bool IsInEditor => false;
#endif
}

// Usage in your scripts
public override void _Ready()
{
    EditorUtilities.LogEditorMessage("Node is ready");
    if (EditorUtilities.IsInEditor)
    {
        // Do editor-only initialization
    }
}
```

**Pros:**
- Simple to implement
- Works at both compile-time and runtime
- No plugin setup required
- Easy to access from anywhere in codebase

**Cons:**
- Cannot access actual editor APIs
- Limited to pure C# logic
- No access to editor scene tree
- Cannot manipulate editor UI

#### Approach 3: EditorResourceImportPlugin

For data loading scenarios:

```csharp
#if TOOLS
using Godot;

[Tool]
public partial class MyDataLoader : EditorResourceImportPlugin
{
    public override string _GetImporterName() => "My Custom Data";

    public override string _GetVisibleName() => "My Custom Data";

    public override string _GetRecogizedExtensions() => "mdata";

    public override string _GetSaveExtension() => "res";

    public override int _GetPresetCount() => 0;

    public override int _GetImportOrder() => 100;

    public override string[] _GetOptions(string path)
    {
        return new string[] { };
    }

    public override Error _Import(string sourceFile, string savePath, Dictionary options, EditorInterface editorInterface)
    {
        // Process data here
        // Access editor interface if needed
        var data = FileAccess.Open(sourceFile, FileAccess.ModeFlags.Read);
        // ... process data
        return Error.Ok;
    }
}
#endif
```

**Pros:**
- Full access to editor context
- Can import custom resources
- Runs during asset pipeline

**Cons:**
- Only works for resource import
- Not general-purpose
- Requires plugin registration

#### Approach 4: Check Engine.EditorHint

Runtime detection with limited editor capabilities:

```csharp
public override void _Ready()
{
    if (Engine.EditorHint == EditorHint.Editor)
    {
        // We're in the editor, but C# tool mode is not available
        // Can do basic setup, but limited editor interaction
        SetupEditorPreview();
    }
    else
    {
        // Runtime initialization
        SetupRuntimeBehavior();
    }
}

private void SetupEditorPreview()
{
    // Can do basic visual setup
    // Cannot access editor-specific APIs or tools
    // Good for preview data or visual debugging
}
```

**Pros:**
- Simple detection
- Works without plugins
- Good for conditional behavior

**Cons:**
- Very limited editor access
- Cannot access editor scene tree
- Cannot modify editor interface

---

## 2. Differences Between [Tool] Scripts, EditorPlugin, and Other Solutions

### Comparison Table

| Feature | [Tool] Scripts (GDScript) | EditorPlugin (C#/GDScript) | Custom C# Solutions |
|---------|----------------------------|----------------------------|-------------------|
| **Availability** | Only GDScript | C# and GDScript | C# only |
| **Editor Context** | Yes | Yes | Partial/Limited |
| **Runtime Access** | Yes | No (editor only) | Yes |
| **Editor APIs** | Full | Full | Very Limited |
| **Scene Tree Access** | Yes | Yes | No |
| **Hot Reload** | Yes | Yes | Yes |
| **Plugin Required** | No | Yes | Depends on approach |
| **Performance** | Good | Good | Excellent |

### [Tool] Scripts (GDScript Only)

```gdscript
@tool
extends Node

export var my_value = 10 setget _get_value setget _set_value

func _get_value():
    return my_value

func _set_value(new_value):
    my_value = new_value
    property_list_changed_notify()
    update_configuration_warning()

func _process(delta):
    # Runs in editor and runtime
    pass

func get_configuration_warning():
    return "Warning message shown in editor"
```

**When to Use:**
- Simple visual nodes
- Inspector property validation
- Custom drawing in editor
- GDScript-based editor tools

**Limitations:**
- GDScript only
- Cannot use with C# classes
- Mixed projects may have complexity

### EditorPlugin (C# and GDScript)

```csharp
#if TOOLS
using Godot;

[Tool]
public partial class CustomInspectorPlugin : EditorPlugin
{
    private EditorInspector _inspector;

    public override void _EnterTree()
    {
        _inspector = GetEditorInterface().GetInspector();
    }

    public override bool _Handles(Object obj)
    {
        return obj is MyCustomResource;
    }

    public override bool _CanForwardToAtGui3d()
    {
        return false;
    }

    public override void _Edit(Object obj)
    {
        // Custom editor UI
        MyCustomResource resource = (MyCustomResource)obj;
        // Add custom controls to inspector
    }

    public override bool _HasMainScreen()
    {
        return false;
    }
}
#endif
```

**When to Use:**
- Custom inspector panels
- Editor-specific UI
- Resource import plugins
- Full editor integration needed

**Limitations:**
- Requires plugin enablement
- Separate from runtime code
- Only runs in editor

### EditorInspectorPlugin

```csharp
#if TOOLS
using Godot;

[Tool]
public partial class MyNodeInspector : EditorInspectorPlugin
{
    public override bool _CanForwardToAtGui3d()
    {
        return true;
    }

    public override bool _ParseProperty(Object obj, int type, string path, int hint, string hintText, int usage)
    {
        // Custom property parsing
        return false;
    }

    public override bool _Handle(Object obj)
    {
        return obj is MyCustomNode;
    }
}
#endif
```

**When to Use:**
- Custom property editors
- Advanced inspector behavior
- Type-specific editing

### EditorSelection (C# 4.2+)

Newer approach for managing editor selection:

```csharp
#if TOOLS
using Godot;

[Tool]
public partial class MyEditorTool : EditorPlugin
{
    public override void _MakeVisible(bool visible)
    {
        base._MakeVisible(visible);

        if (visible)
        {
            // Show custom editor tools
        }
    }

    public override void _ApplyChanges()
    {
        // Apply changes from editor to scene
    }
}
#endif
```

---

## 3. Best Practices for Accessing Settings/Data in Editor vs Runtime

### Architecture Pattern: Separation of Concerns

#### 3-Tier Architecture

```
┌─────────────────────────────────────────────┐
│         Editor Layer (C# Plugins)          │
│  - EditorInspectorPlugin                    │
│  - EditorResourceImportPlugin              │
│  - EditorScenePostImport                  │
└─────────────────────────────────────────────┘
                    ↕
┌─────────────────────────────────────────────┐
│     Shared Data Layer (Resources)          │
│  - ScriptableObject-like patterns           │
│  - Resource definitions                  │
│  - Configuration files                    │
└─────────────────────────────────────────────┘
                    ↕
┌─────────────────────────────────────────────┐
│      Runtime Layer (Game Scripts)          │
│  - Node scripts                         │
│  - Autoload singletons                  │
│  - Game systems                         │
└─────────────────────────────────────────────┘
```

### Data Access Patterns

#### Pattern 1: Resource-Based Configuration

```csharp
// Shared configuration resource
[Tool]
public partial class GameConfig : Resource
{
    [Export]
    public string GameName { get; set; }

    [Export]
    public int MaxPlayers { get; set; }

    [Export]
    public Godot.Collections.Array<string> GameModes { get; set; }

    // Save/Load methods for editor use
    public void SaveToFile(string path)
    {
        ResourceSaver.Save(this, path);
    }

    public static GameConfig LoadFromFile(string path)
    {
        return ResourceLoader.Load<GameConfig>(path);
    }
}

// Editor plugin saves config
#if TOOLS
[Tool]
public partial class ConfigEditor : EditorPlugin
{
    public override void _ApplyChanges()
    {
        var config = new GameConfig
        {
            GameName = "My Game",
            MaxPlayers = 4
        };
        config.SaveToFile("user://config/game_config.tres");
    }
}
#endif

// Runtime loads config
public override void _Ready()
{
    var config = GameConfig.LoadFromFile("user://config/game_config.tres");
    GD.Print($"Game: {config.GameName}");
}
```

**Pros:**
- Works in both editor and runtime
- Godot-native resource format
- Easy to inspect in editor
- Version control friendly

**Cons:**
- Requires save/load logic
- Not automatic synchronization

#### Pattern 2: EditorScript Runtime Bridge

```csharp
// Runtime singleton
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    [Signal]
    public delegate void SettingsChangedEventHandler(Dictionary newSettings);

    public Dictionary<string, Variant> Settings { get; private set; } = new Dictionary<string, Variant>();

    public override void _Ready()
    {
        Instance = this;
    }

    public void UpdateSetting(string key, Variant value)
    {
        Settings[key] = value;
        EmitSignal(nameof(SettingsChanged), Settings);
    }
}

// Editor plugin communicates with runtime
#if TOOLS
[Tool]
public partial class SettingsBridge : EditorPlugin
{
    private GameManager _gameManager;

    public override void _EnterTree()
    {
        // Find or create game manager in editor scene
        var rootScene = EditorInterface.GetEditedSceneRoot();
        if (rootScene != null)
        {
            _gameManager = new GameManager();
            AddControlPlugin(_gameManager);
        }
    }

    public void UpdateEditorSetting(string key, Variant value)
    {
        _gameManager?.UpdateSetting(key, value);
        SaveToProjectSettings(key, value);
    }

    private void SaveToProjectSettings(string key, Variant value)
    {
        var settings = ProjectSettings.LoadResourceFile(
            "res://project.godot",
            ProjectSettings.GetEditorSettingsPath()
        );
        settings.SetSetting($"custom/{key}", value);
        settings.Save();
    }
}
#endif
```

**Pros:**
- Live synchronization
- Signal-based updates
- Runtime preview in editor

**Cons:**
- Requires editor scene setup
- Complex initialization
- Potential circular dependencies

#### Pattern 3: ProjectSettings-Based Storage

```csharp
// Utility class for settings access
public static class ProjectSettingsManager
{
    private const string SETTINGS_PREFIX = "my_game/settings/";

    public static T GetSetting<T>(string key, T defaultValue = default)
    {
        var fullPath = $"{SETTINGS_PREFIX}{key}";
        if (!ProjectSettings.HasSetting(fullPath))
        {
            return defaultValue;
        }
        return (T)ProjectSettings.GetSetting(fullPath);
    }

    public static void SetSetting<T>(string key, T value)
    {
        var fullPath = $"{SETTINGS_PREFIX}{key}";
        ProjectSettings.SetSetting(fullPath, value);
        ProjectSettings.Save();
    }

#if TOOLS
    public static bool IsEditorContext()
    {
        return Engine.EditorHint == EditorHint.Editor;
    }
#endif
}

// Usage anywhere
ProjectSettingsManager.SetSetting("difficulty", "hard");
var difficulty = ProjectSettingsManager.GetSetting<string>("difficulty", "normal");
```

**Pros:**
- Native Godot integration
- Persists across editor sessions
- Simple API

**Cons:**
- Limited to simple types
- No complex object storage
- All-or-nothing access

#### Pattern 4: Static Editor/Runtime Separation

```csharp
// Common interface
public interface IGameSettings
{
    string GetDifficulty();
    void SetDifficulty(string difficulty);
    int GetMaxPlayers();
}

// Editor implementation (plugin-scoped)
#if TOOLS
public class EditorGameSettings : IGameSettings
{
    private Dictionary<string, object> _settings = new Dictionary<string, object>();

    public string GetDifficulty()
    {
        if (!_settings.ContainsKey("difficulty"))
            return "normal";
        return (string)_settings["difficulty"];
    }

    public void SetDifficulty(string difficulty)
    {
        _settings["difficulty"] = difficulty;
        SaveToDisk();
    }

    public int GetMaxPlayers()
    {
        return _settings.ContainsKey("max_players") ? (int)_settings["max_players"] : 2;
    }

    private void SaveToDisk()
    {
        // Save to editor-specific file
        using (var file = FileAccess.Open("user://editor_settings.json", FileAccess.ModeFlags.Write))
        {
            file.StoreString(Json.Stringify(_settings));
        }
    }
}
#endif

// Runtime implementation
public class RuntimeGameSettings : Node, IGameSettings
{
    [Export]
    public string DefaultDifficulty = "normal";

    [Export]
    public int DefaultMaxPlayers = 2;

    public string GetDifficulty()
    {
        return ProjectSettings.GetSetting("game/difficulty", DefaultDifficulty).AsString();
    }

    public void SetDifficulty(string difficulty)
    {
        ProjectSettings.SetSetting("game/difficulty", difficulty);
    }

    public int GetMaxPlayers()
    {
        return ProjectSettings.GetSetting("game/max_players", DefaultMaxPlayers).AsInt32();
    }
}
```

**Pros:**
- Clean separation
- Type-safe
- Context-specific implementations

**Cons:**
- Duplicate code
- Synchronization challenges
- More complex architecture

---

## 4. How Godot 4.x Handles Editor-Specific C# Code

### Compilation Model

Godot 4.x C# uses a sophisticated compilation model:

1. **Assembly Building**: C# scripts compiled to assemblies (.dll)
2. **Hot Reload**: Can reload assemblies at runtime
3. **Editor Separation**: Editor assemblies separate from game assemblies
4. **Conditional Compilation**: `#if TOOLS`, `#if DEBUG`, etc.

### Assembly Organization

```
Godot Project/
├── .mono/
│   ├── temp/
│   │   └── bin/        # Editor assemblies
│   └── data/
│       └── assemblies/    # Runtime assemblies
├── Scripts/
│   ├── Editor/
│   │   └── *.cs          # Editor-only scripts
│   └── Runtime/
│       └── *.cs          # Runtime scripts
└── project.godot
```

### Conditional Compilation Directives

```csharp
// Editor-only code
#if TOOLS
using Godot;
using GodotEditor;

public partial class EditorOnlyClass : EditorPlugin
{
    // Only compiled for editor
}
#endif

// Runtime-only code
#if !TOOLS
public class RuntimeOnlyClass
{
    // Only compiled for game export
}
#endif

// Debug-only code
#if DEBUG
public class DebugHelpers
{
    public static void LogDetailedInfo(string info)
    {
        GD.PrintVerbose($"DEBUG: {info}");
    }
}
#endif

// Release-only code
#if !DEBUG
public class ReleaseOptimizations
{
    public static void OptimizeForPerformance()
    {
        // Release-only optimizations
    }
}
#endif
```

### Editor Plugin Lifecycle

```
Editor Startup
    │
    ├─> Load EditorPlugin assemblies
    │   └─> Call _EnterTree() on each plugin
    │       ├─> Register plugin functionality
    │       └─> Create editor UI elements
    │
    ├─> Open Project
    │   └─> Load Runtime assemblies
    │       └─> Scripts available to nodes
    │
    ├─> Edit Scene
    │   └─> Scripts run in limited mode
    │       └─> _Ready(), _Process() etc. with limited editor access
    │
    └─> Run Game (F6)
        └─> Full runtime execution
            └─> All Godot APIs available
```

### GodotEditor Namespace

Editor-specific C# code has access to `GodotEditor` namespace:

```csharp
#if TOOLS
using Godot;
using GodotEditor;

[Tool]
public partial class MyEditorTool : EditorPlugin
{
    public override void _EnterTree()
    {
        // Access editor interfaces
        var editorInterface = GetEditorInterface();
        var sceneTree = GetSceneTree();
        var editorSelection = GetSelection();
        var editorSettings = GetEditorSettings();

        // Access editor windows
        var inspector = editorInterface.GetInspector();
        var resourcePreview = editorInterface.GetResourcePreviewer();

        // Create editor UI
        var dock = CreateEditorDock();
        AddControlPlugin(dock);
    }

    private Control CreateEditorDock()
    {
        var dock = new Control();
        dock.SetAnchorsPreset(Control.PresetModeFullRect);

        var label = new Label
        {
            Text = "Editor Dock",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        dock.AddChild(label);

        return dock;
    }
}
#endif
```

### Scene Tree Plugin (New in 4.2+)

```csharp
#if TOOLS
using Godot;
using GodotEditor;

[Tool]
public partial class MyScenePlugin : EditorScenePostImport
{
    public override void _PostImport(Scene scene)
    {
        // Modify scene after import
        var root = scene.InstantiateScene();
        // Apply custom modifications
        // Save modified scene
        var packedScene = new PackedScene();
        packedScene.Pack(root);
        ResourceSaver.Save(packedScene, scene.ResourcePath);
    }
}
#endif
```

---

## 5. Patterns for Sharing Settings Between Editor and Runtime

### Pattern 1: Shared Resource with Editor and Runtime Logic

```csharp
// Shared base class
[Tool]
public abstract partial class BaseGameConfig : Resource
{
    [Export]
    public string ConfigName { get; set; }

    public abstract void ApplyConfig(Node target);

    public abstract Dictionary<string, Variant> GetConfigData();
}

// Editor-specific implementation
#if TOOLS
[Tool]
public partial class EditorGameConfig : BaseGameConfig
{
    public override void ApplyConfig(Node target)
    {
        // Editor preview logic
        GD.Print($"Applying config in editor: {ConfigName}");
        // Update editor preview
    }

    public override Dictionary<string, Variant> GetConfigData()
    {
        // Gather from editor UI
        var data = new Dictionary<string, Variant>();
        data["source"] = "editor";
        return data;
    }

    public void SaveToProject()
    {
        var configData = GetConfigData();
        var json = Json.Stringify(configData);
        ProjectSettings.SetSetting($"game/config/{ConfigName}", json);
        ProjectSettings.Save();
    }
}
#endif

// Runtime implementation
public partial class RuntimeGameConfig : BaseGameConfig
{
    public override void ApplyConfig(Node target)
    {
        // Runtime application
        var data = GetConfigData();
        foreach (var kvp in data)
        {
            target.SetMeta(kvp.Key, kvp.Value);
        }
    }

    public override Dictionary<string, Variant> GetConfigData()
    {
        // Load from ProjectSettings
        var json = ProjectSettings.GetSetting($"game/config/{ConfigName}", "{}").AsString();
        return (Dictionary<string, Variant>)Json.ParseString(json);
    }
}
```

### Pattern 2: Signal-Based Configuration Bridge

```csharp
// Configuration manager singleton
public partial class ConfigManager : Node
{
    public static ConfigManager Instance { get; private set; }

    [Signal]
    public delegate void ConfigChangedEventHandler(string key, Variant value);

    private Dictionary<string, Variant> _config = new Dictionary<string, Variant>();

    public override void _Ready()
    {
        Instance = this;
        LoadAllConfigurations();
    }

    public void SetConfig(string key, Variant value)
    {
        _config[key] = value;
        EmitSignal(nameof(ConfigChanged), key, value);
        SaveConfiguration(key, value);
    }

    public Variant GetConfig(string key, Variant defaultValue = default)
    {
        return _config.TryGetValue(key, out var value) ? value : defaultValue;
    }

    private void LoadAllConfigurations()
    {
        // Load from various sources
        LoadFromProjectSettings();
        LoadFromResources();
    }

    private void SaveConfiguration(string key, Variant value)
    {
        // Save to project settings
        ProjectSettings.SetSetting($"config/{key}", value);
        ProjectSettings.Save();
    }
}

// Editor plugin listens and updates
#if TOOLS
[Tool]
public partial class ConfigEditorPlugin : EditorPlugin
{
    private ConfigManager _configManager;

    public override void _EnterTree()
    {
        // Connect to config manager signals
        if (ConfigManager.Instance != null)
        {
            _configManager = new ConfigManager();
            GetEditorInterface().GetEditorSettings().AddChild(_configManager);
        }

        ConfigManager.Instance?.Connect(
            ConfigManager.SignalName.ConfigChanged,
            this,
            nameof(OnConfigChanged)
        );
    }

    private void OnConfigChanged(string key, Variant value)
    {
        // Update editor UI
        GD.Print($"Config changed: {key} = {value}");
        RefreshEditorUI();
    }

    public void RefreshEditorUI()
    {
        // Update inspector values
        // Update dock controls
    }

    public override void _ApplyChanges()
    {
        // Save changes from editor UI
        foreach (var key in GetModifiedKeys())
        {
            var value = GetEditorValue(key);
            ConfigManager.Instance?.SetConfig(key, value);
        }
    }
}
#endif
```

### Pattern 3: YAML/JSON Configuration Files

```csharp
// Configuration model classes
using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

[Serializable]
public class GameConfiguration
{
    [YamlMember(Alias = "game_name")]
    public string GameName { get; set; } = "Untitled Game";

    [YamlMember(Alias = "max_players")]
    public int MaxPlayers { get; set; } = 2;

    [YamlMember(Alias = "difficulty")]
    public string Difficulty { get; set; } = "normal";

    [YamlMember(Alias = "game_modes")]
    public List<string> GameModes { get; set; } = new List<string> { "story", "survival" };

    public Dictionary<string, Variant> ToGodotDictionary()
    {
        var dict = new Dictionary<string, Variant>();
        dict["game_name"] = GameName;
        dict["max_players"] = MaxPlayers;
        dict["difficulty"] = Difficulty;
        dict["game_modes"] = GameModes.ToGodotArray();
        return dict;
    }

    public static GameConfiguration FromGodotDictionary(Dictionary<string, Variant> dict)
    {
        return new GameConfiguration
        {
            GameName = dict.GetValueOrDefault("game_name", "Untitled Game").AsString(),
            MaxPlayers = dict.GetValueOrDefault("max_players", 2).AsInt32(),
            Difficulty = dict.GetValueOrDefault("difficulty", "normal").AsString(),
            GameModes = dict.TryGetValue("game_modes", out var modes)
                ? ((Godot.Collections.Array)modes).ToList<string>()
                : new List<string> { "story", "survival" }
        };
    }
}

// Editor plugin reads/writes YAML
#if TOOLS
[Tool]
public partial class YamlConfigEditor : EditorPlugin
{
    private const string CONFIG_PATH = "res://config/game_config.yaml";

    public override void _EnterTree()
    {
        EnsureConfigExists();
    }

    public GameConfiguration LoadConfiguration()
    {
        using (var file = FileAccess.Open(CONFIG_PATH, FileAccess.ModeFlags.Read))
        {
            var yamlContent = file.GetAsText();
            return Yaml.Deserializer.Deserialize<GameConfiguration>(yamlContent);
        }
    }

    public void SaveConfiguration(GameConfiguration config)
    {
        var yamlContent = Yaml.Serializer.Serialize(config);
        using (var file = FileAccess.Open(CONFIG_PATH, FileAccess.ModeFlags.Write))
        {
            file.StoreString(yamlContent);
        }

        // Update project settings for runtime access
        var godotDict = config.ToGodotDictionary();
        ProjectSettings.SetSetting("game/config_data", Json.Stringify(godotDict));
        ProjectSettings.Save();
    }

    private void EnsureConfigExists()
    {
        if (!FileAccess.FileExists(CONFIG_PATH))
        {
            SaveConfiguration(new GameConfiguration());
        }
    }
}
#endif

// Runtime loads from JSON in ProjectSettings
public override void _Ready()
{
    var configJson = ProjectSettings.GetSetting("game/config_data", "{}").AsString();
    var godotDict = (Dictionary<string, Variant>)Json.ParseString(configJson);
    var config = GameConfiguration.FromGodotDictionary(godotDict);

    GD.Print($"Loaded game: {config.GameName}");
    GD.Print($"Max players: {config.MaxPlayers}");
}
```

### Pattern 4: Singleton Bridge with Editor Proxy

```csharp
// Runtime singleton
public partial class GameDataManager : Node
{
    public static GameDataManager Instance { get; private set; }

    public Dictionary<string, Variant> GameData { get; private set; } = new Dictionary<string, Variant>();

    [Signal]
    public delegate void DataChangedEventHandler(string key, Variant oldValue, Variant newValue);

    public override void _Ready()
    {
        Instance = this;
    }

    public void SetData(string key, Variant value)
    {
        var oldValue = GameData.TryGetValue(key, out var v) ? v : default;
        GameData[key] = value;
        EmitSignal(nameof(DataChanged), key, oldValue, value;
    }

    public Variant GetData(string key, Variant defaultValue = default)
    {
        return GameData.TryGetValue(key, out var value) ? value : defaultValue;
    }
}

// Editor proxy
#if TOOLS
[Tool]
public partial class EditorDataProxy : EditorPlugin
{
    private GameDataManager _dataManager;

    // Forward data access to runtime
    public void SetEditorData(string key, Variant value)
    {
        if (_dataManager != null)
        {
            InitializeDataManager();
        }
        _dataManager.SetData(key, value);
    }

    public Variant GetEditorData(string key, Variant defaultValue = default)
    {
        if (_dataManager != null)
        {
            InitializeDataManager();
        }
        return _dataManager.GetData(key, defaultValue);
    }

    private void InitializeDataManager()
    {
        // Create data manager in editor context
        _dataManager = new GameDataManager();
        AddControlPlugin(_dataManager);

        // Connect to changes
        _dataManager.Connect(
            GameDataManager.SignalName.DataChanged,
            this,
            nameof(OnDataChanged)
        );
    }

    private void OnDataChanged(string key, Variant oldValue, Variant newValue)
    {
        // Sync to project settings
        ProjectSettings.SetSetting($"editor_data/{key}", newValue);
        ProjectSettings.Save();

        // Update editor UI
        RefreshDataDisplay(key, newValue);
    }

    private void RefreshDataDisplay(string key, Variant value)
    {
        // Update inspector/dock UI
        GD.Print($"Editor data changed: {key}");
    }
}
#endif
```

---

## Recommended Best Practices

### 1. Architecture Principles

**Separation of Concerns:**
- Keep editor code in `Scripts/Editor/` with `#if TOOLS`
- Keep runtime code in `Scripts/Runtime/` or root `Scripts/`
- Use shared interfaces for common functionality

**Example Structure:**
```
Scripts/
├── Editor/
│   ├── Plugins/
│   │   ├── MyEditorPlugin.cs
│   │   └── InspectorPlugins/
│   │       └── MyInspector.cs
│   └── Utilities/
│       └── EditorHelpers.cs
├── Runtime/
│   ├── Systems/
│   │   ├── GameManager.cs
│   │   └── DataManager.cs
│   └── Nodes/
│       └── MyNode.cs
└── Shared/
    ├── Interfaces/
    │   └── IGameSettings.cs
    └── Models/
        └── GameConfig.cs
```

### 2. Data Persistence Strategy

**Use ProjectSettings for:**
- Simple key-value pairs
- Editor settings
- User preferences

**Use Resources for:**
- Complex configuration objects
- Game data definitions
- Serializable configurations

**Use Files for:**
- Large datasets
- User-generated content
- Asset metadata

### 3. Communication Patterns

**Editor to Runtime:**
- ProjectSettings (sync on save)
- Resource files (reloaded on change)
- Signals (live preview)

**Runtime to Editor:**
- Limited (runtime doesn't have editor access)
- Write to files (editor monitors)
- ProjectSettings (runtime writes, editor reads)

### 4. Code Organization

**Namespace Organization:**
```csharp
namespace MyGame.Editor.Plugins
{
    // Editor plugin code
}

namespace MyGame.Editor.Inspectors
{
    // Inspector plugins
}

namespace MyGame.Runtime.Systems
{
    // Runtime systems
}

namespace MyGame.Shared
{
    // Shared code
}
```

**Using Aliases for Context:**
```csharp
#if TOOLS
using GameConfig = MyGame.Editor.Models.EditorGameConfig;
#else
using GameConfig = MyGame.Runtime.Models.RuntimeGameConfig;
#endif
```

### 5. Testing Strategy

**Editor Testing:**
- Test with `Engine.EditorHint`
- Verify inspector behavior
- Test save/load cycles

**Runtime Testing:**
- Test in standalone game
- Verify export functionality
- Test data persistence

**Integration Testing:**
- Test editor → runtime data flow
- Verify settings synchronization
- Test hot reload scenarios

---

## Limitations and Workarounds Summary

| Limitation | Workaround | Complexity |
|------------|------------|-------------|
| No [Tool] mode in C# | Use EditorPlugin or static classes | Medium |
| Autoloads not in editor | Create editor-specific singletons | Low |
| Limited editor APIs in C# | Use GodotEditor namespace plugins | Medium |
| No live scene tree editing | Use EditorPlugin with scene tree access | High |
| Mixed GDScript/C# complexity | Separate concerns, use shared interfaces | Medium-High |
| Settings synchronization | ProjectSettings + Resources | Medium |
| Hot reload complexity | Careful assembly organization | High |

---

## Concrete Example: Complete Editor/Runtime Solution

### Scenario: Editor-Configurable Player Spawner

```csharp
// Shared configuration resource
[Tool]
public partial class PlayerSpawnConfig : Resource
{
    [Export]
    public int MaxConcurrentPlayers { get; set; } = 4;

    [Export]
    public float RespawnDelay { get; set; } = 3.0f;

    [Export]
    public Godot.Collections.Array<string> SpawnPoints { get; set; } = new Godot.Collections.Array<string> { "spawn1", "spawn2", "spawn3" };

    [ExportCategory("Advanced")]
    [Export]
    public bool AutoBalanceTeams { get; set; } = true;

    public void SaveToProject()
    {
        var dict = new Dictionary<string, Variant>
        {
            { "max_concurrent", MaxConcurrentPlayers },
            { "respawn_delay", RespawnDelay },
            { "spawn_points", SpawnPoints },
            { "auto_balance", AutoBalanceTeams }
        };

        var json = Json.Stringify(dict);
        ProjectSettings.SetSetting("player_spawner/config", json);
        ProjectSettings.Save();
    }

    public void LoadFromProject()
    {
        var json = ProjectSettings.GetSetting("player_spawner/config", "{}").AsString();
        var dict = (Dictionary<string, Variant>)Json.ParseString(json);

        MaxConcurrentPlayers = dict.GetValueOrDefault("max_concurrent", 4).AsInt32();
        RespawnDelay = dict.GetValueOrDefault("respawn_delay", 3.0f).AsSingle();
        SpawnPoints = dict.TryGetValue("spawn_points", out var points)
            ? (Godot.Collections.Array<string>)points
            : new Godot.Collections.Array<string> { "spawn1", "spawn2", "spawn3" };
        AutoBalanceTeams = dict.GetValueOrDefault("auto_balance", true).AsBool();
    }
}

// Editor plugin for configuration
#if TOOLS
using Godot;
using GodotEditor;

[Tool]
public partial class PlayerSpawnConfigEditor : EditorInspectorPlugin
{
    public override bool _Handles(Object obj)
    {
        return obj is PlayerSpawnConfig;
    }

    public override void _Edit(Object obj)
    {
        var config = (PlayerSpawnConfig)obj;

        // Create custom inspector UI
        var container = new VBoxContainer();

        // Max Players
        container.AddChild(CreateLabeledSlider(
            "Max Concurrent Players",
            1,
            16,
            config.MaxConcurrentPlayers,
            (value) => config.MaxConcurrentPlayers = (int)value
        ));

        // Respawn Delay
        container.AddChild(CreateLabeledSlider(
            "Respawn Delay (seconds)",
            0.5f,
            10.0f,
            config.RespawnDelay,
            (value) => config.RespawnDelay = (float)value
        ));

        // Save Button
        var saveButton = new Button { Text = "Save Configuration" };
        saveButton.Pressed += () =>
        {
            config.SaveToProject();
            GD.Print("Player spawn configuration saved!");
        };
        container.AddChild(saveButton);

        // Add to inspector
        AddCustomControl(container);
    }

    private Control CreateLabeledSlider(string label, float min, float max, float current, System.Action<float> onChanged)
    {
        var container = new VBoxContainer();

        var labelNode = new Label { Text = $"{label}: {current}" };
        container.AddChild(labelNode);

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Value = current,
            Step = 0.1f
        };
        slider.ValueChanged += (value) =>
        {
            labelNode.Text = $"{label}: {value:F1}";
            onChanged((float)value);
        };
        container.AddChild(slider);

        return container;
    }
}
#endif

// Runtime spawner
public partial class PlayerSpawner : Node
{
    [Export]
    public PlayerSpawnConfig Config { get; set; }

    public override void _Ready()
    {
        if (Config == null)
        {
            // Create default config
            Config = new PlayerSpawnConfig();
            Config.LoadFromProject();
        }

        GD.Print($"Player Spawner Ready:");
        GD.Print($"  Max Players: {Config.MaxConcurrentPlayers}");
        GD.Print($"  Respawn Delay: {Config.RespawnDelay}s");
        GD.Print($"  Auto Balance: {Config.AutoBalanceTeams}");
    }

    public void SpawnPlayer(int playerId)
    {
        // Spawn logic using config
        var spawnPointName = Config.SpawnPoints[playerId % Config.SpawnPoints.Count];
        var spawnPoint = GetNode<Node>(spawnPointName);

        var player = new Player
        {
            PlayerId = playerId,
            Team = Config.AutoBalanceTeams ? CalculateTeam(playerId) : -1
        };

        AddChild(player);
        player.GlobalPosition = spawnPoint.GlobalPosition;
    }

    private int CalculateTeam(int playerId)
    {
        return playerId % 2; // Simple balance logic
    }
}

// Register plugin in plugin.cfg
[plugin]

name="Player Spawn Config Editor"
description="Editor for configuring player spawn settings"
author="Your Name"
version="1.0"
script="res://Scripts/Editor/Plugins/PlayerSpawnConfigEditor.cs"

# Autoload the config resource
[autoload]

name="PlayerSpawnConfig"
path="res://Resources/PlayerSpawnConfig.tres"
```

---

## Conclusion

### Key Takeaways

1. **C# Does Not Support [Tool] Mode**: This is the fundamental limitation in Godot 4.x
2. **EditorPlugins Are the Solution**: Use `EditorPlugin` with `#if TOOLS` for editor functionality
3. **Architecture Matters**: Separate editor and runtime code with shared interfaces
4. **Data Synchronization**: Use ProjectSettings and resources to bridge contexts
5. **Compilation Directives**: `#if TOOLS`, `#if DEBUG`, etc., enable conditional compilation

### Recommended Approach

For a planet generation C# project like yours:

1. **Editor Functionality**:
   - Use `EditorPlugin` for configuration UI
   - Create inspector plugins for custom nodes
   - Use resource import plugins for data files

2. **Settings Management**:
   - Store settings in ProjectSettings for sync
   - Use Resource files for complex configurations
   - Implement save/load for editor use

3. **Runtime Access**:
   - Use autoload singletons at runtime
   - Load from ProjectSettings at startup
   - Maintain consistent data models

4. **Code Organization**:
   ```
   Scripts/
   ├── Editor/          # Editor plugins, #if TOOLS
   ├── Runtime/         # Game scripts, no TOOLS
   └── Shared/          # Common interfaces, no directives
   ```

### Future Considerations

Watch for:
- Godot 4.x updates for C# tool mode support
- New editor plugin APIs
- Improved hot reload functionality
- Better editor/runtime integration

---

## References

- Godot C# Documentation: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/
- Editor Plugins: https://docs.godotengine.org/en/stable/tutorials/plugins/editor/
- Godot 4.x Release Notes: https://github.com/godotengine/godot/releases
- Godot GitHub: https://github.com/godotengine/godot
- Community Forum: https://forum.godotengine.org/
