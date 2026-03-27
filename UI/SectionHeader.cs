using Godot;

namespace UI;

/// <summary>
/// A collapsible section header with add/remove buttons and an inner ScrollContainer
/// whose height grows to match its content, capped at the outer ScrollContainer's
/// viewport height. This lets each section expand up to the full tab area before
/// its inner scrollbar kicks in, while the outer scrollbar handles scrolling
/// between sections.
/// </summary>
public partial class SectionHeader : VBoxContainer
{
    [Signal]
    public delegate void AddPressedEventHandler();

    [Signal]
    public delegate void RemovePressedEventHandler();

    [Signal]
    public delegate void CollapseChangedEventHandler(bool isCollapsed);

    [Export]
    public Label? HeaderLabel;

    [Export]
    public Button? AddButton;

    [Export]
    public Button? RemoveButton;

    [Export]
    public Button? CollapseButton;

    [Export]
    public Label? CountLabel;

    [Export]
    public VBoxContainer? ContentContainer;

    [Export]
    public ScrollContainer? ContentScroll;

    private bool _isCollapsed = false;

    /// <summary>
    /// Cached reference to the outer ScrollContainer (SystemScrollBar) that holds
    /// all sections. Used to determine the maximum height a section can grow to.
    /// </summary>
    private ScrollContainer? _outerScroll;

