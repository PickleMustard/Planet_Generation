# VisualDefinition Icon Extension Plan - REVISED (v2)

## Overview

This plan creates a separate `IconDefinition` system for 2D icon textures, distinct from the 3D-focused `VisualDefinition`. Icons support three sizes (64px, 128px, 512px) all eagerly loaded at startup via a static `IconDataLoader` library.

## Requirements Summary

1. **Separate IconDefinition** from VisualDefinition
2. **Three Icon Sizes** (64px, 128px, 512px) all loaded at startup
3. **Static Library** `IconDataLoader` in `UtilityLibrary/DataLoading`
4. **Base Path Pattern** in YAML - library appends `_64`, `_128`, `_512` suffixes
5. **Data Loaders Handle Fallback** - check for null and assign fallback
6. **All Entity Types** (Resources, Recipes, Buildings, Ships, Stations)
7. **Placeholder Icons** for development

---

## Phase 1: IconSize Enumeration

**NEW FILE**: `Scripts/Structures/Enums/IconSize.cs`

```csharp
namespace Structures.Enums;

/// <summary>
/// Standard icon sizes for UI display.
/// All three sizes are loaded at startup for each icon.
/// </summary>
public enum IconSize
{
    Small = 64,    // 64x64 - UI lists, tooltips
    Medium = 128,  // 128x128 - Standard UI panels
    Large = 512    // 512x512 - Detail views, high-DPI
}

public static class IconSizeExtensions
{
    /// <summary>
    /// Gets the pixel dimension for an icon size.
    /// </summary>
    public static int GetPixels(this IconSize size) => (int)size;
    
    /// <summary>
    /// Gets the file suffix for an icon size (e.g., "_64", "_128", "_512").
    /// </summary>
    public static string GetSuffix(this IconSize size) => $"_{(int)size}";
    
    /// <summary>
    /// Gets the default icon size for general UI use.
    /// </summary>
    public static IconSize Default => IconSize.Medium;
}
```

---

## Phase 2: Create IconDefinition

**NEW FILE**: `Scripts/Structures/Resources/IconDefinition.cs`

```csharp
using Godot;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Defines icon textures for all three standard sizes.
/// All sizes are eagerly loaded at startup.
/// </summary>
public class IconDefinition
{
    /// <summary>Base path pattern for icon files (without size suffix).</summary>
    /// <example>res://Assets/Icons/Resources/ore/iron_ore</example>
    public string? BasePath { get; set; }
    
    /// <summary>64x64 icon texture.</summary>
    public Texture2D? SmallTexture { get; set; }
    
    /// <summary>128x128 icon texture.</summary>
    public Texture2D? MediumTexture { get; set; }
    
    /// <summary>512x512 icon texture.</summary>
    public Texture2D? LargeTexture { get; set; }
    
    /// <summary>Scale multiplier for UI display.</summary>
    public float Scale { get; set; } = 1.0f;
    
    /// <summary>Tint color for the icon. White = no tint.</summary>
    public Color Tint { get; set; } = Colors.White;
    
    /// <summary>
    /// Returns true if all three icon sizes are loaded and valid.
    /// </summary>
    public bool HasAllSizes => 
        SmallTexture != null && 
        MediumTexture != null && 
        LargeTexture != null;
    
    /// <summary>
    /// Returns true if at least the medium icon is loaded.
    /// </summary>
    public bool IsValid => MediumTexture != null;
    
    /// <summary>
    /// Gets the icon texture for a specific size.
    /// Returns null if not loaded.
    /// </summary>
    public Texture2D? GetTexture(IconSize size)
    {
        return size switch
        {
            IconSize.Small => SmallTexture,
            IconSize.Medium => MediumTexture,
            IconSize.Large => LargeTexture,
            _ => MediumTexture
        };
    }
    
    /// <summary>
    /// Gets the effective pixel dimensions for a given size, accounting for scale.
    /// </summary>
    public Vector2 GetDimensions(IconSize size)
    {
        float baseSize = size.GetPixels();
        float scaled = baseSize * Scale;
        return new Vector2(scaled, scaled);
    }
}
```

---

## Phase 3: Create IconDataLoader Static Library

**NEW FILE**: `Scripts/UtilityLibrary/DataLoading/IconDataLoader.cs`

