namespace Structures.Enums;

public enum DepartureThreshold
{
    Quarter,
    Half,
    Full
}

public static class DepartureThresholdExtensions
{
    public static float ToFraction(this DepartureThreshold threshold)
    {
        return threshold switch
        {
            DepartureThreshold.Quarter => 0.25f,
            DepartureThreshold.Half => 0.50f,
            DepartureThreshold.Full => 1.0f,
            _ => 1.0f,
        };
    }
}
