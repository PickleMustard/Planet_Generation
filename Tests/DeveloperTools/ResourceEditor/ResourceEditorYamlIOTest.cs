using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using GdUnit4;
using Structures.Enums;
using DeveloperTools.ResourceEditor;
using UtilityLibrary;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static GdUnit4.Assertions;

namespace Tests.DeveloperTools.ResourceEditor;

[TestSuite]
public class ResourceEditorYamlIOTest
{
	private string _tempDir = "";

	[Before]
	public void Setup()
	{
		_tempDir = $"user://test_resource_editor_{Guid.NewGuid():N}";
		DirAccess.MakeDirRecursiveAbsolute(_tempDir);
	}

	[After]
	public void Cleanup()
	{
		if (!string.IsNullOrEmpty(_tempDir) && DirAccess.DirExistsAbsolute(_tempDir))
		{
			// Remove all files in temp dir
			var dir = DirAccess.Open(_tempDir);
			if (dir != null)
			{
				dir.ListDirBegin();
				string fileName = dir.GetNext();
				while (!string.IsNullOrEmpty(fileName))
				{
					if (!fileName.StartsWith("."))
						dir.Remove(fileName);
					fileName = dir.GetNext();
				}
				dir.ListDirEnd();
			}
			// Remove the directory itself
			var parentDir = DirAccess.Open("user://");
			parentDir?.Remove(_tempDir.Replace("user://", ""));
		}
	}

	// ========================================================================
	// WRITE AND READ BACK
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void WriteCategory_ReadBack_ContentMatches()
	{
		var category = new ResourceEditorModel.ResourceCategoryData
		{
			CategoryName = "ore",
			Resources = new List<ResourceEditorModel.ResourceEditEntry>
			{
				new()
				{
					IdName = "iron_ore",
					ResourceTier = 0,
					StateOfMatter = StateOfMatter.Solid,
					MaxStackSize = 100f,
					TransportWeight = 1.0f,
					Tags = new HashSet<string> { "ore", "metallic" },
					IconBasePath = "res://Assets/Icons/Resources/ore/iron_ore",
					IconScale = 1.0f,
					IconTint = Colors.White
				}
			}
		};

		string filePath = $"{_tempDir}/ore.yaml";
		ResourceEditorYamlIO.WriteCategory(filePath, category);

		// Read back raw content
		using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		string content = file.GetAsText();

		AssertThat(content).Contains("id_name: iron_ore");
		AssertThat(content).Contains("resource_tier: 0");
		AssertThat(content).Contains("state_of_matter: solid");
		AssertThat(content).Contains("max_stack_size: 100");
		AssertThat(content).Contains("tags: [ore, metallic]");
		AssertThat(content).Contains("base_path: \"res://Assets/Icons/Resources/ore/iron_ore\"");
		// transport_weight should be omitted (default 1.0)
		AssertThat(content.Contains("transport_weight")).IsFalse();
	}

	// ========================================================================
	// OMISSION RULES
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void WriteCategory_EmptyTags_TagsOmitted()
	{
		var category = new ResourceEditorModel.ResourceCategoryData
		{
			CategoryName = "raw_material",
			Resources = new List<ResourceEditorModel.ResourceEditEntry>
			{
				new()
				{
					IdName = "silicon",
					ResourceTier = 0,
					StateOfMatter = StateOfMatter.Solid,
					MaxStackSize = 100f,
					TransportWeight = 1.0f,
					Tags = new HashSet<string>(),
					IconBasePath = "res://Assets/Icons/Resources/raw_material/silicon"
				}
			}
		};

		string filePath = $"{_tempDir}/raw_material.yaml";
		ResourceEditorYamlIO.WriteCategory(filePath, category);

		using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		string content = file.GetAsText();

		AssertThat(content.Contains("tags")).IsFalse();
	}

