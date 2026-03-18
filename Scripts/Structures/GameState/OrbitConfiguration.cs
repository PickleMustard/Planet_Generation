using System;
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

    [Export]
    public float BaseOrbitalSpeed { get; private set; }

    private const float DefaultBaseOrbitalSpeed = 0.5f;
    private const float MinMass = 100f;

    public OrbitConfiguration()
    {
        MaxBands = 0;
        BandMultipliers = [];
        BandCapacities = [];
        BaseOrbitalSpeed = DefaultBaseOrbitalSpeed;
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
        BaseOrbitalSpeed = DefaultBaseOrbitalSpeed;
    }

    public OrbitConfiguration(float bodyMass, float bodyRadius, float baseOrbitalSpeed)
    {
        MaxBands = CalculateMaxBands(bodyMass);
        BandMultipliers = GetDefaultBandMultipliers(MaxBands);
        BandCapacities = new int[MaxBands];
        for (int i = 0; i < MaxBands; i++)
        {
            BandCapacities[i] = GetDefaultBandCapacities(i);
        }
        BaseOrbitalSpeed = baseOrbitalSpeed;
    }

    public OrbitConfiguration(
        float bodyMass,
        float bodyRadius,
        float[] customMultipliers,
        int[] customCapacities,
        float baseOrbitalSpeed
    )
    {
        MaxBands = CalculateMaxBands(bodyMass);
        BandMultipliers = GetDefaultBandMultipliers(MaxBands);
        BandCapacities = new int[MaxBands];
        for (int i = 0; i < MaxBands; i++)
        {
            BandCapacities[i] = GetDefaultBandCapacities(i);
        }
        BaseOrbitalSpeed = baseOrbitalSpeed;
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

    public OrbitBand CreateOrbitBand(int index, float bodyRadius)
    {
        if (index < 0 || index >= BandMultipliers.Length)
        {
            GameLogger.Warning(
                $"OrbitConfiguration: Invalid band index {index}. Max bands: {MaxBands}"
            );
            return new OrbitBand(index, 1.0f, bodyRadius, 0);
        }

        float radius = bodyRadius * BandMultipliers[index];
        return new OrbitBand(index, BandMultipliers[index], radius, BandCapacities[index]);
    }

    public Godot.Collections.Array<OrbitBand> CreateAllOrbitBands(float bodyRadius)
    {
        var bands = new Godot.Collections.Array<OrbitBand>();
        for (int i = 0; i < MaxBands; i++)
        {
            bands.Add(CreateOrbitBand(i, bodyRadius));
        }
        return bands;
    }

    public static OrbitConfiguration CreateFromMass(float bodyMass, float bodyRadius = 1.0f)
    {
        return new OrbitConfiguration(bodyMass, bodyRadius);
    }

    public override string ToString()
    {
        return $"OrbitConfiguration: MaxBands={MaxBands}, BandMultipliers={BandMultipliers}, BandCapacities={BandCapacities}, BaseOrbitalSpeed={BaseOrbitalSpeed}";
    }
}