```csharp
using Godot;
using Structures.Enums;
using Structures.Resources;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Static library for loading icon textures from the filesystem.
/// Supports loading all three standard sizes (64, 128, 512) from a base path pattern.
/// </summary>
public static class IconDataLoader
{
    // Fallback textures cached by size
    private static readonly Dictionary<IconSize, Texture2D> _fallbackTextures = new();
    private static bool _fallbacksInitialized = false;
    
    // Statistics tracking
    public static int IconsLoaded { get; private set; }
    public static int IconsFailed { get; private set; }
    
    /// <summary>
    /// Resets loading statistics.
    /// </summary>
    public static void ResetStats()
    {
        IconsLoaded = 0;
        IconsFailed = 0;
    }
    
    /// <summary>
    /// Loads an icon definition with all three sizes from a base path.
    /// </summary>
    /// <param name="basePath">Base path without size suffix (e.g., "res://Assets/Icons/ore/iron_ore")</param>
    /// <param name="context">Context for logging (e.g., entity name)</param>
    /// <returns>IconDefinition with loaded textures, or empty if basePath is null/empty</returns>
    public static IconDefinition LoadIcon(string? basePath, string context)
    {
        var icon = new IconDefinition { BasePath = basePath };
        
        if (string.IsNullOrEmpty(basePath))
        {
            return icon; // Return empty - data loader will apply fallback
        }
        
        // Load all three sizes
        icon.SmallTexture = LoadIconTexture(basePath, IconSize.Small, context);
        icon.MediumTexture = LoadIconTexture(basePath, IconSize.Medium, context);
        icon.LargeTexture = LoadIconTexture(basePath, IconSize.Large, context);
        
        return icon;
    }
    
    /// <summary>
    /// Loads a single icon texture for a specific size.
    /// </summary>
    /// <param name="basePath">Base path without size suffix</param>
    /// <param name="size">Icon size to load</param>
    /// <param name="context">Context for logging</param>
    /// <returns>Loaded Texture2D or null if loading fails</returns>
    public static Texture2D? LoadIconTexture(string basePath, IconSize size, string context)
    {
        string fullPath = $"{basePath}{size.GetSuffix()}.svg";
        
        try
        {
            if (!Godot.FileAccess.FileExists(fullPath))
            {
                // Try PNG fallback
                fullPath = $"{basePath}{size.GetSuffix()}.png";
                if (!Godot.FileAccess.FileExists(fullPath))
                {
                    GameLogger.Warning($"Icon not found for {context}: {basePath} ({size})");
                    IconsFailed++;
                    return null;
                }
            }
            
            var texture = GD.Load<Texture2D>(fullPath);
            if (texture != null)
            {
                GameLogger.Debug($"Loaded icon for {context}: {fullPath}");
                IconsLoaded++;
                return texture;
            }
            else
            {
                GameLogger.Error($"Failed to load icon for {context}: {fullPath}");
                IconsFailed++;
                return null;
            }
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Exception loading icon for {context}: {fullPath} - {ex.Message}");
            IconsFailed++;
            return null;
        }
    }
    
    /// <summary>
    /// Gets the fallback texture for a specific size.
    /// Generates and caches fallback on first call.
    /// </summary>
    public static Texture2D GetFallbackIcon(IconSize size)
    {
        InitializeFallbacks();
        return _fallbackTextures.GetValueOrDefault(size, _fallbackTextures[IconSize.Medium]);
    }
    
    /// <summary>
    /// Creates a complete IconDefinition using fallback textures for all sizes.
    /// </summary>
    public static IconDefinition CreateFallbackIconDefinition()
    {
        InitializeFallbacks();
        
        return new IconDefinition
        {
            BasePath = null,
            SmallTexture = _fallbackTextures[IconSize.Small],
            MediumTexture = _fallbackTextures[IconSize.Medium],
            LargeTexture = _fallbackTextures[IconSize.Large]
        };
    }
    
    private static void InitializeFallbacks()
    {
        if (_fallbacksInitialized)
            return;
        
        foreach (IconSize size in Enum.GetValues<IconSize>())
        {
            _fallbackTextures[size] = GenerateFallbackTexture(size);
        }
        
        _fallbacksInitialized = true;
    }
    
    private static Texture2D GenerateFallbackTexture(IconSize size)
    {
        int pixels = size.GetPixels();
        
        // Generate SVG with ? mark
        string svg = $@"<svg width=""{pixels}"" height=""{pixels}"" viewBox=""0 0 {pixels} {pixels}"" xmlns=""http://www.w3.org/2000/svg"">
            <rect width=""{pixels}"" height=""{pixels}"" fill=""#333333"" rx=""8"" ry=""8""/>
            <text x=""{pixels/2}"" y=""{pixels/2 + pixels/8}"" font-family=""Arial, sans-serif"" 
                  font-size=""{pixels/2}"" fill=""#666666"" text-anchor=""middle"">?</text>
        </svg>";
        
        var image = new Image();
        image.LoadSvgFromBuffer(svg.ToUtf8Buffer(), (float)pixels);
        return ImageTexture.CreateFromImage(image);
    }
}
```

