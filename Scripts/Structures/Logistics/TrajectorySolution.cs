using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using UtilityLibrary;

namespace Structures.Logistics;

/// <summary>
/// Wraps the output from a Lambert solver, containing all parameters
/// required to execute an orbital transfer between two positions.
/// </summary>
public class TrajectorySolution
{
    /// <summary>
    /// Criteria for ranking trajectory options.
    /// </summary>
    public enum RankingCriteria
    {
        /// <summary>Lowest delta-v (most fuel efficient)</summary>
        MostEfficient = 0,
        /// <summary>Balanced trade-off between efficiency and time</summary>
        Balanced = 1,
        /// <summary>Lowest time-of-flight (quickest transfer)</summary>
        Quickest = 2
    }

    /// <summary>
    /// Required velocity vector at the start of the transfer (m/s).
    /// This is the velocity the spacecraft must have at the departure point.
    /// </summary>
    public Vector3 InitialVelocity { get; set; }

    /// <summary>
    /// Velocity vector at arrival to the destination (m/s).
    /// </summary>
    public Vector3 FinalVelocity { get; set; }

    /// <summary>
    /// Duration of the transfer in seconds.
    /// </summary>
    public float TimeOfFlight { get; set; }

    /// <summary>
    /// Total delta-v required to execute this trajectory in m/s.
    /// Calculated as the magnitude of the velocity change needed.
    /// Equal to DepartureDeltaV + ArrivalDeltaV.
    /// </summary>
    public float DeltaVRequired { get; set; }

    /// <summary>
    /// Delta-v for the departure burn in m/s.
    /// This is the velocity change needed to leave the origin orbit and enter the transfer arc.
    /// Calculated as |v_lambert_departure - v_orbital_origin|.
    /// </summary>
    public float DepartureDeltaV { get; set; }

    /// <summary>
    /// Delta-v for the arrival burn in m/s.
    /// This is the velocity change needed to leave the transfer arc and enter the destination orbit.
    /// Calculated as |v_lambert_arrival - v_orbital_destination|.
    /// </summary>
    public float ArrivalDeltaV { get; set; }

    /// <summary>
    /// Semi-major axis of the transfer orbit in meters.
    /// </summary>
    public float SemiMajorAxis { get; set; }

    /// <summary>
    /// Eccentricity of the transfer orbit (0 = circular, 0-1 = elliptical).
    /// </summary>
    public float Eccentricity { get; set; }

    /// <summary>
    /// Number of complete revolutions in the transfer orbit.
    /// 0 for direct transfers, 1 or more for multi-revolution transfers.
    /// </summary>
    public int Revolutions { get; set; }

    /// <summary>
    /// The type of transfer - Direct, MultiRev, or GravityAssist.
    /// </summary>
    public TransferType TransferType { get; set; }

    // ============ Extended Properties for Trajectory Planning ============

    /// <summary>
    /// Origin celestial body for this trajectory.
    /// </summary>
    public CelestialBody? OriginBody { get; set; }

    /// <summary>
    /// Destination celestial body for this trajectory.
    /// </summary>
    public CelestialBody? DestinationBody { get; set; }

    /// <summary>
    /// Departure time in seconds from the current time when this trajectory should begin.
    /// </summary>
    public float DepartureTime { get; set; }

    /// <summary>
    /// Fuel required to execute this trajectory in kg.
    /// </summary>
    public float FuelRequired { get; set; }

    /// <summary>
    /// Efficiency score normalized 0-1 (1 = most efficient in the set).
    /// Lower delta-v = higher score.
    /// </summary>
    public float EfficiencyScore { get; set; }

    /// <summary>
    /// Time score normalized 0-1 (1 = fastest in the set).
    /// Lower time-of-flight = higher score.
    /// </summary>
    public float TimeScore { get; set; }

    /// <summary>
    /// Combined score using geometric mean of efficiency and time.
    /// Represents balanced trade-off between the two.
    /// </summary>
    public float CombinedScore { get; set; }

    /// <summary>
    /// Gravitational parameter (μ) used for this trajectory calculation.
    /// </summary>
    public float GravitationalParameter { get; set; }

