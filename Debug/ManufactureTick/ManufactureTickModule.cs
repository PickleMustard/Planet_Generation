#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using Constructables.Tick;

namespace Debug.ManufactureTick;

/// <summary>
/// Debug tab exposing ManufactureTickEngine internals (status, frame timing, error capture,
/// registered tickables) and Pause/Resume/Reset controls. The engine runs on a dedicated
/// background thread and is not a Godot Node, so its state is otherwise invisible to the editor.
/// </summary>
public partial class ManufactureTickModule : BaseDebugModule
{
    public override string ModuleName => "Manufacture Ticks";

    private const double UPDATE_INTERVAL = 0.1; // 10 Hz UI refresh

    private static readonly Color OkColor = new(0.3f, 1f, 0.3f);
    private static readonly Color WarnColor = new(1f, 0.8f, 0.3f);
    private static readonly Color BadColor = new(1f, 0.3f, 0.3f);
    private static readonly Color DimColor = new(0.6f, 0.6f, 0.6f);
    private static readonly Color HeaderColor = new(0.8f, 0.8f, 0.4f);

    private Label? _statusLabel;
    private Label? _tickCountLabel;
    private Label? _tickableCountLabel;
    private Label? _queueLabel;

    private Label? _lastTickLabel;
    private Label? _avgTickLabel;
    private Label? _maxTickLabel;
    private Label? _behindLabel;

    private Label? _exceptionCountLabel;
    private Label? _lastExceptionLabel;

    private VBoxContainer? _tickableListContainer;

    private Button? _pauseButton;
    private Button? _resumeButton;
    private Button? _resetButton;

    private double _updateTimer;
    private int _renderedTickableHash;

    public override void _Ready()
    {
        base._Ready();
        BuildUI();
        UpdateDisplay();
    }

    public override void _Process(double delta)
    {
        if (!IsVisible) return;
        _updateTimer += delta;
        if (_updateTimer < UPDATE_INTERVAL) return;
        _updateTimer = 0;
        UpdateDisplay();
    }

    private void BuildUI()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AnchorRight = 1f;
        AnchorBottom = 1f;

        var scroll = new ScrollContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        AddChild(scroll);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        scroll.AddChild(root);

        AddSectionHeader(root, "STATUS");
        _statusLabel = AddRow(root, "Engine:", "—");
        _tickCountLabel = AddRow(root, "Tick #:", "0");
        _tickableCountLabel = AddRow(root, "Tickables:", "0");
        _queueLabel = AddRow(root, "Pending ops:", "0");

        AddSectionHeader(root, "FRAME TIMING");
        _lastTickLabel = AddRow(root, "Last tick:", "0.00 ms");
        _avgTickLabel = AddRow(root, "Avg (60):", "0.00 ms");
        _maxTickLabel = AddRow(root, "Max:", "0.00 ms");
        _behindLabel = AddRow(root, "Behind by:", "0.00 ms");

        AddSectionHeader(root, "ERRORS");
        _exceptionCountLabel = AddRow(root, "Total:", "0");
        _lastExceptionLabel = new Label
        {
            Text = "Last: (none)",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _lastExceptionLabel.AddThemeColorOverride("font_color", DimColor);
        root.AddChild(_lastExceptionLabel);

        AddSectionHeader(root, "REGISTERED TICKABLES");
        _tickableListContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        root.AddChild(_tickableListContainer);

        AddSectionHeader(root, "CONTROLS");
        var buttonRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pauseButton = new Button { Text = "Pause" };
        _pauseButton.Pressed += OnPausePressed;
        _resumeButton = new Button { Text = "Resume" };
        _resumeButton.Pressed += OnResumePressed;
        _resetButton = new Button { Text = "Reset Stats" };
        _resetButton.Pressed += OnResetPressed;
        buttonRow.AddChild(_pauseButton);
        buttonRow.AddChild(_resumeButton);
        buttonRow.AddChild(_resetButton);
        root.AddChild(buttonRow);
    }

    private static void AddSectionHeader(VBoxContainer parent, string text)
    {
        var spacer = new Control { CustomMinimumSize = new Vector2(0, 8) };
        parent.AddChild(spacer);
        var label = new Label { Text = $"[ {text} ]" };
        label.AddThemeColorOverride("font_color", HeaderColor);
        parent.AddChild(label);
    }

    private static Label AddRow(VBoxContainer parent, string key, string initialValue)
    {
        var row = new HBoxContainer();
        var keyLabel = new Label
        { ThemeTypeVariation = "LabelHighContrast",
            Text = key,
            CustomMinimumSize = new Vector2(140, 0),
        };
        var valueLabel = new Label { ThemeTypeVariation = "LabelHighContrast", Text = initialValue };
        row.AddChild(keyLabel);
        row.AddChild(valueLabel);
        parent.AddChild(row);
        return valueLabel;
    }