---

## Phase 4: Add Icon Property to All Definitions

### 4.1 ResourceDefinition

**FILE**: `Scripts/Structures/Resources/ResourceDefinition.cs`

```csharp
/// <summary>
/// Visual representation including 2D icon for UI display.
/// </summary>
public IconDefinition Icon { get; set; } = new();

/// <summary>
/// Gets the effective icon tint, falling back to DisplayColor if not set.
/// </summary>
public Color GetEffectiveIconTint()
{
    if (Icon?.Tint != Colors.White)
    {
        return Icon.Tint;
    }
    return DisplayColor;
}
```

### 4.2 RecipeDefinition

**FILE**: `Scripts/Structures/Resources/RecipeDefinition.cs`

```csharp
/// <summary>
/// Visual representation for UI display.
/// Recipes must explicitly define an icon_base_path.
/// </summary>
public IconDefinition Icon { get; set; } = new();

/// <summary>
/// Returns true if this recipe has an explicit icon configured.
/// </summary>
public bool HasIcon => Icon?.IsValid == true;
```

### 4.3 BuildingDefinition

**FILE**: `Scripts/Structures/Resources/BuildingDefinition.cs`

```csharp
// Existing Visual property for 3D model
public VisualDefinition Visual { get; set; } = new();

// NEW: Separate Icon property for 2D icons
public IconDefinition Icon { get; set; } = new();
```

### 4.4 ShipDefinition

**FILE**: `Scripts/Structures/Logistics/ShipDefinition.cs`

```csharp
// Existing Visual property for 3D model
public VisualDefinition Visual { get; set; } = new();

// NEW: Separate Icon property for 2D icons
public IconDefinition Icon { get; set; } = new();
```

### 4.5 StationDefinition

**FILE**: `Scripts/Structures/Logistics/StationDefinition.cs`

```csharp
// Existing Visual property for 3D model
public VisualDefinition Visual { get; set; } = new();

// NEW: Separate Icon property for 2D icons
public IconDefinition Icon { get; set; } = new();
```

---

## Phase 5: Update All Configuration Loaders

### 5.1 ResourceConfigLoader

**FILE**: `Scripts/UtilityLibrary/DataLoading/ResourceConfigLoader.cs`

```csharp
// Update ParseResourceDefinition
private static ResourceDefinition ParseResourceDefinition(Dictionary<object, object> dict)
{
    string idName = ReadString(dict, "id_name", "");
    
    var definition = new ResourceDefinition
    {
        IdName = idName,
        ResourceTier = ReadInt(dict, "resource_tier", 0),
        ResourceType = ReadString(dict, "resource_type", ""),
        DisplayColor = ReadColor(dict, "display_color", Colors.White),
        Tags = ReadTags(dict, "tags"),
        TransportWeight = ReadFloat(dict, "transport_weight", 1.0f),
        Icon = ParseIconDefinition(dict, $"resource:{idName}"),
    };
    
    // Apply fallback if icon failed to load
    if (!definition.Icon.IsValid)
    {
        definition.Icon = IconDataLoader.CreateFallbackIconDefinition();
    }
    
    // If no tint specified, inherit from DisplayColor
    if (definition.Icon.Tint == Colors.White)
    {
        definition.Icon.Tint = definition.DisplayColor;
    }
    
    return definition;
}

// Add ParseIconDefinition helper
private static IconDefinition ParseIconDefinition(Dictionary<object, object> dict, string context)
{
    if (!dict.ContainsKey("icon"))
    {
        return new IconDefinition(); // Return empty - fallback applied by caller
    }
    
    var iconDict = dict["icon"] as Dictionary<object, object>;
    if (iconDict == null)
        return new IconDefinition();
    
    // Get base_path (required)
    string? basePath = ReadString(iconDict, "base_path", "");
    if (string.IsNullOrEmpty(basePath))
    {
        GameLogger.Warning($"Icon section missing base_path for {context}");
        return new IconDefinition();
    }
    
    // Load all sizes via IconDataLoader
    var icon = IconDataLoader.LoadIcon(basePath, context);
    
    // Parse optional properties
    if (iconDict.ContainsKey("scale"))
    {
        icon.Scale = ReadFloat(iconDict, "scale", 1.0f);
    }
    
    if (iconDict.ContainsKey("tint"))
    {
        icon.Tint = ReadColor(iconDict, "tint", Colors.White);
    }
    
    return icon;
}
```

