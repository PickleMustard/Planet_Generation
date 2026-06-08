#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.ResourceEditor;

/// <summary>
/// In-memory editable state for resource YAML configuration.
/// Reads via ResourceConfigLoader, writes via ResourceEditorYamlIO.
/// </summary>
public class ResourceEditorModel
{
	// --- Nested types ---

	public class ResourceCategoryData
	{
		public string CategoryName { get; set; } = "";
		public List<ResourceEditEntry> Resources { get; set; } = new();
		public bool IsNew { get; set; }
		public bool IsDirty { get; set; }
	}

	public class ResourceEditEntry
	{
		public string IdName { get; set; } = "";
		public int ResourceTier { get; set; }
		public int BasePrice { get; set; }
		public Dictionary<string, int> ConfigurableValues { get; set; } = new();
		public string? ResourceType { get; set; }
		public float TransportWeight { get; set; } = 1.0f;
		public float MaxStackSize { get; set; } = 100f;
		public StateOfMatter StateOfMatter { get; set; }
		public HashSet<string> Tags { get; set; } = new();
		public string? IconResourcePath { get; set; }
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

	public bool HasUnsavedChanges => _categories.Values.Any(c =>
		c.IsNew || c.IsDirty || c.Resources.Any(r => r.IsNew || r.IsDirty));

	// --- Constructor ---

	public ResourceEditorModel(string categoriesDirectory)
	{
		ArgumentNullException.ThrowIfNull(categoriesDirectory);
		_categoriesDirectory = categoriesDirectory;
	}

	// --- Load ---

	/// <summary>
	/// Reads all category YAML files from disk and populates the model.
	/// Throws InvalidOperationException if the categories directory is missing.
	/// Individual parse failures are logged and skipped.
	/// </summary>
	public void LoadFromDisk()
	{
		if (!DirAccess.DirExistsAbsolute(_categoriesDirectory))
		{
			throw new InvalidOperationException(
				$"Resource categories directory not found: {_categoriesDirectory}");
		}

		var definitions = ResourceConfigLoader
			.LoadResourceDefinitionsFromCategories(_categoriesDirectory);

		var newCategories = new Dictionary<string, ResourceCategoryData>();

		foreach (var def in definitions)
		{
			string categoryKey = def.ResourceType ?? "unknown";

			if (!newCategories.ContainsKey(categoryKey))
			{
				newCategories[categoryKey] = new ResourceCategoryData
				{
					CategoryName = categoryKey,
					IsNew = false,
					IsDirty = false
				};
			}

			var entry = MapDefinitionToEntry(def);
			newCategories[categoryKey].Resources.Add(entry);
		}

		_categories = newCategories;
	}

	// --- CRUD: Categories ---

	/// <summary>
	/// Adds a new empty category. Throws on null/empty name or duplicate.
	/// </summary>
	public void AddCategory(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		if (string.IsNullOrEmpty(name))
			throw new ArgumentException("Category name cannot be empty", nameof(name));

		if (_categories.ContainsKey(name))
			throw new ArgumentException($"Category '{name}' already exists", nameof(name));

		_categories[name] = new ResourceCategoryData
		{
			CategoryName = name,
			IsNew = true,
			IsDirty = false
		};
	}

	/// <summary>
	/// Removes a category and all its resources. Throws if not found.
	/// </summary>
	public void DeleteCategory(string name)
	{
		if (!_categories.ContainsKey(name))
			throw new KeyNotFoundException($"Category '{name}' not found");

		_categories.Remove(name);
	}

	// --- CRUD: Resources ---

	/// <summary>
	/// Adds a resource entry to the specified category. Throws if category not found.
	/// Sets entry.IsNew = true and entry.ResourceType = categoryName.
	/// </summary>
	public void AddResource(string categoryName, ResourceEditEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);

		if (!_categories.ContainsKey(categoryName))
			throw new KeyNotFoundException($"Category '{categoryName}' not found");

