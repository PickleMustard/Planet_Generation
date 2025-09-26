using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Structures;
using UtilityLibrary;

namespace MeshGeneration;
public class BaseMeshGeneration
{
    private static int currentIndex = 0;
    static private float TAU = (1 + (float)Math.Sqrt(5)) / 2;
    private RandomNumberGenerator rand;
    private int subdivide;
    private int[] VerticesPerEdge;
    private ConfigurableSubdivider _subdivider;

    private int VertexIndex = 0;
    private List<Vector3> normals;
    private List<Vector2> uvs;
    private List<int> indices;
    private List<Face> faces;
    private StructureDatabase StrDb;

    public BaseMeshGeneration(RandomNumberGenerator rand, StructureDatabase StrDb, int subdivide, int[] VerticesPerEdge)
    {
        Logger.EnterFunction("BaseMeshGeneration::.ctor", $"subdivide={subdivide}, VPE=[{string.Join(",", VerticesPerEdge ?? Array.Empty<int>())}]");
        this.rand = rand;
        this.StrDb = StrDb;
        this.subdivide = subdivide;
        this.VerticesPerEdge = VerticesPerEdge;

        _subdivider = new ConfigurableSubdivider(StrDb);

        normals = new List<Vector3>();
        uvs = new List<Vector2>();
        indices = new List<int>();
        faces = new List<Face>();
        Logger.ExitFunction("BaseMeshGeneration::.ctor");
    }

    public void PopulateArrays()
    {
        Logger.EnterFunction("PopulateArrays");
        List<Point> cartesionPoints = new List<Point> {
                        new Point(new Vector3(0, 1, TAU)),
                        new Point( new Vector3(0, -1, TAU)),
                        new Point( new Vector3(0, -1, -TAU)),
                        new Point( new Vector3(0, 1, -TAU)),
                        new Point(new Vector3(1, TAU, 0)),
                        new Point( new Vector3(-1, TAU, 0)),
                        new Point( new Vector3(-1, -TAU, 0)),
                        new Point( new Vector3(1, -TAU, 0)),
                        new Point(new Vector3(TAU, 0, 1)),
                        new Point( new Vector3(TAU, 0, -1)),
                        new Point( new Vector3(-TAU, 0, -1)),
                        new Point( new Vector3(-TAU, 0, 1))
        };
        VertexIndex = 12;
        normals = new List<Vector3>();
        uvs = new List<Vector2>();
        foreach (Point p in cartesionPoints)
        {
            p.Position = p.Position.Normalized() * 10f;
            normals.Add(new Vector3(p.Position.X, p.Position.Y, p.Position.Z));
            Logger.Point($"Point: {p}");
            StrDb.VertexPoints.Add(p.Index, p);
        }
        faces = new List<Face>();
        indices = new List<int> {
      0, 5, 4,
      0, 11, 5,
      0, 4, 8,
      0, 8, 1,
      0, 1, 11,
      3, 4, 5,
      3, 5, 10,
      3, 9, 4,
      3, 10, 2,
      3, 2, 9,
      10, 5, 11,
      10, 11, 6,
      8, 4, 9,
      8, 9, 7,
      1, 7, 6,
      1, 6, 11,
      1, 8, 7,
      2, 10, 6,
      2, 7, 9,
      2, 6, 7,
    };
        for (int i = 0; i < indices.Count; i += 3)
        {
            faces.Add(new Face(cartesionPoints[indices[i]], cartesionPoints[indices[i + 1]], cartesionPoints[indices[i + 2]],
                        new Edge(cartesionPoints[indices[i]], cartesionPoints[indices[i + 1]]),
                        new Edge(cartesionPoints[indices[i + 1]], cartesionPoints[indices[i + 2]]),
                        new Edge(cartesionPoints[indices[i + 2]], cartesionPoints[indices[i]])));
        }
        Logger.Info($"PopulateArrays: vertices={StrDb.VertexPoints.Count}, faces={faces.Count}, indices={indices.Count}");
        Logger.ExitFunction("PopulateArrays");
    }


    public void GenerateNonDeformedFaces(VertexDistribution distribution = VertexDistribution.Linear)
    {
        Logger.EnterFunction("GenerateNonDeformedFaces", $"subdivide={subdivide}, distribution={distribution}");
        List<Face> tempFaces = new List<Face>();
        for (int level = 0; level < subdivide; level++)
        {
            var verticesToGenerate = level < VerticesPerEdge.Length ? VerticesPerEdge[level] : VerticesPerEdge[VerticesPerEdge.Length - 1];
            Logger.Info($"Subdivide level {level + 1}/{subdivide}: verticesToGenerate={verticesToGenerate}");
            foreach (Face face in faces)
            {
                var generated = _subdivider.SubdivideFace(face, verticesToGenerate, distribution);
                tempFaces.AddRange(generated);
            }
            faces.Clear();
            StrDb.Edges.Clear();
            faces = new List<Face>(tempFaces);
            tempFaces.Clear();
            Logger.Info($"After level {level + 1}: faces={faces.Count}, edges={StrDb.Edges.Count}");
        }
        Logger.ExitFunction("GenerateNonDeformedFaces");
    }

