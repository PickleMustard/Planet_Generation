using System;

namespace UtilityLibrary
{
    /// <summary>
    /// Represents a single configuration entry with metadata for validation.
    /// Supports numeric ranges, enum options, and descriptive information.
    /// </summary>
    public class ConfigEntry
    {
        /// <summary>
        /// Gets the unique key identifier for this configuration entry.
        /// </summary>
        public string Key { get; init; }

        /// <summary>
        /// Gets the expected type of the configuration value.
        /// </summary>
        public Type ValueType { get; init; }

        /// <summary>
        /// Gets the default value for this configuration entry.
        /// Used for numeric types and general defaults.
        /// </summary>
        public object DefaultValue { get; init; }

        /// <summary>
        /// Gets the minimum allowed value for numeric types.
        /// </summary>
        public object MinValue { get; init; }

        /// <summary>
        /// Gets the maximum allowed value for numeric types.
        /// </summary>
        public object MaxValue { get; init; }

        /// <summary>
        /// Gets the array of valid string options for enum-like configurations.
        /// </summary>
        public string[] ValidOptions { get; init; }

        /// <summary>
        /// Gets the human-readable description of this configuration entry.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets whether changing this setting requires a restart to take effect.
        /// </summary>
        public bool RequiresRestart { get; init; }



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
