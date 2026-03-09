using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace UtilityLibrary
{
	/// <summary>
	/// Unified accessor for RuntimeSettings that provides centralized settings access.
	/// Uses RuntimeSettings singleton as the settings provider.
	/// </summary>
	public static class SettingsAccess
	{
		/// <summary>
		/// Gets current settings implementation, or null if not initialized.
		/// </summary>
		private static ISettingsProvider? Provider
		{
			get
			{
				return RuntimeSettings.Instance as ISettingsProvider;
			}
		}

		/// <summary>
		/// Gets whether a settings provider is available in the current context.
		/// </summary>
		public static bool IsAvailable => Provider != null;

		/// <summary>
		/// Gets a setting value from the settings provider.
		/// </summary>
		/// <typeparam name="T">The expected type of the setting value.</typeparam>
		/// <param name="category">The settings category.</param>
		/// <param name="key">The setting key.</param>
		/// <param name="defaultValue">The default value if not found.</param>
		/// <returns>The setting value, or default if not found.</returns>
		public static T? GetSetting<[MustBeVariant] T>(string category, string key, T? defaultValue = default)
		{
			if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key))
			{
				GameLogger.Warning($"GetSetting called with null or empty category/key.");
				return defaultValue;
			}

			var provider = Provider;
			if (provider != null)
			{
				return provider.GetSetting(category, key, defaultValue);
			}

			return defaultValue;
		}

		/// <summary>
		/// Sets a setting value after validation.
		/// </summary>
		/// <param name="category">The settings category.</param>
		/// <param name="key">The setting key.</param>
		/// <param name="value">The value to set.</param>
		public static void SetSetting(string category, string key, object value)
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

			var provider = Provider;
			if (provider != null)
			{
				provider.SetSetting(category, key, value);
			}
		}

		/// <summary>
		/// Gets all configuration entries from all registered configurables.
		/// </summary>
		/// <returns>An enumerable of all configuration entries.</returns>
		public static IEnumerable<ConfigEntry> GetAllEntries()
		{
			var provider = Provider;
			return provider?.GetAllEntries() ?? Enumerable.Empty<ConfigEntry>();
		}

		/// <summary>
		/// Gets a configurable by category.
		/// </summary>
		/// <param name="category">The settings category.</param>
		/// <returns>The configurable, or null if not found.</returns>
		public static IConfigurable? GetConfigurable(string category)
		{
			var provider = Provider;
			return provider?.GetConfigurable(category);
		}

		/// <summary>
		/// Checks if a setting exists.
		/// </summary>
		/// <param name="category">The settings category.</param>
		/// <param name="key">The setting key.</param>
		/// <returns>True if setting exists.</returns>
		public static bool HasSetting(string category, string key)
		{
			var provider = Provider;
			if (provider != null)
			{
				return provider.HasSetting(category, key);
			}

			return false;
		}

		/// <summary>
		/// Registers a configurable object with the settings system.
		/// </summary>
		/// <param name="configurable">The configurable object to register.</param>
		public static void RegisterConfigurable(IConfigurable configurable)
		{
			if (configurable == null)
			{
				GameLogger.Warning("Attempted to register null configurable.");
				return;
			}

			var provider = Provider;
			if (provider != null)
			{
				provider.RegisterConfigurable(configurable);
			}
		}

		/// <summary>
		/// Converts a Godot Variant to a C# object.
		/// </summary>
		/// <typeparam name="T">The expected type.</typeparam>
		/// <param name="variant">The variant to convert.</param>
		/// <returns>The converted value, or default if conversion fails.</returns>
		private static T? ConvertVariant<T>(Variant variant)
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

		/// <summary>
		/// Converts a C# object to a Godot Variant by checking the runtime type.
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
	}
}