		entry.IsNew = true;
		entry.IsDirty = false;
		entry.ResourceType = categoryName;
		_categories[categoryName].Resources.Add(entry);
		_categories[categoryName].IsDirty = true;
	}

	/// <summary>
	/// Deletes a resource by index from the specified category.
	/// Throws if category not found or index out of range.
	/// </summary>
	public void DeleteResource(string categoryName, int index)
	{
		if (!_categories.ContainsKey(categoryName))
			throw new KeyNotFoundException($"Category '{categoryName}' not found");

		var resources = _categories[categoryName].Resources;
		if (index < 0 || index >= resources.Count)
			throw new ArgumentOutOfRangeException(nameof(index),
				$"Index {index} out of range [0, {resources.Count})");

		resources.RemoveAt(index);
		_categories[categoryName].IsDirty = true;
	}

	/// <summary>
	/// Moves a resource from fromIndex to toIndex within a category.
	/// Throws if category not found or indices out of range.
	/// </summary>
	public void MoveResource(string categoryName, int fromIndex, int toIndex)
	{
		if (!_categories.ContainsKey(categoryName))
			throw new KeyNotFoundException($"Category '{categoryName}' not found");

		var resources = _categories[categoryName].Resources;
		if (fromIndex < 0 || fromIndex >= resources.Count)
			throw new ArgumentOutOfRangeException(nameof(fromIndex));
		if (toIndex < 0 || toIndex >= resources.Count)
			throw new ArgumentOutOfRangeException(nameof(toIndex));

		var entry = resources[fromIndex];
		resources.RemoveAt(fromIndex);
		resources.Insert(toIndex, entry);
		_categories[categoryName].IsDirty = true;
	}

	/// <summary>
	/// Updates a single field on a resource entry by field name.
	/// Supported field names: IdName, ResourceTier, BasePrice, MaxStackSize,
	/// TransportWeight, StateOfMatter, IconResourcePath, IconScale, IconTint.
	/// Throws if category not found, index out of range, or unknown field name.
	/// </summary>
	public void UpdateResourceField(string categoryName, int index,
		string fieldName, object value)
	{
		var entry = GetEntryOrThrow(categoryName, index);

		switch (fieldName)
		{
			case "IdName":
				entry.IdName = value?.ToString() ?? "";
				break;
			case "ResourceTier":
				entry.ResourceTier = Convert.ToInt32(value);
				break;
			case "BasePrice":
				entry.BasePrice = Convert.ToInt32(value);
				break;
			case "MaxStackSize":
				entry.MaxStackSize = Convert.ToSingle(value);
				break;
			case "TransportWeight":
				entry.TransportWeight = Convert.ToSingle(value);
				break;
			case "StateOfMatter":
				entry.StateOfMatter = value is StateOfMatter som
					? som
					: StateOfMatterExtensions.Parse(value?.ToString());
				break;
			case "IconResourcePath":
				entry.IconResourcePath = value?.ToString();
				break;
			case "IconScale":
				entry.IconScale = Convert.ToSingle(value);
				break;
			case "IconTint":
				entry.IconTint = value is Color c
					? c
					: throw new ArgumentException(
						$"Cannot convert '{value}' to Color for field 'IconTint'");
				break;
			default:
				throw new ArgumentException($"Unknown field name: {fieldName}",
					nameof(fieldName));
		}

		entry.IsDirty = true;
	}

	/// <summary>
	/// Replaces the tags set on a resource entry.
	/// Throws if category not found or index out of range.
	/// </summary>
	public void UpdateResourceTags(string categoryName, int index,
		HashSet<string> newTags)
	{
		var entry = GetEntryOrThrow(categoryName, index);
		entry.Tags = new HashSet<string>(newTags);
		entry.IsDirty = true;
	}

