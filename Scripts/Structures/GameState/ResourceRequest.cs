using System.Collections.Generic;
using Constructables.Buildings;

namespace Structures.GameState;

public class ResourceRequest
{
    public Constructables.BuildingConstruction Building { get; }
    public Dictionary<string, float> MissingResources { get; }
    public int Priority { get; set; }
    public double Timestamp { get; }

    public ResourceRequest(Constructables.BuildingConstruction building, Dictionary<string, float> missing, int priority, double timestamp)
    {
        Building = building;
        MissingResources = missing;
        Priority = priority;
        Timestamp = timestamp;
    }
}