    public void GenerateTriangleList()
    {
        Logger.EnterFunction("GenerateTriangleList");
        int added = 0;
        foreach (Face f in faces)
        {
            List<Point> points = f.v.ToList();
            List<Edge> edges = f.e.ToList();
            Triangle newTri = new Triangle(StrDb.BaseTris.Count, points, edges);
            StrDb.BaseTris.Add((f.v[0], f.v[1], f.v[2]), newTri);
            foreach (Edge edge in f.e)
            {
                if (!StrDb.EdgeTriangles.ContainsKey(edge) || StrDb.EdgeTriangles[edge] == null) StrDb.EdgeTriangles[edge] = new List<Triangle>();
                StrDb.EdgeTriangles[edge].Add(newTri);
                Point edgeP = (Point)edge.P;
                Point edgeQ = (Point)edge.Q;
                if (!StrDb.HalfEdgesFrom.ContainsKey(edgeP))
                {
                    StrDb.HalfEdgesFrom.Add(edgeP, new HashSet<Edge>());
                    StrDb.HalfEdgesFrom[edgeP].Add(edge);
                }
                else
                {
                    StrDb.HalfEdgesFrom[edgeP].Add(edge);
                }
                if (!StrDb.HalfEdgesTo.ContainsKey(edgeQ))
                {
                    StrDb.HalfEdgesTo.Add(edgeQ, new HashSet<Edge>());
                    StrDb.HalfEdgesTo[edgeQ].Add(edge);
                }
                else
                {
                    StrDb.HalfEdgesTo[edgeQ].Add(edge);
                }
            }
            added++;
        }
        Logger.Info($"GenerateTriangleList: added={added} triangles, totalEdges={StrDb.Edges.Count}, BaseTris={StrDb.BaseTris.Count}");
        Logger.ExitFunction("GenerateTriangleList");
    }

    public void InitiateDeformation(int numDeformationCycles, int numAbberations, float optimalSideLength)
    {
        Logger.EnterFunction("InitiateDeformation", $"cycles={numDeformationCycles}, abberations={numAbberations}, optimalSideLength={optimalSideLength}");
        HashSet<Point> usedPoints = new HashSet<Point>();
        Task[] deformationPasses = new Task[numDeformationCycles];
        for (int deforms = 0; deforms < numDeformationCycles; deforms++)
        {
            Task firstPass = Task.Factory.StartNew(() => DeformMesh(numAbberations, optimalSideLength));
            deformationPasses[deforms] = firstPass;
        }
        Task.WaitAll(deformationPasses);
        Logger.ExitFunction("InitiateDeformation");
    }

