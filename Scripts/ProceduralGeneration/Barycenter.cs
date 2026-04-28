using Constructables;
using Godot;
using Godot.Collections;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.GameState;

public partial class Barycenter : Node3D, IOrbitalBody
{
    private partial class OrbitalBody : Resource
    {
        public Vector3 position { get; set; }
        public Vector3 velocity { get; set; }
        public float weight { get; set; }
    }

    private OrbitalBody transform = new OrbitalBody();
    private Array<OrbitalBody> entries;
    private int totalBodies;

    public Vector3 Velocity
    {
        get => transform.velocity;
        set => transform.velocity = value;
    }
    public float Radius
    {
        get => 1f;
        set => throw new System.NotImplementedException();
    }
    public float Mass
    {
        get => transform.weight;
        set => transform.weight = value;
    }
    public Vector3 BodyPosition
    {
        get => transform.position;
        set => transform.position = value;
    }
    public string BodyName
    {
        get => "Barycenter";
    }

    public Array<OrbitBand> OrbitBands => throw new System.NotImplementedException();

    public OrbitConfiguration? OrbitConfig => throw new System.NotImplementedException();

    public Node3D SatellitesContainer => throw new System.NotImplementedException();

    public BodyClassification Classification => throw new System.NotImplementedException();
    public BodyBillboardTextures BillboardTextures => throw new System.NotImplementedException();
    public UnifiedCelestialMesh Mesh => throw new System.NotImplementedException();

    public bool UsesBandPlacement => throw new System.NotImplementedException();

    public BuildingConstructionManager BuildingConstructionMgr =>
        throw new System.NotImplementedException();

    public BodyEconomyManager EconomyMgr => throw new System.NotImplementedException();

    public BodyTransferManager TransferMgr => throw new System.NotImplementedException();

    public Barycenter(Vector3 position, Vector3 velocity, float weight)
    {
        transform.position = position;
        transform.velocity = velocity;
        transform.weight = weight;
        this.entries = new Array<OrbitalBody>();
        this.totalBodies = 0;
    }

    public override void _Ready()
    {
        Name = "Barycenter";
        AddToGroup("Barycenter");
    }

    public void RegisterBody()
    {
        totalBodies++;
    }

    public void AddEntry(Vector3 position, Vector3 velocity, float weight)
    {
        OrbitalBody entry = new OrbitalBody();
        entry.position = position;
        entry.velocity = velocity;
        entry.weight = weight;
        entries.Add(entry);
        if (entries.Count == totalBodies)
        {
            Vector3 totalPosition = Vector3.Zero;
            Vector3 totalVelocity = Vector3.Zero;
            float totalWeight = 0f;
            foreach (var e in entries)
            {
                totalPosition += e.position * e.weight;
                totalVelocity += e.velocity * e.weight;
                totalWeight += e.weight;
            }
            position = totalPosition / totalWeight;
            velocity = totalVelocity / totalWeight;
            weight = totalWeight;
            entries.Clear();
        }
    }

    public void InitializeOrbitSystem()
    {
        throw new System.NotImplementedException();
    }

    public int GetBandCount()
    {
        throw new System.NotImplementedException();
    }

    public bool CanAddToBand(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    public int GetBandSatelliteCount(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    public void IncrementBandCount(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    public void DecrementBandCount(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    public OrbitalParameters GetOrbitalParametersForBand(int bandIndex, float startingAngle)
    {
        throw new System.NotImplementedException();
    }

    public OrbitalParameters GetOrbitalParametersAtRadius(float radius, float startingAngle)
    {
        throw new System.NotImplementedException();
    }

    public int GetClosestBandForApproach(float approachSpeed)
    {
        throw new System.NotImplementedException();
    }

    public float GetOrbitBandRadius(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    public float GetOrbitalSpeedForBand(int bandIndex)
    {
        throw new System.NotImplementedException();
    }
}
