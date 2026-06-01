# Resource Editor — Implementation Plan

## Overview

GUI-driven editor for resource YAML configuration, integrated into the debug menu. Replaces manual YAML editing for large-scale resource CRUD operations.

**Build target:** DEBUG only. Zero footprint in release builds.

---

## Current State

Resources defined in 12 category YAML files under `Configuration/ResourceDefinition/categories/`:

| File | Resource Type | Example Resources |
|------|--------------|-------------------|
| `ore.yaml` | ore | iron_ore, copper_ore, uranium_ore |
| `raw_material.yaml` | raw_material | iron, copper, silicon, titanium |
| `fuel.yaml` | fuel | uranium, hydrogen, tritium, antimatter |
| `food.yaml` | food | grain, vegetable, protein, mre |
| `electronic.yaml` | electronic | pcb, microchip, quantum_chip, motor |
| `industrial.yaml` | industrial | fabric, kevlar, ceramics, steel |
| `construction.yaml` | construction | concrete, rebar, high_tensile_wire |
| `alloys.yaml` | alloys | stradium, electrum, ignicite |
| `fluid.yaml` | fluid | water, sulfuric_acid |
| `electricity.yaml` | electricity | low_voltage_wire, high_voltage_wire, superconductive_wire |
| `labor.yaml` | labor | child, adult, scientist, robot, drone, android |

Each YAML entry has these fields (from `ResourceDefinition`):

```yaml
resources:
  - id_name: iron_ore           # string, unique across ALL categories
    resource_tier: 0             # int, 0-4
    state_of_matter: solid      # enum: solid | fluid
    max_stack_size: 100          # float, typically 25-999
    transport_weight: 1.0        # float, 0.1-10.0 (optional, default 1.0)
    tags: [ore, metallic]        # string list (optional, default empty)
    icon:                        # (optional)
      base_path: "res://Assets/Icons/Resources/ore/iron_ore"
      # scale: 1.0               # (optional, default 1.0)
      # tint: [1.0, 1.0, 1.0, 1.0]  # (optional, default white)
```

`resource_type` is **inferred from filename** — not stored in YAML, not user-editable.

---

## Architecture

### Separation from Core

All resource editor code lives under `UI/Debug/ResourceEditor/`. Every `.cs` file wrapped in `#if DEBUG`. No debug editor code in `Scripts/` directory.

**Dependency direction:**
```
UI/Debug/ResourceEditor/  →  Scripts/UtilityLibrary/DataLoading/ResourceConfigLoader (read)
UI/Debug/ResourceEditor/  →  Scripts/Structures/Resources/ResourceDefinition (read)
UI/Debug/ResourceEditor/  →  Scripts/Structures/Enums/StateOfMatter (read)
UI/Debug/ResourceEditor/  →  Scripts/UtilityLibrary/GameLogger (read)
```

Never: core → debug.

**Release build behavior:** `#if DEBUG` compiles everything out. `DebugMenu.tscn` autoload never instantiates. `.tscn` files exist on disk but are never loaded. Zero runtime footprint, zero binary size impact.

### Integration Point

`DebugMenu.InitializeDefaultModules()` (already inside `#if DEBUG`) adds:

```csharp
var resourceEditorScene = GD.Load<PackedScene>("res://UI/Debug/ResourceEditor/ResourceEditorModule.tscn");
var resourceEditor = resourceEditorScene.Instantiate<ResourceEditorModule>();
RegisterModule(resourceEditor);
```

### Data Flow

```
YAML files on disk
       ↓
ResourceConfigLoader.LoadResourceDefinitionsFromCategories()  (existing, read-only)
       ↓
List<ResourceDefinition>  (existing type)
       ↓
ResourceEditorModel.LoadFromDisk()  (NEW: converts ResourceDefinition → ResourceEditEntry)
       ↓
In-memory editable state  (ResourceCategoryData + ResourceEditEntry)
       ↓  (mutations via UpdateResourceField, AddResource, etc.)
       ↓
ResourceEditorYamlIO.WriteAllCategories()  (NEW: serializes back to YAML)
       ↓
YAML files on disk  (overwritten)
```

### Save/Revert Model

Buffered — all changes live in memory. Explicit Save writes to disk. Revert reloads from disk, discarding all unsaved changes.

- `HasUnsavedChanges` scans all `IsDirty`/`IsNew` flags
- Save button disabled when clean
- Revert button disabled when clean
- Tab name appends `" *"` when dirty
- Save runs `Validate()` first — blocks on errors, warns on warnings

---

## File Layout