### 5.2 RecipeConfigLoader

**FILE**: `Scripts/UtilityLibrary/DataLoading/RecipeConfigLoader.cs`

```csharp
// Update ParseRecipeDefinition
private static RecipeDefinition ParseRecipeDefinition(Dictionary<object, object> dict)
{
    string recipeId = ReadString(dict, "recipe_id", "");
    
    var definition = new RecipeDefinition
    {
        RecipeId = recipeId,
        DisplayName = ReadString(dict, "display_name", ""),
        Description = ReadString(dict, "description", ""),
        Category = ReadString(dict, "category", ""),
        WorkRequired = ReadFloat(dict, "work_required", 10.0f),
        InputResources = ParseResourceList(dict, "input_resources"),
        OutputResources = ParseResourceList(dict, "output_resources"),
        Icon = ParseIconDefinition(dict, $"recipe:{recipeId}"),
    };
    
    // Apply fallback if icon failed to load
    if (!definition.Icon.IsValid)
    {
        definition.Icon = IconDataLoader.CreateFallbackIconDefinition();
    }
    
    return definition;
}

// Add ParseIconDefinition (same pattern as ResourceConfigLoader)
```

### 5.3 BuildingConfigLoader

**FILE**: `Scripts/UtilityLibrary/DataLoading/BuildingConfigLoader.cs`

```csharp
// Add icon counters
public static int IconsLoadedCount { get; private set; }
public static int IconsFailedCount { get; private set; }

public static void ResetIconLoadingStats()
{
    IconsLoadedCount = 0;
    IconsFailedCount = 0;
}

// Update ParseBuildingDefinition
private static BuildingDefinition ParseBuildingDefinition(Dictionary<object, object> dict)
{
    string idName = ReadString(dict, "id_name", "");
    
    var definition = new BuildingDefinition
    {
        IdName = idName,
        DisplayName = ReadString(dict, "display_name", ""),
        // ... other properties ...
        Visual = ParseVisualDefinition(dict),
        Icon = ParseIconDefinition(dict, $"building:{idName}"),
    };
    
    // Apply fallback if icon failed to load
    if (!definition.Icon.IsValid)
    {
        definition.Icon = IconDataLoader.CreateFallbackIconDefinition();
    }
    
    return definition;
}

// Add ParseIconDefinition
private static IconDefinition ParseIconDefinition(Dictionary<object, object> dict, string context)
{
    if (!dict.ContainsKey("icon"))
    {
        return new IconDefinition();
    }
    
    var iconDict = dict["icon"] as Dictionary<object, object>;
    if (iconDict == null)
        return new IconDefinition();
    
    string? basePath = ReadString(iconDict, "base_path", "");
    if (string.IsNullOrEmpty(basePath))
    {
        return new IconDefinition();
    }
    
    var icon = IconDataLoader.LoadIcon(basePath, context);
    
    // Track stats
    if (icon.IsValid)
        IconsLoadedCount++;
    else
        IconsFailedCount++;
    
    icon.Scale = ReadFloat(iconDict, "scale", 1.0f);
    icon.Tint = ReadColor(iconDict, "tint", Colors.White);
    
    return icon;
}
```

### 5.4 StationConfigLoader

