#if DEBUG
using System.Collections.Generic;
using Godot;
using GdUnit4;
using Structures.Enums;
using DeveloperTools.ResourceEditor;
using static GdUnit4.Assertions;

namespace Tests.DeveloperTools.ResourceEditor;

[TestSuite]
public class ResourceCardTest
{
	private PackedScene? _cardScene;
	private PackedScene? _tagsPopupScene;

	[Before]
	public void Setup()
	{
		_cardScene = GD.Load<PackedScene>("res://DeveloperTools/ResourceEditor/ResourceCard.tscn");
		_tagsPopupScene = GD.Load<PackedScene>("res://DeveloperTools/ResourceEditor/TagsPopup.tscn");
	}

	// Helper: create card and add to a temporary tree node so _Ready fires
	private static (ResourceCard card, Node root) CreateCardInTree(
		PackedScene scene, ResourceEditorModel model,
		string category, int index,
		ResourceEditorModel.ResourceEditEntry entry, HashSet<string> allTags)
	{
		var root = new Node();
		((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
		var card = scene.Instantiate<ResourceCard>();
		card.Initialize(model, category, index, entry, allTags);
		root.AddChild(card);
		return (card, root);
	}

	private static (TagsPopup popup, Node root) CreatePopupInTree(
		PackedScene scene, ResourceEditorModel model,
		string category, int index,
		ResourceEditorModel.ResourceEditEntry entry, HashSet<string> allTags)
	{
		var root = new Node();
		((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
		var popup = scene.Instantiate<TagsPopup>();
		popup.Initialize(model, category, index, entry, allTags);
		root.AddChild(popup);
		return (popup, root);
	}

	private static void Cleanup(Node root)
	{
		root.QueueFree();
	}

	// ========================================================================
	// CARD INSTANTIATION
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void CardInstantiates_WithSampleEntry()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "iron_ore",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string> { "ore", "metallic" },
			IconResourcePath = null,
			IconScale = 1.0f,
			IconTint = Colors.White
		};

		var allTags = new HashSet<string> { "ore", "metallic", "rare" };
		var (card, root) = CreateCardInTree(_cardScene!, model, "ore", 0, entry, allTags);

		AssertThat(card).IsNotNull();
		AssertThat(card.Name).IsEqual("ResourceCard_iron_ore");

		Cleanup(root);
	}

	// ========================================================================
	// SPINBOX VALUE CHANGE TRIGGERS MODEL UPDATE
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void SpinBoxValueChange_TriggersModelUpdate()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "test_ore",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string>()
		};

		var (card, root) = CreateCardInTree(_cardScene!, model, "ore", 0, entry, new HashSet<string>());

		var spinBox = card.GetNode<SpinBox>("%TierSpinBox");
		AssertThat(spinBox).IsNotNull();

		spinBox.Value = 3;

		AssertThat(model.Categories["ore"].Resources[0].ResourceTier).IsEqual(3);
		AssertThat(model.Categories["ore"].Resources[0].IsDirty).IsTrue();

		Cleanup(root);
	}

