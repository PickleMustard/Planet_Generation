using System;
using System.Collections.Generic;
using Godot;
using UtilityLibrary;

namespace UI;

/// <summary>
/// Toast notification system with queue management, priority levels, and rich text support.
/// Displays up to 3 messages at once with configurable duration and animations.
/// </summary>
public partial class ToastSystem : CanvasLayer
{
    /// <summary>
    /// Singleton instance of the ToastSystem.
    /// </summary>
    public static ToastSystem? Instance { get; private set; }

    /// <summary>
    /// Priority levels for toast messages.
    /// </summary>
    public enum ToastPriority
    {
        /// <summary>Informational message (blue theme)</summary>
        Info,

        /// <summary>Warning message (yellow/orange theme)</summary>
        Warning,

        /// <summary>Error message (red theme)</summary>
        Error,
    }

    /// <summary>
    /// Animation duration for slide-in/out effects in seconds.
    /// </summary>
    [Export]
    public float AnimationDuration { get; set; } = 0.5f;

    /// <summary>
    /// Space between toast messages in pixels.
    /// </summary>
    [Export]
    public int MessageSpacing { get; set; } = 8;

    /// <summary>
    /// Maximum number of toast messages to display simultaneously.
    /// </summary>
    [Export]
    public int MaxVisibleMessages { get; set; } = 3;

    private struct QueuedToast
    {
        public string Message;
        public float Duration;
        public ToastPriority Priority;
        public bool UseRichText;
    }

    private VBoxContainer _toastContainer = null!;
    private readonly Queue<QueuedToast> _messageQueue = new();
    private readonly List<Control> _visibleToasts = new();
    private readonly Dictionary<ToastPriority, Color> _priorityColors = new();

