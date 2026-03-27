using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Godot;

namespace UtilityLibrary
{
    /// <summary>
    /// Central singleton manager for all runtime settings.
    /// Handles configuration persistence, validation, and change notifications.
    /// </summary>
    public partial class RuntimeSettings : Node
    {
        /// <summary>
        /// Gets the singleton instance of RuntimeSettings.
        /// </summary>
        public static RuntimeSettings? Instance { get; private set; }

        /// <summary>
        /// Emitted when a setting value changes.
        /// </summary>
        [Signal]
        public delegate void SettingChangedEventHandler(string category, string key, Variant value);

        /// <summary>
        /// Emitted when settings are loaded from file.
        /// </summary>
        [Signal]
        public delegate void SettingsLoadedEventHandler();

        private readonly System.Collections.Generic.Dictionary<string, IConfigurable> _configurables = new();
        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, Variant>> _settingsCache = new();
        private readonly ConfigFile _configFile = new();
        private const string SettingsFilePath = "res://settings.cfg";
        private bool _isLoaded;

        public override void _Ready()
        {
            Instance = this;
            LoadFromFile();
            // Initialize GameLogger early so logging settings are registered
            // before any other code tries to log messages
            GameLogger.Initialize();
        }

        public override void _ExitTree()
        {
            // Ensure all cached settings are saved to file on exit
            SaveToFile();
            base._ExitTree();
        }

        /// <summary>
        /// Registers a configurable object with the settings system.
        /// </summary>
        /// <param name="configurable">The configurable object to register.</param>
        public void RegisterConfigurable(IConfigurable configurable)
        {
            if (configurable == null)
            {
                GD.PrintErr("Attempted to register null configurable.");
                return;
            }

            string category = configurable.SettingsCategory;
            if (string.IsNullOrEmpty(category))
            {
                GD.PrintErr("Configurable has null or empty category.");
                return;
            }

            if (_configurables.ContainsKey(category))
            {
                GD.PrintErr($"Category '{category}' is already registered. Overwriting.");
            }

            _configurables[category] = configurable;

            if (!_settingsCache.ContainsKey(category))
            {
                _settingsCache[category] = new System.Collections.Generic.Dictionary<string, Variant>();
            }

            GameLogger.Debug($"Registered configurable for category: {category}");

            // Apply loaded settings from the config file to this configurable
            ApplyLoadedSettings(configurable, category);

            // Write default values for this configurable if the file already exists
            // (handles late-registering configurables like ThreadPooler, TaskTimer)
            WriteDefaultsForConfigurable(configurable);
        }