```
UI/Debug/ResourceEditor/
├── ResourceEditorModel.cs          # In-memory editable state + CRUD + validation
├── ResourceEditorYamlIO.cs         # YAML serialization (write-only, read via ResourceConfigLoader)
├── ResourceEditorModule.cs         # BaseDebugModule — main two-panel editor scene
├── ResourceEditorModule.tscn       # Scene file for the module
├── ResourceCard.cs                 # Per-resource card control with inline editing
├── TagsPopup.cs                    # Tag editing popup
└── IconPickerPopup.cs             # Icon file picker popup with preview

Tests/UI/Debug/ResourceEditor/
├── ResourceEditorModelTest.cs      # Pure unit tests
├── ResourceEditorYamlIOTest.cs     # Requires Godot runtime
├── ResourceEditorModuleTest.cs     # Requires Godot runtime
├── ResourceCardTest.cs             # Requires Godot runtime
├── IconPickerPopupTest.cs          # Requires Godot runtime
└── ResourceEditorIntegrationTest.cs # Requires Godot runtime
```

---

## UI Specification

### Two-Panel Layout

```
┌─────────────────────────────────────────────────────────────┐
│ ┌──────────────┐ ┌────────────────────────────────────────┐ │
│ │  Categories  │ │  [Category Name]      [+ New Resource] │ │
│ │              │ │ ┌────────────────────────────────────┐ │ │
│ │  ● ore       │ │ │  [icon] iron_ore                   │ │ │
│ │    raw_mat…  │ │ │  Tier: [0 ▲▼]  State: [Solid ▾]   │ │ │
│ │    fuel      │ │ │  Stack: ═══●═══ 100                │ │ │
│ │    food      │ │ │  Trans: ═●══════ 1.0               │ │ │
│ │    electron… │ │ │  Tags: [ore, metallic] (2)        │ │ │
│ │    industrial│ │ │  [▲] [▼]                    [✕]     │ │ │
│ │    constru…  │ │ └────────────────────────────────────┘ │ │
│ │    alloys    │ │ ┌────────────────────────────────────┐ │ │
│ │    fluid     │ │ │  [icon] copper_ore                  │ │ │
│ │    electri…  │ │ │  ...                               │ │ │
│ │    labor     │ │ └────────────────────────────────────┘ │ │
│ │              │ │                                        │ │
│ │              │ │  (scrollable)                          │ │
│ │              │ │                                        │ │
│ │ [+ New Cat]  │ │                                        │ │
│ │ [✕ Del Cat]  │ │                                        │ │
│ └──────┤───────┘ └────────────────────────────────────────┘ │
│                     [Revert]                    [Save]       │
└─────────────────────────────────────────────────────────────┘
```

### Left Panel (Category Panel)

- **Width:** 1/5 of total horizontal space initially
- **Resizable:** User drags the `HSplitContainer` splitter edge to change width
- **Contents:**
  - `Label "Categories"` — centered, font size 14
  - `ItemList` — scrollable, each category as a selectable item, `AllowReselect = true`
  - `"+ New Category"` button — bottom of list
  - `"✕ Delete Category"` button — below New Category, disabled when nothing selected
- **Selection:** Clicking a category populates the right panel with its resources
- **Styling:** `StyleBoxFlat` dark background `Color(0.12f, 0.12f, 0.14f)`, matching existing debug modules

### Right Panel (Resource List)

- **Header:** Category name label + `"+ New Resource"` button
- **Body:** `ScrollContainer` with `VBoxContainer` of `ResourceCard` instances
- **Each `ResourceCard : PanelContainer`:**

  **Header row** (`HBoxContainer`):
  - `TextureRect` 64×64, `KeepAspectCentered` stretch, shows resource icon (fallback icon if none)
    - Click → opens `IconPickerPopup` (FileDialog filtered to `*.svg;*.png` under `res://Assets/Icons/Resources/`)
    - On file selected: extract base path (strip `_NNNxNNN.ext` suffix), update model
  - `Label` displaying `IdName`
    - Click → swaps to `LineEdit` pre-filled with current name
    - `TextSubmitted` or focus loss → calls `model.UpdateResourceField(..., "IdName", ...)`, swaps back to Label
    - Empty text → reverts to old name
    - Duplicate name → `AcceptDialog` warning, reverts

  **Fields** (`GridContainer`, 2 columns — label + control):

  | Field | Control | Range | Step | Notes |
  |-------|---------|-------|------|-------|
  | `resource_tier` | `SpinBox` | 0–4 | 1 | Small range → +/- buttons |
  | `max_stack_size` | `HSlider` + value `Label` | 25–999 | 1 | Large range → slider |
  | `transport_weight` | `HSlider` + value `Label` | 0.1–10.0 | 0.1 | Large range → slider |
  | `state_of_matter` | `OptionButton` | Solid / Fluid | — | Limited options → dropdown |

  **Tags row** (`HBoxContainer`):
  - `Button` text `"Tags (N)"` where N = `entry.Tags.Count`
  - Click → opens `TagsPopup`

  **Action row** (`HBoxContainer`, right-aligned):
  - `Button "▲"` — move up, disabled when index == 0
  - `Button "▼"` — move down, disabled when last
  - `Button "✕"` — delete, shows `ConfirmationDialog` first

