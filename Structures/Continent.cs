using System.Collections.Generic;
using Godot;


public class Continent
{
    public enum CRUST_TYPE
    {
        Continental = 0,
        Oceanic = 1
    };

    public enum BOUNDARY_TYPE
    {
        Divergent,    // Pulling apart - rift valleys, mid-ocean ridges
        Convergent,   // Pushing together - mountains, trenches
        Transform     // Sliding past - earthquakes
    }
    // VoronoiCell fields
    public int StartingIndex;
    public List<VoronoiCell> cells;
    public HashSet<VoronoiCell> boundaryCells;
    public HashSet<Point> points;
    public List<Point> ConvexHull;
    public Vector3 averagedCenter;
    public Vector3 uAxis;
    public Vector3 vAxis;

    // Tectonic Plate fields
    public Vector2 movementDirection;
    public float rotation;

    public CRUST_TYPE elevation;
    public float averageHeight;
    public float averageMoisture;

    // Stress accumulation fields
    public HashSet<int> neighborContinents;
    public float stressAccumulation;          // Total stress buildup
    public Dictionary<int, float> neighborStress; // Stress per neighboring continent
    public Dictionary<int, BOUNDARY_TYPE> boundaryTypes; // Type of boundary with neighbors

    public Continent(int StartingIndex, List<VoronoiCell> cells, HashSet<VoronoiCell> boundaryCells, HashSet<Point> points, List<Point> ConvexHull, Vector3 averagedCenter, Vector3 uAxis, Vector3 vAxis, Vector2 movementDirection, float rotation, CRUST_TYPE elevation, float averageHeight, float averageMoisture, HashSet<int> neighborContinents, float stressAccumulation, Dictionary<int, float> neighborStress, Dictionary<int, BOUNDARY_TYPE> boundaryTypes)
    {
        this.StartingIndex = StartingIndex;
        this.cells = cells;
        this.boundaryCells = boundaryCells;
        this.points = points;
        this.ConvexHull = ConvexHull;
        this.averagedCenter = averagedCenter;
        this.uAxis = uAxis;
        this.vAxis = vAxis;
        this.movementDirection = movementDirection;
        this.rotation = rotation;
        this.elevation = elevation;
        this.averageHeight = averageHeight;
        this.averageMoisture = averageMoisture;
        this.neighborContinents = neighborContinents;
        this.stressAccumulation = stressAccumulation;
        this.neighborStress = neighborStress;
        this.boundaryTypes = boundaryTypes;
    }

    override public string ToString()
    {
        return $"StartingIndex: {StartingIndex} | Elevation: {elevation} | Average Height: {averageHeight} | Average Moisture: {averageMoisture} | Stress Accumulation: {stressAccumulation} | Neighbor Stress: {printDictionary(neighborStress)} | Boundary Types: {printDictionary(boundaryTypes)}";
    }

    private string printDictionary<T, U>(Dictionary<T, U> dictionary)
    {
        string output = "";
        foreach (var pair in dictionary)
        {
            output += $"{pair.Key}: {pair.Value}\n";
        }
        return output;

    }
}

