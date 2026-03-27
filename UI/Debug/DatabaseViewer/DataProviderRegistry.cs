#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Registry that discovers and manages all data providers using reflection.
/// Discovers classes tagged with [DebugData] attribute and groups them by category.
/// </summary>
public static class DataProviderRegistry
{
    private static readonly Dictionary<string, IDataProvider> _providers = new();
    private static readonly Dictionary<string, List<IDataProvider>> _providersByCategory = new();
    private static readonly Dictionary<Type, IDataProvider> _providerFactories = new();
    private static bool _initialized;

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    public static IReadOnlyDictionary<string, IDataProvider> Providers => _providers;

    /// <summary>
    /// Gets all categories with their providers.
    /// </summary>
    public static IReadOnlyDictionary<string, List<IDataProvider>> ProvidersByCategory =>
        _providersByCategory;

    /// <summary>
    /// Gets all category names.
    /// </summary>
    public static IEnumerable<string> Categories => _providersByCategory.Keys;

    /// <summary>
    /// Initializes the registry by discovering all data providers.
    /// </summary>
    public static void Initialize()
    {
        System.Console.WriteLine("Initializing data provider registry...");
        if (_initialized)
        {
            return;
        }

        DiscoverProviders();
        _initialized = true;
    }

    /// <summary>
    /// Discovers all classes with [DebugData] attribute and registers them.
    /// </summary>
    private static void DiscoverProviders()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                DiscoverProvidersInAssembly(assembly);
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var type in ex.Types.Where(t => t != null))
                {
                    TryRegisterProviderType(type!);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr(
                    $"Error discovering providers in assembly {assembly.FullName}: {ex.Message}"
                );
            }
        }
    }

    private static void DiscoverProvidersInAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            TryRegisterProviderType(type);
        }
    }

    private static void TryRegisterProviderType(Type type)
    {
        if (type == null || !type.IsClass || type.IsAbstract)
        {
            return;
        }

        var debugDataAttr = type.GetCustomAttribute<DebugDataAttribute>();
        if (debugDataAttr == null)
        {
            return;
        }

        if (typeof(IDebugDataProvider).IsAssignableFrom(type))
        {
            GD.Print($"Registering debug data provider: {type.Name}");
            RegisterDebugDataProvider(type, debugDataAttr);
        }
        else if (typeof(IDataProvider).IsAssignableFrom(type))
        {
            RegisterDataProvider(type, debugDataAttr);
        }
    }

    private static void RegisterDebugDataProvider(Type type, DebugDataAttribute attribute)
    {
        try
        {
            IDebugDataProvider? instance = null;

            instance = FindSingletonInstance(type);

            if (instance == null)
            {
                instance = Activator.CreateInstance(type) as IDebugDataProvider;
            }

            if (instance != null)
            {
                var key = $"{attribute.Category}.{instance.Name}";
                if (_providers.ContainsKey(key))
                {
                    return;
                }
                System.Console.WriteLine($"Registering provider: {instance.Name}");
                RegisterProvider(instance, attribute.Category);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr(
                $"Failed to create instance of {type.Name}: {ex.Message}\n {ex.StackTrace}"
            );
        }
    }

    private static IDebugDataProvider? FindSingletonInstance(Type type)
    {
        var instanceProperty = type.GetProperty(
            "Instance",
            System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.FlattenHierarchy
        );
        GD.Print($"Instance property: {instanceProperty!.Name}");

        if (instanceProperty != null && instanceProperty.PropertyType == type)
        {
            var instance = instanceProperty.GetValue(null) as IDebugDataProvider;
            if (instance != null)
            {
                return instance;
            }
        }

        return null;
    }

    private static void RegisterDataProvider(Type type, DebugDataAttribute attribute)
    {
        try
        {
            var instance = Activator.CreateInstance(type) as IDataProvider;
            if (instance != null)
            {
                RegisterProvider(instance, attribute.Category);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to create instance of {type.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers a provider instance manually.
    /// </summary>
    /// <param name="provider">The provider to register.</param>
    /// <param name="category">Optional category override.</param>
    public static void RegisterProvider(IDataProvider provider, string? category = null)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        var providerCategory = category ?? provider.Category;
        var key = $"{providerCategory}.{provider.Name}";

        if (_providers.ContainsKey(key))
        {
            GD.PrintErr($"Provider already registered: {key}");
            return;
        }

        _providers[key] = provider;

        if (!_providersByCategory.ContainsKey(providerCategory))
        {
            _providersByCategory[providerCategory] = new List<IDataProvider>();
        }

        _providersByCategory[providerCategory].Add(provider);
    }

    /// <summary>
    /// Unregisters a provider.
    /// </summary>
    /// <param name="provider">The provider to unregister.</param>
    public static void UnregisterProvider(IDataProvider provider)
    {
        if (provider == null)
        {
            return;
        }

        var key = _providers.FirstOrDefault(p => p.Value == provider).Key;
        if (key != null)
        {
            _providers.Remove(key);
        }

        foreach (var category in _providersByCategory.Values)
        {
            category.Remove(provider);
        }
    }

    /// <summary>
    /// Gets providers by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>List of providers in the category.</returns>
    public static List<IDataProvider> GetProvidersByCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return _providers.Values.ToList();
        }

        return _providersByCategory.TryGetValue(category, out var providers)
            ? providers.ToList()
            : new List<IDataProvider>();
    }

    /// <summary>
    /// Gets a provider by name.
    /// </summary>
    /// <param name="name">The provider name.</param>
    /// <returns>The provider or null if not found.</returns>
    public static IDataProvider? GetProvider(string name)
    {
        return _providers.Values.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Gets a provider by key (category.name).
    /// </summary>
    /// <param name="key">The full provider key.</param>
    /// <returns>The provider or null if not found.</returns>
    public static IDataProvider? GetProviderByKey(string key)
    {
        return _providers.TryGetValue(key, out var provider) ? provider : null;
    }

    /// <summary>
    /// Refreshes all providers that need refreshing.
    /// </summary>
    public static void RefreshAll()
    {
        foreach (var provider in _providers.Values)
        {
            if (provider.NeedsRefresh)
            {
                try
                {
                    provider.Refresh();
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Error refreshing provider {provider.Name}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Searches all providers for the given pattern.
    /// </summary>
    /// <param name="pattern">The search pattern (supports wildcards).</param>
    /// <returns>Dictionary of provider names to matching paths.</returns>
    public static Dictionary<string, List<string>> SearchAll(string pattern)
    {
        var results = new Dictionary<string, List<string>>();

        foreach (var provider in _providers.Values)
        {
            try
            {
                var matches = provider.Search(pattern).ToList();
                if (matches.Count > 0)
                {
                    results[provider.Name] = matches;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Error searching provider {provider.Name}: {ex.Message}");
            }
        }

        return results;
    }

    /// <summary>
    /// Clears all registered providers.
    /// </summary>
    public static void Clear()
    {
        _providers.Clear();
        _providersByCategory.Clear();
        _providerFactories.Clear();
        _initialized = false;
    }

    /// <summary>
    /// Gets the total count of registered providers.
    /// </summary>
    public static int ProviderCount => _providers.Count;

    /// <summary>
    /// Checks if the registry has been initialized.
    /// </summary>
    public static bool IsInitialized => _initialized;
}
#endif
