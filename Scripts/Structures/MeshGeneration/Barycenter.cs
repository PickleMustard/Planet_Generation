using Godot;
using Godot.Collections;

public partial class Barycenter : Resource
{
    private partial class Transform : Resource
    {
        public Vector3 position { get; set; }
        public Vector3 velocity { get; set; }
        public float weight { get; set; }
    }

    private Transform transform = new Transform();
    private Array<Transform> entries;
    private int totalBodies;

    public float Weight
    {
        get => transform.weight;
        set => transform.weight = value;
    }
    public Vector3 Position
    {
        get => transform.position;
        set => transform.position = value;
    }
    public Vector3 Velocity
    {
        get => transform.velocity;
        set => transform.velocity = value;
    }

    public Barycenter(Vector3 position, Vector3 velocity, float weight)
    {
        transform.position = position;
        transform.velocity = velocity;
        transform.weight = weight;
        this.entries = new Array<Transform>();
        this.totalBodies = 0;
    }

    public void RegisterBody()
    {
        totalBodies++;
    }

    public void AddEntry(Vector3 position, Vector3 velocity, float weight)
    {
        Transform entry = new Transform();
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
}