**FILE**: `Scripts/UtilityLibrary/DataLoading/StationConfigLoader.cs`

Same pattern as BuildingConfigLoader:
- Add icon counters
- Add `Icon` parsing in `ParseStationDefinition`
- Apply fallback if needed
- Add `ParseIconDefinition` helper

### 5.5 ShipConfigLoader

**FILE**: `Scripts/UtilityLibrary/DataLoading/ShipConfigLoader.cs`

Same pattern as BuildingConfigLoader:
- Add icon counters
- Add `Icon` parsing in `ParseShipDefinition`
- Apply fallback if needed
- Add `ParseIconDefinition` helper

---

## Phase 6: Directory Structure

```
Assets/
└── Icons/
    ├── Resources/
    │   ├── ore/
    │   │   ├── iron_ore_64.svg       # Small (64x64)
    │   │   ├── iron_ore_128.svg      # Medium (128x128)
    │   │   ├── iron_ore_512.svg      # Large (512x512)
    │   │   ├── copper_ore_64.svg
    │   │   ├── copper_ore_128.svg
    │   │   └── copper_ore_512.svg
    │   └── ... (other categories)
    ├── Recipes/
    │   ├── power/
    │   │   ├── fusion_reaction_64.svg
    │   │   ├── fusion_reaction_128.svg
    │   │   └── fusion_reaction_512.svg
    │   └── ... (other categories)
    ├── Buildings/
    │   ├── power/
    │   │   ├── wind_turbine_64.svg
    │   │   ├── wind_turbine_128.svg
    │   │   └── wind_turbine_512.svg
    │   └── ... (other categories)
    ├── Ships/
    │   ├── courier/
    │   │   ├── light_courier_64.svg
    │   │   ├── light_courier_128.svg
    │   │   └── light_courier_512.svg
    │   └── ... (other categories)
    └── Stations/
        ├── architect/
        │   ├── ramshackle_builder_64.svg
        │   ├── ramshackle_builder_128.svg
        │   └── ramshackle_builder_512.svg
        └── ... (other categories)
```

---

## Phase 7: YAML Configuration Format

### 7.1 Resource YAML

**FILE**: `Configuration/ResourceDefinition/categories/ore.yaml`

```yaml
resources:
  - id_name: iron_ore
    resource_tier: 0
    display_color: [139, 69, 19]
    tags: [ore, metallic]
    icon:
      base_path: "res://Assets/Icons/Resources/ore/iron_ore"
      # scale: 1.0  # Optional
      # tint: [255, 255, 255]  # Optional - inherits display_color if not set
  
  - id_name: copper_ore
    resource_tier: 0
    display_color: [184, 115, 51]
    tags: [ore, metallic, conductive]
    icon:
      base_path: "res://Assets/Icons/Resources/ore/copper_ore"
```

### 7.2 Recipe YAML

**FILE**: `Configuration/Recipes/Power/power_recipes.yaml`

```yaml
recipes:
  - recipe_id: "fusion_reaction"
    display_name: "Fusion Reaction"
    description: "Sustained nuclear fusion generating massive amounts of power"
    category: power
    work_required: 10.0
    icon:
      base_path: "res://Assets/Icons/Recipes/power/fusion_reaction"
    input_resources:
      - tritium: 2
    output_resources:
      - power: 100
```

### 7.3 Building YAML

**FILE**: `Configuration/Buildings/Power/Wind.yaml`

```yaml
buildings:
  - id_name: wind_turbine
    display_name: Wind Turbine Farm
    # ... other properties ...
    visual:
      model_path: "res://Models/Buildings/wind_turbine.glb"
      scale: 1.0
      rotation_offset: [0, 0, 0]
    icon:  # NEW - separate from visual
      base_path: "res://Assets/Icons/Buildings/power/wind_turbine"
```

### 7.4 Ship YAML

**FILE**: `Configuration/ships/Fast_Courier.yaml`

```yaml
ships:
  - name: Light_Courier
    dry_mass: 100
    cargo_capacity: 50
    # ... other properties ...
    visual:
      model_path: "res://Models/Ships/light_courier.glb"
      scale: 1.0
    icon:  # NEW - separate from visual
      base_path: "res://Assets/Icons/Ships/courier/light_courier"
```