- **Styling:** `StyleBoxFlat` with `BgColor = Color(0.15f, 0.15f, 0.18f)`, border 1px `Color(0.3f, 0.3f, 0.3f)`, content margins 8px

### Bottom Toolbar

- `HBoxContainer` with dark background panel
- Left side: spacer `Control` with `SizeFlagsHorizontal = ExpandFill`
- Center-right: `"Revert"` button (disabled when clean)
- Right: `"Save"` button (disabled when clean)

### TagsPopup

```
┌──────────────────────────┐
│  Current Tags:           │
│  [ore] [metallic] [×]   │  ← HFlowContainer, click [×] to remove
│                          │
│  All Tags:               │
│  ┌────────────────────┐  │
│  │ ☑ ore              │  │  ← ScrollContainer + VBoxContainer
│  │ ☑ metallic          │  │     CheckBox, checked = assigned
│  │ ☐ conductive        │  │
│  │ ☐ radioactive       │  │
│  │ ☐ valuables         │  │
│  │ ...                 │  │
│  └────────────────────┘  │
│  [________] [Add]        │  ← LineEdit + Button for new tag
│              [Close]     │
└──────────────────────────┘
```

- Size: 300×400 minimum
- Positioned near the triggering button
- Current tags: `HFlowContainer` with small `Button` per tag. Clicking removes tag.
- All tags: scrollable `VBoxContainer` of `CheckBox` items, one per unique tag across all resources. Checked if assigned to this resource.
- Add new tag: `LineEdit` + `"Add"` button. Validates: non-empty, no spaces. On add: creates tag, assigns to resource, refreshes.
- Close: `Button` or click outside popup

### IconPickerPopup

- Godot `FileDialog` in `OpenFile` mode
- File filter: `*.svg;*.png`
- Starting directory: `res://Assets/Icons/Resources/`
- Preview `TextureRect` 128×128 updates on file selection
- Confirm / Cancel buttons
- Base path extraction: regex `_(\d+)x(\d+)\.\w+$` strips size suffix
  - Example: `iron_ore_128x128.svg` → `iron_ore`
  - Fallback: strip extension only + `GameLogger.Warning`
- Emits custom signal `IconSelected(string basePath)`

### New Category Dialog

- `ConfirmationDialog` with `LineEdit` child
- Validates: non-empty, lowercase only, no spaces, no `.yaml` suffix, no duplicate category name
- On invalid: `AcceptDialog` with reason
- On valid: `_model.AddCategory(name)`, refresh list, select new category

### New Resource

- `"+ New Resource"` button at top of right panel
- Creates `ResourceEditEntry` with defaults:
  - `IdName = "new_resource"`
  - `ResourceTier = 0`
  - `StateOfMatter = StateOfMatter.Solid`
  - `MaxStackSize = 100f`
  - `TransportWeight = 1.0f`
  - `Tags = new HashSet<string>()`
  - `IconBasePath = null`
  - `IconScale = 1.0f`
  - `IconTint = Colors.White`
- Calls `_model.AddResource(...)`, refreshes resource list

---

## Data Model

### ResourceEditorModel

```csharp
#if DEBUG
using System.Collections.Generic;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary.DataLoading;

namespace UI.Debug.ResourceEditor;

public class ResourceEditorModel
{
    // --- Nested types ---
    public class ResourceCategoryData
    {
        public string CategoryName { get; set; }
        public List<ResourceEditEntry> Resources { get; set; } = new();
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    public class ResourceEditEntry
    {
        public string IdName { get; set; } = "";
        public int ResourceTier { get; set; }
        public string? ResourceType { get; set; }
        public float TransportWeight { get; set; } = 1.0f;
        public float MaxStackSize { get; set; } = 100f;
        public StateOfMatter StateOfMatter { get; set; }
        public HashSet<string> Tags { get; set; } = new();
        public string? IconBasePath { get; set; }
        public float IconScale { get; set; } = 1.0f;
        public Color IconTint { get; set; } = Colors.White;
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    // --- Fields ---
    private readonly string _categoriesDirectory;
    private Dictionary<string, ResourceCategoryData> _categories = new();

    // --- Properties ---
    public IReadOnlyDictionary<string, ResourceCategoryData> Categories => _categories;
    public bool HasUnsavedChanges { get; }  // scans all IsDirty/IsNew

    // --- Methods ---
    public void LoadFromDisk();
    public void AddCategory(string name);
    public void DeleteCategory(string name);
    public void AddResource(string categoryName, ResourceEditEntry entry);
    public void DeleteResource(string categoryName, int index);
    public void MoveResource(string categoryName, int fromIndex, int toIndex);
    public void UpdateResourceField(string categoryName, int index, string fieldName, object value);
    public void UpdateResourceTags(string categoryName, int index, HashSet<string> newTags);
    public HashSet<string> GetAllTags();
    public List<string> Validate();
}
#endif
```

