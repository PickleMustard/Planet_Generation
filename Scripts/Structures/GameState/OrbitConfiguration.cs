using Godot;
using UtilityLibrary;

namespace Structures.GameState;

public partial class OrbitConfiguration : Resource
{
    [Export]
    public int MaxBands { get; private set; }

    [Export]
    public float[] BandMultipliers { get; private set; }

    [Export]
    public int[] BandCapacities { get; private set; }

    private const float MinMass = 100f;

    public OrbitConfiguration()
    {
        MaxBands = 0;
        BandMultipliers = [];
        BandCapacities = [];
    }

    public OrbitConfiguration(float bodyMass, float bodyRadius)
    {
        MaxBands = CalculateMaxBands(bodyMass);
        BandMultipliers = GetDefaultBandMultipliers(MaxBands);
        BandCapacities = new int[MaxBands];
        for (int i = 0; i < MaxBands; i++)
        {
            BandCapacities[i] = GetDefaultBandCapacities(i);
        }
    }

    public OrbitConfiguration(
        float bodyMass,
        float bodyRadius,
        float[] customMultipliers,
        int[] customCapacities
    )
    {
        MaxBands = CalculateMaxBands(bodyMass);
        BandMultipliers = customMultipliers ?? GetDefaultBandMultipliers(MaxBands);
        BandCapacities = new int[MaxBands];
        for (int i = 0; i < MaxBands; i++)
        {
            BandCapacities[i] = customCapacities != null && i < customCapacities.Length
                ? customCapacities[i]
                : GetDefaultBandCapacities(i);
        }
    }

    public static int CalculateMaxBands(float bodyMass)
    {
        if (bodyMass <= MinMass)
        {
            return 0;
        }

        float logMass = (float)Mathf.Log(bodyMass);
        int maxBands = (int)Mathf.Floor(logMass) - 1;
        GD.Print(
            $"OrbitConfiguration: CalculateMaxBands | LogMass: {logMass} | MaxBands: {maxBands}"
        );
        return Mathf.Max(0, maxBands);
    }

    public static float[] GetDefaultBandMultipliers(int maxBands)
    {
        return maxBands switch
        {
            0 => [],
            1 => [1.5f],
            2 => [1.5f, 2.5f],
            3 => [1.5f, 2.5f, 4.0f],
            _ => [1.5f, 2.5f, 4.0f, 6.0f, 6.0f],
        };
    }

    public static int GetDefaultBandCapacities(int currentBandN)
    {
        return 5 * (int)Mathf.Pow(2, currentBandN);
    }

    /// <summary>
    /// Creates an orbit band with physics-based velocity calculations.
    /// </summary>
    /// <param name="index">Band index.</param>
    /// <param name="bodyRadius">Radius of the body.</param>
    /// <param name="hostMass">Mass of the host body for velocity calculation.</param>
    /// <returns>Configured OrbitBand with physics-derived speeds.</returns>
    public OrbitBand CreateOrbitBand(int index, float bodyRadius, float hostMass)
    {
        if (index < 0 || index >= BandMultipliers.Length)
        {
            GameLogger.Warning(
                $"OrbitConfiguration: Invalid band index {index}. Max bands: {MaxBands}"
            );
            return new OrbitBand(index, 1.0f, bodyRadius, 0, 0f, 0f);
        }

        float radius = bodyRadius * BandMultipliers[index];

        // Calculate physics-based angular speed: ω = sqrt(G*M/r^3)
        float angularSpeed = OrbitalParameters.CalculateAngularSpeed(hostMass, radius);

        // Calculate linear speed: v = r * ω
        float linearSpeed = radius * angularSpeed;

        return new OrbitBand(index, BandMultipliers[index], radius, BandCapacities[index], angularSpeed, linearSpeed);
    }

    /// <summary>
    /// Creates all orbit bands with physics-based velocity calculations.
    /// </summary>
    /// <param name="bodyRadius">Radius of the body.</param>
    /// <param name="hostMass">Mass of the host body for velocity calculation.</param>
    /// <returns>Array of configured OrbitBands.</returns>
    public Godot.Collections.Array<OrbitBand> CreateAllOrbitBands(float bodyRadius, float hostMass)
    {
        var bands = new Godot.Collections.Array<OrbitBand>();
        for (int i = 0; i < MaxBands; i++)
        {
            bands.Add(CreateOrbitBand(i, bodyRadius, hostMass));
        }
        return bands;
    }

    /// <summary>
    /// Creates an orbit configuration from mass, calculating physics-based orbital parameters.
    /// </summary>
    /// <param name="bodyMass">Mass of the body.</param>
    /// <param name="bodyRadius">Radius of the body.</param>
    /// <returns>Configured OrbitConfiguration.</returns>
    public static OrbitConfiguration CreateFromMass(float bodyMass, float bodyRadius = 1.0f)
    {
        return new OrbitConfiguration(bodyMass, bodyRadius);
    }

    public override string ToString()
    {
        return $"OrbitConfiguration: MaxBands={MaxBands}, BandMultipliers={BandMultipliers}, BandCapacities={BandCapacities}";
    }
}
