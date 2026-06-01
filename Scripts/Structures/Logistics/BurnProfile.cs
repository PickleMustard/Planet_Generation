using Godot;
using Structures.Enums;
using UtilityLibrary;

namespace Structures.Logistics;

/// <summary>
/// Describes the three-phase burn profile for an orbital transfer:
/// Accelerating (departure burn) → Coasting (ballistic arc) → Decelerating (arrival burn).
///
/// Built from a TrajectorySolution's departure/arrival ΔV split, the ship's engine
/// characteristics, and its total mass at transfer initiation.
///
/// High-ΔV/fast routes have longer burn phases and shorter (or zero) coast.
/// Low-ΔV/efficient routes have brief burns and a long coast on a gravity-assisted arc.
/// </summary>
public class BurnProfile
{
    // ========================================================================
    // PHASE DURATIONS
    // ========================================================================

    /// <summary>
    /// Duration of the acceleration (departure burn) phase in seconds.
    /// </summary>
    public float AccelBurnDuration { get; private set; }

    /// <summary>
    /// Duration of the coasting (engine-off, ballistic arc) phase in seconds.
    /// Zero for continuous-burn trajectories where burn time exceeds or equals TOF.
    /// </summary>
    public float CoastDuration { get; private set; }

    /// <summary>
    /// Duration of the deceleration (arrival burn) phase in seconds.
    /// </summary>
    public float DecelBurnDuration { get; private set; }

    /// <summary>
    /// Total transfer time in seconds (sum of all phases).
    /// </summary>
    public float TotalDuration { get; private set; }

    // ========================================================================
    // FUEL BUDGETS
    // ========================================================================

    /// <summary>
    /// Total fuel allocated for the acceleration phase in kg.
    /// Calculated via Tsiolkovsky using the departure ΔV and total mass at departure.
    /// </summary>
    public float AccelFuelBudget { get; private set; }

    /// <summary>
    /// Total fuel allocated for the deceleration phase in kg.
    /// Calculated via Tsiolkovsky using the arrival ΔV and mass after the departure burn.
    /// </summary>
    public float DecelFuelBudget { get; private set; }

    /// <summary>
    /// Total fuel budget for the entire transfer (accel + decel) in kg.
    /// </summary>
    public float TotalFuelBudget { get; private set; }

    // ========================================================================
    // FUEL RATES
    // ========================================================================

    /// <summary>
    /// Fuel consumption rate during the acceleration phase in kg/s.
    /// Zero if AccelBurnDuration is zero.
    /// </summary>
    public float AccelFuelRate { get; private set; }

    /// <summary>
    /// Fuel consumption rate during the deceleration phase in kg/s.
    /// Zero if DecelBurnDuration is zero.
    /// </summary>
    public float DecelFuelRate { get; private set; }

    // ========================================================================
    // PHASE BOUNDARIES (pre-computed for fast lookup)
    // ========================================================================

    /// <summary>
    /// Time at which the acceleration phase ends and coasting begins.
    /// Equal to AccelBurnDuration.
    /// </summary>
    public float AccelEndTime { get; private set; }

    /// <summary>
    /// Time at which coasting ends and deceleration begins.
    /// Equal to TotalDuration - DecelBurnDuration.
    /// </summary>
    public float DecelStartTime { get; private set; }

    // ========================================================================
    // PRIVATE CONSTRUCTOR — use Calculate() factory
    // ========================================================================

    private BurnProfile() { }

    // ========================================================================
    // FACTORY
    // ========================================================================