### ResourceEditorYamlIO

```csharp
#if DEBUG
namespace UI.Debug.ResourceEditor;

public static class ResourceEditorYamlIO
{
    public static void WriteCategory(string filePath, ResourceEditorModel.ResourceCategoryData category);
    public static void WriteAllCategories(
        string directoryPath,
        Dictionary<string, ResourceEditorModel.ResourceCategoryData> categories
    );
}
#endif
```

**YAML output format** (matches existing files exactly):

```yaml
resources:
  - id_name: iron_ore
    resource_tier: 0
    state_of_matter: solid
    max_stack_size: 100
    tags: [ore, metallic]
    icon:
      base_path: "res://Assets/Icons/Resources/ore/iron_ore"
```

**Serialization rules:**
- `tags` omitted if empty set
- `tags` uses inline flow style `[tag1, tag2]` when present
- `icon` section omitted if `IconBasePath` is null
- `icon.scale` omitted if 1.0 (default)
- `icon.tint` omitted if `Colors.White` (default)
- `transport_weight` omitted if 1.0 (default)
- `resource_type` never written (inferred from filename)
- String values quoted
- Numeric values unquoted
- Indentation: 2 spaces

---

## Validation Rules

`ResourceEditorModel.Validate()` returns `List<string>` of error messages:

| Check | Level | Message |
|-------|-------|---------|
| Duplicate `id_name` across all categories | Error | `"Duplicate resource id_name '{name}' found in categories: {cat1}, {cat2}"` |
| Empty `id_name` on any entry | Error | `"Resource in category '{cat}' at index {i} has empty id_name"` |
| `IconBasePath` non-null, non-empty, but doesn't start with `res://` | Warning | `"Resource '{name}' has icon path not starting with res://: '{path}'"` |

---

## Error Handling

| Scenario | Handling |
|----------|----------|
| Categories directory missing on `LoadFromDisk()` | `InvalidOperationException` with message |
| Individual YAML parse failure on load | `GameLogger.Error`, skip file, continue with partial load |
| YAML write failure on save | `GameLogger.Error`, `AcceptDialog` with error, don't reload model |
| Invalid category name on New Category | `AcceptDialog` with reason, don't add |
| Empty name on inline edit | Revert to old name |
| Duplicate name on inline edit | `AcceptDialog` warning, revert |
| Invalid tag name (empty/has spaces) | `AcceptDialog`, don't add |
| Icon file can't load | `GameLogger.Warning`, show fallback icon |
| Icon base path extraction fails | Use full path without extension, `GameLogger.Warning` |
| Delete category/resource | Always show `ConfirmationDialog` first |
| Revert with unsaved changes | Show `ConfirmationDialog` "Discard all unsaved changes?" |

---

## Implementation Tickets

### Ticket 1: ResourceEditorModel + ResourceEditorYamlIO

**Priority:** High | **Dependencies:** None | **Estimate:** 2 days

#### Description
Create the in-memory editable data model and YAML serialization layer. This is the foundation all UI tickets depend on. The model reads via existing `ResourceConfigLoader` (converting `ResourceDefinition` → `ResourceEditEntry`) and writes via new `ResourceEditorYamlIO`.

#### New Files
- `UI/Debug/ResourceEditor/ResourceEditorModel.cs` — `#if DEBUG`, namespace `UI.Debug.ResourceEditor`
- `UI/Debug/ResourceEditor/ResourceEditorYamlIO.cs` — `#if DEBUG`, namespace `UI.Debug.ResourceEditor`
- `Tests/UI/Debug/ResourceEditor/ResourceEditorModelTest.cs`
- `Tests/UI/Debug/ResourceEditor/ResourceEditorYamlIOTest.cs`

#### Inputs
- `ResourceConfigLoader.LoadResourceDefinitionsFromCategories(string)` — returns `List<ResourceDefinition>`
- `ResourceDefinition` class fields (read-only reference)
- `StateOfMatter` enum
- `GameLogger` for logging
- YamlDotNet (already in project)
- `Godot.DirAccess`, `Godot.FileAccess` for directory/file operations

#### Outputs
- `ResourceEditorModel` class with full CRUD, validation, and dirty tracking
- `ResourceEditorYamlIO` static class with YAML write methods
- Test suites for both

#### Behavior

**ResourceEditorModel:**

1. Constructor stores `_categoriesDirectory` path
2. `LoadFromDisk()`:
   - Call `ResourceConfigLoader.LoadResourceDefinitionsFromCategories(_categoriesDirectory)`
   - Group results by `ResourceType` into `Dictionary<string, ResourceCategoryData>`
   - Each `ResourceDefinition` mapped to `ResourceEditEntry` (copy all fields)
   - All `IsNew = false`, `IsDirty = false` after load
   - Throws `InvalidOperationException` if directory missing
