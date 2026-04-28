using System;
using Godot;

namespace UI.TransferPlanning;

/// <summary>
/// Bottom bar: validation errors + dispatch/schedule button.
/// </summary>
public partial class TransferStatusBar : Control
{
    public event Action? DispatchPressed;

    private Label? _validationLabel;
    private Button? _dispatchButton;

    public override void _Ready()
    {
        _validationLabel = GetNodeOrNull<Label>("HBoxContainer/ValidationLabel");
        _dispatchButton = GetNodeOrNull<Button>("HBoxContainer/DispatchButton");

        if (_dispatchButton != null)
        {
            _dispatchButton.Pressed += () => DispatchPressed?.Invoke();
            _dispatchButton.Disabled = true;
        }
    }

    /// <summary>
    /// Sets an error message and disables the dispatch button.
    /// </summary>
    public void SetError(string message)
    {
        if (_validationLabel != null)
        {
            _validationLabel.Text = message;
            _validationLabel.Modulate = new Color(1f, 0.5f, 0.5f);
        }

        if (_dispatchButton != null)
            _dispatchButton.Disabled = true;
    }

    /// <summary>
    /// Clears validation errors and enables the dispatch button.
    /// </summary>
    public void SetValid()
    {
        if (_validationLabel != null)
        {
            _validationLabel.Text = "Ready";
            _validationLabel.Modulate = new Color(0.5f, 1f, 0.5f);
        }

        if (_dispatchButton != null)
            _dispatchButton.Disabled = false;
    }

    public void SetButtonText(string text)
    {
        if (_dispatchButton != null)
            _dispatchButton.Text = text;
    }
}