        /// <summary>
        /// Applies loaded settings from the config file to a configurable during registration.
        /// This ensures configurables receive their configured values from the settings file
        /// at initialization time, not just when settings are changed at runtime.
        /// </summary>
        /// <param name="configurable">The configurable to apply settings to.</param>
        /// <param name="category">The settings category.</param>
        private void ApplyLoadedSettings(IConfigurable configurable, string category)
        {
            // Check if the config file has a section for this category
            if (!_configFile.HasSection(category))
            {
                GameLogger.Debug($"No settings section found for category: {category}");
                return;
            }

            // Get all keys in the category section
            string[] keys = _configFile.GetSectionKeys(category);

            foreach (string key in keys)
            {
                Variant value = _configFile.GetValue(category, key);

                // Update the settings cache
                if (!_settingsCache.ContainsKey(category))
                {
                    _settingsCache[category] = new System.Collections.Generic.Dictionary<string, Variant>();
                }
                _settingsCache[category][key] = value;

                // Apply the setting to the configurable (this updates their cached fields)
                try
                {
                    // Convert Variant to object for ApplySetting
                    object? convertedValue = ConvertVariantToObject(value);
                    if (convertedValue != null)
                    {
                        configurable.ApplySetting(key, convertedValue);
                        GameLogger.Debug($"Applied loaded setting to [{category}]: {key} = {convertedValue}");
                    }
                }
                catch (Exception ex)
                {
                    GameLogger.Warning($"Failed to apply loaded setting [{category}].{key}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Converts a Godot Variant to a C# object for passing to ApplySetting.
        /// </summary>
        /// <param name="variant">The variant to convert.</param>
        /// <returns>The converted object, or null if conversion fails.</returns>
        private object? ConvertVariantToObject(Variant variant)
        {
            Variant.Type type = variant.VariantType;

            try
            {
                switch (type)
                {
                    case Variant.Type.Bool:
                        return variant.AsBool();
                    case Variant.Type.Int:
                        return variant.AsInt32();
                    case Variant.Type.Float:
                        return variant.AsSingle();
                    case Variant.Type.String:
                        return variant.AsString();
                    case Variant.Type.Vector2:
                        return variant.AsVector2();
                    case Variant.Type.Vector3:
                        return variant.AsVector3();
                    case Variant.Type.Color:
                        return variant.AsColor();
                    default:
                        // Try string conversion as fallback
                        return variant.AsString();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts a C# object to a Godot Variant by checking the runtime type.
        /// This is necessary because Variant.From(object) doesn't work - the type
        /// must be known at compile time for the generic constraint.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A Variant containing the value.</returns>
        private static Variant ObjectToVariant(object value)
        {
            if (value == null)
            {
                return default;
            }

            Type type = value.GetType();

            if (type == typeof(bool))
                return Variant.From((bool)value);
            if (type == typeof(int))
                return Variant.From((int)value);
            if (type == typeof(float))
                return Variant.From((float)value);
            if (type == typeof(double))
                return Variant.From((double)value);
            if (type == typeof(string))
                return Variant.From((string)value);
            if (type == typeof(Vector2))
                return Variant.From((Vector2)value);
            if (type == typeof(Vector3))
                return Variant.From((Vector3)value);
            if (type == typeof(Color))
                return Variant.From((Color)value);
            if (type == typeof(long))
                return Variant.From((long)value);
            if (type == typeof(byte))
                return Variant.From((byte)value);
            if (type == typeof(short))
                return Variant.From((short)value);
            if (type == typeof(ushort))
                return Variant.From((ushort)value);
            if (type == typeof(uint))
                return Variant.From((uint)value);
            if (type == typeof(ulong))
                return Variant.From((ulong)value);

            // Fallback: try to convert to string representation
            GameLogger.Warning($"ObjectToVariant: Unsupported type {type.Name}, converting to string");
            return Variant.From(value.ToString());
        }

        /// <summary>
        /// Gets a setting value, checking cache, config file, then defaults.
        /// </summary>
        /// <typeparam name="T">The expected type of the setting value.</typeparam>
        /// <param name="category">The settings category.</param>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value, or default if not found.</returns>
        [return: MaybeNull]
        public T GetSetting<[MustBeVariant] T>(string category, string key)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key))
            {
                return default;
            }

            if (_settingsCache.TryGetValue(category, out var categoryCache) &&
                categoryCache.TryGetValue(key, out var cachedValue))
            {
                return ConvertVariant<T>(cachedValue);
            }

            if (_configFile.HasSectionKey(category, key))
            {
                Variant fileValue = _configFile.GetValue(category, key);
                if (!_settingsCache.ContainsKey(category))
                {
                    _settingsCache[category] = new System.Collections.Generic.Dictionary<string, Variant>();
                }
                _settingsCache[category][key] = fileValue;
                return ConvertVariant<T>(fileValue);
            }

            if (_configurables.TryGetValue(category, out var configurable))
            {
                T defaultValue = (T)configurable.GetSettingDefault(key)!;
                if (defaultValue != null)
                {
                    Variant defaultVariant = ObjectToVariant(defaultValue);
                    if (!_settingsCache.ContainsKey(category))
                    {
                        _settingsCache[category] = new System.Collections.Generic.Dictionary<string, Variant>();
                    }
                    _settingsCache[category][key] = defaultVariant;
                    return ConvertVariant<T>(defaultVariant);
                }
            }

            // Only log error if there's no configurable AND the value wasn't in the file
            GameLogger.Warning($"Setting not found: [{category}] {key} (no configurable registered and no value in file)");
            return default;
        }

        /// <summary>
        /// Sets a setting value after validation.
        /// </summary>
        /// <param name="category">The settings category.</param>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The value to set.</param>
        public void SetSetting(string category, string key, object value)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key))
            {
                GameLogger.Warning("SetSetting called with null or empty category/key.");
                return;
            }

            if (value == null)
            {
                GameLogger.Warning($"SetSetting called with null value for [{category}] {key}.");
                return;
            }

            // Try to get a configurable for validation and applying
            _configurables.TryGetValue(category, out var configurable);

            // Validate the value if a configurable exists
            if (configurable != null)
            {
                ConfigEntry? entry = FindConfigEntry(configurable, key);
                if (entry != null && !entry.IsValid(value))
                {
                    GameLogger.Warning($"Invalid value for setting [{category}] {key}: {value}");
                    return;
                }
            }
            else
            {
                GameLogger.Debug($"No configurable registered for category: {category}, writing value without validation");
            }

            Variant variantValue = ObjectToVariant(value);

            if (!_settingsCache.ContainsKey(category))
            {
                _settingsCache[category] = new System.Collections.Generic.Dictionary<string, Variant>();
            }
            _settingsCache[category][key] = variantValue;

            _configFile.SetValue(category, key, variantValue);

            // Apply the setting if a configurable is registered
            if (configurable != null)
            {
                configurable.ApplySetting(key, value);
            }

            EmitSignal(SignalName.SettingChanged, category, key, variantValue);

            GameLogger.Debug($"Setting changed: [{category}] {key} = {value}");

            // Persist the change immediately to disk
            SaveToFile();
        }

        /// <summary>
        /// Resets a setting to its default value.
        /// </summary>
        /// <param name="category">The settings category.</param>
        /// <param name="key">The setting key.</param>
        public void ResetSetting(string category, string key)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key))
            {
                GD.PrintErr("ResetSetting called with null or empty category/key.");
                return;
            }

