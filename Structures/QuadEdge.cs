namespace Structures;
public class QuadEdge
{
    Edge[] edges = new Edge[4];

    public QuadEdge()
    {
        edges[0].QuadIndex = 0;
        edges[1].QuadIndex = 1;
        edges[2].QuadIndex = 2;
        edges[3].QuadIndex = 3;

        edges[0].Next = edges[0];
        edges[1].Next = edges[3];
        edges[2].Next = edges[2];
        edges[3].Next = edges[1];

    }

    public Edge this[int key]
    {
        get { return edges[key]; }
        set { edges[key] = value; }
    }

}