3. CRUD methods mutate `_categories`, set `IsDirty = true` on affected entries/categories
4. `HasUnsavedChanges` scans all categories/entries for `IsDirty || IsNew`
5. `Validate()` returns error list (never throws)
6. `GetAllTags()` unions all `Tags` sets across all entries

**ResourceEditorYamlIO:**

1. `WriteCategory()`:
   - Build `Dictionary<string, object>` matching YAML structure
   - Omit optional fields per serialization rules above
   - Use `YamlDotNet.Serialization.SerializerBuilder` with `UnderscoredNamingConvention`
   - Write via `Godot.FileAccess` in `Write` mode
2. `WriteAllCategories()`:
   - Iterate categories, call `WriteCategory` for each
   - Scan directory for files not in categories dictionary → delete via `DirAccess.RemoveFile`
   - Reset all `IsNew = false`, `IsDirty = false` on categories and entries

#### Error Handling
- `LoadFromDisk()`: `InvalidOperationException` for missing directory. `GameLogger.Error` + skip for individual YAML parse failures.
- `WriteCategory()`: `GameLogger.Error` + throw for write failures.
- `Validate()`: Never throws — returns error list.
- CRUD methods: standard BCL exceptions for invalid inputs.

#### Testing
- `ResourceEditorModelTest` — pure unit tests (no `[RequireGodotRuntime]`):
  - `AddCategory`, `DeleteCategory`, `AddResource`, `DeleteResource`, `MoveResource`
  - `UpdateResourceField` for each field name
  - `UpdateResourceTags`
  - `HasUnsavedChanges` true/false states
  - `Validate()` — duplicate names, empty names, bad icon paths
  - `GetAllTags()` — unions across categories
- `ResourceEditorYamlIOTest` — requires `[RequireGodotRuntime]`:
  - Write a category to `user://` temp path, read back, verify content
  - Round-trip: write → read (via ResourceConfigLoader) → verify same data
  - Omission rules: empty tags, null icon, default transport_weight

---

### Ticket 2: ResourceEditorModule Shell & Two-Panel Layout

**Priority:** High | **Dependencies:** Ticket 1 | **Estimate:** 2 days

#### Description
Create the `ResourceEditorModule` debug module extending `BaseDebugModule`. Builds two-panel layout with category list (left), resource list (right), and Save/Revert toolbar. Wires up `ResourceEditorModel`. Resource cards are placeholder labels — full card UI in Ticket 3.

#### New Files
- `UI/Debug/ResourceEditor/ResourceEditorModule.cs` — `#if DEBUG`, namespace `UI.Debug.ResourceEditor`
- `UI/Debug/ResourceEditor/ResourceEditorModule.tscn`
- `Tests/UI/Debug/ResourceEditor/ResourceEditorModuleTest.cs`

#### Modified Files
- `UI/Debug/DebugMenu.cs` — add module instantiation in `InitializeDefaultModules()`:
  ```csharp
  var resourceEditorScene = GD.Load<PackedScene>("res://UI/Debug/ResourceEditor/ResourceEditorModule.tscn");
  var resourceEditor = resourceEditorScene.Instantiate<ResourceEditorModule>();
  RegisterModule(resourceEditor);
  ```

#### Inputs
- `ResourceEditorModel` from Ticket 1
- `ResourceEditorYamlIO` from Ticket 1
- `BaseDebugModule`, `IDebugModule`, `DebugMenu` (existing)
- `GameLogger` (existing)

#### Outputs
- Functional two-panel editor with category selection, Save, Revert
- Registered in debug menu as "Resources" tab

#### Behavior

1. `ModuleName => "Resources"`
2. `_Ready()` → `BuildUI()` → `LoadModel()`
3. `BuildUI()`:
   - Root `VBoxContainer` anchored full rect
   - `HSplitContainer` with `SplitOffset` = 1/5 viewport width
   - Left: `Label "Categories"` + `ItemList` + `Button "+ New Category"` + `Button "✕ Delete Category"`
   - Right: category name `Label` + `Button "+ New Resource"` + `ScrollContainer` with `VBoxContainer`
   - Bottom: toolbar with `Button "Revert"` + `Button "Save"`
   - Styling: `StyleBoxFlat` dark backgrounds matching existing debug modules
4. `LoadModel()`:
   - `new ResourceEditorModel(_categoriesDirectory)` → `LoadFromDisk()`
   - Populate `_categoryList`
   - Select first category if any
   - Try/catch with `GameLogger.Error` + `AcceptDialog` on failure
