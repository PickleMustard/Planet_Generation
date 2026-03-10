using System.Collections.Generic;

namespace UtilityLibrary
{
    /// <summary>
    /// Interface for objects that can be configured through the settings system.
    /// Implementations define their configuration schema and handle setting changes.
    /// </summary>
    public interface IConfigurable
    {
        /// <summary>
        /// Gets the category name for grouping this configurable's settings in the UI.
        /// </summary>
        string SettingsCategory { get; }

        /// <summary>
        /// Applies a setting value to this configurable object.
        /// </summary>
        /// <param name="key">The setting key to apply.</param>
        /// <param name="value">The value to set.</param>
        void ApplySetting(string key, object value);

        /// <summary>
        /// Gets the default value for a specific setting key.
        /// </summary>
        /// <param name="key">The setting key to look up.</param>
        /// <returns>The default value for the specified key, or null if not found.</returns>
        object? GetSettingDefault(string key);

        /// <summary>
        /// Gets all configuration entries for this configurable object.
        /// </summary>
        /// <returns>An enumerable collection of configuration entries.</returns>
        IEnumerable<ConfigEntry> GetConfigEntries();
    }
}
