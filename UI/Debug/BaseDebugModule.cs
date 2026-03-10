#if DEBUG
using Godot;

namespace UI.Debug;

/// <summary>
/// Abstract base class implementing common functionality for debug modules.
/// Provides default implementations for visibility toggling and base lifecycle.
/// Auto-registers with DebugMenu on ready.
/// </summary>
public abstract partial class BaseDebugModule : Control, IDebugModule
{
    private bool _isVisible;
    private bool _registered;

    /// <summary>
    /// The display name of the module.
    /// </summary>
    public abstract string ModuleName { get; }

    /// <summary>
    /// Whether the module is currently visible.
    /// </summary>
    public new bool IsVisible => _isVisible;

    /// <summary>
    /// Called when the module is enabled/shown.
    /// Override to implement custom enable logic.
    /// </summary>
    public virtual void OnModuleEnabled()
    {
        _isVisible = true;
        Show();
    }

    /// <summary>
    /// Called when the module is disabled/hidden.
    /// Override to implement custom disable logic.
    /// </summary>
    public virtual void OnModuleDisabled()
    {
        _isVisible = false;
        Hide();
    }

    /// <summary>
    /// Toggles the module visibility.
    /// </summary>
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

    public override void _Ready()
    {
        base._Ready();
        _isVisible = false;
        Hide();

        Name = ModuleName;
        RegisterWithDebugMenu();
    }

    private void RegisterWithDebugMenu()
    {
        if (_registered)
        {
            return;
        }

        if (DebugMenu.Instance != null)
        {
            if (GetParent() != null)
            {
                _registered = true;
                return;
            }

            DebugMenu.Instance.RegisterModule(this);
            _registered = true;
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        if (_registered && DebugMenu.Instance != null)
        {
            _registered = false;
            DebugMenu.Instance.UnregisterModule(this);
        }
    }
}
#endif
