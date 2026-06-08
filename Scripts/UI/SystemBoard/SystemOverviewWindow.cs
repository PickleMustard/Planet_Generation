using Godot;
using Structures.GameState;
using UI.OrbitalScheduling;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.SystemBoard;

/// <summary>
/// Full-screen, read-only System Overview overlay: a paper-shelled window hosting
/// a <see cref="SystemBoardView"/> that maps the whole star system (bodies, orbits,
/// satellites, stations, logistics units). Built entirely in code (no .tscn / HSM
/// edits), following the <c>OrbitalScheduleWindow</c> overlay pattern. Opened via
/// <see cref="ShowWindow"/> from an existing window's button.
/// </summary>
public sealed partial class SystemOverviewWindow : Control, IOverlayPanel
{
    public static SystemOverviewWindow? Instance { get; private set; }

    private static readonly Theme? PaperTheme = ResourceLoader.Load<Theme>(
        "res://UI/Theme/wireframe_paper/wireframe_paper.tres");

    private static readonly PackedScene? BoardScene =
        ResourceLoader.Load<PackedScene>("res://UI/SystemBoard/SystemBoardView.tscn");

    private const int PanelWidth = 1280;
    private const int PanelHeight = 760;

    private SystemBoardView? _board;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (PaperTheme != null) Theme = PaperTheme;
        BuildLayout();
        Hide();
    }

    private void BuildLayout()
    {
        var backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.6f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.GuiInput += OnBackdropInput;
        AddChild(backdrop);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            ThemeTypeVariation = "PaperPanel",
            CustomMinimumSize = new Vector2(PanelWidth, PanelHeight),
        };
        center.AddChild(panel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        panel.AddChild(col);

        var header = new HBoxContainer();
        var title = new Label { Text = "SYSTEM OVERVIEW", ThemeTypeVariation = "LabelHand" };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        var close = new Button { Text = "✕" };
        close.Pressed += RequestClose;
        header.AddChild(close);
        col.AddChild(header);

        var boardPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 660),
        };
        col.AddChild(boardPanel);

        _board = BoardScene?.Instantiate<SystemBoardView>();
        if (_board != null)
        {
            _board.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _board.SizeFlagsVertical = SizeFlags.ExpandFill;
            _board.SetPickMode(false);
            boardPanel.AddChild(_board);
        }
    }

    public void ShowWindow()
    {
        var container = OrbitalScheduleUiHelpers.FindSystemContainer(this)
            ?? GetTree()?.Root?.GetNodeOrNull("GameScene/system_container");
        if (container != null)
            _board?.SetSystem(container);
        else
            GameLogger.Warning("[SystemOverviewWindow] system_container not found.");
        Show();
        GameLogger.Info("[SystemOverviewWindow] Opened");
    }

    public void HideWindow()
    {
        Hide();
        GameLogger.Info("[SystemOverviewWindow] Closed");
    }

    /// <summary>This overlay has no inner stack, so Back is equivalent to Close.</summary>
    public void RequestBack() => RequestClose();

    public void RequestClose() => HideWindow();

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            RequestClose();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            RequestClose();
    }
}
