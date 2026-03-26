namespace Structures.Logistics;

/// <summary>
/// Immutable bundle of the six classical Keplerian orbital elements plus mean anomaly.
/// All angular quantities are in degrees.
/// </summary>
public readonly struct ClassicalOrbitalElements
{
    /// <summary>Semi-major axis in meters.</summary>
    public float SemiMajorAxis { get; init; }

    /// <summary>Eccentricity (0 = circular, 0-1 = elliptical).</summary>
    public float Eccentricity { get; init; }

    /// <summary>Inclination relative to the XZ reference plane in degrees.</summary>
    public float InclinationDeg { get; init; }

    /// <summary>Longitude of the ascending node (RAAN) in degrees.</summary>
    public float AscendingNodeLongitudeDeg { get; init; }

    /// <summary>Argument of periapsis in degrees.</summary>
    public float ArgumentOfPeriapsisDeg { get; init; }

    /// <summary>True anomaly at epoch in degrees.</summary>
    public float TrueAnomalyDeg { get; init; }

    /// <summary>Mean anomaly at epoch in degrees.</summary>
    public float MeanAnomalyDeg { get; init; }

    public override string ToString()
    {
        return $"a={SemiMajorAxis:F2}m, e={Eccentricity:F4}, "
            + $"i={InclinationDeg:F2}°, Ω={AscendingNodeLongitudeDeg:F2}°, "
            + $"ω={ArgumentOfPeriapsisDeg:F2}°, ν={TrueAnomalyDeg:F2}°, M={MeanAnomalyDeg:F2}°";
    }
}