    /// <summary>
    /// The VBoxContainer inside ContentScroll that holds the actual list items.
    /// We watch its children for size changes.
    /// </summary>
    private Control? _innerList;

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            _isCollapsed = value;
            UpdateCollapseState();
        }
    }

    public override void _EnterTree()
    {
        // Cache nodes if not set via exports
        HeaderLabel ??= GetNodeOrNull<Label>("Header/HeaderLabel");
        AddButton ??= GetNodeOrNull<Button>("Header/AddButton");
        RemoveButton ??= GetNodeOrNull<Button>("Header/RemoveButton");
        CollapseButton ??= GetNodeOrNull<Button>("Header/CollapseButton");
        CountLabel ??= GetNodeOrNull<Label>("Header/CountLabel");
        ContentContainer ??= GetNodeOrNull<VBoxContainer>("Content");
        ContentScroll ??= GetNodeOrNull<ScrollContainer>("Content/ContentScroll");

        // Wire up events
        if (AddButton != null)
        {
            AddButton.Pressed += () => EmitSignal(SignalName.AddPressed);
        }

        if (RemoveButton != null)
        {
            RemoveButton.Pressed += () => EmitSignal(SignalName.RemovePressed);
        }

        if (CollapseButton != null)
        {
            CollapseButton.Toggled += OnCollapseToggled;
        }

        UpdateCollapseState();
    }

    public override void _Ready()
    {
        // Find the outer ScrollContainer by walking up the tree
        _outerScroll = FindOuterScrollContainer();

        // Find the inner list (first child of ContentScroll)
        if (ContentScroll != null && ContentScroll.GetChildCount() > 0)
        {
            _innerList = ContentScroll.GetChild(0) as Control;
        }

        // Connect to the inner list's child-order and size changes so we can
        // recalculate the scroll height whenever items are added or removed.
        if (_innerList != null)
        {
            _innerList.ChildOrderChanged += OnContentChanged;
            _innerList.Resized += OnContentChanged;
        }

        // Also recalculate if the outer scroll container resizes (e.g. window resize)
        if (_outerScroll != null)
        {
            _outerScroll.Resized += OnContentChanged;
        }

        // Initial sizing
        CallDeferred(MethodName.RecalculateScrollHeight);
    }

    /// <summary>
    /// Walks up the node tree to find the nearest ScrollContainer ancestor that is
    /// NOT our own ContentScroll. This is the outer "SystemScrollBar".
    /// </summary>
    private ScrollContainer? FindOuterScrollContainer()
    {
        Node? current = GetParent();
        while (current != null)
        {
            if (current is ScrollContainer sc && sc != ContentScroll)
            {
                return sc;
            }
            current = current.GetParent();
        }
        return null;
    }

    /// <summary>
    /// Called when the inner list's children or size change, or when the outer
    /// scroll container resizes. Defers recalculation to let the layout settle.
    /// </summary>
    private void OnContentChanged()
    {
        if (ContentScroll == null || !IsInsideTree())
            return;

        CallDeferred(MethodName.RecalculateScrollHeight);
    }

    /// <summary>
    /// Sets the inner ContentScroll's custom_minimum_size.y to the smaller of:
    ///   - the natural height the content wants (so it grows with items), or
    ///   - the outer ScrollContainer's viewport height (so it doesn't exceed the tab).
    /// When content is smaller than the viewport, no inner scrollbar appears.
    /// When content exceeds the viewport, the inner scrollbar clips it.
    /// </summary>
    private void RecalculateScrollHeight()
    {
        if (ContentScroll == null || _isCollapsed)
            return;

        // Get the natural height the content wants
        float contentHeight = 0f;
        if (_innerList != null)
        {
            contentHeight = _innerList.GetCombinedMinimumSize().Y;
        }

        // Get the maximum available height from the outer scroll viewport
        float maxHeight = contentHeight; // fallback: no cap
        if (_outerScroll != null)
        {
            maxHeight = _outerScroll.Size.Y;
        }

        // The scroll container's height is the smaller of content vs available space
        float targetHeight = Mathf.Min(contentHeight, maxHeight);

        // Guard against layout oscillation: only update if the value actually changed.
        // Setting CustomMinimumSize triggers a layout pass which fires Resized,
        // which would call this method again — skip if the delta is negligible.
        if (Mathf.Abs(ContentScroll.CustomMinimumSize.Y - targetHeight) < 1f)
            return;

        ContentScroll.CustomMinimumSize = new Vector2(
            ContentScroll.CustomMinimumSize.X,
            targetHeight
        );
    }

    private void OnCollapseToggled(bool pressed)
    {
        _isCollapsed = !pressed;
        UpdateCollapseState();
        EmitSignal(SignalName.CollapseChanged, _isCollapsed);
    }

    private void UpdateCollapseState()
    {
        // Use ContentContainer if available, otherwise fall back to ContentScroll
        Control? contentControl = (Control?)ContentContainer ?? ContentScroll;

        if (contentControl != null)
            contentControl.Visible = !_isCollapsed;

        if (CollapseButton != null)
            CollapseButton.ButtonPressed = !_isCollapsed;

        // Update arrow icon
        if (CollapseButton != null)
        {
            CollapseButton.Text = _isCollapsed ? "▶" : "▼";
        }

        // When collapsing, zero out the scroll height; when expanding, recalculate
        if (_isCollapsed)
        {
            if (ContentScroll != null)
            {
                ContentScroll.CustomMinimumSize = new Vector2(
                    ContentScroll.CustomMinimumSize.X,
                    0f
                );
            }
        }
        else
        {
            CallDeferred(MethodName.RecalculateScrollHeight);
        }
    }

    public void SetCount(int count)
    {
        if (CountLabel != null)
            CountLabel.Text = $"{count} items";
    }

    public int GetChildItemCount()
    {
        return ContentContainer?.GetChildCount() ?? 0;
    }

    public void ClearContent()
    {
        // Use ContentContainer if available, otherwise fall back to ContentScroll
        VBoxContainer? container = ContentContainer;

        if (container == null && ContentScroll != null)
        {
            // ContentScroll might directly contain the children (common in custom layouts)
            // Check if ContentScroll has any VBoxContainer children
            foreach (Node child in ContentScroll.GetChildren())
            {
                if (child is VBoxContainer vbox)
                {
                    container = vbox;
                    break;
                }
            }
        }

        if (container != null)
        {
            foreach (Node child in container.GetChildren())
            {
                child.QueueFree();
            }
        }
    }
}