    /// <summary>
    /// Predicted position of origin at departure time.
    /// </summary>
    public Vector3 PredictedOriginPosition { get; set; }

    /// <summary>
    /// Predicted position of destination at arrival time.
    /// </summary>
    public Vector3 PredictedDestinationPosition { get; set; }

    /// <summary>
    /// Orbital velocity of the origin (ship in its orbit band) at departure time (m/s).
    /// Used to compute correct delta-v: |v_lambert - v_orbital| at each endpoint.
    /// </summary>
    public Vector3 OriginOrbitalVelocity { get; set; }

    /// <summary>
    /// Orbital velocity of the destination orbit band at arrival time (m/s).
    /// Used to compute correct delta-v: |v_lambert - v_orbital| at each endpoint.
    /// </summary>
    public Vector3 DestinationOrbitalVelocity { get; set; }

    /// <summary>
    /// The orbit band index at the origin body where the ship departs from.
    /// </summary>
    public int OriginBandIndex { get; set; } = -1;

    /// <summary>
    /// The target orbit band index at the destination body.
    /// -1 means no specific band was targeted (legacy behavior).
    /// </summary>
    public int DestinationBandIndex { get; set; } = -1;

    /// <summary>
    /// Creates a new TrajectorySolution with default values.
    /// </summary>
    public TrajectorySolution()
    {
        InitialVelocity = Vector3.Zero;
        FinalVelocity = Vector3.Zero;
        TimeOfFlight = 0f;
        DeltaVRequired = 0f;
        SemiMajorAxis = 0f;
        Eccentricity = 0f;
        Revolutions = 0;
        TransferType = TransferType.Direct;
    }

    /// <summary>
    /// Creates a new TrajectorySolution with the specified values.
    /// </summary>
    /// <param name="initialVelocity">Required velocity at departure.</param>
    /// <param name="finalVelocity">Velocity at arrival.</param>
    /// <param name="timeOfFlight">Transfer duration in seconds.</param>
    /// <param name="semiMajorAxis">Semi-major axis of transfer orbit.</param>
    /// <param name="eccentricity">Eccentricity of transfer orbit.</param>
    /// <param name="revolutions">Number of complete revolutions.</param>
    /// <param name="transferType">Type of transfer.</param>
    public TrajectorySolution(
        Vector3 initialVelocity,
        Vector3 finalVelocity,
        float timeOfFlight,
        float semiMajorAxis,
        float eccentricity,
        int revolutions,
        TransferType transferType
    )
    {
        InitialVelocity = initialVelocity;
        FinalVelocity = finalVelocity;
        TimeOfFlight = timeOfFlight;
        SemiMajorAxis = semiMajorAxis;
        Eccentricity = eccentricity;
        Revolutions = revolutions;
        TransferType = transferType;

        // Default delta-v: sum of velocity magnitudes (fallback when orbital velocities are unknown).
        // For accurate delta-v, call RecalculateDeltaV() after setting OriginOrbitalVelocity
        // and DestinationOrbitalVelocity.
        DepartureDeltaV = initialVelocity.Length();
        ArrivalDeltaV = finalVelocity.Length();
        DeltaVRequired = DepartureDeltaV + ArrivalDeltaV;
    }

    /// <summary>
    /// Recalculates delta-v using the correct formula that accounts for existing orbital velocities.
    /// ΔV = |v_lambert_departure - v_orbital_origin| + |v_lambert_arrival - v_orbital_destination|
    ///
    /// This must be called after OriginOrbitalVelocity and DestinationOrbitalVelocity are set.
    /// If orbital velocities are zero/unset, falls back to the raw velocity magnitude sum.
    /// </summary>
    public void RecalculateDeltaV()
    {
        bool hasOriginVelocity = OriginOrbitalVelocity.LengthSquared() > 1e-10f;
        bool hasDestVelocity = DestinationOrbitalVelocity.LengthSquared() > 1e-10f;

        if (!hasOriginVelocity && !hasDestVelocity)
        {
            // No orbital velocities available — split using raw Lambert velocity magnitudes
            DepartureDeltaV = InitialVelocity.Length();
            ArrivalDeltaV = FinalVelocity.Length();
            DeltaVRequired = DepartureDeltaV + ArrivalDeltaV;
            return;
        }

        // Departure burn: difference between required Lambert velocity and current orbital velocity
        DepartureDeltaV = hasOriginVelocity
            ? (InitialVelocity - OriginOrbitalVelocity).Length()
            : InitialVelocity.Length();

        // Arrival burn: difference between Lambert arrival velocity and destination orbital velocity
        ArrivalDeltaV = hasDestVelocity
            ? (FinalVelocity - DestinationOrbitalVelocity).Length()
            : FinalVelocity.Length();

        DeltaVRequired = DepartureDeltaV + ArrivalDeltaV;
    }

