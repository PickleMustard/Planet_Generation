using Godot;

namespace UtilityLibrary;
public static class Randomizer
{
    public static RandomNumberGenerator rng = new RandomNumberGenerator();

    public static RandomNumberGenerator GetRandomNumberGenerator() { return rng; }
}
