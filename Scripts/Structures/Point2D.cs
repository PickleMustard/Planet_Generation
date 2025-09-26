
using Godot;

namespace Structures;
public class Point2D : IPoint
{
    private float[] _position;
    private int _index;

    public int Index { get { return _index; } set { _index = value; } }
    public int VectorSpace { get { return 2; } }
    public float Stress { get; set; }
    public float[] Components { get { return _position; } set { _position = new float[value.Length]; value.CopyTo(_position, 0); } }
    public Vector2 Position { get { return new Vector2(_position[0], _position[1]); } set { _position[0] = value.X; _position[1] = value.Y; } }

    public Point2D(float x, float y, int i)
    {
        _position = new float[] { x, y };
        Index = i;
    }
}