	[TestCase]
	[RequireGodotRuntime]
	public void WriteCategory_NullIcon_IconSectionOmitted()
	{
		var category = new ResourceEditorModel.ResourceCategoryData
		{
			CategoryName = "alloys",
			Resources = new List<ResourceEditorModel.ResourceEditEntry>
			{
				new()
				{
					IdName = "stradium",
					ResourceTier = 3,
					StateOfMatter = StateOfMatter.Solid,
					MaxStackSize = 100f,
					TransportWeight = 1.0f,
					Tags = new HashSet<string> { "metal", "ferrous" },
					IconBasePath = null
				}
			}
		};

		string filePath = $"{_tempDir}/alloys.yaml";
		ResourceEditorYamlIO.WriteCategory(filePath, category);

		using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		string content = file.GetAsText();

		AssertThat(content.Contains("icon")).IsFalse();
		AssertThat(content.Contains("base_path")).IsFalse();
	}

	[TestCase]
	[RequireGodotRuntime]
	public void WriteCategory_NonDefaultTransportWeight_Included()
	{
		var category = new ResourceEditorModel.ResourceCategoryData
		{
			CategoryName = "fluid",
			Resources = new List<ResourceEditorModel.ResourceEditEntry>
			{
				new()
				{
					IdName = "water",
					ResourceTier = 0,
					StateOfMatter = StateOfMatter.Fluid,
					MaxStackSize = 200f,
					TransportWeight = 0.5f,
					Tags = new HashSet<string> { "aquatic" },
					IconBasePath = "res://Assets/Icons/Resources/fuel/water"
				}
			}
		};

		string filePath = $"{_tempDir}/fluid.yaml";
		ResourceEditorYamlIO.WriteCategory(filePath, category);

		using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		string content = file.GetAsText();

		AssertThat(content).Contains("transport_weight: 0.5");
	}

	[TestCase]
	[RequireGodotRuntime]
	public void WriteCategory_IconWithScaleAndTint_AllFieldsPresent()
	{
		var tint = new Color(1.0f, 0.8f, 0.5f, 1.0f);
		var category = new ResourceEditorModel.ResourceCategoryData
		{
			CategoryName = "special",
			Resources = new List<ResourceEditorModel.ResourceEditEntry>
			{
				new()
				{
					IdName = "glow_ore",
					ResourceTier = 4,
					StateOfMatter = StateOfMatter.Solid,
					MaxStackSize = 50f,
					TransportWeight = 3.0f,
					Tags = new HashSet<string> { "rare" },
					IconBasePath = "res://Assets/Icons/Resources/special/glow_ore",
					IconScale = 1.5f,
					IconTint = tint
				}
			}
		};

		string filePath = $"{_tempDir}/special.yaml";
		ResourceEditorYamlIO.WriteCategory(filePath, category);

		using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		string content = file.GetAsText();

		AssertThat(content).Contains("base_path: \"res://Assets/Icons/Resources/special/glow_ore\"");
		AssertThat(content).Contains("scale: 1.5");
		AssertThat(content).Contains("tint:");
	}

	// ========================================================================
	// EMPTY CATEGORY
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void WriteCategory_EmptyResources_WritesEmptyList()
	{
		var category = new ResourceEditorModel.ResourceCategoryData
		{
			CategoryName = "empty",
			Resources = new List<ResourceEditorModel.ResourceEditEntry>()
		};

		string filePath = $"{_tempDir}/empty.yaml";
		ResourceEditorYamlIO.WriteCategory(filePath, category);

		using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		string content = file.GetAsText();

		AssertThat(content).Contains("resources: []");
	}

