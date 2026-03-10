# Logging Level Fix Summary

## Problem
The project setting for log level was not affecting the actual level used during runtime. The setting was configured to "INFO" but the log was outputting at "DEBUG" level.

## Root Cause
The `RuntimeSettings.RegisterConfigurable()` method was loading settings from the config file during initialization but **never calling `ApplySetting()`** on the configurables. This meant:

1. `settings.cfg` was loaded into `RuntimeSettings._configFile`
2. Configurables registered with RuntimeSettings
3. But their cached fields never received the configured values
4. Configurables continued using their default values

**Impact:**
- GameLogger's `logMode` stayed at default `Mode.DEBUG`
- TaskTimer and ThreadPooler had to manually query RuntimeSettings in their initialization as a workaround

## Solution

### 1. Updated RuntimeSettings.cs
Added `ApplyLoadedSettings()` method to automatically apply configured settings when a configurable registers:

**File:** `Scripts/UtilityLibrary/Settings/RuntimeSettings.cs`

**Changes:**
- Added `ApplyLoadedSettings(IConfigurable, string)` method (lines 136-177)
- Added `ConvertVariantToObject(Variant)` helper method (lines 179-206)
- Modified `RegisterConfigurable()` to call `ApplyLoadedSettings()` after registration (line 85)

**Flow:**
1. ConfigFile loads settings from `settings.cfg` during `RuntimeSettings._Ready()`
2. When `GameLogger.Initialize()` calls `RegisterConfigurable(Provider)`:
   - Provider is stored in `_configurables` dictionary
   - `ApplyLoadedSettings()` checks config file for "logging" section
   - For each key (level, log_to_file, log_to_console):
     - Retrieves value from config file
     - Calls `Provider.ApplySetting(key, value)`
     - Updates `_settingsCache`
   - Provider's `ApplySetting()` updates GameLogger's `logMode` field
3. GameLogger's `LogMessage()` now uses the correct cached `logMode` value

### 2. Updated TaskTimer.cs
Fixed TaskTimer to cache settings in `ApplySetting()` and removed manual RuntimeSettings queries:

**File:** `Scripts/UtilityLibrary/TaskTimer.cs`

**Changes:**
- Modified `ApplySetting()` to directly update cached fields (`_progressPanelVisible`, `_collapseDelay`) before emitting signals (lines 70-81)
- Removed manual `GetSetting()` calls from `_Ready()` method (removed lines 109-110)

**Why:**
- Signals are emitted during ApplySetting but signal handlers aren't connected until after registration
- Direct field updates ensure settings are applied even during registration
- Signals still emitted for runtime updates (handlers update fields again, harmless redundancy)

### 3. Updated ThreadPooler.cs
Added cached fields and updated to use them instead of querying RuntimeSettings:

**File:** `Scripts/UtilityLibrary/TaskSystem/ThreadPooler.cs`

**Changes:**
- Added cached fields: `_allocationPercentage` (float, default 0.75f) and `_manualThreadCount` (int, default 0) (lines 35-36)
- Updated `ApplySetting()` to cache setting values (lines 68-80)
- Modified `Initialize()` to use cached fields instead of querying RuntimeSettings (lines 116-130)

**Why:**
- Ensures settings are properly cached during registration
- Eliminates unnecessary RuntimeSettings queries during initialization
- Maintains proper separation of concerns

### 4. Updated settings.cfg
Changed logging level from DEBUG to INFO for testing:

**File:** `settings.cfg` (line 17)

**Change:** `level="INFO"` (was "DEBUG")

## Benefits

1. **Fixes the immediate bug:** GameLogger now respects the configured log level
2. **Proper architecture:** RuntimeSettings owns settings configuration, configurables cache values locally
3. **Consistent behavior:** All configurables follow the same pattern for receiving settings
4. **No runtime queries:** Configurables use cached values instead of querying RuntimeSettings on every operation
5. **Cleaner code:** Removed workaround manual GetSetting() calls from configurables

## Testing

To verify the fix works correctly:

1. **Test INFO level:**
   - Set `level="INFO"` in settings.cfg
   - Run project
   - Verify DEBUG messages are NOT output
   - Verify INFO, WARNING, ERROR, CRITICAL messages ARE output

2. **Test WARNING level:**
   - Set `level="WARNING"` in settings.cfg
   - Run project
   - Verify only WARNING, ERROR, CRITICAL messages are output

3. **Test runtime changes (if applicable):**
   - Use settings panel or console command to change level while running
   - Verify logging behavior changes immediately
   - This works because `SetSetting()` still calls `ApplySetting()`

## Files Modified

1. `Scripts/UtilityLibrary/Settings/RuntimeSettings.cs` - Added ApplyLoadedSettings mechanism
2. `Scripts/UtilityLibrary/TaskTimer.cs` - Fixed ApplySetting to cache values
3. `Scripts/UtilityLibrary/TaskSystem/ThreadPooler.cs` - Added cached fields and fixed ApplySetting
4. `settings.cfg` - Changed default logging level to INFO for testing

## Architectural Principle

**Configurables cache settings locally and never query RuntimeSettings during runtime.**

This ensures:
- Fast performance (no dictionary lookups or config file reads)
- Clear separation of concerns (RuntimeSettings manages config, configurables manage state)
- Predictable behavior (settings applied once during registration, cached for use)
- Runtime updates work (SetSetting calls ApplySetting to update cached values)
