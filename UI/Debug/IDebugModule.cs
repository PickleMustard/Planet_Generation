#if DEBUG

namespace UI.Debug;

/// <summary>
/// Base interface that all debug modules must implement.
/// Defines the contract for module lifecycle and visibility.
/// </summary>
public interface IDebugModule
{
    /// <summary>
    /// The display name of the module.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Whether the module is currently visible.
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Called when the module is enabled/shown.
    /// </summary>
    void OnModuleEnabled();

    /// <summary>
    /// Called when the module is disabled/hidden.
    /// </summary>
    void OnModuleDisabled();

    /// <summary>
    /// Toggles the module visibility.
    /// </summary>
    void Toggle();
}
#endif