	/// <summary>
	/// Replaces the configurable values map on a resource entry.
	/// Throws if category not found or index out of range.
	/// </summary>
	public void UpdateConfigurableValues(string categoryName, int index,
		Dictionary<string, int> newValues)
	{
		var entry = GetEntryOrThrow(categoryName, index);
		entry.ConfigurableValues = new Dictionary<string, int>(newValues);
		entry.IsDirty = true;
	}

	// --- Queries ---

	/// <summary>
	/// Returns the union of all tags across all resources in all categories.
	/// </summary>
	public HashSet<string> GetAllTags()
	{
		var allTags = new HashSet<string>();
		foreach (var category in _categories.Values)
		{
			foreach (var entry in category.Resources)
			{
				allTags.UnionWith(entry.Tags);
			}
		}
		return allTags;
	}

	/// <summary>
	/// Validates model state and returns a list of error/warning messages.
	/// Never throws.
	/// </summary>
	public List<string> Validate()
	{
		var errors = new List<string>();

		// Check for empty id_name
		foreach (var category in _categories.Values)
		{
			for (int i = 0; i < category.Resources.Count; i++)
			{
				var entry = category.Resources[i];
				if (string.IsNullOrEmpty(entry.IdName))
				{
					errors.Add(
						$"Resource in category '{category.CategoryName}' " +
						$"at index {i} has empty id_name");
				}
			}
		}

		// Check for duplicate id_name across all categories
		var nameToCategories = new Dictionary<string, List<string>>();
		foreach (var category in _categories.Values)
		{
			foreach (var entry in category.Resources)
			{
				if (string.IsNullOrEmpty(entry.IdName)) continue;

				if (!nameToCategories.ContainsKey(entry.IdName))
					nameToCategories[entry.IdName] = new List<string>();

				if (!nameToCategories[entry.IdName]
					.Contains(category.CategoryName))
				{
					nameToCategories[entry.IdName]
						.Add(category.CategoryName);
				}
			}
		}

		foreach (var kvp in nameToCategories)
		{
			if (kvp.Value.Count > 1)
			{
				errors.Add(
					$"Duplicate resource id_name '{kvp.Key}' " +
					$"found in categories: {string.Join(", ", kvp.Value)}");
			}
		}

		// Check icon paths
		foreach (var category in _categories.Values)
		{
			foreach (var entry in category.Resources)
			{
				if (!string.IsNullOrEmpty(entry.IconResourcePath) &&
					!entry.IconResourcePath.StartsWith("res://"))
				{
					errors.Add(
						$"Resource '{entry.IdName}' has icon path " +
						$"not starting with res://: '{entry.IconResourcePath}'");
				}
			}
		}

		return errors;
	}

	// --- Private helpers ---

	private ResourceEditEntry GetEntryOrThrow(string categoryName, int index)
	{
		if (!_categories.ContainsKey(categoryName))
			throw new KeyNotFoundException(
				$"Category '{categoryName}' not found");

		var resources = _categories[categoryName].Resources;
		if (index < 0 || index >= resources.Count)
			throw new ArgumentOutOfRangeException(nameof(index),
				$"Index {index} out of range [0, {resources.Count})");

		return resources[index];
	}

	private static ResourceEditEntry MapDefinitionToEntry(ResourceDefinition def)
	{
		return new ResourceEditEntry
		{
			IdName = def.IdName ?? "",
			ResourceTier = def.ResourceTier,
			BasePrice = def.BasePrice,
			ConfigurableValues = new Dictionary<string, int>(
				def.ConfigurableValues ?? new Dictionary<string, int>()),
			ResourceType = def.ResourceType,
			TransportWeight = def.TransportWeight,
			MaxStackSize = def.MaxStackSize,
			StateOfMatter = def.StateOfMatter,
			Tags = new HashSet<string>(def.Tags ?? new HashSet<string>()),
			IconResourcePath = def.Icon?.ResourcePath,
			IconScale = def.Icon?.Scale ?? 1.0f,
			IconTint = def.Icon?.Tint ?? Colors.White,
			IsNew = false,
			IsDirty = false
		};
	}
}
#endif
