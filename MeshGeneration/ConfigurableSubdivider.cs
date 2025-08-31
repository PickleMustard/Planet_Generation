using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;

namespace MeshGeneration;
public class ConfigurableSubdivider
{
    private readonly Dictionary<VertexDistribution, IVertexGenerator> _generators;
    public ConfigurableSubdivider()
    {
        _generators = new Dictionary<VertexDistribution, IVertexGenerator>(){
            { VertexDistribution.Linear, new LinearVertexGenerator() },
            { VertexDistribution.Geometric, new GeometricVertexGenerator() },
            { VertexDistribution.Custom, new LinearVertexGenerator() }
        };
    }

    public Face[] SubdivideFace(Face face, int verticesToGenerate, VertexDistribution distribution = VertexDistribution.Linear)
    {
        if (verticesToGenerate <= 0) { return new[] { face }; }
        IVertexGenerator generator = _generators[distribution];
        List<Point>[] generatedPoints = new List<Point>[3];

        for (int i = 0; i < 3; ++i)
        {
            Point start = face.v[i];
            Point end = face.v[(i + 1) % 3];
            generatedPoints[i] = generator.GenerateVertices(verticesToGenerate, start, end).ToList();
        }

        List<Point> interiorPoints = GenerateInteriorPoints(face, verticesToGenerate);
        return CreateBarycentricFaces(face, generatedPoints, interiorPoints, verticesToGenerate);
    }

    private List<Point> GenerateInteriorPoints(Face face, int verticesToGenerate)
    {
        List<Point> interiorPoints = new List<Point>();
        if (verticesToGenerate <= 2)
        {
            return interiorPoints;
        }

        int resolution = verticesToGenerate + 1;
        for (int i = 1; i < resolution; i++)
        {
            for (int j = 1; j < resolution; j++)
            {
                int k = resolution - j - i;
                if (k > 0)
                {
                    float u = (float)i / resolution;
                    float v = (float)j / resolution;
                    float w = (float)k / resolution;
                    Point newPoint = CalculateBarycentricPoint(face.v[0], face.v[1], face.v[2], u, v, w);
                    interiorPoints.Add(newPoint);
                }
            }
        }
        return interiorPoints;
    }

    private Point CalculateBarycentricPoint(Point a, Point b, Point c, float u, float v, float w)
    {
        Vector3 aVec = a.ToVector3();
        Vector3 bVec = b.ToVector3();
        Vector3 cVec = c.ToVector3();
        Vector3 result = aVec * u + bVec * v + cVec * w;
        Point resultPoint = new Point(result);
        if (GenerateDocArrayMesh.VertexPoints.ContainsKey(resultPoint.Index))
        {
            resultPoint = GenerateDocArrayMesh.VertexPoints[resultPoint.Index];
        }
        else
        {
            GenerateDocArrayMesh.VertexPoints.Add(resultPoint.Index, resultPoint);
        }
        return resultPoint;
    }

