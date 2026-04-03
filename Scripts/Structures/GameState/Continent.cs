using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.MeshGeneration;


namespace Structures.GameState;

public partial class Continent : Resource
{
    /// <summary>
    /// Runtime economy for this continent. Lazily initialized when the first building is placed.
    /// </summary>
    public ContinentEconomy? Economy { get; set; }

    /// <summary>
    /// Initializes the economy for this continent if not already created,
    /// and registers it with the EconomyManager for ticking.
    /// </summary>
    public void InitializeEconomy()
    {
        if (Economy != null)
            return;

        Economy = new ContinentEconomy(this);
        EconomyManager.Instance?.RegisterEconomy(Economy);
    }

    public enum CRUST_TYPE
    {
        Continental = 0,
        Oceanic = 1
    };

    public enum BOUNDARY_TYPE
    {
        Divergent,
        Convergent,
        Transform
    }

    public int StartingIndex;
    public List<VoronoiCell> cells;
    public HashSet<VoronoiCell> boundaryCells;
    public HashSet<Edge> boundaryEdges = new HashSet<Edge>();
    public HashSet<Point> points;
    public List<Point> ConvexHull;
    public Vector3 averagedCenter;
    public Vector3 uAxis;
    public Vector3 vAxis;

    public Vector2 movementDirection;
    public float velocity;
    public float rotation;

    public CRUST_TYPE elevation;
    public float averageHeight;
    public float averageMoisture;

    public HashSet<int> neighborContinents;
    public float stressAccumulation;
    public Dictionary<int, float> neighborStress;
    public Dictionary<int, BOUNDARY_TYPE> boundaryTypes;

    /// <summary>
    /// Available resource types on this continent with their weights.
    /// Key is the resource ID, value is the selection weight.
    /// </summary>
    public Dictionary<string, float> ContinentalResources { get; set; } = new();

    /// <summary>
    /// Total resource abundance values for each resource type.
    /// Key is the resource ID, value is the total abundance.
    /// </summary>
    public Dictionary<string, float> ResourceAbundance { get; set; } = new();

    /// <summary>
    /// Aggregates resource data from all cells in this continent into
    /// ContinentalResources and ResourceAbundance for backward compatibility.
    /// </summary>
    public void AggregateResourcesFromCells()
    {
        ContinentalResources.Clear();
        ResourceAbundance.Clear();

        if (cells == null || cells.Count == 0)
            return;

        var resourceSums = new Dictionary<string, float>();
        var resourceCounts = new Dictionary<string, int>();

        foreach (var cell in cells)
        {
            foreach (var kvp in cell.Resources)
            {
                if (!resourceSums.ContainsKey(kvp.Key))
                {
                    resourceSums[kvp.Key] = 0f;
                    resourceCounts[kvp.Key] = 0;
                }
                resourceSums[kvp.Key] += kvp.Value;
                resourceCounts[kvp.Key]++;
            }
        }

        foreach (var kvp in resourceSums)
        {
            ContinentalResources[kvp.Key] = kvp.Value / resourceCounts[kvp.Key];
            ResourceAbundance[kvp.Key] = kvp.Value;
        }
    }

    public Continent(int StartingIndex, List<VoronoiCell> cells, HashSet<VoronoiCell> boundaryCells,
            HashSet<Point> points, List<Point> ConvexHull, Vector3 averagedCenter,
            Vector3 uAxis, Vector3 vAxis, Vector2 movementDirection, float velocity,
            float rotation, CRUST_TYPE elevation, float averageHeight, float averageMoisture,
            HashSet<int> neighborContinents, float stressAccumulation, Dictionary<int, float> neighborStress, Dictionary<int, BOUNDARY_TYPE> boundaryTypes)
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
        this.velocity = velocity;
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

    private string printDictionary<T, U>(Dictionary<T, U> dictionary) where T : notnull
    {
        string output = "";
        foreach (var pair in dictionary)
        {
            output += $"{pair.Key}: {pair.Value}\n";
        }
        return output;

    }
}

