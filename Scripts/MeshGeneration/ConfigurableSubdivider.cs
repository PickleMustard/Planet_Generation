using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;
using UtilityLibrary;

using static MeshGeneration.StructureDatabase;

namespace MeshGeneration;

/// <summary>
/// A configurable mesh subdivider that can subdivide triangular faces using different vertex distribution strategies.
/// This class provides flexible subdivision capabilities for procedural mesh generation, supporting various
/// vertex distribution patterns including linear, geometric, and custom distributions.
/// </summary>
public class ConfigurableSubdivider
{
    /// <summary>
    /// The structure database used for managing points, edges, and faces during subdivision.
    /// </summary>
    private StructureDatabase StrDb;
    
    /// <summary>
    /// Dictionary mapping vertex distribution types to their corresponding vertex generators.
    /// </summary>
    private readonly Dictionary<VertexDistribution, IVertexGenerator> _generators;
    
    /// <summary>
    /// Initializes a new instance of the ConfigurableSubdivider class.
    /// </summary>
    /// <param name="db">The structure database to use for managing mesh data during subdivision.</param>
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

    /// <summary>
    /// Subdivides a triangular face into smaller faces using the specified vertex distribution strategy.
    /// </summary>
    /// <param name="face">The triangular face to subdivide.</param>
    /// <param name="verticesToGenerate">The number of vertices to generate along each edge of the face.</param>
    /// <param name="distribution">The vertex distribution strategy to use (default: Linear).</param>
    /// <returns>An array of new faces created from the subdivision process.</returns>
    /// <remarks>
    /// This method generates vertices along each edge of the face and creates interior points,
    /// then constructs new triangular faces using barycentric subdivision. The number of 
    /// resulting faces depends on the verticesToGenerate parameter.
    /// </remarks>
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

    /// <summary>
    /// Generates interior points within a triangular face using barycentric coordinates.
    /// </summary>
    /// <param name="face">The triangular face in which to generate interior points.</param>
    /// <param name="verticesToGenerate">The number of vertices to generate along each edge.</param>
    /// <returns>A list of interior points generated within the face.</returns>
    /// <remarks>
    /// This method uses barycentric coordinates to distribute points evenly within the triangular face.
    /// Points are generated in a grid pattern based on the resolution (verticesToGenerate + 1).
    /// No interior points are generated if verticesToGenerate is 2 or less.
    /// </remarks>
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

    /// <summary>
    /// Calculates a point using barycentric coordinates within a triangle defined by three vertices.
    /// </summary>
    /// <param name="a">The first vertex of the triangle.</param>
    /// <param name="b">The second vertex of the triangle.</param>
    /// <param name="c">The third vertex of the triangle.</param>
    /// <param name="u">The barycentric coordinate weight for vertex a.</param>
    /// <param name="v">The barycentric coordinate weight for vertex b.</param>
    /// <param name="w">The barycentric coordinate weight for vertex c.</param>
    /// <returns>A new point calculated using the barycentric coordinates.</returns>
    /// <remarks>
    /// Barycentric coordinates allow for interpolation within a triangle where u + v + w = 1.
    /// The resulting point is a weighted average of the three triangle vertices.
    /// </remarks>
    private Point CalculateBarycentricPoint(Point a, Point b, Point c, float u, float v, float w)
    {
        Logger.EnterFunction("CalculateBarycentricPoint", $"u={u:F3}, v={v:F3}, w={w:F3}");
        Vector3 aVec = a.ToVector3();
        Vector3 bVec = b.ToVector3();
        Vector3 cVec = c.ToVector3();
        Vector3 result = aVec * u + bVec * v + cVec * w;
        Point resultPoint = StrDb.GetOrCreatePoint(result);
        Logger.ExitFunction("CalculateBarycentricPoint", $"returned pointIndex={resultPoint.Index}");
        return resultPoint;
    }

    /// <summary>
    /// Creates new triangular faces using barycentric subdivision based on edge and interior points.
    /// </summary>
    /// <param name="face">The original face being subdivided.</param>
    /// <param name="edgePoints">Array of three lists containing points generated along each edge.</param>
    /// <param name="interiorPoints">List of points generated in the interior of the face.</param>
    /// <param name="verticesToGenerate">The number of vertices generated along each edge.</param>
    /// <returns>An array of new faces created from the subdivision.</returns>
    /// <remarks>
    /// This method handles different subdivision strategies based on the verticesToGenerate parameter:
    /// - For 1 vertex: Creates 4 new triangular faces
    /// - For 2 vertices: Creates 9 new triangular faces with a center point
    /// - For 3+ vertices: Uses CreateTriangularGrid for more complex subdivision
    /// </remarks>
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

    /// <summary>
    /// Creates a triangular grid of faces for higher resolution subdivisions (3+ vertices per edge).
    /// </summary>
    /// <param name="face">The original face being subdivided.</param>
    /// <param name="edgePoints">Array of three lists containing points generated along each edge.</param>
    /// <param name="interiorPoints">List of points generated in the interior of the face.</param>
    /// <param name="verticesToGenerate">The number of vertices generated along each edge.</param>
    /// <returns>A list of new faces created from the triangular grid subdivision.</returns>
    /// <remarks>
    /// This method uses barycentric coordinates to map all points (vertices, edge points, and interior points)
    /// to a coordinate system, then creates triangular faces by connecting adjacent points in the grid.
    /// The algorithm creates two triangles for each valid grid position to ensure complete coverage.
    /// </remarks>
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

    /// <summary>
    /// Calculates the centroid (geometric center) of a list of vertices.
    /// </summary>
    /// <param name="vertices">The list of vertices for which to calculate the centroid.</param>
    /// <returns>A new Point representing the centroid of the input vertices.</returns>
    /// <remarks>
    /// The centroid is calculated as the average position of all vertices in the list.
    /// This method is useful for finding the center point of a polygon or vertex group.
    /// </remarks>
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
