using System;
using Godot;
using ProceduralGeneration.PlanetGeneration;

namespace UtilityLibrary;

/// <summary>
/// Provides standardized conversion between IOrbitalBody and Godot Dictionary representations.
/// Uses typed lambda callbacks to eliminate runtime type checking at call sites.
/// </summary>
public static class OrbitalBodyConverter
{
    // Type identifier constants
    public const string CelestialBodyTypeKey = "CelestialBody";
    public const string SatelliteBodyTypeKey = "SatelliteBody";

    // Dictionary keys
    public const string BodyKey = "body";
    public const string BodyTypeKey = "body_type";

    /// <summary>
    /// Converts an IOrbitalBody to a Godot Dictionary for storage in blackboards.
    /// </summary>
    /// <param name="body">The orbital body to convert. Must not be null.</param>
    /// <returns>A Dictionary containing the body and its type identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when body is null.</exception>
    /// <exception cref="ArgumentException">Thrown when body is not CelestialBody or SatelliteBody.</exception>
    public static Godot.Collections.Dictionary ToDictionary(IOrbitalBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return body switch
        {
            CelestialBody celestialBody => new Godot.Collections.Dictionary
            {
                [BodyKey] = celestialBody,
                [BodyTypeKey] = CelestialBodyTypeKey
            },
            SatelliteBody satelliteBody => new Godot.Collections.Dictionary
            {
                [BodyKey] = satelliteBody,
                [BodyTypeKey] = SatelliteBodyTypeKey
            },
            _ => throw new ArgumentException(
                $"Unsupported IOrbitalBody type: {body.GetType().Name}. " +
                $"Only {CelestialBodyTypeKey} and {SatelliteBodyTypeKey} are supported.",
                nameof(body))
        };
    }

    /// <summary>
    /// Extracts body from Dictionary and invokes the appropriate typed callback.
    /// </summary>
    /// <param name="dict">The dictionary containing the body data.</param>
    /// <param name="onCelestialBody">Callback invoked when the body is a CelestialBody.</param>
    /// <param name="onSatelliteBody">Callback invoked when the body is a SatelliteBody.</param>
    /// <exception cref="ArgumentNullException">Thrown when dict or any callback is null.</exception>
    /// <exception cref="ArgumentException">Thrown when dictionary is malformed or contains unknown type.</exception>
    public static void FromDictionary(
        Godot.Collections.Dictionary dict,
        Action<CelestialBody> onCelestialBody,
        Action<SatelliteBody> onSatelliteBody)
    {
        ArgumentNullException.ThrowIfNull(dict);
        ArgumentNullException.ThrowIfNull(onCelestialBody);
        ArgumentNullException.ThrowIfNull(onSatelliteBody);

        if (!dict.ContainsKey(BodyKey))
        {
            throw new ArgumentException(
                $"Dictionary missing required key: {BodyKey}",
                nameof(dict));
        }

        if (!dict.ContainsKey(BodyTypeKey))
        {
            throw new ArgumentException(
                $"Dictionary missing required key: {BodyTypeKey}",
                nameof(dict));
        }

        Variant bodyVariant = dict[BodyKey];
        string typeKey = dict[BodyTypeKey].AsString();

        switch (typeKey)
        {
            case CelestialBodyTypeKey:
                if (bodyVariant.Obj is not CelestialBody celestialBody)
                {
                    throw new ArgumentException(
                        $"Dictionary claims type {CelestialBodyTypeKey} but body is {bodyVariant.Obj?.GetType().Name ?? "null"}",
                        nameof(dict));
                }
                onCelestialBody(celestialBody);
                break;

            case SatelliteBodyTypeKey:
                if (bodyVariant.Obj is not SatelliteBody satelliteBody)
                {
                    throw new ArgumentException(
                        $"Dictionary claims type {SatelliteBodyTypeKey} but body is {bodyVariant.Obj?.GetType().Name ?? "null"}",
                        nameof(dict));
                }
                onSatelliteBody(satelliteBody);
                break;

            default:
                throw new ArgumentException(
                    $"Unknown body type key: {typeKey}. " +
                    $"Expected {CelestialBodyTypeKey} or {SatelliteBodyTypeKey}.",
                    nameof(dict));
        }
    }

