using System;
using System.Collections.Generic;
using Godot;
using GdUnit4;
using Structures.Enums;
using DeveloperTools.ResourceEditor;
using static GdUnit4.Assertions;

namespace Tests.DeveloperTools.ResourceEditor;

[TestSuite]
public class ResourceEditorModelTest
{
	// ========================================================================
	// ADD CATEGORY
	// ========================================================================

	[TestCase]
	public void AddCategory_ValidName_CategoryAdded()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("test_cat");

		AssertThat(model.Categories).ContainsKey("test_cat");
		AssertThat(model.Categories["test_cat"].IsNew).IsTrue();
		AssertThat(model.Categories["test_cat"].Resources).IsEmpty();
	}

	[TestCase]
	public void AddCategory_NullName_Throws()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		AssertThat(() => model.AddCategory(null!))
			.Throws<ArgumentNullException>();
	}

	[TestCase]
	public void AddCategory_DuplicateName_Throws()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		AssertThat(() => model.AddCategory("ore"))
			.Throws<ArgumentException>();
	}

	[TestCase]
	public void AddCategory_SetsUnsavedChanges()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		AssertThat(model.HasUnsavedChanges).IsFalse();

		model.AddCategory("new_cat");
		AssertThat(model.HasUnsavedChanges).IsTrue();
	}

	// ========================================================================
	// DELETE CATEGORY
	// ========================================================================

	[TestCase]
	public void DeleteCategory_ExistingCategory_CategoryRemoved()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("to_delete");
		model.DeleteCategory("to_delete");

		AssertThat(model.Categories.ContainsKey("to_delete")).IsFalse();
	}

	[TestCase]
	public void DeleteCategory_Nonexistent_Throws()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		AssertThat(() => model.DeleteCategory("nope"))
			.Throws<KeyNotFoundException>();
	}

	// ========================================================================
	// ADD RESOURCE
	// ========================================================================

	[TestCase]
	public void AddResource_ValidEntry_EntryAdded()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = NewEntry("iron_ore");
		model.AddResource("ore", entry);

		var resources = model.Categories["ore"].Resources;
		AssertThat(resources.Count).IsEqual(1);
		AssertThat(resources[0].IdName).IsEqual("iron_ore");
		AssertThat(resources[0].IsNew).IsTrue();
		AssertThat(resources[0].ResourceType).IsEqual("ore");
	}

	[TestCase]
	public void AddResource_MissingCategory_Throws()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		AssertThat(() => model.AddResource("missing", NewEntry("x")))
			.Throws<KeyNotFoundException>();
	}

	[TestCase]
	public void AddResource_SetsCategoryDirty()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		// New category is already dirty from AddCategory, reset it
		model.Categories["ore"].IsNew = false;
		model.Categories["ore"].IsDirty = false;

		model.AddResource("ore", NewEntry("iron_ore"));
		AssertThat(model.Categories["ore"].IsDirty).IsTrue();
	}

	// ========================================================================
	// DELETE RESOURCE
	// ========================================================================

	[TestCase]
	public void DeleteResource_ValidIndex_EntryRemoved()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		model.AddResource("ore", NewEntry("a"));
		model.AddResource("ore", NewEntry("b"));

		model.DeleteResource("ore", 0);

		AssertThat(model.Categories["ore"].Resources.Count).IsEqual(1);
		AssertThat(model.Categories["ore"].Resources[0].IdName).IsEqual("b");
	}

	[TestCase]
	public void DeleteResource_OutOfRange_Throws()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		AssertThat(() => model.DeleteResource("ore", 0))
			.Throws<ArgumentOutOfRangeException>();
	}

	[TestCase]
	public void DeleteResource_MissingCategory_Throws()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		AssertThat(() => model.DeleteResource("missing", 0))
			.Throws<KeyNotFoundException>();
	}

	// ========================================================================
	// MOVE RESOURCE
	// ========================================================================

	[TestCase]
	public void MoveResource_SwapsPositions()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		model.AddResource("ore", NewEntry("first"));
		model.AddResource("ore", NewEntry("second"));
		model.AddResource("ore", NewEntry("third"));

		model.MoveResource("ore", 0, 2);

		var resources = model.Categories["ore"].Resources;
		AssertThat(resources[0].IdName).IsEqual("second");
		AssertThat(resources[1].IdName).IsEqual("third");
		AssertThat(resources[2].IdName).IsEqual("first");
	}

	[TestCase]
	public void MoveResource_SameIndex_NoOp()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		model.AddResource("ore", NewEntry("a"));
		model.AddResource("ore", NewEntry("b"));

		model.MoveResource("ore", 0, 0);

		AssertThat(model.Categories["ore"].Resources[0].IdName).IsEqual("a");
	}

	[TestCase]
	public void MoveResource_InvalidFromIndex_Throws()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		model.AddResource("ore", NewEntry("a"));

		AssertThat(() => model.MoveResource("ore", 5, 0))
			.Throws<ArgumentOutOfRangeException>();
	}

	// ========================================================================
	// UPDATE RESOURCE FIELD
	// ========================================================================

	[TestCase]
	public void UpdateResourceField_IdName_Updates()
	{
		var model = SetupModelWithEntry();
		model.UpdateResourceField("ore", 0, "IdName", "new_name");

		AssertThat(model.Categories["ore"].Resources[0].IdName)
			.IsEqual("new_name");
	}

	[TestCase]
	public void UpdateResourceField_ResourceTier_Updates()
	{
		var model = SetupModelWithEntry();
		model.UpdateResourceField("ore", 0, "ResourceTier", 3);

		AssertThat(model.Categories["ore"].Resources[0].ResourceTier)
			.IsEqual(3);
	}

	[TestCase]
	public void UpdateResourceField_BasePrice_Updates()
	{
		var model = SetupModelWithEntry();
		model.UpdateResourceField("ore", 0, "BasePrice", 1050);

		AssertThat(model.Categories["ore"].Resources[0].BasePrice)
			.IsEqual(1050);
	}

	[TestCase]
	public void UpdateResourceField_MaxStackSize_Updates()
	{
		var model = SetupModelWithEntry();
		model.UpdateResourceField("ore", 0, "MaxStackSize", 500f);

		AssertThat(model.Categories["ore"].Resources[0].MaxStackSize)
			.IsEqual(500f);
	}

	[TestCase]
	public void UpdateResourceField_TransportWeight_Updates()
	{
		var model = SetupModelWithEntry();
		model.UpdateResourceField("ore", 0, "TransportWeight", 2.5f);

		float actual = model.Categories["ore"].Resources[0].TransportWeight;
		AssertThat(Mathf.Abs(actual - 2.5f) < 0.001f).IsTrue();
	}

	[TestCase]
	public void UpdateResourceField_StateOfMatter_Enum_Updates()
	{
		var model = SetupModelWithEntry();
		model.UpdateResourceField("ore", 0, "StateOfMatter",
			StateOfMatter.Fluid);

		AssertThat(model.Categories["ore"].Resources[0].StateOfMatter)
			.IsEqual(StateOfMatter.Fluid);
	}

	[TestCase]
	public void UpdateResourceField_StateOfMatter_String_Updates()
	{
		var model = SetupModelWithEntry();
		model.UpdateResourceField("ore", 0, "StateOfMatter", "fluid");

		AssertThat(model.Categories["ore"].Resources[0].StateOfMatter)
			.IsEqual(StateOfMatter.Fluid);
	}

	[TestCase]
	public void UpdateResourceField_IconResourcePath_Updates()
	{
		var model = SetupModelWithEntry();
		model.UpdateResourceField("ore", 0, "IconResourcePath",
			"res://Assets/Icons/test");

		AssertThat(model.Categories["ore"].Resources[0].IconResourcePath)
			.IsEqual("res://Assets/Icons/test");
	}

	[TestCase]
	public void UpdateResourceField_IconScale_Updates()
	{
		var model = SetupModelWithEntry();
		model.UpdateResourceField("ore", 0, "IconScale", 2.0f);

		AssertThat(model.Categories["ore"].Resources[0].IconScale)
			.IsEqual(2.0f);
	}

	[TestCase]
	public void UpdateResourceField_IconTint_Updates()
	{
		var model = SetupModelWithEntry();
		var tint = new Color(1.0f, 0.5f, 0.0f, 1.0f);
		model.UpdateResourceField("ore", 0, "IconTint", tint);

		AssertThat(model.Categories["ore"].Resources[0].IconTint)
			.IsEqual(tint);
	}

	[TestCase]
	public void UpdateResourceField_UnknownField_Throws()
	{
		var model = SetupModelWithEntry();
		AssertThat(() => model.UpdateResourceField("ore", 0, "BadField", 42))
			.Throws<ArgumentException>();
	}

	[TestCase]
	public void UpdateResourceField_SetsEntryDirty()
	{
		var model = SetupModelWithEntry();
		var entry = model.Categories["ore"].Resources[0];
		entry.IsDirty = false;

		model.UpdateResourceField("ore", 0, "ResourceTier", 2);
		AssertThat(entry.IsDirty).IsTrue();
	}

	// ========================================================================
	// UPDATE RESOURCE TAGS
	// ========================================================================

	[TestCase]
	public void UpdateResourceTags_ReplacesTagSet()
	{
		var model = SetupModelWithEntry();
		var newTags = new HashSet<string> { "rare", "metallic" };

		model.UpdateResourceTags("ore", 0, newTags);

		var tags = model.Categories["ore"].Resources[0].Tags;
		AssertThat(tags.Count).IsEqual(2);
		AssertThat(tags.Contains("rare")).IsTrue();
		AssertThat(tags.Contains("metallic")).IsTrue();
	}

	[TestCase]
	public void UpdateResourceTags_SetsEntryDirty()
	{
		var model = SetupModelWithEntry();
		model.Categories["ore"].Resources[0].IsDirty = false;

		model.UpdateResourceTags("ore", 0, new HashSet<string> { "x" });
		AssertThat(model.Categories["ore"].Resources[0].IsDirty).IsTrue();
	}

	// ========================================================================
	// UPDATE CONFIGURABLE VALUES
	// ========================================================================

	[TestCase]
	public void UpdateConfigurableValues_ReplacesMap()
	{
		var model = SetupModelWithEntry();
		var newValues = new Dictionary<string, int>
		{
			["burn_potential"] = 30,
			["nutrition"] = 5
		};

		model.UpdateConfigurableValues("ore", 0, newValues);

		var values = model.Categories["ore"].Resources[0].ConfigurableValues;
		AssertThat(values.Count).IsEqual(2);
		AssertThat(values["burn_potential"]).IsEqual(30);
		AssertThat(values["nutrition"]).IsEqual(5);
	}

	[TestCase]
	public void UpdateConfigurableValues_CopiesInput()
	{
		var model = SetupModelWithEntry();
		var newValues = new Dictionary<string, int> { ["k"] = 1 };
		model.UpdateConfigurableValues("ore", 0, newValues);

		// Mutating the source must not affect the stored copy.
		newValues["k"] = 999;
		AssertThat(model.Categories["ore"].Resources[0].ConfigurableValues["k"])
			.IsEqual(1);
	}

	[TestCase]
	public void UpdateConfigurableValues_SetsEntryDirty()
	{
		var model = SetupModelWithEntry();
		model.Categories["ore"].Resources[0].IsDirty = false;

		model.UpdateConfigurableValues("ore", 0,
			new Dictionary<string, int> { ["x"] = 1 });
		AssertThat(model.Categories["ore"].Resources[0].IsDirty).IsTrue();
	}

	// ========================================================================
	// HAS UNSAVED CHANGES
	// ========================================================================

	[TestCase]
	public void HasUnsavedChanges_FreshModel_False()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		AssertThat(model.HasUnsavedChanges).IsFalse();
	}

	[TestCase]
	public void HasUnsavedChanges_NewCategory_True()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("new");
		AssertThat(model.HasUnsavedChanges).IsTrue();
	}

	[TestCase]
	public void HasUnsavedChanges_DirtyCategory_true()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		model.Categories["ore"].IsNew = false;
		model.Categories["ore"].IsDirty = false;

		model.Categories["ore"].IsDirty = true;
		AssertThat(model.HasUnsavedChanges).IsTrue();
	}

	[TestCase]
	public void HasUnsavedChanges_DirtyEntry_true()
	{
		var model = SetupModelWithEntry();
		model.Categories["ore"].IsNew = false;
		model.Categories["ore"].IsDirty = false;
		model.Categories["ore"].Resources[0].IsNew = false;
		model.Categories["ore"].Resources[0].IsDirty = false;

		model.Categories["ore"].Resources[0].IsDirty = true;
		AssertThat(model.HasUnsavedChanges).IsTrue();
	}

	[TestCase]
	public void HasUnsavedChanges_AllClean_false()
	{
		var model = SetupModelWithEntry();
		model.Categories["ore"].IsNew = false;
		model.Categories["ore"].IsDirty = false;
		model.Categories["ore"].Resources[0].IsNew = false;
		model.Categories["ore"].Resources[0].IsDirty = false;

		AssertThat(model.HasUnsavedChanges).IsFalse();
	}

	// ========================================================================
	// VALIDATE — DUPLICATE NAMES
	// ========================================================================

	[TestCase]
	public void Validate_DuplicateIdName_ReturnsError()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		model.AddCategory("raw_material");
		model.AddResource("ore", NewEntry("iron"));
		model.AddResource("raw_material", NewEntry("iron"));

		// Reset dirty flags so only Validate is under test
		ResetDirtyFlags(model);

		var errors = model.Validate();
		AssertThat(errors.Count).IsEqual(1);
		AssertThat(errors[0]).Contains("Duplicate resource id_name 'iron'");
	}

	[TestCase]
	public void Validate_NoDuplicates_EmptyList()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		model.AddCategory("fuel");
		model.AddResource("ore", NewEntry("iron_ore"));
		model.AddResource("fuel", NewEntry("hydrogen"));

		ResetDirtyFlags(model);

		var errors = model.Validate();
		AssertThat(errors).IsEmpty();
	}

	// ========================================================================
	// VALIDATE — EMPTY ID NAME
	// ========================================================================

	[TestCase]
	public void Validate_EmptyIdName_ReturnsError()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		model.AddResource("ore", NewEntry(""));

		var errors = model.Validate();
		bool found = false;
		foreach (var e in errors)
			if (e.Contains("empty id_name"))
				found = true;
		AssertThat(found).IsTrue();
	}

	// ========================================================================
	// VALIDATE — BAD ICON PATH
	// ========================================================================

	[TestCase]
	public void Validate_IconPathNoResPrefix_ReturnsError()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		var entry = NewEntry("bad_icon");
		entry.IconResourcePath = "user://bad/path";
		model.AddResource("ore", entry);

		ResetDirtyFlags(model);

		var errors = model.Validate();
		bool found = false;
		foreach (var e in errors)
			if (e.Contains("not starting with res://"))
				found = true;
		AssertThat(found).IsTrue();
	}

	[TestCase]
	public void Validate_NullIconPath_NoError()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		var entry = NewEntry("no_icon");
		entry.IconResourcePath = null;
		model.AddResource("ore", entry);

		ResetDirtyFlags(model);

		var errors = model.Validate();
		foreach (var e in errors)
			AssertThat(e.Contains("icon")).IsFalse();
	}

	// ========================================================================
	// GET ALL TAGS
	// ========================================================================

	[TestCase]
	public void GetAllTags_UnionsAcrossCategories()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		model.AddCategory("fuel");

		var oreEntry = NewEntry("iron_ore");
		oreEntry.Tags = new HashSet<string> { "metallic", "ore" };
		model.AddResource("ore", oreEntry);

		var fuelEntry = NewEntry("hydrogen");
		fuelEntry.Tags = new HashSet<string> { "gas", "fuel" };
		model.AddResource("fuel", fuelEntry);

		var allTags = model.GetAllTags();
		AssertThat(allTags.Count).IsEqual(4);
		AssertThat(allTags.Contains("metallic")).IsTrue();
		AssertThat(allTags.Contains("ore")).IsTrue();
		AssertThat(allTags.Contains("gas")).IsTrue();
		AssertThat(allTags.Contains("fuel")).IsTrue();
	}

	[TestCase]
	public void GetAllTags_NoTags_EmptySet()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		AssertThat(model.GetAllTags()).IsEmpty();
	}

	// ========================================================================
	// CONSTRUCTOR NULL CHECK
	// ========================================================================

	[TestCase]
	public void Constructor_NullPath_Throws()
	{
		AssertThat(() => new ResourceEditorModel(null!))
			.Throws<ArgumentNullException>();
	}

	// ========================================================================
	// HELPERS
	// ========================================================================

	private static ResourceEditorModel.ResourceEditEntry NewEntry(string idName)
	{
		return new ResourceEditorModel.ResourceEditEntry
		{
			IdName = idName,
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string>(),
			IconResourcePath = null,
			IconScale = 1.0f,
			IconTint = Colors.White
		};
	}

	private static ResourceEditorModel SetupModelWithEntry()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");
		model.AddResource("ore", NewEntry("test_ore"));
		return model;
	}

	private static void ResetDirtyFlags(ResourceEditorModel model)
	{
		foreach (var cat in model.Categories.Values)
		{
			cat.IsNew = false;
			cat.IsDirty = false;
			foreach (var entry in cat.Resources)
			{
				entry.IsNew = false;
				entry.IsDirty = false;
			}
		}
	}
}
