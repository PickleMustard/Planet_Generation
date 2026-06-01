using System.Collections.Generic;

namespace UtilityLibrary
{
    /// <summary>
    /// Interface shared by RuntimeSettings and RuntimeSettingsEditorBridge.
    /// Provides unified access to settings in both runtime and editor contexts.
    /// </summary>
    public interface ISettingsProvider
    {
        /// <summary>
        /// Gets a setting value from the specified category and key.
        /// </summary>
        /// <typeparam name="T">The type of the setting value.</typeparam>
        /// <param name="category">The settings category.</param>
        /// <param name="key">The setting key.</param>
        /// <param name="defaultValue">The default value if not found.</param>
        /// <returns>The setting value, or default if not found.</returns>
        T? GetSetting<T>(string category, string key, T? defaultValue = default);

        /// <summary>
        /// Sets a setting value for the specified category and key.
        /// </summary>
        /// <param name="category">The settings category.</param>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The value to set.</param>
        void SetSetting(string category, string key, object value);

        /// <summary>
        /// Gets all configuration entries from all registered configurables.
        /// </summary>
        /// <returns>An enumerable of all configuration entries.</returns>
        IEnumerable<ConfigEntry> GetAllEntries();

        /// <summary>
        /// Gets a configurable object by its category.
        /// </summary>
        /// <param name="category">The settings category.</param>
        /// <returns>The configurable object, or null if not found.</returns>
        IConfigurable? GetConfigurable(string category);

        /// <summary>
        /// Checks if a setting exists.
        /// </summary>
        /// <param name="category">The settings category.</param>
        /// <param name="key">The setting key.</param>
        /// <returns>True if the setting exists.</returns>
        bool HasSetting(string category, string key);

        /// <summary>
        /// Registers a configurable object with the settings system.
        /// </summary>
        /// <param name="configurable">The configurable object to register.</param>
        void RegisterConfigurable(IConfigurable configurable);
    }
}
