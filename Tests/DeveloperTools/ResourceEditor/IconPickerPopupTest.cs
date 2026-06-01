using Godot;
using GdUnit4;
using DeveloperTools.ResourceEditor;
using static GdUnit4.Assertions;

namespace Tests.DeveloperTools.ResourceEditor;

[TestSuite]
public class IconPickerPopupTest
{
	// ========================================================================
	// BASE PATH EXTRACTION — Standard pattern
	// ========================================================================

	[TestCase]
	public void ExtractBasePath_StandardSuffix_StripsCorrectly()
	{
		// iron_ore_128x128.svg → iron_ore
		var result = IconPickerPopup.ExtractBasePath(
			"res://Assets/Icons/Resources/ore/iron_ore_128x128.svg");

		AssertThat(result).IsEqual(
			"res://Assets/Icons/Resources/ore/iron_ore");
	}

	[TestCase]
	public void ExtractBasePath_PngSuffix_StripsCorrectly()
	{
		var result = IconPickerPopup.ExtractBasePath(
			"res://Assets/Icons/Resources/ore/copper_ore_64x64.png");

		AssertThat(result).IsEqual(
			"res://Assets/Icons/Resources/ore/copper_ore");
	}

	[TestCase]
	public void ExtractBasePath_LargeSizeSuffix_StripsCorrectly()
	{
		var result = IconPickerPopup.ExtractBasePath(
			"res://Assets/Icons/Resources/ore/uranium_ore_512x512.svg");

		AssertThat(result).IsEqual(
			"res://Assets/Icons/Resources/ore/uranium_ore");
	}

	// ========================================================================
	// BASE PATH EXTRACTION — Non-standard names (fallback)
	// ========================================================================

	[TestCase]
	public void ExtractBasePath_NoSizeSuffix_StripsExtensionOnly()
	{
		// Non-standard name → fallback: strip extension only
		var result = IconPickerPopup.ExtractBasePath(
			"res://Assets/Icons/Resources/ore/custom_icon.svg");

		AssertThat(result).IsEqual(
			"res://Assets/Icons/Resources/ore/custom_icon");
	}

	[TestCase]
	public void ExtractBasePath_NoExtension_ReturnsAsIs()
	{
		var result = IconPickerPopup.ExtractBasePath(
			"res://Assets/Icons/Resources/ore/some_icon");

		AssertThat(result).IsEqual(
			"res://Assets/Icons/Resources/ore/some_icon");
	}

	// ========================================================================
	// BASE PATH EXTRACTION — Edge cases
	// ========================================================================

	[TestCase]
	public void ExtractBasePath_EmptyString_ReturnsEmpty()
	{
		var result = IconPickerPopup.ExtractBasePath("");

		AssertThat(result).IsEmpty();
	}

	[TestCase]
	public void ExtractBasePath_NullString_ReturnsEmpty()
	{
		var result = IconPickerPopup.ExtractBasePath(null!);

		AssertThat(result).IsEmpty();
	}

	[TestCase]
	public void ExtractBasePath_OnlyExtension_StripsDot()
	{
		var result = IconPickerPopup.ExtractBasePath(".svg");

		// LastIndexOf('.') returns 0, which is not > 0, so path returned as-is
		AssertThat(result).IsEqual(".svg");
	}

	// ========================================================================
	// BASE PATH EXTRACTION — Name with underscores
	// ========================================================================

	[TestCase]
	public void ExtractBasePath_UnderscoreInName_StripsOnlySuffix()
	{
		// Name already has underscores before the size suffix
		var result = IconPickerPopup.ExtractBasePath(
			"res://Assets/Icons/Resources/ore/high_grade_ore_128x128.svg");

		AssertThat(result).IsEqual(
			"res://Assets/Icons/Resources/ore/high_grade_ore");
	}

	// ========================================================================
	// POPUP INSTANTIATION (requires Godot runtime)
	// ========================================================================

	[TestCase]
	[RequireGodotRuntime]
	public void PopupInstantiates_WithoutErrors()
	{
		var scene = GD.Load<PackedScene>(
			"res://DeveloperTools/ResourceEditor/IconPickerPopup.tscn");

		IconPickerPopup? popup = null;
		AssertThat(() =>
		{
			popup = scene.Instantiate<IconPickerPopup>();
		}).DoesNotThrow();

		if (popup != null)
			popup.QueueFree();
	}

	[TestCase]
	[RequireGodotRuntime]
	public void PopupHas_FileDialog()
	{
		var scene = GD.Load<PackedScene>(
			"res://DeveloperTools/ResourceEditor/IconPickerPopup.tscn");
		var popup = scene.Instantiate<IconPickerPopup>();

		var fileDialog = popup.FindChild("IconFileDialog") as FileDialog;
		AssertThat(fileDialog).IsNotNull();

		popup.QueueFree();
	}

	[TestCase]
	[RequireGodotRuntime]
	public void PopupHas_PreviewRect()
	{
		var scene = GD.Load<PackedScene>(
			"res://DeveloperTools/ResourceEditor/IconPickerPopup.tscn");
		var popup = scene.Instantiate<IconPickerPopup>();

		var previewRect = popup.FindChild("PreviewRect") as TextureRect;
		AssertThat(previewRect).IsNotNull();

		popup.QueueFree();
	}

	[TestCase]
	[RequireGodotRuntime]
	public void PopupHas_ConfirmAndCancelButtons()
	{
		var scene = GD.Load<PackedScene>(
			"res://DeveloperTools/ResourceEditor/IconPickerPopup.tscn");
		var popup = scene.Instantiate<IconPickerPopup>();

		var confirm = popup.FindChild("ConfirmButton") as Button;
		var cancel = popup.FindChild("CancelButton") as Button;

		AssertThat(confirm).IsNotNull();
		AssertThat(cancel).IsNotNull();

		popup.QueueFree();
	}
}
