using Godot;
public struct Point : IPoint
{
    public Vector3 Position
    {
        get { return new Vector3(X, Y, Z); }
        set { X = value.X; Y = value.Y; Z = value.Z; }
    }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public int Index { get; set; }

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

    public override string ToString() => $"{X},{Y},{Z}";
}
