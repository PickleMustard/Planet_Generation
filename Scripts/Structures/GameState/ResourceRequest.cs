using System.Collections.Generic;
using Constructables.Buildings;

namespace Structures.GameState;

public class ResourceRequest
{
    public Constructables.Building Building { get; }
    public Dictionary<string, int> MissingResources { get; }
    public int Priority { get; set; }
    public double Timestamp { get; }

    public ResourceRequest(Constructables.Building building, Dictionary<string, int> missing, int priority, double timestamp)
    {
        Building = building;
        MissingResources = missing;
        Priority = priority;
        Timestamp = timestamp;
    }
}
