using Godot;
using UtilityLibrary;

namespace UI;

/// <summary>
/// Main game UI container that holds UI elements like the toast system.
/// This should be added to game scenes (GameScene, SystemGeneration) but not to menus.
/// </summary>
public partial class MainGameUI : CanvasLayer
{
    /// <summary>
    /// Singleton instance of the MainGameUI.
    /// </summary>
    public static MainGameUI? Instance { get; private set; }

    /// <summary>
    /// Reference to the toast system for easy access.
    /// </summary>
    public ToastSystem? ToastSystem { get; private set; }

    public override void _EnterTree()
    {
        if (Instance != null)
        {
            GD.PushError($"Multiple MainGameUI instances detected! Keeping first instance: {Instance.Name}, rejecting: {Name}");
            QueueFree();
            return;
        }

        Instance = this;
        GameLogger.EnterFunction(nameof(_EnterTree), $"MainGameUI instance set: {Name}");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
            GameLogger.ExitFunction(nameof(_ExitTree), "MainGameUI instance cleared");
        }
    }

    public override void _Ready()
    {
        GameLogger.EnterFunction(nameof(_Ready));
        
        // Set layer to 1 as specified
        Layer = 1;
        
        // Find and store reference to ToastSystem
        ToastSystem = GetNode<ToastSystem>("ToastSystem");
        
        if (ToastSystem == null)
        {
            GameLogger.Error("ToastSystem not found as child of MainGameUI!");
            GD.PrintErr("ToastSystem not found as child of MainGameUI. Make sure ToastSystem.tscn is instantiated in MainGameUI.tscn.");
        }
        else
        {
            GameLogger.Debug($"ToastSystem found and linked: {ToastSystem.Name}");
        }
        
        GameLogger.ExitFunction(nameof(_Ready), $"MainGameUI ready with layer {Layer}");
    }

    /// <summary>
    /// Helper method to show a toast message via the embedded toast system.
    /// </summary>
    public void ShowToast(string message, float duration = 3.0f, 
                         ToastSystem.ToastPriority priority = ToastSystem.ToastPriority.Info,
                         bool useRichText = true)
    {
        ToastSystem?.Show(message, duration, priority, useRichText);
    }

    /// <summary>
    /// Helper method to show an informational toast.
    /// </summary>
    public void ShowInfoToast(string message, float duration = 3.0f, bool useRichText = true)
    {
        ToastSystem?.ShowInfo(message, duration, useRichText);
    }

    /// <summary>
    /// Helper method to show a warning toast.
    /// </summary>
    public void ShowWarningToast(string message, float duration = 4.0f, bool useRichText = true)
    {
        ToastSystem?.ShowWarning(message, duration, useRichText);
    }

    /// <summary>
    /// Helper method to show an error toast.
    /// </summary>
    public void ShowErrorToast(string message, float duration = 5.0f, bool useRichText = true)
    {
        ToastSystem?.ShowError(message, duration, useRichText);
    }

    /// <summary>
    /// Clears all toast messages.
    /// </summary>
    public void ClearAllToasts()
    {
        ToastSystem?.ClearAll();
    }
}