    private Face[] CreateBarycentricFaces(Face face, List<Point>[] edgePoints, List<Point> interiorPoints, int verticesToGenerate)
    {
        List<Face> faces = new List<Face>();
        if (verticesToGenerate == 1)
        {
            faces.Add(new Face(face.v[0], edgePoints[0][0], edgePoints[2][0], new Edge(face.v[0], edgePoints[0][0]), new Edge(edgePoints[0][0], edgePoints[2][0]), new Edge(edgePoints[2][0], face.v[0])));
            faces.Add(new Face(edgePoints[0][0], face.v[1], edgePoints[1][0], new Edge(edgePoints[0][0], face.v[1]), new Edge(face.v[1], edgePoints[1][0]), new Edge(edgePoints[1][0], edgePoints[0][0])));
            faces.Add(new Face(edgePoints[1][0], face.v[2], edgePoints[2][0], new Edge(edgePoints[1][0], face.v[2]), new Edge(face.v[2], edgePoints[2][0]), new Edge(edgePoints[2][0], edgePoints[1][0])));
            faces.Add(new Face(edgePoints[2][0], edgePoints[0][0], edgePoints[1][0], new Edge(edgePoints[2][0], edgePoints[0][0]), new Edge(edgePoints[0][0], edgePoints[1][0]), new Edge(edgePoints[1][0], edgePoints[2][0])));
        }
        else if (verticesToGenerate == 2)
        {
            Point center = CalculateBarycentricPoint(face.v[0], face.v[1], face.v[2], (1f / 3f), (1f / 3f), (1f / 3f));
            //PolygonRendererSDL.DrawPoint(GenerateDocArrayMesh.instance, GenerateDocArrayMesh.instance.size, center.ToVector3(), 0.1f, Colors.White);
            Edge[] edges = new Edge[] {
                Edge.AddEdge(face.v[0], edgePoints[0][0]), Edge.AddEdge(edgePoints[0][0], edgePoints[2][1]), Edge.AddEdge(edgePoints[2][1], face.v[0]),
                Edge.AddEdge(edgePoints[0][0], edgePoints[0][1]),Edge.AddEdge(edgePoints[0][1], center),Edge.AddEdge(center, edgePoints[0][0]),
                Edge.AddEdge(edgePoints[2][1], center),Edge.AddEdge(center, edgePoints[2][0]), Edge.AddEdge(edgePoints[2][0], edgePoints[2][1]),
                Edge.AddEdge(edgePoints[0][1], face.v[1]), Edge.AddEdge(face.v[1], edgePoints[1][0]), Edge.AddEdge(edgePoints[1][0], edgePoints[0][1]),
                Edge.AddEdge(center, edgePoints[0][1]), Edge.AddEdge(edgePoints[0][1], edgePoints[1][0]), Edge.AddEdge(edgePoints[1][0], center),
                Edge.AddEdge(center, edgePoints[1][0]), Edge.AddEdge(edgePoints[1][0], edgePoints[1][1]), Edge.AddEdge(edgePoints[1][1], center),
                Edge.AddEdge(edgePoints[1][1], edgePoints[2][0]), Edge.AddEdge(edgePoints[2][0], center), Edge.AddEdge(center, edgePoints[1][1]),
                Edge.AddEdge(edgePoints[2][0], edgePoints[1][1]), Edge.AddEdge(edgePoints[1][1], face.v[2]), Edge.AddEdge(face.v[2], edgePoints[2][0]),
            };
            faces.Add(new Face(face.v[0], edgePoints[0][0], edgePoints[2][1], edges[0], edges[1], edges[2]));
            faces.Add(new Face(edgePoints[0][0], edgePoints[0][1], center, edges[3], edges[4], edges[5]));
            faces.Add(new Face(edgePoints[2][1], center, edgePoints[2][0], edges[6], edges[7], edges[8]));
            faces.Add(new Face(edgePoints[0][1], face.v[1], edgePoints[1][0], edges[9], edges[10], edges[11]));
            faces.Add(new Face(center, edgePoints[0][1], edgePoints[1][0], edges[12], edges[13], edges[14]));
            faces.Add(new Face(center, edgePoints[1][0], edgePoints[1][1], edges[15], edges[16], edges[17]));
            faces.Add(new Face(edgePoints[1][1], edgePoints[2][0], center, edges[18], edges[19], edges[20]));
            faces.Add(new Face(edgePoints[2][0], edgePoints[1][1], face.v[2], edges[21], edges[22], edges[23]));
            faces.Add(new Face(edgePoints[2][1], edgePoints[0][0], center, Edge.AddEdge(edgePoints[2][1], edgePoints[0][0]), Edge.AddEdge(edgePoints[0][0], center), Edge.AddEdge(center, edgePoints[2][1])));
        }

        else
        {
            faces.AddRange(CreateTriangularGrid(face, edgePoints, interiorPoints, verticesToGenerate));
        }
        return faces.ToArray();
    }

    private List<Face> CreateTriangularGrid(Face face, List<Point>[] edgePoints, List<Point> interiorPoints, int verticesToGenerate)
    {
        List<Face> faces = new List<Face>();
        int resolution = verticesToGenerate + 1;
        Dictionary<(int, int, int), Point> barycentricMap = new Dictionary<(int, int, int), Point>();
        barycentricMap[(resolution, 0, 0)] = face.v[0];
        barycentricMap[(0, resolution, 0)] = face.v[1];
        barycentricMap[(0, 0, resolution)] = face.v[2];
        for (int i = 1; i < resolution; i++)
        {
            barycentricMap[(resolution - i, i, 0)] = edgePoints[0][i - 1];
            barycentricMap[(0, resolution - i, i)] = edgePoints[1][i - 1];
            barycentricMap[(i, 0, resolution - i)] = edgePoints[2][i - 1];
        }

        int interiorIndex = 0;
        for (int i = 1; i < resolution - 1; i++)
        {
            for (int j = 1; j < resolution - i; j++)
            {
                int k = resolution - j - i;
                if (k > 0)
                {
                    barycentricMap[(i, j, k)] = interiorPoints[interiorIndex++];
                }
            }
        }

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int k = resolution - j - i;
                if (k < 0) continue;
                if (i + 1 <= resolution && j + 1 <= resolution && k - 1 >= 0)
                {
                    Point p1 = barycentricMap[(i, j, k)];
                    Point p2 = barycentricMap[(i + 1, j, k - 1)];
                    Point p3 = barycentricMap[(i, j + 1, k - 1)];
                    Edge e1 = Edge.AddEdge(p1, p2);
                    Edge e2 = Edge.AddEdge(p2, p3);
                    Edge e3 = Edge.AddEdge(p3, p1);
                    if (p1 != null && p2 != null && p3 != null)
                    {
                        faces.Add(new Face(p1, p2, p3, e1, e2, e3));
                    }
                }
                if (i + 1 <= resolution && j - 1 >= 0 && k - 1 >= 0)
                {
                    Point p1 = barycentricMap[(i, j, k)];
                    Point p2 = barycentricMap[(i + 1, j - 1, k)];
                    Point p3 = barycentricMap[(i + 1, j, k - 1)];
                    Edge e1 = Edge.AddEdge(p1, p2);
                    Edge e2 = Edge.AddEdge(p2, p3);
                    Edge e3 = Edge.AddEdge(p3, p1);
                    if (p1 != null && p2 != null && p3 != null)
                    {
                        faces.Add(new Face(p1, p2, p3, e1, e2, e3));
                    }
                }
            }
        }
        return faces;
    }

    private Point CalculateCentroid(List<Point> vertices)
    {
        var sum = Vector3.Zero;
        foreach (var vertex in vertices)
            sum += vertex.ToVector3();
        return new Point(sum / vertices.Count);
    }
}
