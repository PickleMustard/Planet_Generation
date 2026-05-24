#if DEBUG
using System;
using System.Text.RegularExpressions;
using Godot;
using Structures.Enums;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.ResourceEditor;

/// <summary>
/// Icon file picker popup with preview. Opens a FileDialog for browsing
/// icon files, displays a preview of the selected icon, and extracts
/// the base path from filenames with size suffixes
/// (e.g., iron_ore_128x128.svg → iron_ore).
/// Emits IconSelected signal with the extracted base path on confirm.
/// GUI layout is defined in IconPickerPopup.tscn.
/// </summary>
public partial class IconPickerPopup : PopupPanel
{
	// --- Signal ---

	/// <summary>
	/// Emitted when the user confirms an icon selection.
	/// basePath is the extracted path without size suffix or extension.
	/// </summary>
	[Signal]
	public delegate void IconSelectedEventHandler(string basePath);

	// --- Regex for size suffix extraction ---

	private static readonly Regex SizeSuffixRegex =
		new(@"_(\d+)x(\d+)\.\w+$", RegexOptions.Compiled);

	// --- State ---

	private string? _selectedFilePath;
	private string? _extractedBasePath;

	// --- UI References ---

	private Button _browseButton = null!;
	private TextureRect _previewRect = null!;
	private Button _confirmButton = null!;
	private Button _cancelButton = null!;
	private FileDialog _fileDialog = null!;

	// ========================================================================
	// LIFECYCLE
	// ========================================================================

	public override void _Ready()
	{
		base._Ready();

		_browseButton = GetNode<Button>("%BrowseButton");
		_previewRect = GetNode<TextureRect>("%PreviewRect");
		_confirmButton = GetNode<Button>("%ConfirmButton");
		_cancelButton = GetNode<Button>("%CancelButton");
		_fileDialog = GetNode<FileDialog>("%IconFileDialog");

		// Connect signals
		_browseButton.Pressed += OnBrowsePressed;
		_confirmButton.Pressed += OnConfirmPressed;
		_cancelButton.Pressed += OnCancelPressed;
		_fileDialog.FileSelected += OnFileSelected;

		_confirmButton.Disabled = true;
	}

	/// <summary>
	/// Opens the popup and resets previous selection state.
	/// </summary>
	public void OpenPicker()
	{
		ResetState();
		base.Popup();
	}

	// ========================================================================
	// FILE DIALOG
	// ========================================================================

	private void OnBrowsePressed()
	{
		_fileDialog.PopupCentered(new Vector2I(800, 600));
	}

	private void OnFileSelected(string path)
	{
		_selectedFilePath = path;

		// Load texture for preview
		var texture = GD.Load<Texture2D>(path);
		if (texture != null)
		{
			_previewRect.Texture = texture;
		}
		else
		{
			GameLogger.Warning($"Failed to load icon for preview: {path}");
			_previewRect.Texture = IconDataLoader.GetFallbackIcon();
		}

		// Extract base path
		_extractedBasePath = ExtractBasePath(path);
		_confirmButton.Disabled = false;
	}

	// ========================================================================
	// BASE PATH EXTRACTION
	// ========================================================================

	/// <summary>
	/// Extracts the base path from a full resource path by stripping
	/// size suffixes like _128x128.svg.
	/// Example: res://Assets/Icons/Resources/ore/iron_ore_128x128.svg
	///       → res://Assets/Icons/Resources/ore/iron_ore
	/// Fallback: strips extension only and logs a warning.
	/// </summary>
	public static string ExtractBasePath(string resourcePath)
	{
		if (string.IsNullOrEmpty(resourcePath))
			return resourcePath ?? "";

		var match = SizeSuffixRegex.Match(resourcePath);
		if (match.Success)
		{
			return resourcePath.Substring(0, match.Index);
		}

		// Fallback: strip extension only
		int dotIndex = resourcePath.LastIndexOf('.');
		if (dotIndex > 0)
		{
			string fallback = resourcePath.Substring(0, dotIndex);
			GameLogger.Warning(
				$"Icon path '{resourcePath}' doesn't match size suffix pattern. " +
				$"Stripping extension only, result: '{fallback}'");
			return fallback;
		}

		GameLogger.Warning(
			$"Could not extract base path from '{resourcePath}'. " +
			$"Using path as-is.");
		return resourcePath;
	}

	// ========================================================================
	// CONFIRM / CANCEL
	// ========================================================================

	private void OnConfirmPressed()
	{
		if (_extractedBasePath != null)
		{
			EmitSignal(SignalName.IconSelected, _extractedBasePath);
		}
		Hide();
		QueueFree();
	}

	private void OnCancelPressed()
	{
		Hide();
		QueueFree();
	}

	// ========================================================================
	// PRIVATE HELPERS
	// ========================================================================

	private void ResetState()
	{
		_selectedFilePath = null;
		_extractedBasePath = null;
		_previewRect.Texture = null;
		_confirmButton.Disabled = true;
	}
}
#endif