5. `OnCategorySelected(long index)`:
   - Set `_selectedCategory`
   - Call `RefreshResourceList()`
6. `RefreshResourceList()`:
   - Clear `_resourceListContainer`
   - If no selection: placeholder label "Select a category"
   - If selected: iterate category resources, add placeholder `Label` per resource showing `entry.IdName`
7. `OnNewCategoryPressed()`:
   - Show `ConfirmationDialog` with `LineEdit` for name
   - Validate: non-empty, lowercase, no spaces, no `.yaml`, no duplicate
   - On valid: `_model.AddCategory(name)`, refresh, select new
   - On invalid: `AcceptDialog` with reason
8. `OnSavePressed()`:
   - Call `_model.Validate()`
   - If errors: `AcceptDialog` listing errors
   - If clean: `ResourceEditorYamlIO.WriteAllCategories(...)` → `_model.LoadFromDisk()` → refresh → success feedback
   - If write fails: `AcceptDialog` with error, don't reload
9. `OnRevertPressed()`:
   - If `HasUnsavedChanges`: `ConfirmationDialog` "Discard all unsaved changes?"
   - On confirmed: `_model.LoadFromDisk()` → refresh → "Reverted" feedback
10. `OnDeleteCategoryPressed()`:
    - `ConfirmationDialog` with warning
    - On confirmed: `_model.DeleteCategory()` → clear selection → refresh
11. Button states: `_saveButton.Disabled = !_model.HasUnsavedChanges`, same for revert
12. Dirty indicator: `Name = ModuleName + " *"` when dirty, `Name = ModuleName` when clean
13. `OnModuleEnabled()` → reload if model null
14. `OnModuleDisabled()` → base only

#### Error Handling
- `LoadModel()`: catch `InvalidOperationException`, show dialog, leave empty
- `OnSavePressed()`: catch IO exceptions, show dialog, don't reload
- `OnNewCategoryPressed()`: validate input before model call
- `OnDeleteCategoryPressed()`: always confirm

#### Testing
- `[RequireGodotRuntime]` tests:
  - Module instantiates without errors
  - `ModuleName` returns "Resources"
  - Category selection changes selected state
  - Save with validation errors shows dialog
  - Revert with unsaved changes shows confirmation
  - Button disabled states update correctly

---

### Ticket 3: Resource Card UI with Inline Editing + TagsPopup

**Priority:** High | **Dependencies:** Ticket 2 | **Estimate:** 3 days

#### Description
Build the full resource card UI replacing Ticket 2's placeholder labels. Each resource displayed as a card with icon, editable name, field controls, tags popup, reorder buttons, and delete. Build the `TagsPopup` for tag management.

#### New Files
- `UI/Debug/ResourceEditor/ResourceCard.cs` — `#if DEBUG`, namespace `UI.Debug.ResourceEditor`
- `UI/Debug/ResourceEditor/TagsPopup.cs` — `#if DEBUG`, namespace `UI.Debug.ResourceEditor`
- `Tests/UI/Debug/ResourceEditor/ResourceCardTest.cs`

#### Inputs
- `ResourceEditorModel` mutation methods from Ticket 1
- `ResourceEditorModule` resource list container from Ticket 2
- `StateOfMatter` enum
- `IconDataLoader` for fallback icon display
- `ResourceDatabase.GetResourceIcon()` for current icon display

#### Outputs
- Complete inline-editable resource card
- Functional tags popup with add/remove/assign
- Integration into `ResourceEditorModule.RefreshResourceList()`

#### Behavior

**ResourceCard : PanelContainer:**

1. Constructor: `(ResourceEditorModel model, string categoryName, int resourceIndex, ResourceEditEntry entry, HashSet<string> allTags)`
2. Layout built in constructor:
   - **Header**: `TextureRect` (64×64, click → `IconPickerPopup` via `FileDialog`) + `Label`/`LineEdit` toggle for name
   - **Fields**: `GridContainer` (2 cols):
     - Tier: `SpinBox` (0–4, step 1, value changed → `model.UpdateResourceField`)
     - Stack Size: `HSlider` (25–999, step 1) + value `Label`
     - Transport Weight: `HSlider` (0.1–10.0, step 0.1) + value `Label`
     - State: `OptionButton` ("Solid", "Fluid", item selected → `model.UpdateResourceField`)
   - **Tags**: `Button "Tags (N)"` → opens `TagsPopup`
   - **Actions**: `Button "▲"` (move up, disabled at 0) + `Button "▼"` (move down, disabled at last) + `Button "✕"` (delete with confirmation)
3. `Refresh(ResourceEditEntry entry, int newIndex)` — updates all controls from entry state
4. Styling: dark panel, border, margins per spec above
5. All field changes call `model.UpdateResourceField()` and set `model.IsDirty`
6. After mutations that change ordering or count, parent calls `RefreshResourceList()` to rebuild all cards

