#if DEBUG
using System;
using System.Reflection;
using Godot;
using UI.Debug.DatabaseViewer;

namespace UI.Debug.Console;

/// <summary>
/// Manages automatic registration of objects with the InstanceRegistry.
/// Handles registration of nodes on tree entry/exit and objects with [DebugData] attribute.
/// </summary>
public static class AutoRegistrationManager
{
    private static bool _initialized;
    private static SceneTree? _sceneTree;
    private static CommandRegistry? _registry;

    /// <summary>
    /// Initializes the auto-registration system. Should be called once during debug menu setup.
    /// </summary>
    /// <param name="sceneTree">The main scene tree to monitor.</param>
    public static void Initialize(SceneTree sceneTree)
    {
        if (_initialized)
        {
            return;
        }

        _sceneTree = sceneTree;
        _initialized = true;

        if (_sceneTree?.Root != null)
        {
            _sceneTree.NodeAdded += OnChildEnteredTree;
            _sceneTree.NodeRemoved += OnChildExitingTree;

            foreach (var child in _sceneTree.Root.GetChildren())
            {
                RegisterNodeRecursive(child);
            }
        }

        GD.Print("[AutoRegistrationManager] Initialized");
    }

    public static void SetRegistry(CommandRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Shuts down the auto-registration system.
    /// </summary>
    public static void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        if (_sceneTree?.Root != null)
        {
            _sceneTree.Root.ChildEnteredTree -= OnChildEnteredTree;
            _sceneTree.Root.ChildExitingTree -= OnChildExitingTree;
        }

        _sceneTree = null;
        _initialized = false;

        GD.Print("[AutoRegistrationManager] Shutdown");
    }

    /// <summary>
    /// Called when a node enters the scene tree.
    /// </summary>
    private static void OnChildEnteredTree(Node node)
    {
        RegisterNodeRecursive(node);
    }

    /// <summary>
    /// Called when a node exits the scene tree.
    /// </summary>
    private static void OnChildExitingTree(Node node)
    {
        UnregisterNodeRecursive(node);
    }

    /// <summary>
    /// Recursively registers a node and all its children.
    /// </summary>
    /// <param name="node">The node to register.</param>
    public static void RegisterNodeRecursive(Node node)
    {
        if (node == null)
        {
            return;
        }

        if (ShouldAutoRegister(node))
        {
            RegisterNode(node);
        }

        if (ShouldRegisterInstanceMethods(node))
            RegisterInstance(node);

        foreach (var child in node.GetChildren())
        {
            RegisterNodeRecursive(child);
        }
    }

    /// <summary>
    /// Recursively unregisters a node and all its children.
    /// </summary>
    /// <param name="node">The node to unregister.</param>
    public static void UnregisterNodeRecursive(Node node)
    {
        if (node == null)
        {
            return;
        }

        InstanceRegistry.Unregister(node);

        foreach (var child in node.GetChildren())
        {
            UnregisterNodeRecursive(child);
        }
    }

    /// <summary>
    /// Registers a single node with the appropriate namespace format.
    /// Also registers with DataProviderRegistry if the node implements IDebugDataProvider.
    /// </summary>
    /// <param name="node">The node to register.</param>
    /// <returns>The namespace assigned to the node.</returns>
    public static string? RegisterNode(Node node)
    {
        if (node == null)
        {
            return null;
        }

        TryRegisterAsDataProvider(node);

        if (IsSingleton(node))
        {
            return InstanceRegistry.RegisterSingleton(node);
        }

        return InstanceRegistry.RegisterNode(node);
    }

    public static void RegisterInstance(object instance)
    {
        if (instance == null)
            return;

        if (_registry != null)
            _registry!.ScanInstance(instance);
    }

    /// <summary>
    /// Attempts to register a node with DataProviderRegistry if it implements IDebugDataProvider.
    /// </summary>
    /// <param name="node">The node to potentially register.</param>
    private static void TryRegisterAsDataProvider(Node node)
    {
        if (node is not IDebugDataProvider provider)
        {
            return;
        }

        var debugDataAttr = GetDebugDataAttribute(node);
        if (debugDataAttr == null)
        {
            return;
        }

        DataProviderRegistry.RegisterProvider(provider, debugDataAttr.Category);
    }

    /// <summary>
    /// Unregisters a single node.
    /// </summary>
    /// <param name="node">The node to unregister.</param>
    /// <returns>True if the node was unregistered.</returns>
    public static bool UnregisterNode(Node node)
    {
        return InstanceRegistry.Unregister(node);
    }

    /// <summary>
    /// Registers a non-node object with the InstanceRegistry.
    /// Objects with [DebugData] attribute will be auto-registered.
    /// </summary>
    /// <param name="obj">The object to register.</param>
    /// <returns>The namespace assigned to the object.</returns>
    public static string? RegisterObject(object obj)
    {
        if (obj == null)
        {
            return null;
        }

        if (obj is Node node)
        {
            return RegisterNode(node);
        }

        return InstanceRegistry.Register(obj);
    }

    /// <summary>
    /// Checks if a node should be auto-registered.
    /// </summary>
    /// <param name="node">The node to check.</param>
    /// <returns>True if the node should be auto-registered.</returns>
    public static bool ShouldAutoRegister(Node node)
    {
        if (node == null)
        {
            return false;
        }

        var type = node.GetType();
        var debugDataAttr =
            Attribute.GetCustomAttribute(type, typeof(DebugDataAttribute)) as DebugDataAttribute;

        return debugDataAttr != null;
    }

    public static bool ShouldRegisterInstanceMethods(Node node)
    {
        if (node == null)
            return false;

        var type = node.GetType();
        var methods = type.GetMethods(
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
        );
        DebugCommandAttribute? debugCommandAttr = null;
        foreach (var method in methods)
        {
            debugCommandAttr = method.GetCustomAttribute<DebugCommandAttribute>();
            if (debugCommandAttr != null)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if a node is a singleton (autoload).
    /// </summary>
    /// <param name="node">The node to check.</param>
    /// <returns>True if the node is an autoload singleton.</returns>
    private static bool IsSingleton(Node node)
    {
        if (node == null || _sceneTree?.Root == null)
        {
            return false;
        }

        if (node.GetParent() != _sceneTree.Root)
        {
            return false;
        }

        var autoloads = _sceneTree.Root.GetChildren();
        foreach (var child in autoloads)
        {
            if (child == node)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if an object type has the [DebugData] attribute.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>True if the object has the [DebugData] attribute.</returns>
    public static bool HasDebugDataAttribute(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        var type = obj.GetType();
        return Attribute.IsDefined(type, typeof(DebugDataAttribute));
    }

    /// <summary>
    /// Gets the DebugDataAttribute from an object type if present.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>The DebugDataAttribute or null.</returns>
    public static DebugDataAttribute? GetDebugDataAttribute(object obj)
    {
        if (obj == null)
        {
            return null;
        }

        var type = obj.GetType();
        return Attribute.GetCustomAttribute(type, typeof(DebugDataAttribute)) as DebugDataAttribute;
    }
}
#endif