    /// <summary>
    /// Calculates a burn profile for the given trajectory, engine, and ship mass.
    ///
    /// Phase durations are derived from the departure/arrival ΔV split:
    ///   accelBurnTime = departureDV / (thrust / totalMass)
    ///   decelBurnTime = arrivalDV   / (thrust / totalMass)
    ///   coastTime     = TOF - accelBurnTime - decelBurnTime
    ///
    /// If total burn time exceeds TOF (continuous-burn trajectory), the burn times are
    /// scaled proportionally so they fill the entire transit with zero coast.
    ///
    /// Fuel budgets use the Tsiolkovsky rocket equation applied sequentially:
    ///   departureFuel = m₀ × (1 - e^(-departureDV / ve))
    ///   arrivalFuel   = m₁ × (1 - e^(-arrivalDV / ve))   where m₁ = m₀ - departureFuel
    /// </summary>
    /// <param name="trajectory">The trajectory solution with DepartureDeltaV and ArrivalDeltaV set.</param>
    /// <param name="engine">The ship's engine definition (provides thrust and exhaust velocity).</param>
    /// <param name="totalMass">The ship's total mass at transfer initiation (dry + fuel + cargo) in kg.</param>
    /// <returns>A fully computed BurnProfile, or null if inputs are invalid.</returns>
    public static BurnProfile? Calculate(
        TrajectorySolution trajectory,
        EngineDefinition engine,
        float totalMass
    )
    {
        if (trajectory == null)
        {
            GameLogger.Warning("BurnProfile.Calculate: Trajectory is null");
            return null;
        }

        if (engine == null)
        {
            GameLogger.Warning("BurnProfile.Calculate: Engine is null");
            return null;
        }

        if (totalMass <= 0f)
        {
            GameLogger.Warning($"BurnProfile.Calculate: Invalid total mass {totalMass}");
            return null;
        }

        float thrust = engine.EffectiveThrust;
        float exhaustVelocity = engine.ExhaustVelocity;
        float tof = trajectory.TimeOfFlight;

        if (thrust <= 0f || exhaustVelocity <= 0f || tof <= 0f)
        {
            GameLogger.Warning(
                $"BurnProfile.Calculate: Invalid engine/trajectory parameters — "
                    + $"thrust: {thrust}, ve: {exhaustVelocity}, TOF: {tof}"
            );
            return null;
        }

        float departureDv = trajectory.DepartureDeltaV;
        float arrivalDv = trajectory.ArrivalDeltaV;

        // Calculate raw burn durations from F=ma → t = ΔV / a, where a = thrust / mass
        float acceleration = thrust / totalMass;
        float rawAccelTime = departureDv / acceleration;
        float rawDecelTime = arrivalDv / acceleration;
        float rawTotalBurnTime = rawAccelTime + rawDecelTime;

        float accelTime;
        float decelTime;
        float coastTime;

        if (rawTotalBurnTime >= tof)
        {
            // Continuous-burn trajectory: burns fill the entire transit, no coast.
            // Scale burn durations proportionally to preserve the departure/arrival ratio.
            float scale = tof / rawTotalBurnTime;
            accelTime = rawAccelTime * scale;
            decelTime = rawDecelTime * scale;
            coastTime = 0f;

            GameLogger.Debug(
                $"BurnProfile.Calculate: Continuous burn — raw burn time {rawTotalBurnTime:F1}s "
                    + $"exceeds TOF {tof:F1}s, scaling by {scale:F3}"
            );
        }
        else
        {
            accelTime = rawAccelTime;
            decelTime = rawDecelTime;
            coastTime = tof - rawTotalBurnTime;
        }

        // Fuel budgets via sequential Tsiolkovsky:
        // Departure burn: fuel consumed from initial mass
        float departureFuel = CalculateFuelForBurn(departureDv, exhaustVelocity, totalMass);

        // Arrival burn: fuel consumed from mass after departure burn
        float massAfterDeparture = totalMass - departureFuel;
        float arrivalFuel = CalculateFuelForBurn(arrivalDv, exhaustVelocity, massAfterDeparture);

        float totalFuel = departureFuel + arrivalFuel;

        // Fuel rates (kg/s during each burn phase)
        float accelFuelRate = accelTime > 0f ? departureFuel / accelTime : 0f;
        float decelFuelRate = decelTime > 0f ? arrivalFuel / decelTime : 0f;

        var profile = new BurnProfile
        {
            AccelBurnDuration = accelTime,
            CoastDuration = coastTime,
            DecelBurnDuration = decelTime,
            TotalDuration = tof,
            AccelFuelBudget = departureFuel,
            DecelFuelBudget = arrivalFuel,
            TotalFuelBudget = totalFuel,
            AccelFuelRate = accelFuelRate,
            DecelFuelRate = decelFuelRate,
            AccelEndTime = accelTime,
            DecelStartTime = tof - decelTime,
        };

        GameLogger.Info(
            $"BurnProfile.Calculate: Accel {accelTime:F1}s ({departureFuel:F2}kg @ {accelFuelRate:F3}kg/s) → "
                + $"Coast {coastTime:F1}s → Decel {decelTime:F1}s ({arrivalFuel:F2}kg @ {decelFuelRate:F3}kg/s) | "
                + $"Total fuel: {totalFuel:F2}kg, TOF: {tof:F1}s"
        );

        return profile;
    }