### 7.5 Station YAML

**FILE**: `Configuration/stations/RamshackleBuilder.yaml`

```yaml
stations:
  - name: Ramshackle_Builder
    station_type: Orbital_Architect
    # ... other properties ...
    visual:
      model_path: "res://Models/Stations/ramshackle_builder.glb"
      scale: 1.0
    icon:  # NEW - separate from visual
      base_path: "res://Assets/Icons/Stations/architect/ramshackle_builder"
```

---

## Phase 8: Database Updates

### 8.1 ResourceDatabase

**FILE**: `Scripts/Structures/Resources/ResourceDatabase.cs`

```csharp
/// <summary>
/// Gets the icon for a resource at a specific size.
/// Always returns a valid texture (uses fallback if needed).
/// </summary>
public Texture2D GetResourceIcon(string resourceId, IconSize size = IconSize.Medium)
{
    if (TryGetResource(resourceId, out var resource) && resource != null)
    {
        var texture = resource.Icon?.GetTexture(size);
        if (texture != null)
            return texture;
    }
    
    return IconDataLoader.GetFallbackIcon(size);
}

/// <summary>
/// Gets the icon tint for a resource.
/// </summary>
public Color GetResourceIconTint(string resourceId)
{
    if (TryGetResource(resourceId, out var resource) && resource != null)
    {
        return resource.GetEffectiveIconTint();
    }
    return Colors.White;
}

/// <summary>
/// Gets the full IconDefinition for a resource.
/// </summary>
public IconDefinition? GetResourceIconDefinition(string resourceId)
{
    if (TryGetResource(resourceId, out var resource) && resource != null)
    {
        return resource.Icon;
    }
    return null;
}
```

### 8.2 RecipeDatabase

**FILE**: `Scripts/Structures/Resources/RecipeDatabase.cs`

```csharp
/// <summary>
/// Gets the icon for a recipe at a specific size.
/// Always returns a valid texture (uses fallback if needed).
/// </summary>
public Texture2D GetRecipeIcon(string recipeId, IconSize size = IconSize.Medium)
{
    if (TryGetRecipe(recipeId, out var recipe) && recipe != null)
    {
        var texture = recipe.Icon?.GetTexture(size);
        if (texture != null)
            return texture;
    }
    
    return IconDataLoader.GetFallbackIcon(size);
}

/// <summary>
/// Gets the full IconDefinition for a recipe.
/// </summary>
public IconDefinition? GetRecipeIconDefinition(string recipeId)
{
    if (TryGetRecipe(recipeId, out var recipe) && recipe != null)
    {
        return recipe.Icon;
    }
    return null;
}
```

### 8.3 Building/Ship/Station Databases

Add similar helper methods if database classes exist for these entities:
- `GetBuildingIcon(id, size)` / `GetBuildingIconDefinition(id)`
- `GetShipIcon(id, size)` / `GetShipIconDefinition(id)`
- `GetStationIcon(id, size)` / `GetStationIconDefinition(id)`

---

## Phase 9: Placeholder Icon Generation

**NEW FILE**: `Scripts/UtilityLibrary/PlaceholderIconGenerator.cs`

