using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;
using UtilityLibrary;

using static MeshGeneration.StructureDatabase;

namespace MeshGeneration;
public class ConfigurableSubdivider
{
    private StructureDatabase StrDb;
    private readonly Dictionary<VertexDistribution, IVertexGenerator> _generators;
    public ConfigurableSubdivider(StructureDatabase db)
    {
        this.StrDb = db;
        Logger.EnterFunction("ConfigurableSubdivider::.ctor");
        _generators = new Dictionary<VertexDistribution, IVertexGenerator>(){
            { VertexDistribution.Linear, new LinearVertexGenerator() },
            { VertexDistribution.Geometric, new GeometricVertexGenerator() },
            { VertexDistribution.Custom, new LinearVertexGenerator() }
        };
        Logger.ExitFunction("ConfigurableSubdivider::.ctor");
    }

    public Face[] SubdivideFace(Face face, int verticesToGenerate, VertexDistribution distribution = VertexDistribution.Linear)
    {
        Logger.EnterFunction("SubdivideFace", $"face=({face.v[0].Index},{face.v[1].Index},{face.v[2].Index}), vToGen={verticesToGenerate}, dist={distribution}");
        if (verticesToGenerate <= 0) { Logger.ExitFunction("SubdivideFace", "returned original face[] length 1"); return new[] { face }; }
        IVertexGenerator generator = _generators[distribution];
        List<Point>[] generatedPoints = new List<Point>[3];

        for (int i = 0; i < 3; ++i)
        {
            Point start = face.v[i];
            Point end = face.v[(i + 1) % 3];
            generatedPoints[i] = generator.GenerateVertices(verticesToGenerate, start, end, StrDb).ToList();
            Logger.Info($"Generated edge points[{i}]={generatedPoints[i].Count}");
        }

        List<Point> interiorPoints = GenerateInteriorPoints(face, verticesToGenerate);
        Logger.Info($"Generated interiorPoints={interiorPoints.Count}");
        Face[] created = CreateBarycentricFaces(face, generatedPoints, interiorPoints, verticesToGenerate);
        Logger.ExitFunction("SubdivideFace", $"returned faces={created.Length}");
        return created;
    }

