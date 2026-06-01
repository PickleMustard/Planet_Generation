using System;

namespace UtilityLibrary
{
    /// <summary>
    /// Represents a single configuration entry with metadata for validation and UI display.
    /// Supports numeric ranges, enum options, and descriptive information.
    /// </summary>
    public class ConfigEntry
    {
        /// <summary>
        /// Gets the unique key identifier for this configuration entry.
        /// </summary>
        public string? Key { get; init; }

        /// <summary>
        /// Gets the expected type of the configuration value.
        /// </summary>
        public Type? ValueType { get; init; }

        /// <summary>
        /// Defines the category of this configuration entry.
        /// </summary>
        public string? SettingsCategory { get; init; }

        /// <summary>
        /// Gets the default value for this configuration entry.
        /// Used for numeric types and general defaults.
        /// </summary>
        public object? DefaultValue { get; init; }

        /// <summary>
        /// Gets the minimum allowed value for numeric types.
        /// </summary>
        public object? MinValue { get; init; }

        /// <summary>
        /// Gets the maximum allowed value for numeric types.
        /// </summary>
        public object? MaxValue { get; init; }

        /// <summary>
        /// Gets the array of valid string options for enum-like configurations.
        /// </summary>
        public string[]? ValidOptions { get; init; }

        /// <summary>
        /// Gets the human-readable description of this configuration entry.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Gets whether changing this setting requires a restart to take effect.
        /// </summary>
        public bool RequiresRestart { get; init; }

        /// <summary>
        /// Gets the property hint string for ProjectSettings (e.g., "0,100,1" for range).
        /// Used to provide editor UI hints like enum dropdowns or slider ranges.
        /// </summary>
        public string? PropertyHintString { get; init; }

        /// <summary>
        /// Gets whether this setting should be hidden from the basic Project Settings view.
        /// When true, user must enable "Advanced" toggle to see this setting.
        /// </summary>
        public bool ShowInAdvanced { get; init; }

        /// <summary>
        /// Gets whether this setting is internal (hidden from Project Settings dialog).
        /// Internal settings can still be accessed programmatically but don't appear in the UI.
        /// </summary>
        public bool IsInternal { get; init; }

        /// <summary>
        /// Validates that a given value is within acceptable bounds for this entry.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <returns>True if the value is valid for this configuration entry.</returns>
        public bool IsValid(object value)
        {
            if (value == null)
            {
                return false;
            }

            if (ValueType == null || !ValueType.IsAssignableFrom(value.GetType()))
            {
                return false;
            }

            if (ValidOptions != null && ValidOptions.Length > 0)
            {
                return Array.Exists(ValidOptions, opt => opt == value.ToString());
            }

            if (MinValue != null && MaxValue != null)
            {
                if (value is IComparable comparable)
                {
                    var minCompare = comparable.CompareTo(MinValue);
                    var maxCompare = comparable.CompareTo(MaxValue);
                    return minCompare >= 0 && maxCompare <= 0;
                }
            }

            return true;
        }
    }
}