```csharp
using Godot;
using System.Collections.Generic;
using System.IO;
using Structures.Enums;

namespace UtilityLibrary;

/// <summary>
/// Generates placeholder SVG icons for development/testing.
/// Creates colored squares with entity initials for all three sizes.
/// </summary>
public static class PlaceholderIconGenerator
{
    private static readonly Dictionary<string, Color> CategoryColors = new()
    {
        ["ore"] = new Color(0.54f, 0.27f, 0.07f),        // Brown
        ["raw_material"] = new Color(0.44f, 0.5f, 0.56f), // Slate
        ["fuel"] = new Color(0.9f, 0.1f, 0.1f),           // Red
        ["food"] = new Color(0.2f, 0.8f, 0.2f),           // Green
        ["electronic"] = new Color(0.1f, 0.6f, 0.9f),     // Blue
        ["industrial"] = new Color(0.6f, 0.6f, 0.6f),     // Gray
        ["construction"] = new Color(0.8f, 0.5f, 0.2f),   // Orange
        ["power"] = new Color(1.0f, 0.8f, 0.0f),          // Yellow
        ["extraction"] = new Color(0.4f, 0.2f, 0.1f),     // Dark brown
        ["agriculture"] = new Color(0.3f, 0.7f, 0.3f),    // Forest green
        ["headquarters"] = new Color(0.6f, 0.3f, 0.8f),   // Purple
        ["courier"] = new Color(0.2f, 0.6f, 0.8f),        // Sky blue
        ["transport"] = new Color(0.8f, 0.4f, 0.2f),      // Rust
        ["freighter"] = new Color(0.5f, 0.5f, 0.7f),      // Steel blue
        ["shipyard"] = new Color(0.7f, 0.5f, 0.3f),       // Bronze
        ["refinery"] = new Color(0.6f, 0.6f, 0.2f),       // Olive
        ["habitat"] = new Color(0.4f, 0.7f, 0.6f),        // Teal
        ["architect"] = new Color(0.8f, 0.3f, 0.5f),      // Magenta
    };
    
    /// <summary>
    /// Generates placeholder icons for all three sizes.
    /// </summary>
    /// <param name="entityName">Name of the entity (for initial and label)</param>
    /// <param name="category">Category for color selection</param>
    /// <param name="outputBasePath">Base path without size suffix (e.g., ".../iron_ore")</param>
    public static void GeneratePlaceholders(string entityName, string category, string outputBasePath)
    {
        string initial = string.IsNullOrEmpty(entityName) ? "?" : entityName[..1].ToUpper();
        Color bgColor = CategoryColors.GetValueOrDefault(category.ToLower(), new Color(0.5f, 0.5f, 0.5f));
        
        foreach (IconSize size in Enum.GetValues<IconSize>())
        {
            GeneratePlaceholderSvg(entityName, initial, bgColor, size, outputBasePath);
        }
        
        GameLogger.Info($"Generated placeholder icons for {entityName}");
    }
    
    private static void GeneratePlaceholderSvg(
        string entityName, string initial, Color bgColor, IconSize size, string outputBasePath)
    {
        int pixels = size.GetPixels();
        string suffix = size.GetSuffix();
        string outputPath = $"{outputBasePath}{suffix}.svg";
        
        // Ensure directory exists
        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        string svg = $@"<svg width=""{pixels}"" height=""{pixels}"" viewBox=""0 0 {pixels} {pixels}"" xmlns=""http://www.w3.org/2000/svg"">
            <defs>
                <linearGradient id=""bg{suffix}"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
                    <stop offset=""0%"" style=""stop-color:{bgColor.Lightened(0.2f).ToHtml(false)};stop-opacity:1"" />
                    <stop offset=""100%"" style=""stop-color:{bgColor.ToHtml(false)};stop-opacity:1"" />
                </linearGradient>
            </defs>
            <rect width=""{pixels}"" height=""{pixels}"" fill=""url(#bg{suffix})"" rx=""{pixels/8}"" ry=""{pixels/8}""/>
            <rect x=""{pixels/32}"" y=""{pixels/32}"" width=""{pixels - pixels/16}"" height=""{pixels - pixels/16}"" 
                  fill=""none"" stroke=""white"" stroke-width=""{pixels/64}"" rx=""{pixels/10}"" ry=""{pixels/10}"" opacity=""0.3""/>
            <text x=""{pixels/2}"" y=""{pixels/2 + pixels/6}"" font-family=""Arial, sans-serif"" font-size=""{pixels/2}"" 
                  font-weight=""bold"" fill=""white"" text-anchor=""middle"" opacity=""0.9"">{initial}</text>
        </svg>";
        
        File.WriteAllText(outputPath, svg);
    }
    
    /// <summary>
    /// Generates all placeholder icons for resources from YAML configs.
    /// </summary>
    public static void GenerateResourcePlaceholders(string categoriesDirectory)
    {
        // This would iterate through resource YAML files and generate icons
        // Implementation depends on specific project needs
        GameLogger.Info("Generating resource placeholders...");
        
        // Example generation:
        // GeneratePlaceholders("iron_ore", "ore", "res://Assets/Icons/Resources/ore/iron_ore");
        // GeneratePlaceholders("copper_ore", "ore", "res://Assets/Icons/Resources/ore/copper_ore");
    }
    
    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
```

---

## Phase 10: Validation Updates

