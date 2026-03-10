#if DEBUG
using System.Collections.Generic;
using Godot;

namespace UI.Debug;

/// <summary>
/// Controller class that handles debug menu toggling via the '`' key
/// and routes input to active modules.
/// </summary>
public partial class DebugMenuController : Node
{
    private readonly List<IDebugModule> _modules = new();
    private bool _menuVisible;
    private Input.MouseModeEnum _previousMouseMode;

    public bool MenuVisible => _menuVisible;

    public override void _Ready()
    {
        SetProcessUnhandledInput(true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Quoteleft)
            {
                ToggleMenu();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    /// <summary>
    /// Registers a debug module with the controller.
    /// </summary>
    /// <param name="module">The module to register.</param>
    public void RegisterModule(IDebugModule module)
    {
        if (!_modules.Contains(module))
        {
            _modules.Add(module);
        }
    }

    /// <summary>
    /// Unregisters a debug module from the controller.
    /// </summary>
    /// <param name="module">The module to unregister.</param>
    public void UnregisterModule(IDebugModule module)
    {
        _modules.Remove(module);
    }

    /// <summary>
    /// Toggles the debug menu visibility.
    /// </summary>
    public void ToggleMenu()
    {
        _menuVisible = !_menuVisible;

        if (_menuVisible)
        {
            _previousMouseMode = Input.MouseMode;
            Input.SetMouseMode(Input.MouseModeEnum.Visible);
        }
        else
        {
            Input.SetMouseMode(_previousMouseMode);
        }

        foreach (var module in _modules)
        {
            if (_menuVisible)
            {
                module.OnModuleEnabled();
            }
            else
            {
                module.OnModuleDisabled();
            }
        }
    }

    /// <summary>
    /// Shows the debug menu.
    /// </summary>
    public void ShowMenu()
    {
        if (!_menuVisible)
        {
            ToggleMenu();
        }
    }

    /// <summary>
    /// Hides the debug menu.
    /// </summary>
    public void HideMenu()
    {
        if (_menuVisible)
        {
            ToggleMenu();
        }
    }

    /// <summary>
    /// Checks if any module is consuming input.
    /// </summary>
    /// <returns>True if input should be consumed.</returns>
    public bool ShouldConsumeInput()
    {
        return _menuVisible;
    }
}
#endif
