using System.Collections.Generic;
using Godot;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.Chyron;

public partial class ChyronPanel : Control
{
    public static ChyronPanel? Instance { get; private set; }

    // Level system
    public enum ChyronLevel { Full, Off }

    [Export] public int MaxBufferEntries { get; set; } = 5;
    [Export] public float TransitionDuration { get; set; } = 0.4f;

    public ChyronLevel Level { get; set; } = ChyronLevel.Full;

    private List<ChyronEntry> _buffer = new();
    private PriorityQueue<ChyronEntry, (int priority, int insertionOrder)> _queue = new();
    private int _insertionCounter;  // FIFO tiebreaker within same priority
    private int _currentBufferIndex;
    private float _currentTimer;
    private bool _isTransitioning;

    // Node refs set in _Ready
    private RichTextLabel? _messageLabel;
    private Label? _priorityBadge;
    private PanelContainer? _panelContainer;

    // PUBLIC API

    public void Enqueue(string message, ChyronPriority priority = ChyronPriority.Normal,
        int displayLength = 5, int displays = 1)
    {
        if (string.IsNullOrEmpty(message))
            return;

        var entry = new ChyronEntry(message, priority, displayLength, displays);
        _queue.Enqueue(entry, ((int)priority, _insertionCounter++));

        TryFillBuffer();

        // If nothing currently showing, show immediately
        if (_buffer.Count > 0 && _panelContainer is not null && !_panelContainer.Visible)
        {
            ShowEntry(_buffer[_currentBufferIndex]);
            ExpandPanel();
        }

        GameLogger.Debug($"Chyron enqueued: '{entry.Message}' (pri={priority}, len={displayLength}s, disp={displays})");
    }

    public void ClearAll()
    {
        _buffer.Clear();
        _queue.Clear();
        _currentBufferIndex = 0;
        _currentTimer = 0f;
        _isTransitioning = false;
        CollapsePanel();
        GameLogger.Debug("Chyron cleared all entries");
    }

    public void SetLevel(ChyronLevel level)
    {
        Level = level;
        if (level == ChyronLevel.Off)
            CollapsePanel();
        else if (_buffer.Count > 0)
            ExpandPanel();
        GameLogger.Debug($"Chyron level set to {level}");
    }

    public int GetPendingCount() => _queue.Count;
    public int GetBufferCount() => _buffer.Count;

    // LIFECYCLE

    public override void _EnterTree()
    {
        if (Instance != null)
        {
            GD.PushError($"Multiple ChyronPanel instances detected! Keeping first, rejecting {Name}");
            QueueFree();
            return;
        }
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        _messageLabel = GetNode<RichTextLabel>("MarginContainer/HBoxContainer/MessageText");
        _priorityBadge = GetNode<Label>("MarginContainer/HBoxContainer/PriorityBadge");
        _panelContainer = GetNode<PanelContainer>(".");

        // Apply Ribbon style variation on self (the root PanelContainer is this node,
        // but we need the actual PanelContainer child if scene structure differs).
        // The scene root IS the PanelContainer; we set ThemeTypeVariation on it.
        if (_panelContainer != null)
        {
            // ChyronStrip: Ribbon variant with 2px top border (header band) and 14/4/14/4 margins
            var chyronStrip = new StyleBoxFlat
            {
                ContentMarginLeft = 14f,
                ContentMarginTop = 4f,
                ContentMarginRight = 14f,
                ContentMarginBottom = 4f,
                BgColor = new Color(0.165f, 0.145f, 0.125f, 0.06f),
                BorderColor = new Color(0.165f, 0.145f, 0.125f, 1f),
            };
            chyronStrip.BorderWidthTop = 2;
            _panelContainer.AddThemeStyleboxOverride("panel", chyronStrip);
        }

        // Add PaperBackground as ruled-line texture
        var paperBg = new PaperBackground
        {
            Name = "PaperBackground",
            DrawRuledLines = true,
            RuleSpacing = 24f,
            DrawDotGrain = false,
        };
        // Insert behind existing children so it renders as background
        if (_panelContainer != null)
        {
            _panelContainer.AddChild(paperBg);
            // Move to front of children list but behind content via z-index
            // PanelContainer's panel stylebox draws behind children, so just ensure
            // MouseFilter lets clicks through.
            paperBg.MouseFilter = MouseFilterEnum.Ignore;
        }

        // Hide priority badge initially
        _priorityBadge?.SetDeferred(Control.PropertyName.Visible, false);

        // Hide panel initially
        Visible = false;
        if (_panelContainer != null)
            _panelContainer.Visible = false;

        // Subscribe to SignalBus
        SubscribeToSignals();
    }

