#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using Debug;
using UtilityLibrary;
using DeveloperTools.Common;

namespace DeveloperTools.LinkProfileEditor;

/// <summary>
/// Debug module for editing link_profiles.yaml. Single-list editor: vertical list
/// of LinkProfileCard instances + Save/Revert toolbar. Mirrors the ResourceEditor
/// module structure. GUI defined in LinkProfileEditorModule.tscn.
/// </summary>
public partial class LinkProfileEditorModule : BaseDebugModule
{
    public override string ModuleName => "Link Profiles";

    private const string ConfigPath = "res://Configuration/LinkProfiles/link_profiles.yaml";

    private LinkProfileEditorModel? _model;

    private VBoxContainer? _cardContainer;
    private Button? _newProfileButton;
    private Button? _saveButton;
    private Button? _revertButton;
    private Label? _feedbackLabel;

    private PackedScene? _cardScene;

    private double _feedbackTimer;

    public override void _Ready()
    {
        base._Ready();

        _cardContainer = GetNode<VBoxContainer>("%CardContainer");
        _newProfileButton = GetNode<Button>("%NewProfileButton");
        _saveButton = GetNode<Button>("%SaveButton");
        _revertButton = GetNode<Button>("%RevertButton");
        _feedbackLabel = GetNode<Label>("%FeedbackLabel");

        _cardScene = GD.Load<PackedScene>("res://DeveloperTools/LinkProfileEditor/LinkProfileCard.tscn");

        _newProfileButton.Pressed += OnNewProfilePressed;
        _saveButton.Pressed += OnSavePressed;
        _revertButton.Pressed += OnRevertPressed;

        _feedbackLabel.Visible = false;
        _feedbackLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1.0f, 0.4f));

        LoadModel();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdateButtonStates();
        UpdateDirtyIndicator();
        UpdateFeedbackLabel(delta);
    }

    private void LoadModel()
    {
        try
        {
            _model = new LinkProfileEditorModel(ConfigPath);
            _model.LoadFromDisk();
            RefreshCards();
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Failed to load link profile editor model: {ex.Message}");
            ShowAcceptDialog("Load Error", ex.Message);
        }
    }

    private void RefreshCards()
    {
        if (_cardContainer == null || _cardScene == null || _model == null)
            return;

        foreach (var child in _cardContainer.GetChildren())
            child.QueueFree();

        var entries = _model.Entries;
        if (entries.Count == 0)
        {
            var placeholder = new Label
            {
                Text = "No link profiles. Click '+ New Profile' to add one.",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            placeholder.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _cardContainer.AddChild(placeholder);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var card = _cardScene.Instantiate<LinkProfileCard>();
            card.Initialize(_model, i, entries[i]);
            card.CardsNeedRebuild += RefreshCards;
            _cardContainer.AddChild(card);
        }
    }

    private void OnNewProfilePressed()
    {
        if (_model == null) return;
        _model.AddProfile();
        RefreshCards();
    }

    private void OnSavePressed()
    {
        if (_model == null) return;

        var errors = _model.Validate();
        if (errors.Count > 0)
        {
            ShowValidationDialog(errors);
            return;
        }

        ExecuteSave();
    }

    private void ExecuteSave()
    {
        if (_model == null) return;

        try
        {
            LinkProfileEditorYamlIO.WriteAll(ConfigPath, _model.Entries);
            _model.LoadFromDisk();
            int reloaded = EditorDatabaseReloader.ReloadAll("LinkProfileDatabase");
            RefreshCards();
            ShowFeedback($"Saved + reloaded {reloaded} DBs.");
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Save failed: {ex.Message}");
            ShowAcceptDialog("Save Error", ex.Message);
        }
    }

    private void OnRevertPressed()
    {
        if (_model == null || !_model.HasUnsavedChanges) return;

        var dialog = new ConfirmationDialog
        {
            Title = "Revert",
            DialogText = "Discard all unsaved changes?"
        };
        dialog.Confirmed += () =>
        {
            try
            {
                _model.LoadFromDisk();
                RefreshCards();
                ShowFeedback("Reverted");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Revert failed: {ex.Message}");
                ShowAcceptDialog("Revert Error", ex.Message);
            }
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(350, 100));
    }

    private void ShowValidationDialog(List<string> errors)
    {
        var richLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(350, 150),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        var text = "";
        foreach (var error in errors)
            text += $"[color=red]{error}[/color]\n";
        richLabel.Text = text;

        var dialog = new AcceptDialog { Title = "Validation Errors" };
        dialog.AddChild(richLabel);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(500, 300));
    }

    private void UpdateButtonStates()
    {
        if (_model == null) return;
        var dirty = _model.HasUnsavedChanges;
        if (_saveButton != null) _saveButton.Disabled = !dirty;
        if (_revertButton != null) _revertButton.Disabled = !dirty;
    }

    private void UpdateDirtyIndicator()
    {
        if (_model == null) return;
        Name = _model.HasUnsavedChanges ? ModuleName + " *" : ModuleName;
    }

    private void ShowFeedback(string message)
    {
        _feedbackTimer = 2.0;
        if (_feedbackLabel != null)
        {
            _feedbackLabel.Text = message;
            _feedbackLabel.Visible = true;
            _feedbackLabel.Modulate = Colors.White;
        }
    }

    private void UpdateFeedbackLabel(double delta)
    {
        if (_feedbackTimer <= 0 || _feedbackLabel == null) return;
        _feedbackTimer -= delta;
        if (_feedbackTimer <= 0)
        {
            _feedbackLabel.Visible = false;
            _feedbackTimer = 0;
        }
        else
        {
            float alpha = _feedbackTimer < 0.5 ? (float)_feedbackTimer / 0.5f : 1.0f;
            _feedbackLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
        }
    }

    public override void OnModuleEnabled()
    {
        if (_model == null) LoadModel();
        base.OnModuleEnabled();
    }

    private void ShowAcceptDialog(string title, string message)
    {
        var dialog = new AcceptDialog { Title = title, DialogText = message };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }
}
#endif
