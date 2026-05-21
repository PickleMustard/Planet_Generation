#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;

namespace Debug.Console;

/// <summary>
/// Static class that tracks all registered object instances and their namespaces.
/// Supports singleton and node-based namespace generation.
/// </summary>
public static class InstanceRegistry
{
    private static readonly Dictionary<string, object> _instances = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<object, string> _reverseLookup = new();
    private static readonly Dictionary<Type, int> _typeCounters = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the total number of registered instances.
    /// </summary>
    public static int InstanceCount => _instances.Count;

    /// <summary>
    /// Registers an instance with an auto-generated namespace.
    /// </summary>
    /// <param name="instance">The instance to register.</param>
    /// <returns>The generated namespace.</returns>
    public static string Register(object instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        lock (_lock)
        {
            if (_reverseLookup.TryGetValue(instance, out var existingNs))
            {
                return existingNs;
            }

            var type = instance.GetType();
            var typeName = type.Name;

            if (!_typeCounters.ContainsKey(type))
            {
                _typeCounters[type] = 0;
            }

            var id = _typeCounters[type]++;
            var ns = $"{typeName}.{id}";

            _instances[ns] = instance;
            _reverseLookup[instance] = ns;

            return ns;
        }
    }

    /// <summary>
    /// Registers an instance with a specific namespace.
    /// </summary>
    /// <param name="instance">The instance to register.</param>
    /// <param name="namespace">The namespace to use.</param>
    /// <returns>The namespace.</returns>
    public static string Register(object instance, string @namespace)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        if (string.IsNullOrEmpty(@namespace))
        {
            return Register(instance);
        }