    public override void _Process(double delta)
    {
        if (Level == ChyronLevel.Off || _buffer.Count == 0)
            return;

        if (_isTransitioning)
            return;

        _currentTimer -= (float)delta;

        if (_currentTimer <= 0f)
        {
            AdvanceEntry();
        }
    }

    // SIGNALBUS SUBSCRIPTIONS

    private void SubscribeToSignals()
    {
        SignalBus.Instance?.Connect(SignalBus.SignalName.MarketOrderRejected,
            Callable.From((string resourceId, int quantity, string reason) =>
                Enqueue($"ORDER REJECTED: {reason}", ChyronPriority.High, 6, 2)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.MarketOrderFilled,
            Callable.From((string resourceId, int quantity, double totalAmount, bool isBuy) =>
                Enqueue($"{(isBuy ? "BUY" : "SELL")} {quantity}× {resourceId} — {totalAmount:C0}",
                    ChyronPriority.Normal, 4, 1)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.MarketPriceChanged,
            Callable.From((string resourceId, double newPrice) =>
                Enqueue($"{resourceId} → {newPrice:C0}", ChyronPriority.Normal, 3, 1)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.ContinentPowerStateChanged,
            Callable.From((int continentIndex, bool isDeficit) =>
                Enqueue(isDeficit ? $"POWER DEFICIT — Continent {continentIndex}" : $"Power restored — Continent {continentIndex}",
                    isDeficit ? ChyronPriority.High : ChyronPriority.Normal, 5, 2)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.ContinentResourceShortage,
            Callable.From((int continentIndex, string resourceId, bool isShortage) =>
            {
                if (isShortage)
                    Enqueue($"SHORTAGE: {resourceId} — Continent {continentIndex}", ChyronPriority.High, 6, 2);
            }));

        SignalBus.Instance?.Connect(SignalBus.SignalName.TransferArrived,
            Callable.From((string orderId, bool fullyAccepted) =>
                Enqueue(fullyAccepted ? $"Transfer delivered: {orderId}" : $"Transfer partial: {orderId}",
                    fullyAccepted ? ChyronPriority.Normal : ChyronPriority.High, 4, 1)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.TransferReverted,
            Callable.From((string orderId, int originContinent, float revertedAmount) =>
                Enqueue($"REVERTED: {revertedAmount:F0} units — {orderId}", ChyronPriority.High, 6, 2)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.OrbitalLegFailed,
            Callable.From((string unitId, int legIndex, string reason) =>
                Enqueue($"LEG FAILED: {unitId} leg {legIndex} — {reason}", ChyronPriority.High, 6, 2)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.StationConstructed,
            Callable.From((Node station) =>
                Enqueue($"STATION ONLINE: {station.Name}", ChyronPriority.Normal, 5, 1)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.StationUpgraded,
            Callable.From((Node station) =>
                Enqueue($"STATION UPGRADED: {station.Name}", ChyronPriority.Normal, 4, 1)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.CompanyBudgetChanged,
            Callable.From((double newBudget) =>
                Enqueue($"Budget: {newBudget:C0}", ChyronPriority.Normal, 3, 1)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.CompanyDebtChanged,
            Callable.From((double newDebt) =>
                Enqueue($"Debt: {newDebt:C0}", ChyronPriority.Normal, 3, 1)));

        SignalBus.Instance?.Connect(SignalBus.SignalName.CompanyAntagonismChanged,
            Callable.From((float newAntagonism) =>
            {
                if (newAntagonism > 0.5f)
                    Enqueue($"Antagonism rising: {newAntagonism:P0}", ChyronPriority.High, 5, 2);
            }));

        GameLogger.Debug("ChyronPanel subscribed to SignalBus");
    }

    // INTERNAL

    private void ShowEntry(ChyronEntry entry)
    {
        if (_messageLabel != null)
        {
            _messageLabel.Text = entry.Message;
            _messageLabel.Visible = true;
        }

        if (_priorityBadge != null)
        {
            _priorityBadge.Visible = entry.Priority == ChyronPriority.High;
        }

        _currentTimer = entry.DisplayLength;
        GameLogger.Debug($"Chyron showing: '{entry.Message}' (timer={entry.DisplayLength}s, remaining={entry.DisplaysRemaining})");
    }

    private void AdvanceEntry()
    {
        if (_buffer.Count == 0)
            return;

        var current = _buffer[_currentBufferIndex];
        current.DisplaysRemaining--;
        _buffer[_currentBufferIndex] = current;

        if (current.DisplaysRemaining <= 0)
        {
            _buffer.RemoveAt(_currentBufferIndex);
            TryFillBuffer();

            if (_buffer.Count == 0)
            {
                CollapsePanel();
                return;
            }

            // Adjust index if removal shifted things
            if (_currentBufferIndex >= _buffer.Count)
                _currentBufferIndex = 0;
        }
        else
        {
            _currentBufferIndex = (_currentBufferIndex + 1) % _buffer.Count;
        }

        TransitionTo(_buffer[_currentBufferIndex]);
    }

    private void TransitionTo(ChyronEntry next)
    {
        if (_isTransitioning)
            return;

        _isTransitioning = true;

        var tween = CreateTween();
        tween.SetParallel(true);

        // Fade out current content
        if (_messageLabel != null)
            tween.TweenProperty(_messageLabel, "modulate:a", 0f, TransitionDuration * 0.5f);
        if (_priorityBadge != null && _priorityBadge.Visible)
            tween.TweenProperty(_priorityBadge, "modulate:a", 0f, TransitionDuration * 0.5f);

        // Swap content at midpoint
        tween.Chain();
        tween.TweenCallback(Callable.From(() =>
        {
            ShowEntry(next);
        }));

        // Fade in new content (parallel so both properties animate together)
        tween.SetParallel(true);
        if (_messageLabel != null)
            tween.TweenProperty(_messageLabel, "modulate:a", 1f, TransitionDuration * 0.5f);
        if (_priorityBadge != null && _priorityBadge.Visible)
            tween.TweenProperty(_priorityBadge, "modulate:a", 1f, TransitionDuration * 0.5f);

        tween.Chain();
        tween.TweenCallback(Callable.From(() => { _isTransitioning = false; }));
    }

    private void CollapsePanel()
    {
        if (_panelContainer == null)
            return;

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.In);

        tween.TweenProperty(this, "custom_minimum_size:y", 0f, TransitionDuration);
        tween.TweenCallback(Callable.From(() =>
        {
            _panelContainer.Visible = false;
            Visible = false;
        }));

        GameLogger.Debug("Chyron collapsed");
    }

    private void ExpandPanel()
    {
        if (_panelContainer == null)
            return;

        Visible = true;
        _panelContainer.Visible = true;

        // Animate to target height
        var targetHeight = _panelContainer.GetCombinedMinimumSize().Y;
        if (targetHeight <= 0f)
            targetHeight = 40f; // fallback

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.Out);

        tween.TweenProperty(this, "custom_minimum_size:y", targetHeight, TransitionDuration);

        GameLogger.Debug("Chyron expanded");
    }

    private void TryFillBuffer()
    {
        while (_buffer.Count < MaxBufferEntries && _queue.Count > 0)
        {
            var entry = _queue.Dequeue();
            _buffer.Add(entry);
        }
    }
}