    /// <summary>
    /// Extracts body from Dictionary and invokes the appropriate typed callback with return value.
    /// </summary>
    /// <typeparam name="TResult">The return type from the callbacks.</typeparam>
    /// <param name="dict">The dictionary containing the body data.</param>
    /// <param name="onCelestialBody">Callback invoked when the body is a CelestialBody.</param>
    /// <param name="onSatelliteBody">Callback invoked when the body is a SatelliteBody.</param>
    /// <returns>The result from the invoked callback.</returns>
    /// <exception cref="ArgumentNullException">Thrown when dict or any callback is null.</exception>
    /// <exception cref="ArgumentException">Thrown when dictionary is malformed or contains unknown type.</exception>
    public static TResult FromDictionary<TResult>(
        Godot.Collections.Dictionary dict,
        Func<CelestialBody, TResult> onCelestialBody,
        Func<SatelliteBody, TResult> onSatelliteBody)
    {
        ArgumentNullException.ThrowIfNull(dict);
        ArgumentNullException.ThrowIfNull(onCelestialBody);
        ArgumentNullException.ThrowIfNull(onSatelliteBody);

        if (!dict.ContainsKey(BodyKey))
        {
            throw new ArgumentException(
                $"Dictionary missing required key: {BodyKey}",
                nameof(dict));
        }

        if (!dict.ContainsKey(BodyTypeKey))
        {
            throw new ArgumentException(
                $"Dictionary missing required key: {BodyTypeKey}",
                nameof(dict));
        }

        Variant bodyVariant = dict[BodyKey];
        string typeKey = dict[BodyTypeKey].AsString();

        return typeKey switch
        {
            CelestialBodyTypeKey => bodyVariant.Obj is CelestialBody celestialBody
                ? onCelestialBody(celestialBody)
                : throw new ArgumentException(
                    $"Dictionary claims type {CelestialBodyTypeKey} but body is {bodyVariant.Obj?.GetType().Name ?? "null"}",
                    nameof(dict)),

            SatelliteBodyTypeKey => bodyVariant.Obj is SatelliteBody satelliteBody
                ? onSatelliteBody(satelliteBody)
                : throw new ArgumentException(
                    $"Dictionary claims type {SatelliteBodyTypeKey} but body is {bodyVariant.Obj?.GetType().Name ?? "null"}",
                    nameof(dict)),

            _ => throw new ArgumentException(
                $"Unknown body type key: {typeKey}. " +
                $"Expected {CelestialBodyTypeKey} or {SatelliteBodyTypeKey}.",
                nameof(dict))
        };
    }

    /// <summary>
    /// Dispatches an IOrbitalBody to the appropriate typed callback without Dictionary conversion.
    /// Use when you already have the object but need typed access.
    /// </summary>
    /// <param name="body">The orbital body to dispatch. Must not be null.</param>
    /// <param name="onCelestialBody">Callback invoked when the body is a CelestialBody.</param>
    /// <param name="onSatelliteBody">Callback invoked when the body is a SatelliteBody.</param>
    /// <exception cref="ArgumentNullException">Thrown when body or any callback is null.</exception>
    /// <exception cref="ArgumentException">Thrown when body is not CelestialBody or SatelliteBody.</exception>
    public static void Match(
        IOrbitalBody body,
        Action<CelestialBody> onCelestialBody,
        Action<SatelliteBody> onSatelliteBody)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(onCelestialBody);
        ArgumentNullException.ThrowIfNull(onSatelliteBody);

