namespace Structures;
public static class Biome
{
    public enum BiomeType
    {
        Tundra, Icecap, Desert, Grassland, Forest, Rainforest, Taiga, Ocean, Coastal, Mountain, SandDesert, StoneDesert, Swamp, Glacier, FrozenPlain, RustedPlain, RustedMountain, RustedDesert, ScouredPlain
    }
    public enum TemperateBiomeType
    {
        Tundra, Icecap, SandDesert, StoneDesert, Grassland, Forest, Rainforest, Taiga, Ocean, Coastal, Swamp, Mountain
    }
    public enum TropicalBiomeType
    {
        Ocean, Coastal, Grassland, Swamp, Rainforest
    }
    public enum DesertBiomeType
    {
        SandDesert, StoneDesert, Tundra
    }
    public enum RustedBiomeType
    {
        RustedPlain, RustedMountain, RustedDesert
    }
    public enum IceBiomeType
    {
        FrozenPlain, Tundra, Icecap, Taiga, Glacier
    }
    public enum ScouredBiomeType
    {
        ScouredPlain
    }
}