    /// <summary>
    /// Gets a human-readable description of this trajectory solution.
    /// </summary>
    /// <returns>A string describing the key parameters of this solution.</returns>
    public string GetDescription()
    {
        return $"Transfer: {TransferType}, TOF: {TimeOfFlight:F1}s, " +
               $"ΔV: {DeltaVRequired:F2} m/s (depart: {DepartureDeltaV:F2}, arrive: {ArrivalDeltaV:F2}), " +
               $"Revolutions: {Revolutions}, a: {SemiMajorAxis:F0}m, e: {Eccentricity:F3}";
    }

    // ============ Static Filtering and Ranking Methods ============

    /// <summary>
    /// Filters trajectory options by available delta-v budget.
    /// Removes any options that require more delta-v than the available budget.
    /// </summary>
    /// <param name="options">List of trajectory options to filter.</param>
    /// <param name="availableDeltaV">Available delta-v budget in m/s.</param>
    /// <returns>Filtered list containing only options within budget.</returns>
    public static List<TrajectorySolution> FilterByDeltaV(
        List<TrajectorySolution> options,
        float availableDeltaV
    )
    {
        if (options == null || options.Count == 0)
        {
            GameLogger.Debug("TrajectorySolution.FilterByDeltaV: No options provided, returning empty list");
            return new List<TrajectorySolution>();
        }

        if (availableDeltaV <= 0f)
        {
            GameLogger.Warning($"TrajectorySolution.FilterByDeltaV: Invalid availableDeltaV {availableDeltaV}, returning empty list");
            return new List<TrajectorySolution>();
        }

        var filtered = options.FindAll(o => o.DeltaVRequired <= availableDeltaV);

        GameLogger.Debug(
            $"TrajectorySolution.FilterByDeltaV: Filtered {options.Count} options to {filtered.Count} " +
            $"within budget of {availableDeltaV:F2} m/s"
        );

        return filtered;
    }

    /// <summary>
    /// Calculates normalized scores for all trajectory options in the list.
    /// Must be called before ranking to populate EfficiencyScore, TimeScore, and CombinedScore.
    /// </summary>
    /// <param name="options">List of trajectory options to score.</param>
    public static void CalculateScores(List<TrajectorySolution> options)
    {
        if (options == null || options.Count == 0)
        {
            return;
        }

        // Find max values for normalization
        float maxDeltaV = options.Max(o => o.DeltaVRequired);
        float maxTOF = options.Max(o => o.TimeOfFlight);

        // Avoid division by zero
        float deltaVRange = maxDeltaV - options.Min(o => o.DeltaVRequired);
        float tofRange = maxTOF - options.Min(o => o.TimeOfFlight);

        foreach (var option in options)
        {
            // Efficiency: higher delta-v = lower score (1 = best, 0 = worst)
            if (deltaVRange > 0f)
            {
                option.EfficiencyScore = 1f - ((option.DeltaVRequired - options.Min(o => o.DeltaVRequired)) / deltaVRange);
            }
            else
            {
                option.EfficiencyScore = 1f; // All options have same delta-v
            }

            // Time: higher TOF = lower score (1 = best, 0 = worst)
            if (tofRange > 0f)
            {
                option.TimeScore = 1f - ((option.TimeOfFlight - options.Min(o => o.TimeOfFlight)) / tofRange);
            }
            else
            {
                option.TimeScore = 1f; // All options have same TOF
            }

            // Combined: geometric mean for balanced trade-off
            option.CombinedScore = Mathf.Sqrt(option.EfficiencyScore * option.TimeScore);
        }

        GameLogger.Debug(
            $"TrajectorySolution.CalculateScores: Calculated scores for {options.Count} options"
        );
    }

