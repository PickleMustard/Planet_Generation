namespace Structures.MeshGeneration;

public class Edge2D
{
    private Point2D _p;
    private Point2D _q;
    private int _index;

    public Point2D P { get { return _p; } set { _p = (Point2D)value; } }
    public Point2D Q { get { return _q; } set { _q = (Point2D)value; } }
    public int Index { get { return _index; } set { _index = value; } }

    public Edge2D(Point2D p, Point2D q, int ind)
    {
        P = p;
        Q = q;
        Index = ind;
    }

}
