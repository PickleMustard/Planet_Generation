#if DEBUG
using System.Collections.Generic;
using Godot;
using UI.Debug.Console;
using CellInfoModule = UI.Debug.CellInfo.CellInfo;
using DatabaseViewerModule = UI.Debug.DatabaseViewer.DatabaseViewer;

namespace UI.Debug;

/// <summary>
/// Main DebugMenu autoload singleton with DEBUG conditional compilation.
/// Serves as the container for all debug modules.
/// </summary>
public partial class DebugMenu : CanvasLayer
{
    private static DebugMenu _instance;

    public static DebugMenu Instance => _instance;

    private DebugMenuController _controller;
    private readonly List<IDebugModule> _modules = new();
    private TabContainer _tabContainer;
    private PanelContainer _debugPanel;

    public new bool IsVisible => _controller?.MenuVisible ?? false;

    public override void _EnterTree()
    {
        _instance = this;
    }

    public override void _ExitTree()
    {
        AutoRegistrationManager.Shutdown();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    public override void _Ready()
    {
        Layer = 100;

        AutoRegistrationManager.Initialize(GetTree());

        _controller = new DebugMenuController();
        AddChild(_controller);

        _tabContainer = new TabContainer
        {
            Name = "ModuleTabs",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetRight = 0,
            OffsetBottom = 0,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        _debugPanel = new PanelContainer
        {
            Name = "DebugPanel",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        _debugPanel.AddChild(_tabContainer);
        AddChild(_debugPanel);

        _controller.RegisterModule(new VisibilityToggleModule(_debugPanel));

        InitializeDefaultModules();
    }

    private void InitializeDefaultModules()
    {
        var console = new DebugConsole();
        RegisterModule(console);

        var databaseViewer = new DatabaseViewerModule();
        RegisterModule(databaseViewer);

        var cellInfo = new CellInfoModule();
        RegisterModule(cellInfo);
    }

    /// <summary>
    /// Registers a debug module with the menu.
    /// </summary>
    /// <param name="module">The module to register.</param>
    public void RegisterModule(IDebugModule module)
    {
        if (!_modules.Contains(module))
        {
            _modules.Add(module);
            _controller.RegisterModule(module);

            if (module is Control control)
            {
                _tabContainer.AddChild(control);
            }
        }
    }

    /// <summary>
    /// Unregisters a debug module from the menu.
    /// </summary>
    /// <param name="module">The module to unregister.</param>
    public void UnregisterModule(IDebugModule module)
    {
        if (_modules.Remove(module))
        {
            _controller.UnregisterModule(module);

            if (module is Control control && control.IsInsideTree())
            {
                _tabContainer.RemoveChild(control);
            }
        }
    }

    /// <summary>
    /// Toggles the debug menu.
    /// </summary>
    public void Toggle()
    {
        _controller?.ToggleMenu();
    }

    /// <summary>
    /// Shows the debug menu.
    /// </summary>
    public void ShowMenu()
    {
        _controller?.ShowMenu();
    }

    /// <summary>
    /// Hides the debug menu.
    /// </summary>
    public void HideMenu()
    {
        _controller?.HideMenu();
    }

    /// <summary>
    /// Checks if input should be consumed by the debug menu.
    /// </summary>
    /// <returns>True if input should be consumed.</returns>
    public bool ShouldConsumeInput()
    {
        return _controller?.ShouldConsumeInput() ?? false;
    }

    private class VisibilityToggleModule : IDebugModule
    {
        private readonly Control _target;
        private bool _isVisible;

        public string ModuleName => "VisibilityToggle";
        public bool IsVisible => _isVisible;

        public VisibilityToggleModule(Control target)
        {
            _target = target;
        }

        public void OnModuleEnabled()
        {
            _isVisible = true;
            _target.Show();
        }

        public void OnModuleDisabled()
        {
            _isVisible = false;
            _target.Hide();
        }

        public void Toggle()
        {
            if (_isVisible)
            {
                OnModuleDisabled();
            }
            else
            {
                OnModuleEnabled();
            }
        }
    }
}
#endif