    public override void _EnterTree()
    {
        if (Instance != null)
        {
            GD.PushError(
                $"Multiple ToastSystem instances detected! Keeping first instance: {Instance.Name}, rejecting: {Name}"
            );
            QueueFree();
            return;
        }

        Instance = this;
        GameLogger.EnterFunction(nameof(_EnterTree), $"ToastSystem instance set: {Name}");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
            GameLogger.ExitFunction(nameof(_ExitTree), "ToastSystem instance cleared");
        }
    }

    public override void _Ready()
    {
        GameLogger.EnterFunction(nameof(_Ready));

        // Set layer to 1 as specified
        Layer = 1;

        // Initialize priority colors
        InitializePriorityColors();

        GameLogger.ExitFunction(nameof(_Ready), $"ToastSystem ready with layer {Layer}");
    }

    private void InitializePriorityColors()
    {
        _priorityColors[ToastPriority.Info] = new Color(0.53f, 0.8f, 1.0f); // #88CCFF
        _priorityColors[ToastPriority.Warning] = new Color(1.0f, 0.8f, 0.0f); // #FFCC00
        _priorityColors[ToastPriority.Error] = new Color(1.0f, 0.4f, 0.4f); // #FF6666
    }

    /// <summary>
    /// Shows a toast notification with the specified message and options.
    /// </summary>
    /// <param name="message">The message to display (supports BBCode when useRichText is true)</param>
    /// <param name="duration">How long to display the message in seconds (default: 3)</param>
    /// <param name="priority">Priority level affecting color theme (default: Info)</param>
    /// <param name="useRichText">Whether to enable BBCode rich text formatting (default: true)</param>
    public void Show(
        string message,
        float duration = 3.0f,
        ToastPriority priority = ToastPriority.Info,
        bool useRichText = true
    )
    {
        GameLogger.EnterFunction(
            nameof(Show),
            $"message: '{message}', duration: {duration}, priority: {priority}, useRichText: {useRichText}"
        );

        if (string.IsNullOrEmpty(message))
        {
            GameLogger.Warning("Attempted to show empty toast message");
            GameLogger.ExitFunction(nameof(Show), "empty message");
            return;
        }

        // Queue the message
        var toast = new QueuedToast
        {
            Message = message,
            Duration = duration,
            Priority = priority,
            UseRichText = useRichText,
        };

        _messageQueue.Enqueue(toast);

        // Process queue
        ProcessQueue();

        GameLogger.ExitFunction(nameof(Show), $"queued, pending: {_messageQueue.Count}");
    }

    /// <summary>
    /// Clears all visible toast messages and empties the queue.
    /// </summary>
    public void ClearAll()
    {
        GameLogger.EnterFunction(nameof(ClearAll));

        // Clear all visible toasts
        foreach (var toast in _visibleToasts)
        {
            if (IsInstanceValid(toast))
            {
                AnimateDismiss(toast, true);
            }
        }

        _visibleToasts.Clear();
        _messageQueue.Clear();

        GameLogger.ExitFunction(nameof(ClearAll), "all toasts cleared");
    }

    /// <summary>
    /// Gets the number of messages waiting in the queue.
    /// </summary>
    /// <returns>The count of pending messages</returns>
    public int GetPendingCount() => _messageQueue.Count;

    /// <summary>
    /// Gets the number of currently visible toast messages.
    /// </summary>
    /// <returns>The count of visible messages</returns>
    public int GetVisibleCount() => _visibleToasts.Count;

    private void ProcessQueue()
    {
        // If we have space and messages in queue, show next message
        while (_visibleToasts.Count < MaxVisibleMessages && _messageQueue.Count > 0)
        {
            var nextToast = _messageQueue.Dequeue();
            CreateAndShowToast(nextToast);
        }
    }

    private void CreateAndShowToast(QueuedToast toast)
    {
        GD.Print("CreateAndShowToast");
        // Create panel container
        var panel = new PanelContainer();
        panel.Name = $"Toast_{Guid.NewGuid():N}";

        // Create style based on priority
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.12f, 0.9f),
            BorderColor = _priorityColors[toast.Priority],
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };

        panel.AddThemeStyleboxOverride("panel", style);

        // Create label (RichTextLabel for BBCode support)
        if (toast.UseRichText)
        {
            var richLabel = new RichTextLabel
            {
                Name = "RichTextLabel",
                FitContent = true,
                ScrollActive = false,
                BbcodeEnabled = true,
                Text = toast.Message,
                CustomMinimumSize = new Vector2(280, 0),
            };

            // Configure BBCode - use default theme fonts

            panel.AddChild(richLabel);
        }
        else
        {
            var label = new Label
            {
                Name = "Label",
                Text = toast.Message,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(280, 0),
            };

            panel.AddChild(label);
        }

        // Add to container and visible list
        _toastContainer.AddChild(panel);
        _visibleToasts.Add(panel);

        // Position off-screen initially
        panel.Position = new Vector2(panel.Size.X, 0);

        // Animate slide-in
        AnimateSlideIn(
            panel,
            () =>
            {
                // Start dismissal timer
                var timer = GetTree().CreateTimer(toast.Duration);
                timer.Timeout += () =>
                {
                    if (IsInstanceValid(panel))
                    {
                        DismissToast(panel);
                    }
                };
            }
        );

        GameLogger.Debug($"Toast created: '{toast.Message}' ({toast.Priority}, {toast.Duration}s)");
    }

    private void AnimateSlideIn(Control panel, Action? onComplete = null)
    {
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Back);
        tween.SetEase(Tween.EaseType.Out);

        tween.TweenProperty(panel, "position:x", 0, AnimationDuration);

        if (onComplete != null)
        {
            tween.TweenCallback(Callable.From(onComplete));
        }
    }

    private void DismissToast(Control panel)
    {
        if (!_visibleToasts.Contains(panel))
            return;

        AnimateDismiss(panel, false);
        _visibleToasts.Remove(panel);

        // Clean up after animation
        var timer = GetTree().CreateTimer(AnimationDuration + 0.1f);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(panel))
            {
                panel.QueueFree();
                GameLogger.Debug($"Toast dismissed and freed: {panel.Name}");
            }

            // Process next message in queue
            ProcessQueue();
        };
    }

    private void AnimateDismiss(Control panel, bool immediate)
    {
        if (immediate)
        {
            panel.QueueFree();
            return;
        }

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Back);
        tween.SetEase(Tween.EaseType.In);

        tween.TweenProperty(panel, "position:x", panel.Size.X, AnimationDuration);
        tween
            .TweenProperty(panel, "modulate:a", 0, AnimationDuration * 0.5f)
            .SetDelay(AnimationDuration * 0.5f);
    }

    // Public helper method for common use cases
    /// <summary>
    /// Shows an informational toast message.
    /// </summary>
    public void ShowInfo(string message, float duration = 3.0f, bool useRichText = true)
    {
        Show(message, duration, ToastPriority.Info, useRichText);
    }

    /// <summary>
    /// Shows a warning toast message.
    /// </summary>
    public void ShowWarning(string message, float duration = 4.0f, bool useRichText = true)
    {
        Show(message, duration, ToastPriority.Warning, useRichText);
    }

    /// <summary>
    /// Shows an error toast message.
    /// </summary>
    public void ShowError(string message, float duration = 5.0f, bool useRichText = true)
    {
        Show(message, duration, ToastPriority.Error, useRichText);
    }
}

