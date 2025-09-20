namespace Structures;
public class Edge2D
{
    public Point2D P { get; set; }
    public Point2D Q { get; set; }
    public int Index { get; set; }

    public Edge2D(Point2D p, Point2D q, int i)
    {
        P = p;
        Q = q;
        Index = i;
    }
}
