using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static Structures.Biome;

namespace Structures;
public class Point : IPoint, IEquatable<Point>
{
    public static int DetermineIndex(float x, float y, float z)
    {
        int ix = BitConverter.SingleToInt32Bits(MathF.Round(x, 6));
        int iy = BitConverter.SingleToInt32Bits(MathF.Round(y, 6));
        int iz = BitConverter.SingleToInt32Bits(MathF.Round(z, 6));
        return HashCode.Combine(ix, iy, iz);
    }
    public Vector3 Position
    {
        get { return new Vector3(X, Y, Z); }
        set { X = value.X; Y = value.Y; Z = value.Z; }
    }
    public float Height { get; set; }
    public Vector3 Velocity { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public Edge Edge { get; set; }
    public float Stress { get; set; }
    public int Index { get; set; }
    public bool continentBorder { get; set; }
    public float Radius { get; set; }
    public HashSet<int> ContinentIndices { get; set; } = new HashSet<int>();
    public BiomeType Biome
    {
        get;
        set;
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
        X = newLocation.X;
        Y = newLocation.Y;
        Z = newLocation.Z;
        Radius = 0;
    }

    public Point(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
        Index = DetermineIndex(x, y, z);
        Radius = 0;
    }

    public Point(Vector3 v)
    {
        X = v.X;
        Y = v.Y;
        Z = v.Z;
        Index = DetermineIndex(v.X, v.Y, v.Z);
        Radius = 0;
    }

    public Point(Vector3 v, int i)
    {
        X = v.X;
        Y = v.Y;
        Z = v.Z;
        Index = i;
        Radius = 0;
    }

    public Point()
    {
        X = 0;
        Y = 0;
        Z = 0;
        Index = DetermineIndex(0, 0, 0);
        Radius = 0;
    }

    public Point(IPoint copy)
    {
        X = copy.X;
        Y = copy.Y;
        Z = copy.Z;
        Index = copy.Index;
        Radius = 0;
    }

    public static Vector3[] ToVectors3(IEnumerable<Point> points) => points.Select(point => point.ToVector3()).ToArray();
    public static Point[] ToPoints(IEnumerable<Vector3> vertices) => vertices.Select(vertex => ToPoint(vertex)).ToArray();
    public static Point ToPoint(Vector3 vertex) => new Point(vertex);
    public Vector3 ToVector3() => new Vector3(X, Y, Z);
    public Vector2 ToVector2() => new Vector2(X, Y);

    public override string ToString() => $"Point: ({Index},{X},{Y},{Z})";
}
