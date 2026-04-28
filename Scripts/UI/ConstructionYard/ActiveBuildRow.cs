using System.Text;
using Constructables;
using Godot;

namespace UI.ConstructionYard;

/// <summary>
/// Row representing a ship actively being built: icon, name, status, progress bar,
/// delivered-vs-required resource summary, pause toggle, cancel button.
/// </summary>
public partial class ActiveBuildRow : HBoxContainer
{
    [Signal]
    public delegate void PauseToggledEventHandler(LogisticsUnit ship);

    [Signal]
    public delegate void CancelRequestedEventHandler(LogisticsUnit ship);

    [Export]
    private Label? _nameLabel;

    [Export]
    private Label? _statusLabel;

    [Export]
    private ProgressBar? _progressBar;

    [Export]
    private Label? _resourcesLabel;

    [Export]
    private Button? _pauseButton;

    [Export]
    private Button? _cancelButton;

    private LogisticsUnit? _ship;

    public LogisticsUnit? Ship => _ship;

    public override void _Ready()
    {
        if (_pauseButton != null)
            _pauseButton.Pressed += OnPausePressed;
        if (_cancelButton != null)
            _cancelButton.Pressed += OnCancelPressed;
    }

    public override void _ExitTree()
    {
        if (_pauseButton != null)
            _pauseButton.Pressed -= OnPausePressed;
        if (_cancelButton != null)
            _cancelButton.Pressed -= OnCancelPressed;
    }

    public void Bind(LogisticsUnit ship)
    {
        _ship = ship;

        if (_nameLabel != null)
            _nameLabel.Text = ship.Name;

        if (_progressBar != null)
        {
            _progressBar.MinValue = 0;
            _progressBar.MaxValue = 1;
            _progressBar.ShowPercentage = true;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_ship == null)
            return;

        if (_progressBar != null)
            _progressBar.Value = _ship.GetProgress();

        if (_statusLabel != null)
        {
            string status = _ship.GetDisplayStatus();
            _statusLabel.Text = status;
            _statusLabel.Modulate = status switch
            {
                "InProgress" => new Color(0.6f, 0.9f, 0.5f),
                "Blocked" => new Color(0.95f, 0.75f, 0.35f),
                "Paused" => new Color(0.75f, 0.75f, 0.8f),
                "Complete" => new Color(0.5f, 0.9f, 0.9f),
                _ => Colors.White,
            };
        }

        if (_resourcesLabel != null)
        {
            var required = _ship.requiredResources;
            var available = _ship.availableResources;
            var sb = new StringBuilder();
            bool first = true;
            foreach (var kvp in required)
            {
                if (!first)
                    sb.Append("  ");
                int have = available.TryGetValue(kvp.Key, out int a) ? a : 0;
                sb.Append($"{kvp.Key}: {have}/{kvp.Value}");
                first = false;
            }
            _resourcesLabel.Text = sb.Length > 0 ? sb.ToString() : string.Empty;
        }

        if (_pauseButton != null)
            _pauseButton.Text = _ship.IsManuallyPaused ? "Resume" : "Pause";
    }

    private void OnPausePressed()
    {
        if (_ship != null)
            EmitSignal(SignalName.PauseToggled, _ship);
    }

    private void OnCancelPressed()
    {
        if (_ship != null)
            EmitSignal(SignalName.CancelRequested, _ship);
    }
}
