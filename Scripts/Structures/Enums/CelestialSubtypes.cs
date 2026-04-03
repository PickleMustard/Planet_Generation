namespace Structures.Enums;

public enum StarSubtype
{
    MainSequence,
    RedGiant,
    WhiteDwarf,
    RedDwarf,
    BlueSupergiant,
    YellowDwarf,
    BinaryPrimary,
    VariableStar,
}

public enum RockyPlanetSubtype
{
    Scoured,
    Desert,
    Temperate,
    Tropical,
    Ocean,
    Cool,
    Ice,
    Rusted,
    Volcanic,
}

public enum GasGiantSubtype
{
    HotJupiter,
    StandardJupiter,
    ColdJupiter,
    FailedStar,
    RingedGiant,
    StormyGiant,
    PuffyGiant,
}

public enum IceGiantSubtype
{
    StandardNeptune,
    UranusType,
    MethaneGiant,
    AmmoniaGiant,
    ToxicGiant,
    SilverGiant,
    Cryovolcanic,
}

public enum DwarfPlanetSubtype
{
    IcyKuiper,
    RockyBelt,
    MixedComposition,
    Plutoid,
    CeresType,
    ScatteredDisk,
    DetachedObject,
}

public enum BlackHoleSubtype
{
    StellarMass,
    Intermediate,
    Supermassive,
    Primordial,
    Rotating,
    Charged,
}

public enum NeutronStarSubtype
{
    Pulsar,
    Magnetar,
    Isolated,
    Binary,
    Millisecond,
    XRayPulsar,
}

public enum SatelliteSubtype
{
    // Moon subtypes
    RockyMoon,
    IcyMoon,
    VolcanicMoon,
    TidallyLocked,
    CapturedAsteroid,

    // Asteroid subtypes
    Carbonaceous,
    Silicate,
    Metallic,
    IceAsteroid,

    // Comet subtypes
    ShortPeriod,
    LongPeriod,
    HalleyType,
    EnckeType,
}

public enum BeltSubtype
{
    AsteroidBelt,
    IceBelt,
    DebrisDisk,
    DustRing,
    ResonantBelt,
    ShepherdBelt,
}