	// ========================================================================
	// ROUND-TRIP: WRITE THEN PARSE WITH YAMLDESERIALIZER
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void WriteCategory_RoundTrip_YamlParsable()
	{
		var category = new ResourceEditorModel.ResourceCategoryData
		{
			CategoryName = "ore",
			Resources = new List<ResourceEditorModel.ResourceEditEntry>
			{
				new()
				{
					IdName = "copper_ore",
					ResourceTier = 0,
					StateOfMatter = StateOfMatter.Solid,
					MaxStackSize = 100f,
					TransportWeight = 1.0f,
					Tags = new HashSet<string> { "ore", "conductive" },
					IconBasePath = "res://Assets/Icons/Resources/ore/copper_ore"
				}
			}
		};

		string filePath = $"{_tempDir}/ore.yaml";
		ResourceEditorYamlIO.WriteCategory(filePath, category);

		// Read back and parse with YamlDotNet
		using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		string content = file.GetAsText();

		var deserializer = new DeserializerBuilder()
			.WithNamingConvention(UnderscoredNamingConvention.Instance)
			.Build();

		var yamlData = deserializer.Deserialize<Dictionary<string, object>>(content);

		AssertThat(yamlData.ContainsKey("resources")).IsTrue();
		var resourcesList = yamlData["resources"] as List<object>;
		AssertThat(resourcesList).IsNotNull();
		AssertThat(resourcesList!.Count).IsEqual(1);

		var resourceDict = resourcesList[0] as Dictionary<object, object>;
		AssertThat(resourceDict).IsNotNull();
		AssertThat(resourceDict!["id_name"].ToString()).IsEqual("copper_ore");
		AssertThat(resourceDict["state_of_matter"].ToString()).IsEqual("solid");
	}

	// ========================================================================
	// WRITE ALL CATEGORIES
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void WriteAllCategories_ResetsDirtyFlags()
	{
		var categories = new Dictionary<string, ResourceEditorModel.ResourceCategoryData>
		{
			["ore"] = new()
			{
				CategoryName = "ore",
				IsNew = true,
				IsDirty = true,
				Resources = new List<ResourceEditorModel.ResourceEditEntry>
				{
					new()
					{
						IdName = "iron_ore",
						ResourceTier = 0,
						StateOfMatter = StateOfMatter.Solid,
						MaxStackSize = 100f,
						IsNew = true,
						IsDirty = true,
						Tags = new HashSet<string>()
					}
				}
			}
		};

		ResourceEditorYamlIO.WriteAllCategories(_tempDir, categories);

		AssertThat(categories["ore"].IsNew).IsFalse();
		AssertThat(categories["ore"].IsDirty).IsFalse();
		AssertThat(categories["ore"].Resources[0].IsNew).IsFalse();
		AssertThat(categories["ore"].Resources[0].IsDirty).IsFalse();
	}

	[TestCase]
	[RequireGodotRuntime]
	public void WriteAllCategories_DeletesOrphanFiles()
	{
		// Create an orphan file
		string orphanPath = $"{_tempDir}/orphan.yaml";
		var orphanFile = FileAccess.Open(orphanPath, FileAccess.ModeFlags.Write);
		orphanFile.StoreString("resources: []\n");
		orphanFile.Close();

		// Verify orphan exists
		AssertThat(FileAccess.FileExists(orphanPath)).IsTrue();

		// Write categories without "orphan" key
		var categories = new Dictionary<string, ResourceEditorModel.ResourceCategoryData>();
		ResourceEditorYamlIO.WriteAllCategories(_tempDir, categories);

		// Orphan should be deleted
		AssertThat(FileAccess.FileExists(orphanPath)).IsFalse();
	}

	// ========================================================================
	// WRITE CATEGORY NULL ARGUMENTS
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void WriteCategory_NullFilePath_Throws()
	{
		var category = new ResourceEditorModel.ResourceCategoryData
		{
			CategoryName = "test",
			Resources = new List<ResourceEditorModel.ResourceEditEntry>()
		};
		AssertThat(() => ResourceEditorYamlIO.WriteCategory(null!, category))
			.Throws<ArgumentNullException>();
	}

	[TestCase]
	[RequireGodotRuntime]
	public void WriteCategory_NullCategory_Throws()
	{
		AssertThat(() => ResourceEditorYamlIO.WriteCategory($"{_tempDir}/test.yaml", null!))
			.Throws<ArgumentNullException>();
	}
}