	// ========================================================================
	// HSLIDER VALUE CHANGE TRIGGERS MODEL UPDATE (STACK SIZE)
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void HSliderValueChange_StackSize_TriggersModelUpdate()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "test_ore",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string>()
		};

		var (card, root) = CreateCardInTree(_cardScene!, model, "ore", 0, entry, new HashSet<string>());

		var slider = card.GetNode<HSlider>("%StackSlider");
		AssertThat(slider).IsNotNull();

		slider.Value = 500;

		AssertThat(model.Categories["ore"].Resources[0].MaxStackSize).IsEqual(500f);

		Cleanup(root);
	}

	// ========================================================================
	// HSLIDER VALUE CHANGE TRIGGERS MODEL UPDATE (TRANSPORT WEIGHT)
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void HSliderValueChange_TransportWeight_TriggersModelUpdate()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "test_ore",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string>()
		};

		var (card, root) = CreateCardInTree(_cardScene!, model, "ore", 0, entry, new HashSet<string>());

		var slider = card.GetNode<HSlider>("%WeightSlider");
		AssertThat(slider).IsNotNull();

		slider.Value = 2.5;

		float actual = model.Categories["ore"].Resources[0].TransportWeight;
		AssertThat(Mathf.Abs(actual - 2.5f) < 0.01f).IsTrue();

		Cleanup(root);
	}

	// ========================================================================
	// OPTIONBUTTON SELECTION TRIGGERS MODEL UPDATE
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void OptionButtonSelection_TriggersModelUpdate()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("fluid");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "water",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 200f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string>()
		};

		var (card, root) = CreateCardInTree(_cardScene!, model, "fluid", 0, entry, new HashSet<string>());

		var optionBtn = card.GetNode<OptionButton>("%StateOption");
		AssertThat(optionBtn).IsNotNull();

		optionBtn.Select(1);
		optionBtn.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);

		AssertThat(model.Categories["fluid"].Resources[0].StateOfMatter)
			.IsEqual(StateOfMatter.Fluid);

		Cleanup(root);
	}

	// ========================================================================
	// INLINE NAME EDIT: VALID TEXT UPDATES
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void InlineNameEdit_ValidText_UpdatesModel()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "old_name",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string>()
		};

		var (card, root) = CreateCardInTree(_cardScene!, model, "ore", 0, entry, new HashSet<string>());

		var nameEdit = card.GetNode<LineEdit>("%NameEdit");
		AssertThat(nameEdit).IsNotNull();

		nameEdit.Text = "new_name";
		nameEdit.EmitSignal(LineEdit.SignalName.TextSubmitted, "new_name");

		AssertThat(model.Categories["ore"].Resources[0].IdName).IsEqual("new_name");

		Cleanup(root);
	}

	// ========================================================================
	// INLINE NAME EDIT: EMPTY TEXT REVERTS
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void InlineNameEdit_EmptyText_RevertsToOldName()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "original",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string>()
		};

		var (card, root) = CreateCardInTree(_cardScene!, model, "ore", 0, entry, new HashSet<string>());

		var nameEdit = card.GetNode<LineEdit>("%NameEdit");
		AssertThat(nameEdit).IsNotNull();

		nameEdit.Text = "";
		nameEdit.EmitSignal(LineEdit.SignalName.TextSubmitted, "");

		AssertThat(model.Categories["ore"].Resources[0].IdName).IsEqual("original");

		Cleanup(root);
	}

	// ========================================================================
	// DELETE BUTTON SHOWS CONFIRMATION DIALOG
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void DeleteButton_Pressed_ShowsConfirmationDialog()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "to_delete",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string>()
		};

		var (card, root) = CreateCardInTree(_cardScene!, model, "ore", 0, entry, new HashSet<string>());

		var deleteBtn = card.GetNode<Button>("%DeleteButton");
		AssertThat(deleteBtn).IsNotNull();

		deleteBtn.EmitSignal(BaseButton.SignalName.Pressed);

		bool foundDialog = false;
		foreach (var child in card.GetChildren())
		{
			if (child is ConfirmationDialog)
			{
				foundDialog = true;
				break;
			}
		}
		AssertThat(foundDialog).IsTrue();

		Cleanup(root);
	}

	// ========================================================================
	// TAGS POPUP SHOWS CORRECT CHECKBOXES
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void TagsPopup_ShowsCorrectCheckboxes()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "iron_ore",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string> { "ore", "metallic" }
		};

		var allTags = new HashSet<string> { "ore", "metallic", "rare", "conductive" };

		var (popup, root) = CreatePopupInTree(_tagsPopupScene!, model, "ore", 0, entry, allTags);

		var allTagsVBox = popup.GetNode<VBoxContainer>("%AllTagsVBox");
		AssertThat(allTagsVBox).IsNotNull();

		int checkBoxCount = 0;
		int checkedCount = 0;
		foreach (var child in allTagsVBox.GetChildren())
		{
			if (child is CheckBox cb)
			{
				checkBoxCount++;
				if (cb.ButtonPressed)
					checkedCount++;
			}
		}

		AssertThat(checkBoxCount).IsEqual(4);
		AssertThat(checkedCount).IsEqual(2); // ore + metallic are assigned

		Cleanup(root);
	}

	// ========================================================================
	// ADDING TAG THROUGH POPUP WORKS
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void TagsPopup_AddingTag_Works()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "iron_ore",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string> { "ore" }
		};

		var allTags = new HashSet<string> { "ore", "metallic" };
		var (popup, root) = CreatePopupInTree(_tagsPopupScene!, model, "ore", 0, entry, allTags);

		var newTagEdit = popup.GetNode<LineEdit>("%NewTagEdit");
		var addBtn = popup.GetNode<Button>("%AddTagButton");
		AssertThat(newTagEdit).IsNotNull();
		AssertThat(addBtn).IsNotNull();

		newTagEdit.Text = "heavy";
		addBtn.EmitSignal(BaseButton.SignalName.Pressed);

		AssertThat(model.Categories["ore"].Resources[0].Tags.Contains("heavy")).IsTrue();

		Cleanup(root);
	}

	// ========================================================================
	// REMOVING TAG BY CLICKING CURRENT TAG BUTTON WORKS
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void TagsPopup_RemovingCurrentTag_Works()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "iron_ore",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string> { "ore", "metallic" }
		};

		var allTags = new HashSet<string> { "ore", "metallic" };
		var (popup, root) = CreatePopupInTree(_tagsPopupScene!, model, "ore", 0, entry, allTags);

		var flow = popup.GetNode<HFlowContainer>("%CurrentTagsFlow");
		AssertThat(flow).IsNotNull();

		bool clicked = false;
		foreach (var child in flow.GetChildren())
		{
			if (child is Button btn && btn.Text == "ore")
			{
				btn.EmitSignal(BaseButton.SignalName.Pressed);
				clicked = true;
				break;
			}
		}
		AssertThat(clicked).IsTrue();

		AssertThat(model.Categories["ore"].Resources[0].Tags.Contains("ore")).IsFalse();
		AssertThat(model.Categories["ore"].Resources[0].Tags.Contains("metallic")).IsTrue();

		Cleanup(root);
	}

	// ========================================================================
	// INVALID TAG NAME SHOWS ERROR DIALOG
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void TagsPopup_InvalidTagName_ShowsErrorDialog()
	{
		var model = new ResourceEditorModel("res://nonexistent");
		model.AddCategory("ore");

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "iron_ore",
			ResourceTier = 0,
			StateOfMatter = StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string>()
		};

		var (popup, root) = CreatePopupInTree(_tagsPopupScene!, model, "ore", 0, entry, new HashSet<string>());

		var newTagEdit = popup.GetNode<LineEdit>("%NewTagEdit");
		var addBtn = popup.GetNode<Button>("%AddTagButton");
		AssertThat(newTagEdit).IsNotNull();
		AssertThat(addBtn).IsNotNull();

		newTagEdit.Text = "bad tag";
		addBtn.EmitSignal(BaseButton.SignalName.Pressed);

		AssertThat(model.Categories["ore"].Resources[0].Tags.Contains("bad tag")).IsFalse();

		bool foundDialog = false;
		foreach (var child in popup.GetChildren())
		{
			if (child is AcceptDialog)
			{
				foundDialog = true;
				break;
			}
		}
		AssertThat(foundDialog).IsTrue();

		Cleanup(root);
	}
}
#endif
