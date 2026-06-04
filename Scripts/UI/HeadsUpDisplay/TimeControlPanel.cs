using Constructables;
using Constructables.Tick;
using Godot;
using Structures.Enums;
using Structures.GameState;
using UtilityLibrary;

/// <summary>
/// Bottom-left time control. Shows the current quarter/year from <see cref="TimeKeeper"/> (refreshed on
/// each <c>MonthElapsed</c> rollover) and exposes a single pause-all toggle: pausing freezes the
/// <see cref="ManufactureTickEngine"/> (factories, intra-planetary transfers, the clock itself) and
/// halts every in-flight ship by disabling its process mode. Resuming restores both.
/// </summary>
public partial class TimeControlPanel : PanelContainer
{
    private Label? _quarterYearLabel;
    private Button? _pauseResumeButton;
    private bool _paused;

    public override void _Ready()
    {
        _quarterYearLabel = GetNodeOrNull<Label>("VBoxContainer/QuarterYearLabel");
        _pauseResumeButton = GetNodeOrNull<Button>("VBoxContainer/PauseResumeButton");

        var clock = TimeKeeper.Instance;
        UpdateClockLabel(clock?.CurrentYear ?? 0, (int)(clock?.CurrentQuarter ?? Quarter.Q1));

        if (SignalBus.Instance != null)
            SignalBus.Instance.MonthElapsed += OnMonthElapsed;
        if (_pauseResumeButton != null)
            _pauseResumeButton.Pressed += OnPausePressed;

        base._Ready();
    }

    public override void _ExitTree()
    {
        if (SignalBus.Instance != null)
            SignalBus.Instance.MonthElapsed -= OnMonthElapsed;
        if (_pauseResumeButton != null)
            _pauseResumeButton.Pressed -= OnPausePressed;
        base._ExitTree();
    }

    private void OnMonthElapsed(int year, int quarter) => UpdateClockLabel(year, quarter);

    private void UpdateClockLabel(int year, int quarter)
    {
        if (_quarterYearLabel != null)
            _quarterYearLabel.Text = $"{(Quarter)quarter} · Year {year + 1}";
    }

    private void OnPausePressed()
    {
        _paused = !_paused;
        SetShipsProcessing(!_paused);

        if (_paused)
            ManufactureTickEngine.Instance?.Pause();
        else
            ManufactureTickEngine.Instance?.Resume();

        if (_pauseResumeButton != null)
            _pauseResumeButton.Text = _paused ? "Resume" : "Pause";

        GameLogger.Info($"[TimeControlPanel] Simulation {(_paused ? "paused" : "resumed")}");
    }

    /// <summary>
    /// Ships drive their own movement from <c>_PhysicsProcess</c>, independent of the tick engine, so they
    /// need their own freeze. Disabling process mode halts orbit/transfer advancement and fuel burn; the
    /// schedule state is left untouched so resuming picks up exactly where it left off.
    /// </summary>
    private void SetShipsProcessing(bool processing)
    {
        var systemData = SystemData.FindIn(GetTree());
        if (systemData == null)
            return;

        var mode = processing ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        foreach (var unit in systemData.GetAllShips())
            unit.ProcessMode = mode;
    }
}