            if (!_configurables.TryGetValue(category, out var configurable))
            {
                GD.PrintErr($"No configurable registered for category: {category}");
                return;
            }

            object? defaultValue = configurable.GetSettingDefault(key);
            if (defaultValue != null)
            {
                SetSetting(category, key, defaultValue);
                GameLogger.Debug($"Reset setting [{category}] {key} to default: {defaultValue}");
            }
            else
            {
                if (_settingsCache.TryGetValue(category, out var categoryCache))
                {
                    categoryCache.Remove(key);
                }
                _configFile.EraseSectionKey(category, key);
                GameLogger.Debug($"Cleared setting [{category}] {key}");
            }
        }

        /// <summary>
        /// Resets all settings to their default values.
        /// </summary>
        public void ResetAllSettings()
        {
            foreach (var kvp in _configurables)
            {
                string category = kvp.Key;
                IConfigurable configurable = kvp.Value;

                foreach (ConfigEntry entry in configurable.GetConfigEntries())
                {
                    ResetSetting(category, entry.Key!);
                }
            }

            GameLogger.Info("All settings reset to defaults.");
        }

        /// <summary>
        /// Saves current settings to the configuration file.
        /// </summary>
        public void SaveToFile()
        {
            Error error = _configFile.Save(SettingsFilePath);
            if (error == Error.Ok)
            {
                GameLogger.Info($"Settings saved to {SettingsFilePath}");
            }
            else
            {
                GameLogger.Error($"Failed to save settings: {error}");
            }
        }

        /// <summary>
        /// Creates a new settings file with default values from all registered configurables.
        /// Called when no settings file exists.
        /// </summary>
        private void CreateDefaultSettingsFile()
        {
            // Iterate all registered configurables and save their defaults
            foreach (var kvp in _configurables)
            {
                string category = kvp.Key;
                IConfigurable configurable = kvp.Value;

                foreach (ConfigEntry entry in configurable.GetConfigEntries())
                {
                    if (entry.Key != null && entry.DefaultValue != null)
                    {
                        Variant defaultVariant = ObjectToVariant(entry.DefaultValue);
                        _configFile.SetValue(category, entry.Key, defaultVariant);

                        if (!_settingsCache.ContainsKey(category))
                        {
                            _settingsCache[category] = new System.Collections.Generic.Dictionary<string, Variant>();
                        }
                        _settingsCache[category][entry.Key] = defaultVariant;

                        GameLogger.Debug($"Created default setting: [{category}] {entry.Key} = {entry.DefaultValue}");
                    }
                }
            }

            // Save the newly created file
            SaveToFile();
            GD.Print($"Created default settings file at {SettingsFilePath}");
        }

        /// <summary>
        /// Writes default values for a newly registered configurable to the settings file.
        /// Called when a configurable registers after initial file creation.
        /// </summary>
        /// <param name="configurable">The configurable to write defaults for.</param>
        private void WriteDefaultsForConfigurable(IConfigurable configurable)
        {
            string category = configurable.SettingsCategory;

            // Only write if the file already exists (not first run case)
            if (!FileAccess.FileExists(SettingsFilePath))
            {
                return;
            }

            foreach (ConfigEntry entry in configurable.GetConfigEntries())
            {
                if (entry.Key != null && entry.DefaultValue != null)
                {
                    // Only write if this key doesn't already exist in the file
                    if (!_configFile.HasSectionKey(category, entry.Key))
                    {
                        Variant defaultVariant = ObjectToVariant(entry.DefaultValue);
                        _configFile.SetValue(category, entry.Key, defaultVariant);

                        if (!_settingsCache.ContainsKey(category))
                        {
                            _settingsCache[category] = new System.Collections.Generic.Dictionary<string, Variant>();
                        }
                        _settingsCache[category][entry.Key] = defaultVariant;

                        GameLogger.Debug($"Wrote default for unregistered category: [{category}] {entry.Key} = {entry.DefaultValue}");
                    }
                }
            }

            // Save after writing defaults for this configurable
            SaveToFile();
        }

        /// <summary>
        /// Loads settings from the configuration file (user://settings.cfg).
        /// If the file doesn't exist, creates a new file with default values from configurables.
        /// </summary>
        public void LoadFromFile()
        {
            // Check if settings file exists
            if (!FileAccess.FileExists(SettingsFilePath))
            {
                // No file exists - create one with default values from all registered configurables
                GD.Print($"No settings file found at {SettingsFilePath}, creating with defaults.");
                CreateDefaultSettingsFile();
                _isLoaded = true;
                EmitSignal(SignalName.SettingsLoaded);
                return;
            }

            Error error = _configFile.Load(SettingsFilePath);
            if (error == Error.Ok)
            {
                _isLoaded = true;
                GD.Print($"Settings loaded from {SettingsFilePath}");
                EmitSignal(SignalName.SettingsLoaded);
            }
            else
            {
                GD.PrintErr($"Failed to load settings: {error}");
                // Try to create a new file with defaults as fallback
                CreateDefaultSettingsFile();
                _isLoaded = true;
                EmitSignal(SignalName.SettingsLoaded);
            }
        }

        /// <summary>
        /// Gets all configuration entries from all registered configurables.
        /// </summary>
        /// <returns>An enumerable of all configuration entries.</returns>
        public IEnumerable<ConfigEntry> GetAllEntries()
        {
            foreach (var kvp in _configurables)
            {
                foreach (ConfigEntry entry in kvp.Value.GetConfigEntries())
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// Checks if settings have been loaded.
        /// </summary>
        /// <returns>True if settings are loaded.</returns>
        public bool IsLoaded()
        {
            return _isLoaded;
        }

        /// <summary>
        /// Checks if a setting exists.
        /// </summary>
        /// <param name="category">The settings category.</param>
        /// <param name="key">The setting key.</param>
        /// <returns>True if the setting exists.</returns>
        public bool HasSetting(string category, string key)
        {
            if (_settingsCache.TryGetValue(category, out var categoryCache) &&
                categoryCache.ContainsKey(key))
            {
                return true;
            }

            if (_configFile.HasSectionKey(category, key))
            {
                return true;
            }

            if (_configurables.TryGetValue(category, out var configurable))
            {
                foreach (ConfigEntry entry in configurable.GetConfigEntries())
                {
                    if (entry.Key == key)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a configurable by category.
        /// </summary>
        /// <param name="category">The settings category.</param>
        /// <returns>The configurable, or null if not found.</returns>
        public IConfigurable? GetConfigurable(string category)
        {
            _configurables.TryGetValue(category, out var configurable);
            return configurable;
        }

        private ConfigEntry? FindConfigEntry(IConfigurable configurable, string key)
        {
            foreach (ConfigEntry entry in configurable.GetConfigEntries())
            {
                if (entry.Key == key)
                {
                    return entry;
                }
            }
            return null;
        }

        [return: MaybeNull]
        private T ConvertVariant<[MustBeVariant] T>(Variant variant)
        {
            try
            {
                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)variant.AsBool();
                }
                if (typeof(T) == typeof(int))
                {
                    return (T)(object)variant.AsInt32();
                }
                if (typeof(T) == typeof(float))
                {
                    return (T)(object)variant.AsSingle();
                }
                if (typeof(T) == typeof(double))
                {
                    return (T)(object)variant.AsDouble();
                }
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)variant.AsString();
                }

                return variant.As<T>();
            }
            catch (Exception ex)
            {
                GameLogger.Warning($"Failed to convert variant to {typeof(T)}: {ex.Message}");
                return default;
            }
        }
    }
}