        lock (_lock)
        {
            if (_reverseLookup.TryGetValue(instance, out var existingNs))
            {
                if (existingNs == @namespace)
                {
                    return existingNs;
                }
                _instances.Remove(existingNs);
            }

            _instances[@namespace] = instance;
            _reverseLookup[instance] = @namespace;



            return @namespace;
        }
    }

    /// <summary>
    /// Registers a singleton by type name (without numeric suffix).
    /// </summary>
    /// <param name="instance">The singleton instance.</param>
    /// <returns>The namespace (type name only).</returns>
    public static string RegisterSingleton(object instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        var typeName = instance.GetType().Name;
        return Register(instance, typeName);
    }

    /// <summary>
    /// Registers a Godot Node with namespace format TypeName.NodeName.
    /// </summary>
    /// <param name="node">The node to register.</param>
    /// <returns>The generated namespace.</returns>
    public static string RegisterNode(Godot.Node node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var typeName = node.GetType().Name;
        var nodeNameStr = node.Name.ToString();
        var nodeName = string.IsNullOrEmpty(nodeNameStr) ? $"_{GetNextId(node.GetType())}" : nodeNameStr;
        var ns = $"{typeName}.{nodeName}";

        return Register(node, ns);
    }

    /// <summary>
    /// Registers a logistics unit (ship) under the Constructables.Ships namespace.
    /// </summary>
    /// <param name="ship">The logistics unit to register.</param>
    /// <returns>The generated namespace (e.g., Ships.0).</returns>
    public static string RegisterShip(object ship)
    {
        if (ship == null)
        {
            throw new ArgumentNullException(nameof(ship));
        }

        lock (_lock)
        {
            // Check if already registered
            if (_reverseLookup.TryGetValue(ship, out var existingNs))
            {
                return existingNs;
            }

            // Count existing ships
            int shipCount = _instances.Count(kvp =>
                kvp.Key.StartsWith("Ships.", StringComparison.OrdinalIgnoreCase));

            var ns = $"Ships.{shipCount}";
            _instances[ns] = ship;
            _reverseLookup[ship] = ns;

            return ns;
        }
    }

    /// <summary>
    /// Registers a station under the Constructables.Stations namespace.
    /// </summary>
    /// <param name="station">The station to register.</param>
    /// <returns>The generated namespace (e.g., Stations.0).</returns>
    public static string RegisterStation(object station)
    {
        if (station == null)
        {
            throw new ArgumentNullException(nameof(station));
        }

        lock (_lock)
        {
            // Check if already registered
            if (_reverseLookup.TryGetValue(station, out var existingNs))
            {
                return existingNs;
            }

            // Count existing stations
            int stationCount = _instances.Count(kvp =>
                kvp.Key.StartsWith("Stations.", StringComparison.OrdinalIgnoreCase));

            var ns = $"Stations.{stationCount}";
            _instances[ns] = station;
            _reverseLookup[station] = ns;

            return ns;
        }
    }

    /// <summary>
    /// Gets all registered ship namespaces.
    /// </summary>
    /// <returns>Enumerable of ship namespaces.</returns>
    public static IEnumerable<string> GetAllShipNamespaces()
    {
        lock (_lock)
        {
            return _instances.Keys
                .Where(ns => ns.StartsWith("Ships.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(ns => ns)
                .ToList();
        }
    }

    /// <summary>
    /// Gets all registered station namespaces.
    /// </summary>
    /// <returns>Enumerable of station namespaces.</returns>
    public static IEnumerable<string> GetAllStationNamespaces()
    {
        lock (_lock)
        {
            return _instances.Keys
                .Where(ns => ns.StartsWith("Stations.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(ns => ns)
                .ToList();
        }
    }

    /// <summary>
    /// Registers a continent economy under the ContinentEconomy namespace.
    /// </summary>
    /// <param name="economy">The continent economy to register.</param>
    /// <param name="continentIndex">The continent index.</param>
    /// <returns>The generated namespace (e.g., ContinentEconomy.0).</returns>
    public static string RegisterContinentEconomy(object economy, int continentIndex)
    {
        if (economy == null)
        {
            throw new ArgumentNullException(nameof(economy));
        }

        var ns = $"ContinentEconomy.{continentIndex}";
        return Register(economy, ns);
    }

    /// <summary>
    /// Registers a station economy under the StationEconomy namespace.
    /// </summary>
    /// <param name="economy">The station economy to register.</param>
    /// <param name="stationId">The station ID.</param>
    /// <returns>The generated namespace (e.g., StationEconomy.0).</returns>
    public static string RegisterStationEconomy(object economy, string stationId)
    {
        if (economy == null)
        {
            throw new ArgumentNullException(nameof(economy));
        }

        var ns = $"StationEconomy.{stationId}";
        return Register(economy, ns);
    }

    /// <summary>
    /// Gets all registered economy namespaces (both continent and station economies).
    /// </summary>
    /// <returns>Enumerable of economy namespaces.</returns>
    public static IEnumerable<string> GetAllEconomyNamespaces()
    {
        lock (_lock)
        {
            return _instances.Keys
                .Where(ns => ns.StartsWith("ContinentEconomy.", StringComparison.OrdinalIgnoreCase)
                          || ns.StartsWith("StationEconomy.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(ns => ns)
                .ToList();
        }
    }

    /// <summary>
    /// Gets the next sequential ID for a type (used for node fallback naming).
    /// </summary>
    private static int GetNextId(Type type)
    {
        lock (_lock)
        {
            if (!_typeCounters.ContainsKey(type))
            {
                _typeCounters[type] = 0;
            }
            return _typeCounters[type]++;
        }
    }

    /// <summary>
    /// Tries to get an instance by namespace.
    /// </summary>
    /// <param name="namespace">The namespace to look up.</param>
    /// <param name="instance">The found instance.</param>
    /// <returns>True if found.</returns>
    public static bool TryGetInstance(string @namespace, out object? instance)
    {
        if (string.IsNullOrEmpty(@namespace))
        {
            instance = null;
            return false;
        }

        lock (_lock)
        {
            return _instances.TryGetValue(@namespace, out instance);
        }
    }

    /// <summary>
    /// Gets the namespace for an instance.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The namespace or null if not registered.</returns>
    public static string? GetNamespace(object instance)
    {
        if (instance == null)
        {
            return null;
        }

        lock (_lock)
        {
            return _reverseLookup.TryGetValue(instance, out var ns) ? ns : null;
        }
    }

    /// <summary>
    /// Gets all namespaces for instances of a specific type.
    /// </summary>
    /// <typeparam name="T">The type to filter by.</typeparam>
    /// <returns>Enumerable of namespaces.</returns>
    public static IEnumerable<string> GetNamespaces<T>()
    {
        lock (_lock)
        {
            var type = typeof(T);
            return _instances
                .Where(kvp => type.IsAssignableFrom(kvp.Value.GetType()))
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }

    /// <summary>
    /// Gets all namespaces that start with the given prefix.
    /// </summary>
    /// <param name="prefix">The prefix to match.</param>
    /// <returns>Enumerable of matching namespaces.</returns>
    public static IEnumerable<string> GetNamespacesByPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return Enumerable.Empty<string>();
        }

        lock (_lock)
        {
            return _instances.Keys
                .Where(ns => ns.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Gets all registered namespaces.
    /// </summary>
    /// <returns>Enumerable of all namespaces.</returns>
    public static IEnumerable<string> GetAllNamespaces()
    {
        lock (_lock)
        {
            return _instances.Keys.ToList();
        }
    }

    /// <summary>
    /// Gets all registered instances.
    /// </summary>
    /// <returns>Enumerable of all instances.</returns>
    public static IEnumerable<object> GetAllInstances()
    {
        lock (_lock)
        {
            return _instances.Values.ToList();
        }
    }

    /// <summary>
    /// Unregisters an instance.
    /// </summary>
    /// <param name="instance">The instance to unregister.</param>
    /// <returns>True if the instance was registered.</returns>
    public static bool Unregister(object instance)
    {
        if (instance == null)
        {
            return false;
        }

        lock (_lock)
        {
            if (_reverseLookup.TryGetValue(instance, out var ns))
            {
                _instances.Remove(ns);
                _reverseLookup.Remove(instance);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Unregisters an instance by namespace.
    /// </summary>
    /// <param name="namespace">The namespace to unregister.</param>
    /// <returns>True if the namespace was registered.</returns>
    public static bool UnregisterByNamespace(string @namespace)
    {
        if (string.IsNullOrEmpty(@namespace))
        {
            return false;
        }

        lock (_lock)
        {
            if (_instances.TryGetValue(@namespace, out var instance))
            {
                _instances.Remove(@namespace);
                _reverseLookup.Remove(instance);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all registered instances.
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _instances.Clear();
            _reverseLookup.Clear();
            _typeCounters.Clear();
        }
    }
}
#endif
