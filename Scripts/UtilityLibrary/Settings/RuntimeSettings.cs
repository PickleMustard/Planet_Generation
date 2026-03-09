using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

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
        public static RuntimeSettings Instance { get; private set; }

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

        private const string SettingsFilePath = "res://settings.cfg";

        private readonly System.Collections.Generic.Dictionary<string, IConfigurable> _configurables = new();
        private readonly ConfigFile _configFile = new();
        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, Variant>> _settingsCache = new();
        private bool _isLoaded;

        public override void _Ready()
        {
            Instance = this;
            LoadFromFile();
            // Initialize GameLogger early so logging settings are registered
            // before any other code tries to log messages
            GameLogger.Initialize();
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
                T defaultValue = (T)configurable.GetSettingDefault(key);
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

            GD.PrintErr($"Setting not found: [{category}] {key}");
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

            if (!_configurables.TryGetValue(category, out var configurable))
            {
                GameLogger.Warning($"No configurable registered for category: {category}");
                return;
            }

            ConfigEntry entry = FindConfigEntry(configurable, key);
            if (entry != null && !entry.IsValid(value))
            {
                GameLogger.Warning($"Invalid value for setting [{category}] {key}: {value}");
                return;
            }

            Variant variantValue = ObjectToVariant(value);

            if (!_settingsCache.ContainsKey(category))
            {
                _settingsCache[category] = new System.Collections.Generic.Dictionary<string, Variant>();
            }
            _settingsCache[category][key] = variantValue;

            _configFile.SetValue(category, key, variantValue);

            configurable.ApplySetting(key, value);

            EmitSignal(SignalName.SettingChanged, category, key, variantValue);

            GameLogger.Debug($"Setting changed: [{category}] {key} = {value}");
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

            object defaultValue = configurable.GetSettingDefault(key);
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
                    ResetSetting(category, entry.Key);
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
        /// Loads settings from the configuration file.
        /// If settings.cfg doesn't exist, generates it with all default values.
        /// </summary>
        public void LoadFromFile()
        {
            // Check if settings file exists
            if (!FileAccess.FileExists(SettingsFilePath))
            {
                GD.Print($"Settings file not found at {SettingsFilePath}. Generating with default values.");
                GenerateDefaultSettingsFile();
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
                _isLoaded = true;
                EmitSignal(SignalName.SettingsLoaded);
            }
        }

        /// <summary>
        /// Generates a settings.cfg file with all default values from registered IConfigurables.
        /// </summary>
        private void GenerateDefaultSettingsFile()
        {
            foreach (var kvp in _configurables)
            {
                string category = kvp.Key;
                IConfigurable configurable = kvp.Value;

                foreach (ConfigEntry entry in configurable.GetConfigEntries())
                {
                    if (entry.DefaultValue != null && entry.Key != null)
                    {
                        Variant variant = ObjectToVariant(entry.DefaultValue);
                        _configFile.SetValue(category, entry.Key, variant);
                        GameLogger.Debug($"Generated default setting: [{category}] {entry.Key} = {entry.DefaultValue}");
                    }
                }
            }

            Error error = _configFile.Save(SettingsFilePath);
            if (error == Error.Ok)
            {
                GD.Print($"Default settings file created at {SettingsFilePath}");
            }
            else
            {
                GD.PrintErr($"Failed to create default settings file: {error}");
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
        public IConfigurable GetConfigurable(string category)
        {
            _configurables.TryGetValue(category, out var configurable);
            return configurable;
        }

        private ConfigEntry FindConfigEntry(IConfigurable configurable, string key)
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