    private void UpdateDisplay()
    {
        var engine = ManufactureTickEngine.Instance;
        if (engine == null)
        {
            ShowEngineDown();
            return;
        }

        bool running = engine.IsRunning;
        bool paused = engine.IsPaused;

        SetButtonsEnabled(running);

        if (_statusLabel != null)
        {
            string status = running ? (paused ? "Running (PAUSED)" : "Running") : "Stopped";
            _statusLabel.Text = status;
            _statusLabel.AddThemeColorOverride(
                "font_color",
                !running ? BadColor : (paused ? WarnColor : OkColor)
            );
        }

        if (_tickCountLabel != null)
            _tickCountLabel.Text = engine.TickCount.ToString();
        if (_tickableCountLabel != null)
            _tickableCountLabel.Text = engine.TickableCount.ToString();
        if (_queueLabel != null)
            _queueLabel.Text = engine.PendingOpsCount.ToString();

        double last = engine.LastTickMs;
        double avg = engine.AverageTickMs();
        double max = engine.MaxTickElapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        double behind = engine.BehindByMs;
        double target = engine.TargetTickMs;

        if (_lastTickLabel != null)
        {
            _lastTickLabel.Text = $"{last:F2} ms (target {target:F1})";
            _lastTickLabel.AddThemeColorOverride("font_color", ColorForBudget(last, target));
        }
        if (_avgTickLabel != null)
        {
            _avgTickLabel.Text = $"{avg:F2} ms";
            _avgTickLabel.AddThemeColorOverride("font_color", ColorForBudget(avg, target));
        }
        if (_maxTickLabel != null)
        {
            _maxTickLabel.Text = $"{max:F2} ms";
            _maxTickLabel.AddThemeColorOverride("font_color", ColorForBudget(max, target));
        }
        if (_behindLabel != null)
        {
            _behindLabel.Text = behind > 0 ? $"{behind:F2} ms" : "0.00 ms";
            _behindLabel.AddThemeColorOverride(
                "font_color",
                behind <= 0 ? OkColor : (behind > target ? BadColor : WarnColor)
            );
        }

        long exCount = engine.ExceptionCount;
        if (_exceptionCountLabel != null)
        {
            _exceptionCountLabel.Text = exCount.ToString();
            _exceptionCountLabel.AddThemeColorOverride(
                "font_color",
                exCount == 0 ? OkColor : BadColor
            );
        }

        if (_lastExceptionLabel != null)
        {
            var (type, message, tickable, utc) = engine.GetLastException();
            if (type == null)
            {
                _lastExceptionLabel.Text = "Last: (none)";
                _lastExceptionLabel.AddThemeColorOverride("font_color", DimColor);
            }
            else
            {
                _lastExceptionLabel.Text =
                    $"Last: {type} in {tickable} @ {utc.ToLocalTime():HH:mm:ss} — {message}";
                _lastExceptionLabel.AddThemeColorOverride("font_color", BadColor);
            }
        }

        UpdateTickableList(engine.SnapshotTickables());
    }

    private void UpdateTickableList(IReadOnlyList<IManufactureTickable> tickables)
    {
        if (_tickableListContainer == null) return;

        int hash = 17;
        for (int i = 0; i < tickables.Count; i++)
            hash = hash * 31 + (tickables[i]?.GetType().GetHashCode() ?? 0) + tickables[i]!.TickPriority;
        if (hash == _renderedTickableHash && _tickableListContainer.GetChildCount() > 0)
            return;
        _renderedTickableHash = hash;

        foreach (var child in _tickableListContainer.GetChildren())
            child.QueueFree();

        if (tickables.Count == 0)
        {
            var empty = new Label { Text = "  (none registered)" };
            empty.AddThemeColorOverride("font_color", DimColor);
            _tickableListContainer.AddChild(empty);
            return;
        }

        var grouped = new Dictionary<string, int>();
        for (int i = 0; i < tickables.Count; i++)
        {
            var t = tickables[i];
            if (t == null) continue;
            var key = $"{t.GetType().Name} (priority {t.TickPriority})";
            grouped[key] = grouped.TryGetValue(key, out var c) ? c + 1 : 1;
        }

        foreach (var kvp in grouped)
        {
            var label = new Label
            {
                Text = kvp.Value > 1 ? $"  {kvp.Key} × {kvp.Value}" : $"  {kvp.Key}",
            };
            _tickableListContainer.AddChild(label);
        }
    }

    private void ShowEngineDown()
    {
        SetButtonsEnabled(false);
        if (_statusLabel != null)
        {
            _statusLabel.Text = "Engine not running";
            _statusLabel.AddThemeColorOverride("font_color", DimColor);
        }
        if (_tickCountLabel != null) _tickCountLabel.Text = "—";
        if (_tickableCountLabel != null) _tickableCountLabel.Text = "—";
        if (_queueLabel != null) _queueLabel.Text = "—";
        if (_lastTickLabel != null) _lastTickLabel.Text = "—";
        if (_avgTickLabel != null) _avgTickLabel.Text = "—";
        if (_maxTickLabel != null) _maxTickLabel.Text = "—";
        if (_behindLabel != null) _behindLabel.Text = "—";
        if (_exceptionCountLabel != null) _exceptionCountLabel.Text = "—";
        if (_lastExceptionLabel != null) _lastExceptionLabel.Text = "Last: (engine down)";

        if (_tickableListContainer != null)
        {
            foreach (var child in _tickableListContainer.GetChildren())
                child.QueueFree();
            _renderedTickableHash = 0;
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        if (_pauseButton != null) _pauseButton.Disabled = !enabled;
        if (_resumeButton != null) _resumeButton.Disabled = !enabled;
        if (_resetButton != null) _resetButton.Disabled = !enabled;
    }

    private static Color ColorForBudget(double valueMs, double targetMs)
    {
        if (valueMs <= 0) return DimColor;
        if (valueMs > targetMs) return BadColor;
        if (valueMs > targetMs * 0.5) return WarnColor;
        return OkColor;
    }

    private static void OnPausePressed() => ManufactureTickEngine.Instance?.Pause();
    private static void OnResumePressed() => ManufactureTickEngine.Instance?.Resume();
    private static void OnResetPressed() => ManufactureTickEngine.Instance?.ResetTimingStats();
}
#endif