    private List<Point> GenerateInteriorPoints(Face face, int verticesToGenerate)
    {
        Logger.EnterFunction("GenerateInteriorPoints", $"vToGen={verticesToGenerate}");
        List<Point> interiorPoints = new List<Point>();
        if (verticesToGenerate <= 2)
        {
            Logger.ExitFunction("GenerateInteriorPoints", "returned 0 points (<=2)");
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
        Logger.ExitFunction("GenerateInteriorPoints", $"returned {interiorPoints.Count} points");
        return interiorPoints;
    }

    private Point CalculateBarycentricPoint(Point a, Point b, Point c, float u, float v, float w)
    {
        Logger.EnterFunction("CalculateBarycentricPoint", $"u={u:F3}, v={v:F3}, w={w:F3}");
        Vector3 aVec = a.ToVector3();
        Vector3 bVec = b.ToVector3();
        Vector3 cVec = c.ToVector3();
        Vector3 result = aVec * u + bVec * v + cVec * w;
        Point resultPoint = new Point(result);
        if (StrDb.VertexPoints.ContainsKey(resultPoint.Index))
        {
            resultPoint = StrDb.VertexPoints[resultPoint.Index];
        }
        else
        {
            StrDb.VertexPoints.Add(resultPoint.Index, resultPoint);
        }
        Logger.ExitFunction("CalculateBarycentricPoint", $"returned pointIndex={resultPoint.Index}");
        return resultPoint;
    }

    private Face[] CreateBarycentricFaces(Face face, List<Point>[] edgePoints, List<Point> interiorPoints, int verticesToGenerate)
    {
        Logger.EnterFunction("CreateBarycentricFaces", $"vToGen={verticesToGenerate}, interior={interiorPoints.Count}");
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
            Edge[] edges = new Edge[] {
                StrDb.AddEdge(face.v[0], edgePoints[0][0]), StrDb.AddEdge(edgePoints[0][0], edgePoints[2][1]), StrDb.AddEdge(edgePoints[2][1], face.v[0]),
                StrDb.AddEdge(edgePoints[0][0], edgePoints[0][1]),StrDb.AddEdge(edgePoints[0][1], center),StrDb.AddEdge(center, edgePoints[0][0]),
                StrDb.AddEdge(edgePoints[2][1], center),StrDb.AddEdge(center, edgePoints[2][0]), StrDb.AddEdge(edgePoints[2][0], edgePoints[2][1]),
                StrDb.AddEdge(edgePoints[0][1], face.v[1]), StrDb.AddEdge(face.v[1], edgePoints[1][0]), StrDb.AddEdge(edgePoints[1][0], edgePoints[0][1]),
                StrDb.AddEdge(center, edgePoints[0][1]), StrDb.AddEdge(edgePoints[0][1], edgePoints[1][0]), StrDb.AddEdge(edgePoints[1][0], center),
                StrDb.AddEdge(center, edgePoints[1][0]), StrDb.AddEdge(edgePoints[1][0], edgePoints[1][1]), StrDb.AddEdge(edgePoints[1][1], center),
                StrDb.AddEdge(edgePoints[1][1], edgePoints[2][0]), StrDb.AddEdge(edgePoints[2][0], center), StrDb.AddEdge(center, edgePoints[1][1]),
                StrDb.AddEdge(edgePoints[2][0], edgePoints[1][1]), StrDb.AddEdge(edgePoints[1][1], face.v[2]), StrDb.AddEdge(face.v[2], edgePoints[2][0]),
            };
            faces.Add(new Face(face.v[0], edgePoints[0][0], edgePoints[2][1], edges[0], edges[1], edges[2]));
            faces.Add(new Face(edgePoints[0][0], edgePoints[0][1], center, edges[3], edges[4], edges[5]));
            faces.Add(new Face(edgePoints[2][1], center, edgePoints[2][0], edges[6], edges[7], edges[8]));
            faces.Add(new Face(edgePoints[0][1], face.v[1], edgePoints[1][0], edges[9], edges[10], edges[11]));
            faces.Add(new Face(center, edgePoints[0][1], edgePoints[1][0], edges[12], edges[13], edges[14]));
            faces.Add(new Face(center, edgePoints[1][0], edgePoints[1][1], edges[15], edges[16], edges[17]));
            faces.Add(new Face(edgePoints[1][1], edgePoints[2][0], center, edges[18], edges[19], edges[20]));
            faces.Add(new Face(edgePoints[2][0], edgePoints[1][1], face.v[2], edges[21], edges[22], edges[23]));
            faces.Add(new Face(edgePoints[2][1], edgePoints[0][0], center, StrDb.AddEdge(edgePoints[2][1], edgePoints[0][0]), StrDb.AddEdge(edgePoints[0][0], center), StrDb.AddEdge(center, edgePoints[2][1])));
        }

        else
        {
            faces.AddRange(CreateTriangularGrid(face, edgePoints, interiorPoints, verticesToGenerate));
        }
        Logger.ExitFunction("CreateBarycentricFaces", $"returned faces={faces.Count}");
        return faces.ToArray();
    }

    private List<Face> CreateTriangularGrid(Face face, List<Point>[] edgePoints, List<Point> interiorPoints, int verticesToGenerate)
    {
        Logger.EnterFunction("CreateTriangularGrid", $"vToGen={verticesToGenerate}");
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
                    Edge e1 = StrDb.AddEdge(p1, p2);
                    Edge e2 = StrDb.AddEdge(p2, p3);
                    Edge e3 = StrDb.AddEdge(p3, p1);
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
                    Edge e1 = StrDb.AddEdge(p1, p2);
                    Edge e2 = StrDb.AddEdge(p2, p3);
                    Edge e3 = StrDb.AddEdge(p3, p1);
                    if (p1 != null && p2 != null && p3 != null)
                    {
                        faces.Add(new Face(p1, p2, p3, e1, e2, e3));
                    }
                }
            }
        }
        Logger.ExitFunction("CreateTriangularGrid", $"returned faces={faces.Count}");
        return faces;
    }

    private Point CalculateCentroid(List<Point> vertices)
    {
        Logger.EnterFunction("CalculateCentroid", $"count={vertices.Count}");
        var sum = Vector3.Zero;
        foreach (var vertex in vertices)
            sum += vertex.ToVector3();
        Point result = new Point(sum / vertices.Count);
        Logger.ExitFunction("CalculateCentroid", $"returned pointIndex={result.Index}");
        return result;
    }
}
