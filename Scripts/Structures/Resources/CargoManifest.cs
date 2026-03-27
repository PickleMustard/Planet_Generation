using System.Collections.Generic;
using Godot;

namespace Structures.Resources;

/// <summary>
/// Represents a cargo manifest that tracks resources being transported or stored.
/// </summary>
public class CargoManifest
{
    /// <summary>
    /// Default mass per unit of resource when no specific mass is defined.
    /// </summary>
    private const float DefaultMassPerUnit = 1.0f;

    /// <summary>
    /// Dictionary mapping resource IDs to their quantities.
    /// </summary>
    private Dictionary<string, float> _resources = new();

    /// <summary>
    /// Gets the dictionary of resources in this manifest.
    /// Resource ID mapped to quantity.
    /// </summary>
    public Dictionary<string, float> Resources => _resources;

    /// <summary>
    /// Gets the total mass of all resources in the manifest.
    /// Calculated by summing the mass of each resource quantity.
    /// </summary>
    public float TotalCargoMass
    {
        get
        {
            float total = 0.0f;
            foreach (var kvp in _resources)
            {
                total += GetResourceMass(kvp.Key) * kvp.Value;
            }
            return total;
        }
    }

    /// <summary>
    /// Gets the number of different resource types in this manifest.
    /// </summary>
    public int ResourceCount => _resources.Count;

    /// <summary>
    /// Gets the total number of resource units across all types.
    /// </summary>
    public float TotalUnits
    {
        get
        {
            float total = 0.0f;
            foreach (var quantity in _resources.Values)
            {
                total += quantity;
            }
            return total;
        }
    }

    /// <summary>
    /// Loads a resource into the manifest.
    /// </summary>
    /// <param name="resourceId">The unique identifier of the resource.</param>
    /// <param name="quantity">The quantity to add (must be positive).</param>
    /// <returns>True if the resource was loaded successfully, false otherwise.</returns>
    public bool LoadResource(string resourceId, float quantity)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            GD.PrintErr("CargoManifest: Cannot load resource with null or empty resource ID.");
            return false;
        }

        if (quantity <= 0.0f)
        {
            GD.PrintErr($"CargoManifest: Cannot load resource '{resourceId}' with non-positive quantity: {quantity}");
            return false;
        }

        if (_resources.ContainsKey(resourceId))
        {
            _resources[resourceId] += quantity;
        }
        else
        {
            _resources[resourceId] = quantity;
        }

        return true;
    }

    /// <summary>
    /// Unloads (removes) a resource from the manifest.
    /// </summary>
    /// <param name="resourceId">The unique identifier of the resource.</param>
    /// <param name="quantity">The quantity to remove (must be positive).</param>
    /// <returns>True if the resource was successfully unloaded, false if insufficient quantity or invalid input.</returns>
    public bool UnloadResource(string resourceId, float quantity)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            GD.PrintErr("CargoManifest: Cannot unload resource with null or empty resource ID.");
            return false;
        }

        if (quantity <= 0.0f)
        {
            GD.PrintErr($"CargoManifest: Cannot unload resource '{resourceId}' with non-positive quantity: {quantity}");
            return false;
        }

        if (!_resources.TryGetValue(resourceId, out float currentQuantity))
        {
            GD.PrintErr($"CargoManifest: Cannot unload resource '{resourceId}' - resource not found in manifest.");
            return false;
        }

        if (currentQuantity < quantity)
        {
            GD.PrintErr($"CargoManifest: Cannot unload {quantity} of resource '{resourceId}' - only {currentQuantity} available.");
            return false;
        }

        _resources[resourceId] -= quantity;

        if (_resources[resourceId] <= 0.0f)
        {
            _resources.Remove(resourceId);
        }

        return true;
    }

    /// <summary>
    /// Gets the quantity of a specific resource in the manifest.
    /// </summary>
    /// <param name="resourceId">The unique identifier of the resource.</param>
    /// <returns>The quantity of the resource, or 0 if not present.</returns>
    public float GetResourceQuantity(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return 0.0f;
        }

        return _resources.TryGetValue(resourceId, out float quantity) ? quantity : 0.0f;
    }

    /// <summary>
    /// Checks if a specific resource exists in the manifest.
    /// </summary>
    /// <param name="resourceId">The unique identifier of the resource.</param>
    /// <returns>True if the resource is present in the manifest.</returns>
    public bool HasResource(string resourceId)
    {
        return !string.IsNullOrEmpty(resourceId) && _resources.ContainsKey(resourceId);
    }

    /// <summary>
    /// Clears all resources from the manifest.
    /// </summary>
    public void Clear()
    {
        _resources.Clear();
    }

    /// <summary>
    /// Gets the mass of a single unit of the specified resource.
    /// Falls back to default mass if resource definition is not found.
    /// </summary>
    /// <param name="resourceId">The unique identifier of the resource.</param>
    /// <returns>The mass per unit of the resource.</returns>
    private float GetResourceMass(string resourceId)
    {
        // Try to get mass from ResourceDatabase if available
        if (ResourceDatabase.Instance.TryGetResource(resourceId, out var definition) && definition != null)
        {
            // Check if ResourceDefinition has a Mass property
            // For now, return default - this can be extended when Mass is added to ResourceDefinition
            return DefaultMassPerUnit;
        }

        return DefaultMassPerUnit;
    }

    /// <summary>
    /// Gets a string representation of the cargo manifest.
    /// </summary>
    /// <returns>A string listing all resources and their quantities.</returns>
    public override string ToString()
    {
        if (_resources.Count == 0)
        {
            return "CargoManifest (empty)";
        }

        var lines = new List<string> { $"CargoManifest (Total Mass: {TotalCargoMass:F2}, Resources: {_resources.Count})" };
        foreach (var kvp in _resources)
        {
            lines.Add($"  - {kvp.Key}: {kvp.Value:F2} (Mass: {GetResourceMass(kvp.Key) * kvp.Value:F2})");
        }

        return string.Join("\n", lines);
    }
}