**UPDATE**: `Scripts/UtilityLibrary/DataLoading/YamlValidator.cs`

Add validation for icon sections:

```csharp
public static ValidationResult ValidateResourceDefinition(string filePath)
{
    // Existing validation...
    
    // Validate icon sections if present
    foreach (var resource in resources)
    {
        if (resource.TryGetValue("icon", out var iconObj) 
            && iconObj is Dictionary<object, object> icon)
        {
            if (icon.TryGetValue("base_path", out var basePathObj))
            {
                string? basePath = basePathObj?.ToString();
                if (!string.IsNullOrEmpty(basePath))
                {
                    // Check all three sizes exist
                    foreach (IconSize size in Enum.GetValues<IconSize>())
                    {
                        string fullPath = $"{basePath}{size.GetSuffix()}.svg";
                        string pngPath = $"{basePath}{size.GetSuffix()}.png";
                        
                        if (!FileExists(fullPath) && !FileExists(pngPath))
                        {
                            errors.Add($"Icon {size} not found: {fullPath} (or .png)");
                        }
                    }
                }
                else
                {
                    errors.Add("Icon section missing base_path");
                }
            }
        }
    }
}
```

---

## Implementation Order

1. **Phase 1**: Create IconSize enum
2. **Phase 2**: Create IconDefinition class
3. **Phase 3**: Create IconDataLoader static library with fallback generation
4. **Phase 4**: Add Icon property to ResourceDefinition and RecipeDefinition
5. **Phase 5**: Add Icon property to BuildingDefinition, ShipDefinition, StationDefinition
6. **Phase 6**: Update ResourceConfigLoader with icon parsing
7. **Phase 7**: Update RecipeConfigLoader with icon parsing
8. **Phase 8**: Update BuildingConfigLoader with icon parsing
9. **Phase 9**: Update StationConfigLoader with icon parsing
10. **Phase 10**: Update ShipConfigLoader with icon parsing
11. **Phase 11**: Update Database classes with icon helper methods
12. **Phase 12**: Create Assets/Icons directory structure
13. **Phase 13**: Create PlaceholderIconGenerator
14. **Phase 14**: Update YAML validators
15. **Phase 15**: Generate initial placeholder icons
16. **Phase 16**: Test and verify

---

## Default Behavior Summary

| Scenario | Behavior |
|----------|----------|
| No `icon:` section in YAML | Uses fallback icons for all sizes |
| `base_path` is empty/null | Uses fallback icons for all sizes |
| Individual size file missing | That size uses fallback; others load normally |
| All size files missing | Uses fallback icons for all sizes |
| `scale` not specified | 1.0 (no scaling) |
| `tint` not specified | Resources: inherit from `display_color`; Others: White |

---

## Testing Checklist

- [ ] IconSize enum has correct values (64, 128, 512) and GetSuffix() works
- [ ] IconDefinition.HasAllSizes returns true only when all three loaded
- [ ] IconDefinition.GetTexture() returns correct size
- [ ] IconDataLoader.LoadIcon() loads all three sizes from base path
- [ ] IconDataLoader generates fallback textures on first call
- [ ] ResourceConfigLoader parses icon section and applies fallback
- [ ] RecipeConfigLoader parses icon section and applies fallback
- [ ] BuildingConfigLoader parses both visual (3D) and icon (2D) sections
- [ ] All config loaders track icon loading stats
- [ ] ResourceDatabase.GetResourceIcon() returns correct size or fallback
- [ ] PlaceholderIconGenerator creates all three sizes
- [ ] YAML validation catches missing icon files
- [ ] File suffixes are correct (_64, _128, _512)

---

## Performance Considerations

1. **Eager Loading**: All 3 sizes loaded at startup per icon
2. **SVG Source**: Scalable format - single source file per size
3. **Memory**: ~100 icons × 3 sizes × 512×512×4 bytes ≈ 300MB worst case
4. **Fallback Caching**: Fallback textures created once and reused
5. **Static Library**: No instance overhead, direct function calls

---

## Backward Compatibility

- All `icon:` sections are **optional**
- Existing `visual:` sections for 3D models **unchanged**
- Missing icons show **fallback** (no crashes)
- Existing YAML without icon sections continues to work
- No breaking changes to existing APIs