    /// <summary>
    /// Ranks trajectory options by the specified criteria.
    /// Call CalculateScores() first to populate scores.
    /// </summary>
    /// <param name="options">List of trajectory options to rank.</param>
    /// <param name="criteria">Ranking criteria (MostEfficient, Balanced, or Quickest).</param>
    /// <returns>Sorted list from best to worst according to criteria.</returns>
    public static List<TrajectorySolution> RankBy(
        List<TrajectorySolution> options,
        RankingCriteria criteria
    )
    {
        if (options == null || options.Count == 0)
        {
            return new List<TrajectorySolution>();
        }

        // Ensure scores are calculated
        CalculateScores(options);

        var ranked = new List<TrajectorySolution>(options);

        switch (criteria)
        {
            case RankingCriteria.MostEfficient:
                // Sort by delta-v (lowest first)
                ranked.Sort((a, b) => a.DeltaVRequired.CompareTo(b.DeltaVRequired));
                break;

            case RankingCriteria.Balanced:
                // Sort by combined score (highest first)
                ranked.Sort((a, b) => b.CombinedScore.CompareTo(a.CombinedScore));
                break;

            case RankingCriteria.Quickest:
                // Sort by time-of-flight (lowest first)
                ranked.Sort((a, b) => a.TimeOfFlight.CompareTo(b.TimeOfFlight));
                break;

            default:
                GameLogger.Warning($"TrajectorySolution.RankBy: Unknown criteria {criteria}, using MostEfficient");
                ranked.Sort((a, b) => a.DeltaVRequired.CompareTo(b.DeltaVRequired));
                break;
        }

        GameLogger.Debug(
            $"TrajectorySolution.RankBy: Ranked {ranked.Count} options by {criteria}"
        );

        return ranked;
    }

    /// <summary>
    /// Gets the top N trajectory options from the list.
    /// </summary>
    /// <param name="options">List of trajectory options.</param>
    /// <param name="count">Number of top options to return.</param>
    /// <returns>List containing up to count best options.</returns>
    public static List<TrajectorySolution> GetTopOptions(
        List<TrajectorySolution> options,
        int count
    )
    {
        if (options == null || options.Count == 0)
        {
            return new List<TrajectorySolution>();
        }

        count = Mathf.Clamp(count, 1, options.Count);
        return options.GetRange(0, count);
    }

    /// <summary>
    /// Calculates the fuel required for this trajectory given the engine and ship mass.
    /// Uses the Tsiolkovsky rocket equation: Δm = m₀ × (1 - e^(-Δv/Isp×g₀))
    /// </summary>
    /// <param name="totalMass">Total mass of the ship (dry mass + fuel + cargo) in kg.</param>
    /// <param name="exhaustVelocity">Engine exhaust velocity in m/s.</param>
    /// <returns>Fuel required in kg.</returns>
    public float CalculateFuelRequired(float totalMass, float exhaustVelocity)
    {
        if (totalMass <= 0f || exhaustVelocity <= 0f)
        {
            GameLogger.Warning(
                $"TrajectorySolution.CalculateFuelRequired: Invalid parameters - " +
                $"mass: {totalMass}, exhaustVelocity: {exhaustVelocity}"
            );
            return float.MaxValue;
        }

        // Tsiolkovsky rocket equation: fuel = mass × (1 - e^(-deltaV / exhaustVelocity))
        FuelRequired = totalMass * (1f - Mathf.Exp(-DeltaVRequired / exhaustVelocity));

        return FuelRequired;
    }

    /// <summary>
    /// Gets a detailed description including scores when available.
    /// </summary>
    /// <returns>String with full trajectory details.</returns>
    public string GetDetailedDescription()
    {
        string baseDesc = GetDescription();

        if (EfficiencyScore > 0f || TimeScore > 0f || FuelRequired > 0f)
        {
            return $"{baseDesc}, Efficiency: {EfficiencyScore:P0}, Time: {TimeScore:P0}, " +
                   $"Fuel: {FuelRequired:F2}kg";
        }

        return baseDesc;
    }
}
