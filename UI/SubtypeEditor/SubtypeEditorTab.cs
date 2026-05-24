using System;
using Godot;

namespace UI.SubtypeEditor;

/// <summary>
/// Top-level subtype editor tab: toolbar (Save / Reload / Generate / Reset) + feedback label
/// + HSplitContainer with browser and detail panels.
/// Layout defined in SubtypeEditorTab.tscn. Delegates generation logic to the parent
/// <see cref="UI.IndividualPlanetGenerator"/> via events.
/// </summary>
public partial class SubtypeEditorTab : VBoxContainer
{
    [Export] private Button? _saveButton;
    [Export] private Button? _reloadButton;
    [Export] private Button? _generateMidpointButton;
    [Export] private Button? _generateRandomButton;
    [Export] private Button? _resetSelectionButton;
    [Export] private Label? _feedbackLabel;
    [Export] private HSplitContainer? _split;
    [Export] private SubtypeBrowserPanel? _subtypeBrowser;
    [Export] private SubtypeDetailPanel? _subtypeDetail;

    private SubtypeEditorModel? _subtypeModel;
    private double _feedbackTimer;

    /// <summary>Raised when the user clicks Save.</summary>
    public event Action? SaveRequested;

    /// <summary>Raised when the user clicks Reload from disk or Reset selection.</summary>
    public event Action? ReloadRequested;

    /// <summary>Raised when the user clicks Generate. true = midpoint, false = random.</summary>
    public event Action<bool>? GenerateRequested;

    /// <summary>Raised when the user clicks Reset selection.</summary>
    public event Action? ResetRequested;

    public SubtypeBrowserPanel? Browser => _subtypeBrowser;
    public SubtypeDetailPanel? Detail => _subtypeDetail;
    public SubtypeEditorModel? Model => _subtypeModel;

    public override void _Ready()
    {
        if (_saveButton != null)
            _saveButton.Pressed += () => SaveRequested?.Invoke();
        if (_reloadButton != null)
            _reloadButton.Pressed += () => ReloadRequested?.Invoke();
        if (_generateMidpointButton != null)
            _generateMidpointButton.Pressed += () => GenerateRequested?.Invoke(true);
        if (_generateRandomButton != null)
            _generateRandomButton.Pressed += () => GenerateRequested?.Invoke(false);
        if (_resetSelectionButton != null)
            _resetSelectionButton.Pressed += () => ResetRequested?.Invoke();
    }

    public void Initialize(SubtypeEditorModel model)
    {
        _subtypeModel = model;
        _subtypeBrowser?.Initialize(model);
        _subtypeDetail?.Initialize(model);
        if (_subtypeBrowser != null)
            _subtypeBrowser.SubtypeSelected += OnSubtypeSelected;
    }

    private void OnSubtypeSelected(string subtypeId)
    {
        _subtypeDetail?.SetSubtype(subtypeId);
    }

    /// <summary>Show a feedback message at the toolbar that fades out over 2 seconds.</summary>
    public void ShowFeedback(string message)
    {
        if (_feedbackLabel == null) return;
        _feedbackLabel.Text = message;
        _feedbackLabel.Visible = true;
        _feedbackLabel.Modulate = Colors.White;
        _feedbackTimer = 2.0;
    }

    /// <summary>Call each frame to animate the feedback label fade-out.</summary>
    public void UpdateFeedback(double delta)
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
            float alpha = _feedbackTimer < 0.5 ? (float)_feedbackTimer / 0.5f : 1f;
            _feedbackLabel.Modulate = new Color(1f, 1f, 1f, alpha);
        }
    }
}