**TagsPopup : PopupPanel:**

1. Constructor: `(ResourceEditorModel model, string categoryName, int resourceIndex, ResourceEditEntry entry, HashSet<string> allTags)`
2. Layout:
   - Top: `HFlowContainer` of current tags as small `Button` (text = tag name, click → remove)
   - Middle: `ScrollContainer` + `VBoxContainer` of ALL tags as `CheckBox` (checked = assigned, toggle → add/remove)
   - Bottom: `LineEdit` (placeholder "New tag name") + `Button "Add"` + `Button "Close"`
3. Size: 300×400 minimum, positioned near trigger button
4. Validate new tag: non-empty, no spaces → `AcceptDialog` on invalid
5. On any change: call `model.UpdateResourceTags()`, refresh popup display

**Integration in ResourceEditorModule:**

- Replace placeholder `Label` with `ResourceCard` instances in `RefreshResourceList()`
- Add `"+ New Resource"` button handler: create default `ResourceEditEntry`, call `_model.AddResource()`, refresh
- After any card mutation (move, delete, field update): call `RefreshResourceList()` to rebuild

#### Error Handling
- Inline name edit: empty → revert, duplicate → `AcceptDialog` + revert
- Icon load failure: `GameLogger.Warning`, show fallback
- Invalid new tag: `AcceptDialog`, don't add
- Delete: always confirm via `ConfirmationDialog`

#### Testing
- `[RequireGodotRuntime]` tests:
  - Card instantiation with sample entry
  - SpinBox value change triggers model update
  - HSlider value change triggers model update
  - OptionButton selection triggers model update
  - Inline name edit: empty text reverts, valid text updates
  - Delete button shows confirmation dialog
  - TagsPopup shows correct checkboxes
  - Adding tag through popup works
  - Removing tag by clicking current tag button works
  - Invalid tag name shows error dialog

---

### Ticket 4: Category Delete, IconPickerPopup, Enhanced Save/Revert UX

**Priority:** Medium | **Dependencies:** Ticket 3 | **Estimate:** 2 days

#### Description
Complete remaining interactive features: category deletion with confirmation, icon file picker popup with preview, and enhanced save/revert UX with inline validation warnings and clear feedback.

#### New Files
- `UI/Debug/ResourceEditor/IconPickerPopup.cs` — `#if DEBUG`, namespace `UI.Debug.ResourceEditor`
- `Tests/UI/Debug/ResourceEditor/IconPickerPopupTest.cs`

#### Modified Files
- `UI/Debug/ResourceEditor/ResourceEditorModule.cs` — category delete button + enhanced save flow

#### Inputs
- `ResourceEditorModule` from Ticket 2
- `ResourceCard` from Ticket 3
- `ResourceEditorModel.Validate()` from Ticket 1
- `IconDataLoader.GetFallbackIcon()` (existing)

#### Outputs
- Icon picker popup with file browser + preview
- Enhanced save with validation error display + success feedback
- Category delete button wired up

#### Behavior

**Category Deletion** (already has button from Ticket 2, wire up logic):
1. On pressed: `ConfirmationDialog` "Delete category '{name}' and all its resources? Will not take effect until Save."
2. On confirmed: `_model.DeleteCategory()`, clear `_selectedCategory`, refresh category list, clear right panel
3. Disabled when no category selected

**IconPickerPopup : PopupPanel:**

1. Layout:
   - `VBoxContainer`:
     - `Label "Select Icon"`
     - `FileDialog` embedded, `OpenFile` mode, filtered `*.svg;*.png`, starting at `res://Assets/Icons/Resources/`
     - `TextureRect` preview (128×128), updates on file selection in FileDialog
     - `HBoxContainer`: `Button "Confirm"` (disabled until file selected) + `Button "Cancel"`
2. On file selected in FileDialog:
   - Load texture via `GD.Load<Texture2D>()`
   - Display in preview TextureRect
   - If load fails: show fallback icon, `GameLogger.Warning`
3. On Confirm:
   - Extract base path from full path: regex `_(\d+)x(\d+)\.\w+$` to strip size suffix
   - Fallback: strip extension only + `GameLogger.Warning` if regex doesn't match
   - Emit custom signal `IconSelected(string basePath)`
   - Close popup
4. On Cancel: close without emitting signal

**Enhanced Save UX:**

1. Validation errors: `AcceptDialog` with `RichTextLabel` listing errors in `[color=red]`
2. Validation warnings: shown in `[color=yellow]` in same dialog, allow save after acknowledge
3. Success: brief `Label "Saved successfully!"` auto-fades after 2 seconds (timer in `_Process`)
4. Failure (IOException): `AcceptDialog` with error, don't reload model

**Enhanced Revert UX:**

