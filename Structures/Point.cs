using System;
using Godot;
public class Point : IPoint, IEquatable<Point>
{
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
    public int Index { get; set; }
    public bool continentBorder { get; set; }

    public bool Equals(Point other)
    {
        return other.Index == Index && other.X == X && other.Y == Y && other.Z == Z;
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
        return p1.Equals(p2);
    }

    public static bool operator !=(Point p1, Point p2)
    {
        return !p1.Equals(p2);
    }

    public void Move(Vector3 newLocation)
    {
        X = newLocation.X;
        Y = newLocation.Y;
        Z = newLocation.Z;
    }

    public Point(float x, float y, float z, int i = 0)
    {
        X = x;
        Y = y;
        Z = z;
        Index = i;
    }

    public Point(Vector3 v, int i = 0)
    {
        X = v.X;
        Y = v.Y;
        Z = v.Z;
        Index = i;
    }

    public Point()
    {
        X = 0;
        Y = 0;
        Z = 0;
        Index = 0;
    }

    public Point(IPoint copy)
    {
        X = copy.X;
        Y = copy.Y;
        Z = copy.Z;
        Index = copy.Index;
    }

    public override string ToString() => $"Point: ({Index},{X},{Y},{Z})";
}
