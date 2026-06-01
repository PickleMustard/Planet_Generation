using Constructables;
using Constructables.Stations.Behaviors;
using Godot;
using Structures.Logistics;

namespace UI.StationWindow;

/// <summary>
/// Card representing one active build slot in the ShipyardPanel.
/// Shows ship name, construction progress, per-resource delivery status,
/// and a pause/resume toggle. When bound to a null ship, displays "Empty Slot".
/// </summary>
public partial class ShipyardSlotCard : VBoxContainer
{
    [Export]
    private Label? _shipNameLabel;

    [Export]
    private ProgressBar? _constructionProgressBar;

    [Export]
    private VBoxContainer? _resourceList;

    [Export]
    private Button? _pauseButton;

    private LogisticsUnit? _ship;
    private ShipyardBehavior? _shipyard;

    public override void _Ready()
    {
        if (_pauseButton != null)
            _pauseButton.Pressed += OnPausePressed;

        if (_shipNameLabel != null)
            _shipNameLabel.GuiInput += OnLabelInput;
    }

    public override void _ExitTree()
    {
        if (_pauseButton != null)
            _pauseButton.Pressed -= OnPausePressed;
    }

    [Signal]
    public delegate void SlotSelectedEventHandler(int slotIndex);

    /// <summary>
    /// Binds this card to a specific ship in an active build slot.
    /// Pass null <paramref name="ship"/> to show an empty slot.
    /// </summary>
    public void Bind(LogisticsUnit? ship, ShipyardBehavior? shipyard, int slotIndex)
    {
        _ship = ship;
        _shipyard = shipyard;
        _slotIndex = slotIndex;
        UpdateDisplay();
    }

    /// <summary>
    /// Refreshes all displayed values from the bound ship's current state.
    /// </summary>
    public void UpdateDisplay()
    {
        if (_ship == null || !IsInstanceValid(_ship))
        {
            ShowEmptySlot();
            return;
        }

        ShowActiveBuild();
    }

    private int _slotIndex = -1;

    private void ShowEmptySlot()
    {
        if (_shipNameLabel != null)
            _shipNameLabel.Text = "Empty Slot";

        if (_constructionProgressBar != null)
        {
            _constructionProgressBar.Value = 0;
            _constructionProgressBar.Visible = true;
        }

        ClearResourceList();

        if (_pauseButton != null)
        {
            _pauseButton.Text = "--";
            _pauseButton.Disabled = true;
        }
    }

    private void ShowActiveBuild()
    {
        if (_shipNameLabel != null)
            _shipNameLabel.Text = _ship?.ShipDef?.Name ?? _ship?.Name ?? "Unknown";

        float progress = _ship != null ? (_shipyard?.GetBuildProgress(_ship) ?? 0f) : 0f;
        if (_constructionProgressBar != null)
        {
            _constructionProgressBar.Value = progress * 100.0;
            _constructionProgressBar.Visible = true;
        }

        ClearResourceList();
        if (_resourceList != null && _ship != null && _shipyard != null)
        {
            var required = _shipyard.GetRequiredResources(_ship);
            var delivered = _shipyard.GetSecuredResources(_ship);

            foreach (var kvp in required)
            {
                string resName = kvp.Key;
                int requiredAmount = kvp.Value;
                int deliveredAmount = 0;
                if (delivered.ContainsKey(resName))
                    deliveredAmount = delivered[resName];

                var row = CreateResourceRow(resName, deliveredAmount, requiredAmount);
                _resourceList.AddChild(row);
            }
        }

        if (_pauseButton != null)
        {
            _pauseButton.Text = (_ship != null && _shipyard?.IsShipPaused(_ship) == true) ? "Resume" : "Pause";
            _pauseButton.Disabled = false;
        }
    }

    private void ClearResourceList()
    {
        if (_resourceList == null) return;
        foreach (var child in _resourceList.GetChildren())
            child.QueueFree();
    }

    private HBoxContainer CreateResourceRow(string resName, int delivered, int required)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var label = new Label
        {
            Text = $"{resName} {delivered}/{required}",
            CustomMinimumSize = new Vector2(120, 0),
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        row.AddChild(label);

        var miniBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = required,
            Value = delivered,
            CustomMinimumSize = new Vector2(50, 12),
            ShowPercentage = false,
        };
        row.AddChild(miniBar);

        return row;
    }

    private void OnPausePressed()
    {
        if (_ship == null || !IsInstanceValid(_ship) || _shipyard == null) return;

        bool newPaused = !_shipyard.IsShipPaused(_ship);
        _shipyard.SetShipPaused(_ship, newPaused);
        UpdateDisplay();
    }

    private void OnLabelInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn && btn.Pressed && btn.ButtonIndex == MouseButton.Left)
        {
            EmitSignal(SignalName.SlotSelected, _slotIndex);
        }
    }
}