1. If `HasUnsavedChanges` false: button already disabled, no action
2. If true: `ConfirmationDialog` "Discard all unsaved changes?"
3. On confirmed: `_model.LoadFromDisk()`, refresh, brief "Reverted" label
4. After revert: dirty indicator cleared

#### Error Handling
- Category delete on missing category: `GameLogger.Warning`, refresh list
- Icon file load failure: show fallback, warn
- Icon base path extraction failure: fallback + warn
- Save: never silently swallow errors, always show dialog on failure
- Revert: always confirm if dirty

#### Testing
- `[RequireGodotRuntime]` tests:
  - Base path extraction: `iron_ore_128x128.svg` → `iron_ore`
  - Base path extraction: non-standard name → fallback with extension stripped
  - Popup opens and closes
  - Category delete removes from model
  - Save with duplicate names shows error dialog
  - Save with valid data shows success feedback
  - Revert confirmation dialog appears when dirty
  - Dirty indicator in tab name updates

---

### Ticket 5: Integration Testing & Edge Case Hardening

**Priority:** Medium | **Dependencies:** Tickets 1–4 | **Estimate:** 1 day

#### Description
Comprehensive integration tests covering full save/load/edit/revert cycle, YAML serialization edge cases, and DebugMenu registration. Fix bugs discovered during testing.

#### New Files
- `Tests/UI/Debug/ResourceEditor/ResourceEditorIntegrationTest.cs`

#### Modified Files
- Bug fixes to Tickets 1–4 files as needed

#### Inputs
- All files from Tickets 1–4
- `ResourceDatabase` singleton (for verifying saved YAML can be re-loaded)
- `ResourceConfigLoader.LoadResourceDefinitionsFromCategories()` (for round-trip verification)

#### Outputs
- Comprehensive integration test suite
- Bug fixes discovered during testing

#### Test Scenarios

1. **Full round-trip:**
   - Write 2 category YAML files with known content via `ResourceEditorYamlIO`
   - `LoadFromDisk()` → verify categories and resources loaded
   - Mutate: add resource, edit field, delete resource, reorder
   - `Validate()` → empty
   - `WriteAllCategories()`
   - Fresh `ResourceEditorModel` → `LoadFromDisk()` → verify mutations persisted
   - Also: `ResourceConfigLoader.LoadResourceDefinitionsFromCategories()` parses written files without errors

2. **Revert test:**
   - Load → mutate → `LoadFromDisk()` (revert)
   - Verify model matches original state

3. **Validation test:**
   - Add two resources with same `IdName` in different categories
   - `Validate()` → returns duplicate error
   - Fix one name → `Validate()` → empty

4. **Empty category test:**
   - Create category with no resources → save → reload → verify persists as `resources: []`

5. **Special characters in tags:**
   - Tags with underscores and hyphens → survive round-trip
   - Tag with spaces → rejected by TagsPopup

6. **Icon path edge cases:**
   - Null icon → YAML omits `icon` section
   - Icon with path → YAML includes `icon.base_path`
   - Icon with path + scale + tint → YAML includes all fields

7. **DebugMenu registration:**
   - Module appears in `DebugMenu._modules`
   - Toggling debug menu shows/hides module
   - Tab name is "Resources"

8. **Concurrent modification safety:**
   - Edit resource → save → edit same resource again → save → verify second edit persisted

9. **Delete category cleanup:**
   - Add category → save → verify file on disk
   - Delete category → save → verify file removed from disk

10. **Large dataset:**
    - Create category with 50 resources → verify UI remains responsive
    - Save/reload → verify all 50 persisted

#### Testing Notes
- All tests require `[RequireGodotRuntime]` for `Godot.FileAccess`
- Use `user://` temp directories, unique per test run
- Clean up temp files in `[After]` method
- Tag discovered bugs: `// BUG: [description]`
- Fix trivial bugs inline, note complex bugs for separate fix

---

## Execution Order

```
Ticket 1 (Model + YAML IO)     ← no dependencies, start here
     ↓
Ticket 2 (Module Shell)         ← depends on Ticket 1
     ↓
Ticket 3 (ResourceCard + Tags) ← depends on Ticket 2
     ↓
Ticket 4 (IconPicker + UX)     ← depends on Ticket 3
     ↓
Ticket 5 (Integration Tests)   ← depends on Tickets 1–4
```

Strict sequential dependency chain. Each ticket testable independently.

---

## Open Questions / Future Enhancements

- **Undo/redo stack:** Not in current scope. Could be added as Ticket 6 later.
- **Biome affinity / elevation range editing:** Current `ResourceDefinition` doesn't have these fields in the editor model. Add when `ResourceDefinition` gains these properties.
- **Resource group editor:** `resource_groups.yaml` editing. Separate feature.
- **Recipe editor:** `RecipeDatabase` editing. Separate feature.
- **Import/export:** Bulk resource import from JSON/CSV. Not in scope.