    private void DeformMesh(int numAbberations, float optimalSideLength)
    {
        try
        {
            Logger.EnterFunction("DeformMesh", $"optimalSideLength={optimalSideLength}");
            int alteredIndex = 0;
            for (int abberation = 0; abberation < numAbberations; abberation++)
            {
                Point randomPoint = StrDb.SelectRandomPoint(rand);
                HashSet<Edge> edgesWithPointFrom = StrDb.HalfEdgesFrom[randomPoint];
                HashSet<Edge> edgesWithPointTo = StrDb.HalfEdgesTo[randomPoint];
                HashSet<Edge> allEdgesWithPoint = new HashSet<Edge>(edgesWithPointFrom);
                foreach (Edge e in edgesWithPointTo)
                {
                    allEdgesWithPoint.Add(e);
                }
                foreach (Edge e in edgesWithPointFrom)
                {
                    allEdgesWithPoint.Add(e);
                }
                List<Edge> allEdges = allEdgesWithPoint.ToList();
                bool EnoughEdges = allEdgesWithPoint.Count > 5;

                if (allEdgesWithPoint.Count > 0)
                {
                    foreach (Edge e in allEdgesWithPoint)
                    {
                        List<Triangle> trisWithEdge = StrDb.EdgeTriangles[e];
                        if (trisWithEdge.Count < 2) continue;
                        Triangle alterTri1 = trisWithEdge.ElementAt(0);
                        Triangle alterTri2 = trisWithEdge.ElementAt(1);
                        alteredIndex = alterTri1.Index;
                        var points1 = alterTri1.Points;
                        var points2 = alterTri2.Points;

                        Point sharedPoint1 = (Point)e.Q;
                        Point sharedPoint2 = (Point)e.P;
                        Point t1UnsharedPoint = (Point)alterTri1.Points.Where(p2 => p2 != sharedPoint1 && p2 != sharedPoint2).ElementAt(0);
                        Point t2UnsharedPoint = (Point)alterTri2.Points.Where(p2 => p2 != sharedPoint1 && p2 != sharedPoint2).ElementAt(0);
                        Vector3 p1Pos = new Vector3(((Point)e.P).Position.X, ((Point)e.P).Position.Y, ((Point)e.P).Position.Z);
                        Vector3 q1Pos = new Vector3(((Point)e.Q).Position.X, ((Point)e.Q).Position.Y, ((Point)e.Q).Position.Z);
                        Vector3 t1Pos = new Vector3(t1UnsharedPoint.Position.X, t1UnsharedPoint.Position.Y, t1UnsharedPoint.Position.Z);
                        Vector3 t2Pos = new Vector3(t2UnsharedPoint.Position.X, t2UnsharedPoint.Position.Y, t2UnsharedPoint.Position.Z);
                        float sharedEdgeLength = (p1Pos - q1Pos).Length();
                        float newEdgeLength = (t1Pos - t2Pos).Length();
                        var index = e.Index;
                        StrDb.RemoveEdge(e);
                        Edge sharedTriEdge = StrDb.AddEdge(t1UnsharedPoint, t2UnsharedPoint, index);
                        if (Mathf.Abs(sharedEdgeLength - newEdgeLength) > optimalSideLength / .5f)
                        {
                            continue;
                        }


                        var otherEdgesT1 = alterTri1.Edges.Where(edge => edge != e).ToList();
                        index = otherEdgesT1[0].Index;
                        StrDb.RemoveEdge((Edge)otherEdgesT1[0]);
                        Edge triEdge2 = StrDb.AddEdge(sharedPoint1, t1UnsharedPoint, index);
                        index = otherEdgesT1[1].Index;
                        StrDb.RemoveEdge((Edge)otherEdgesT1[1]);
                        Edge triEdge3 = StrDb.AddEdge(sharedPoint1, t2UnsharedPoint, index);
                        List<Point> updatedPoints = new List<Point> { sharedPoint1, t1UnsharedPoint, t2UnsharedPoint };
                        List<Edge> updatedEdges = new List<Edge> { sharedTriEdge, triEdge2, triEdge3 };
                        Triangle deformedTri1 = new Triangle(alterTri1.Index, updatedPoints, updatedEdges);
                        StrDb.UpdateTriangle(alterTri1, deformedTri1);

                        var otherEdgesT2 = alterTri2.Edges.Where(edge => edge != e).ToList();
                        index = otherEdgesT2[0].Index;
                        StrDb.RemoveEdge((Edge)otherEdgesT2[0]);
                        triEdge2 = StrDb.AddEdge(sharedPoint2, t2UnsharedPoint, index);
                        index = otherEdgesT2[1].Index;
                        StrDb.RemoveEdge((Edge)otherEdgesT2[1]);
                        triEdge3 = StrDb.AddEdge(sharedPoint2, t1UnsharedPoint, index);
                        updatedPoints = new List<Point> { sharedPoint2, t1UnsharedPoint, t2UnsharedPoint };
                        updatedEdges = new List<Edge> { sharedTriEdge, triEdge2, triEdge3 };
                        Triangle deformedTri2 = new Triangle(alterTri2.Index, updatedPoints, updatedEdges);
                        StrDb.UpdateTriangle(alterTri2, deformedTri2);
                    }
                }
            }

            for (int index = 0; index < 1; index++)
            {
                foreach (Point p in StrDb.VertexPoints.Values)
                {
                    Edge[] edgesWithPoint = StrDb.GetEdgesFromPoint(p);
                    HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();
                    foreach (Edge e in edgesWithPoint)
                    {
                        foreach (Triangle t in StrDb.EdgeTriangles[e])
                        {
                            trianglesWithPoint.Add(t);
                        }
                    }
                    Vector3 average = new Vector3(0, 0, 0);
                    foreach (Triangle t in trianglesWithPoint)
                    {
                        Vector3 triCenter = new Vector3(0, 0, 0);
                        triCenter = new Vector3(((Point)t.Points[0]).Position.X, ((Point)t.Points[0]).Position.Y, ((Point)t.Points[0]).Position.Z);
                        triCenter += new Vector3(((Point)t.Points[1]).Position.X, ((Point)t.Points[1]).Position.Y, ((Point)t.Points[1]).Position.Z);
                        triCenter += new Vector3(((Point)t.Points[2]).Position.X, ((Point)t.Points[2]).Position.Y, ((Point)t.Points[2]).Position.Z);
                        triCenter /= 3f;
                        average += triCenter;
                    }
                    average /= trianglesWithPoint.Count;

                    Point newPoint = new Point(p.Position + (average - p.Position) / currentIndex, p.Index);
                    StrDb.UpdatePointBaseMesh(p, newPoint);
                }
            }
            currentIndex++;
            Logger.ExitFunction("DeformMesh", $"updatedVertices={StrDb.VertexPoints.Count}");
        }
        catch (Exception e)
        {
            Logger.Error($"DeformMesh Error: {e.Message}\n{e.StackTrace}", "ERROR");
            GD.PrintErr($"Error in DeformMesh: {e.Message}\n{e.StackTrace}");
        }
    }
}