    /// <summary>
    /// Reconstructs a burn profile from previously-saved values. Unlike <see cref="Calculate"/> this
    /// does not recompute durations/fuel from mass — it restores the exact precomputed values so a
    /// loaded transfer resumes its fuel consumption identically, independent of the ship's current
    /// (post-burn) mass.
    /// </summary>
    public static BurnProfile Restore(
        float accelBurnDuration,
        float coastDuration,
        float decelBurnDuration,
        float totalDuration,
        float accelFuelBudget,
        float decelFuelBudget,
        float totalFuelBudget,
        float accelFuelRate,
        float decelFuelRate,
        float accelEndTime,
        float decelStartTime
    )
    {
        return new BurnProfile
        {
            AccelBurnDuration = accelBurnDuration,
            CoastDuration = coastDuration,
            DecelBurnDuration = decelBurnDuration,
            TotalDuration = totalDuration,
            AccelFuelBudget = accelFuelBudget,
            DecelFuelBudget = decelFuelBudget,
            TotalFuelBudget = totalFuelBudget,
            AccelFuelRate = accelFuelRate,
            DecelFuelRate = decelFuelRate,
            AccelEndTime = accelEndTime,
            DecelStartTime = decelStartTime,
        };
    }

    // ========================================================================
    // PHASE QUERIES
    // ========================================================================

    /// <summary>
    /// Returns the transit phase at the given time into the transfer.
    /// </summary>
    /// <param name="timeInTransfer">Elapsed time since transfer start in seconds.</param>
    /// <returns>The current transit phase.</returns>
    public TransitPhase GetPhaseAtTime(float timeInTransfer)
    {
        if (timeInTransfer < AccelEndTime)
        {
            return TransitPhase.Accelerating;
        }

        if (timeInTransfer < DecelStartTime)
        {
            return TransitPhase.Coasting;
        }

        return TransitPhase.Decelerating;
    }

    /// <summary>
    /// Returns the fuel consumption rate (kg/s) at the given time into the transfer.
    /// Returns 0 during the coasting phase.
    /// </summary>
    /// <param name="timeInTransfer">Elapsed time since transfer start in seconds.</param>
    /// <returns>Fuel rate in kg/s.</returns>
    public float GetFuelRateAtTime(float timeInTransfer)
    {
        return GetPhaseAtTime(timeInTransfer) switch
        {
            TransitPhase.Accelerating => AccelFuelRate,
            TransitPhase.Decelerating => DecelFuelRate,
            _ => 0f,
        };
    }

    /// <summary>
    /// Returns a human-readable summary of the burn profile.
    /// </summary>
    public string GetDescription()
    {
        string coastInfo = CoastDuration > 0f
            ? $"Coast {CoastDuration:F1}s → "
            : "";

        return $"Accel {AccelBurnDuration:F1}s ({AccelFuelBudget:F1}kg) → "
            + $"{coastInfo}Decel {DecelBurnDuration:F1}s ({DecelFuelBudget:F1}kg) | "
            + $"Total: {TotalFuelBudget:F1}kg over {TotalDuration:F1}s";
    }

    // ========================================================================
    // PRIVATE HELPERS
    // ========================================================================

    /// <summary>
    /// Calculates fuel required for a single burn using the Tsiolkovsky rocket equation.
    /// fuel = mass × (1 - e^(-ΔV / ve))
    /// </summary>
    /// <param name="deltaV">Delta-v of the burn in m/s.</param>
    /// <param name="exhaustVelocity">Engine exhaust velocity in m/s.</param>
    /// <param name="mass">Ship mass before the burn in kg.</param>
    /// <returns>Fuel consumed in kg.</returns>
    private static float CalculateFuelForBurn(float deltaV, float exhaustVelocity, float mass)
    {
        if (deltaV <= 0f || exhaustVelocity <= 0f || mass <= 0f)
        {
            return 0f;
        }

        return mass * (1f - Mathf.Exp(-deltaV / exhaustVelocity));
    }
}