        switch (body)
        {
            case CelestialBody celestialBody:
                onCelestialBody(celestialBody);
                break;

            case SatelliteBody satelliteBody:
                onSatelliteBody(satelliteBody);
                break;

            default:
                throw new ArgumentException(
                    $"Unsupported IOrbitalBody type: {body.GetType().Name}. " +
                    $"Only {CelestialBodyTypeKey} and {SatelliteBodyTypeKey} are supported.",
                    nameof(body));
        }
    }

    /// <summary>
    /// Dispatches an IOrbitalBody to the appropriate typed callback with return value.
    /// </summary>
    /// <typeparam name="TResult">The return type from the callbacks.</typeparam>
    /// <param name="body">The orbital body to dispatch. Must not be null.</param>
    /// <param name="onCelestialBody">Callback invoked when the body is a CelestialBody.</param>
    /// <param name="onSatelliteBody">Callback invoked when the body is a SatelliteBody.</param>
    /// <returns>The result from the invoked callback.</returns>
    /// <exception cref="ArgumentNullException">Thrown when body or any callback is null.</exception>
    /// <exception cref="ArgumentException">Thrown when body is not CelestialBody or SatelliteBody.</exception>
    public static TResult Match<TResult>(
        IOrbitalBody body,
        Func<CelestialBody, TResult> onCelestialBody,
        Func<SatelliteBody, TResult> onSatelliteBody)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(onCelestialBody);
        ArgumentNullException.ThrowIfNull(onSatelliteBody);

        return body switch
        {
            CelestialBody celestialBody => onCelestialBody(celestialBody),
            SatelliteBody satelliteBody => onSatelliteBody(satelliteBody),
            _ => throw new ArgumentException(
                $"Unsupported IOrbitalBody type: {body.GetType().Name}. " +
                $"Only {CelestialBodyTypeKey} and {SatelliteBodyTypeKey} are supported.",
                nameof(body))
        };
    }

    /// <summary>
    /// Sets an IOrbitalBody into a blackboard using the standardized Dictionary format.
    /// </summary>
    /// <param name="blackboard">The blackboard to set the value in. Must not be null.</param>
    /// <param name="key">The key for the blackboard entry.</param>
    /// <param name="body">The orbital body to store. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when blackboard, key, or body is null.</exception>
    /// <exception cref="ArgumentException">Thrown when body is not CelestialBody or SatelliteBody.</exception>
    public static void SetToBlackboard(Blackboard blackboard, string key, IOrbitalBody body)
    {
        ArgumentNullException.ThrowIfNull(blackboard);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(body);

        Godot.Collections.Dictionary dict = ToDictionary(body);
        blackboard.SetVar(key, dict);
    }

    /// <summary>
    /// Gets body from blackboard and dispatches to typed callbacks.
    /// </summary>
    /// <param name="blackboard">The blackboard to read from. Must not be null.</param>
    /// <param name="key">The key for the blackboard entry.</param>
    /// <param name="onCelestialBody">Callback invoked when the body is a CelestialBody.</param>
    /// <param name="onSatelliteBody">Callback invoked when the body is a SatelliteBody.</param>
    /// <exception cref="ArgumentNullException">Thrown when blackboard, key, or any callback is null.</exception>
    /// <exception cref="ArgumentException">Thrown when blackboard value is malformed or contains unknown type.</exception>
    /// <exception cref="InvalidOperationException">Thrown when key doesn't exist in blackboard.</exception>
    public static void GetFromBlackboard(
        Blackboard blackboard,
        string key,
        Action<CelestialBody> onCelestialBody,
        Action<SatelliteBody> onSatelliteBody)
    {
        ArgumentNullException.ThrowIfNull(blackboard);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(onCelestialBody);
        ArgumentNullException.ThrowIfNull(onSatelliteBody);

        Variant value = blackboard.GetVar(key);

        if (value.Obj is not Godot.Collections.Dictionary dict)
        {
            throw new InvalidOperationException(
                $"Blackboard key '{key}' does not contain a Dictionary. " +
                $"Actual type: {value.Obj?.GetType().Name ?? "null"}");
        }

        FromDictionary(dict, onCelestialBody, onSatelliteBody);
    }

    /// <summary>
    /// Gets body from blackboard and dispatches to typed callbacks with return value.
    /// </summary>
    /// <typeparam name="TResult">The return type from the callbacks.</typeparam>
    /// <param name="blackboard">The blackboard to read from. Must not be null.</param>
    /// <param name="key">The key for the blackboard entry.</param>
    /// <param name="onCelestialBody">Callback invoked when the body is a CelestialBody.</param>
    /// <param name="onSatelliteBody">Callback invoked when the body is a SatelliteBody.</param>
    /// <returns>The result from the invoked callback.</returns>
    /// <exception cref="ArgumentNullException">Thrown when blackboard, key, or any callback is null.</exception>
    /// <exception cref="ArgumentException">Thrown when blackboard value is malformed or contains unknown type.</exception>
    /// <exception cref="InvalidOperationException">Thrown when key doesn't exist in blackboard.</exception>
    public static TResult GetFromBlackboard<TResult>(
        Blackboard blackboard,
        string key,
        Func<CelestialBody, TResult> onCelestialBody,
        Func<SatelliteBody, TResult> onSatelliteBody)
    {
        ArgumentNullException.ThrowIfNull(blackboard);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(onCelestialBody);
        ArgumentNullException.ThrowIfNull(onSatelliteBody);

        Variant value = blackboard.GetVar(key);

        if (value.Obj is not Godot.Collections.Dictionary dict)
        {
            throw new InvalidOperationException(
                $"Blackboard key '{key}' does not contain a Dictionary. " +
                $"Actual type: {value.Obj?.GetType().Name ?? "null"}");
        }

        return FromDictionary(dict, onCelestialBody, onSatelliteBody);
    }

    /// <summary>
    /// Gets the type key for an IOrbitalBody without conversion.
    /// </summary>
    /// <param name="body">The orbital body. Must not be null.</param>
    /// <returns>The type key string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when body is null.</exception>
    /// <exception cref="ArgumentException">Thrown when body is not CelestialBody or SatelliteBody.</exception>
    public static string GetBodyTypeKey(IOrbitalBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return body switch
        {
            CelestialBody => CelestialBodyTypeKey,
            SatelliteBody => SatelliteBodyTypeKey,
            _ => throw new ArgumentException(
                $"Unsupported IOrbitalBody type: {body.GetType().Name}. " +
                $"Only {CelestialBodyTypeKey} and {SatelliteBodyTypeKey} are supported.",
                nameof(body))
        };
    }
}
