using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static Structures.Biome;

namespace Structures;
public class Point : IPoint, IEquatable<Point>
{
    private int _index;
    private float[] _position = new float[3];
    private float _stress;

    // Deterministic, collision-free mapping from quantized coordinates -> unique index
    private static readonly object IndexLock = new object();
    private static readonly Dictionary<(int ix, int iy, int iz), int> KeyToIndex = new Dictionary<(int, int, int), int>();
    private static int NextIndex = 0;

    public float[] Components { get { return _position; } set { value.CopyTo(_position, 0); } }
    public int Index { get { return _index; } set { _index = value; } }
    public int VectorSpace { get { return 3; } }
    public Vector3 Position { get { return new Vector3(_position[0], _position[1], _position[2]); } set { _position[0] = value.X; _position[1] = value.Y; _position[2] = value.Z; } }
    public HashSet<int> ContinentIndecies { get; set; }
    public float Height { get; set; }
    public Vector3 Velocity { get; set; }
    public float Stress { get { return _stress; } set { _stress = value; } }
    public bool isOnContinentBorder { get; set; }
    public float Radius { get; set; }
    public BiomeType Biome { get; set; }

    private static int QuantizeKey(float x, float y, float z)
    {
        // Normalize tiny values to 0 to avoid -0 vs +0 differences, then round
        float qx = MathF.Abs(x) < 1e-6f ? 0f : MathF.Round(x, 6);
        float qy = MathF.Abs(y) < 1e-6f ? 0f : MathF.Round(y, 6);
        float qz = MathF.Abs(z) < 1e-6f ? 0f : MathF.Round(z, 6);

        String key = $"{qx},{qy},{qz}";
        return HashCode.Combine(qx, qy, qz);
    }

    public static int DetermineIndex(float x, float y, float z)
    {
        var key = QuantizeKey(x, y, z);
        return key;
    }

    public bool Equals(Point other)
    {
        if ((Object)other == null) return false;
        return other.Index == Index;
    }

    public override bool Equals(Object obj)
    {
        return false;
    }

    public override int GetHashCode()
    {
        return this.Index.GetHashCode();
    }

    public static bool operator ==(Point p1, Point p2)
    {
        if ((Object)p1 == null || (Object)p2 == null)
            return false;
        return p1.Equals(p2);
    }

    public static bool operator !=(Point p1, Point p2)
    {
        if ((Object)p1 == null || (Object)p2 == null)
            return true;
        return !p1.Equals(p2);
    }

    public void Move(Vector3 newLocation)
    {
        Position = newLocation;
        Radius = 0;
    }

    public Point(float x, float y, float z)
    {
        Components = new float[] { x, y, z };
        Index = DetermineIndex(x, y, z);
        Radius = 0;
        ContinentIndecies = new HashSet<int>();
    }

    public Point(Vector3 v)
    {
        Position = v;
        Index = DetermineIndex(v.X, v.Y, v.Z);
        Radius = 0;
        ContinentIndecies = new HashSet<int>();
    }

    public Point(Vector3 v, int i)
    {
        Position = v;
        Index = i;
        Radius = 0;
        ContinentIndecies = new HashSet<int>();
    }

    public Point()
    {
        Position = Vector3.Zero;
        Index = DetermineIndex(0, 0, 0);
        Radius = 0;
        ContinentIndecies = new HashSet<int>();
    }

    public Point(IPoint copy)
    {
        copy.Components.CopyTo(Components, 0);
        Index = copy.Index;
        if (copy.VectorSpace == 3)
        {
            ContinentIndecies = new HashSet<int>(((Point)copy).ContinentIndecies);
            Radius = 0;
        }
    }

    public static Vector3[] ToVectors3(IEnumerable<IPoint> points) => points.Select(point => ((Point)point).ToVector3()).ToArray();
    public static Point[] ToPoints(IEnumerable<Vector3> vertices) => vertices.Select(vertex => ToPoint(vertex)).ToArray();
    public static Point ToPoint(Vector3 vertex) => new Point(vertex);
    public Vector3 ToVector3() => new Vector3(Components[0], Components[1], Components[2]);
    public Vector2 ToVector2() => new Vector2(Components[0], Components[1]);
    public Edge ReverseEdge(Edge e) { var t = e.Q; e.Q = e.P; e.P = t; return e; }

    private string printContinents()
    {
        string continents = "";
        foreach (int continentIndex in ContinentIndecies)
        {
            continents += $"{continentIndex}, ";
        }
        return continents;
    }

    public override string ToString() => $"Point: ({Index},{Components[0]},{Components[1]},{Components[2]}) Continents: {printContinents()}";
}
