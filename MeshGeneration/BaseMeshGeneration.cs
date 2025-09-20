using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Structures;

using static MeshGeneration.StructureDatabase;

namespace MeshGeneration;
public class BaseMeshGeneration
{
    private static int currentIndex = 0;
    static private float TAU = (1 + (float)Math.Sqrt(5)) / 2;
    private RandomNumberGenerator rand;
    private int subdivide;
    private int[] VerticesPerEdge;
    private ConfigurableSubdivider _subdivider = new ConfigurableSubdivider();

    private int VertexIndex = 0;
    private List<Vector3> normals;
    private List<Vector2> uvs;
    private List<int> indices;
    private List<Face> faces;

    public BaseMeshGeneration(RandomNumberGenerator rand, int subdivide, int[] VerticesPerEdge)
    {
        this.rand = rand;
        this.subdivide = subdivide;
        this.VerticesPerEdge = VerticesPerEdge;

        normals = new List<Vector3>();
        uvs = new List<Vector2>();
        indices = new List<int>();
        faces = new List<Face>();
    }

    public void PopulateArrays()
    {
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
            p.Position = p.Position.Normalized();
            normals.Add(new Vector3(p.Position.X, p.Position.Y, p.Position.Z));
            GD.PrintRaw($"Point: {p}");
            VertexPoints.Add(p.Index, p);
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

    }


    public void GenerateNonDeformedFaces(VertexDistribution distribution = VertexDistribution.Linear)
    {
        List<Face> tempFaces = new List<Face>();
        for (int level = 0; level < subdivide; level++)
        {
            var verticesToGenerate = level < VerticesPerEdge.Length ? VerticesPerEdge[level] : VerticesPerEdge[VerticesPerEdge.Length - 1];
            foreach (Face face in faces)
            {
                tempFaces.AddRange(_subdivider.SubdivideFace(face, verticesToGenerate, distribution));
            }
            faces.Clear();
            Edges.Clear();
            faces = new List<Face>(tempFaces);
            tempFaces.Clear();
        }
    }

    public void GenerateTriangleList()
    {
        foreach (Face f in faces)
        {
            List<IPoint> points = f.v.Cast<IPoint>().ToList();
            List<IEdge> edges = f.e.Cast<IEdge>().ToList();
            Triangle newTri = new Triangle(BaseTris.Count, points, edges);
            BaseTris.Add((f.v[0], f.v[1], f.v[2]), newTri);
            foreach (Edge edge in f.e)
            {
                if (!EdgeTriangles.ContainsKey(edge) || EdgeTriangles[edge] == null) EdgeTriangles[edge] = new List<Triangle>();
                EdgeTriangles[edge].Add(newTri);
                Point edgeP = (Point)edge.P;
                Point edgeQ = (Point)edge.Q;
                if (!HalfEdgesFrom.ContainsKey(edgeP))
                {
                    HalfEdgesFrom.Add(edgeP, new HashSet<Edge>());
                    HalfEdgesFrom[edgeP].Add(edge);
                }
                else
                {
                    HalfEdgesFrom[edgeP].Add(edge);
                }
                if (!HalfEdgesTo.ContainsKey(edgeQ))
                {
                    HalfEdgesTo.Add(edgeQ, new HashSet<Edge>());
                    HalfEdgesTo[edgeQ].Add(edge);
                }
                else
                {
                    HalfEdgesTo[edgeQ].Add(edge);
                }
            }
        }
    }

    public void InitiateDeformation(int numDeformationCycles, int numAbberations, float optimalSideLength)
    {
        currentIndex = 0;
        HashSet<Point> usedPoints = new HashSet<Point>();
        List<Task> deformationPasses = new List<Task>();
        for (int deforms = 0; deforms < numDeformationCycles; deforms++)
        {
            List<Point> pointsToDeform = new List<Point>();
            for (int i = 0; i < numAbberations; i++)
            {
                Point randomPoint = SelectRandomPoint(rand);
                while (usedPoints.Contains(randomPoint))
                {
                    randomPoint = SelectRandomPoint(rand);
                }
                pointsToDeform.Add(randomPoint);
                usedPoints.Add(randomPoint);
            }
            Task firstPass = Task.Factory.StartNew(() => DeformMesh(pointsToDeform, optimalSideLength));
            deformationPasses.Add(firstPass);
        }
        Task.WaitAll(deformationPasses.ToArray());
    }

    private void DeformMesh(List<Point> pointsToDeform, float optimalSideLength)
    {
        int alteredIndex = 0;
        foreach (Point p in pointsToDeform)
        {
            Point randomPoint = SelectRandomPoint(rand);
            HashSet<Edge> edgesWithPointFrom = HalfEdgesFrom[randomPoint];
            HashSet<Edge> edgesWithPointTo = HalfEdgesTo[randomPoint];
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
                    List<Triangle> trisWithEdge = EdgeTriangles[e];
                    if (trisWithEdge.Count < 2) continue;
                    Triangle alterTri1 = trisWithEdge.ElementAt(0);
                    Triangle alterTri2 = trisWithEdge.ElementAt(1);
                    alteredIndex = alterTri1.Index;
                    var points1 = alterTri1.Points;
                    var points2 = alterTri2.Points;

                    Point sharedPoint1 = (Point)e.Q;
                    Point sharedPoint2 = (Point)e.P;
                    Point t1UnsharedPoint = (Point)alterTri1.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
                    Point t2UnsharedPoint = (Point)alterTri2.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
                    Vector3 p1Pos = new Vector3(((Point)e.P).Position.X, ((Point)e.P).Position.Y, ((Point)e.P).Position.Z);
                    Vector3 q1Pos = new Vector3(((Point)e.Q).Position.X, ((Point)e.Q).Position.Y, ((Point)e.Q).Position.Z);
                    Vector3 t1Pos = new Vector3(t1UnsharedPoint.Position.X, t1UnsharedPoint.Position.Y, t1UnsharedPoint.Position.Z);
                    Vector3 t2Pos = new Vector3(t2UnsharedPoint.Position.X, t2UnsharedPoint.Position.Y, t2UnsharedPoint.Position.Z);
                    float sharedEdgeLength = (p1Pos - q1Pos).Length();
                    float newEdgeLength = (t1Pos - t2Pos).Length();
                    Edge sharedTriEdge = new Edge(e.Index, t1UnsharedPoint, t2UnsharedPoint);
                    if (Mathf.Abs(sharedEdgeLength - newEdgeLength) > optimalSideLength / .5f)
                    {
                        continue;
                    }

                    RemoveEdge(e);
                    AddEdge(sharedTriEdge);

                    var otherEdgesT1 = alterTri1.Edges.Where(edge => edge != e).ToList();
                    Edge triEdge2 = new Edge(otherEdgesT1[0].Index, sharedPoint1, t1UnsharedPoint);
                    RemoveEdge((Edge)otherEdgesT1[0]);
                    AddEdge(triEdge2);
                    Edge triEdge3 = new Edge(otherEdgesT1[1].Index, sharedPoint1, t2UnsharedPoint);
                    RemoveEdge((Edge)otherEdgesT1[1]);
                    AddEdge(triEdge3);
                    List<IPoint> updatedPoints = new List<IPoint> { sharedPoint1, t1UnsharedPoint, t2UnsharedPoint };
                    List<IEdge> updatedEdges = new List<IEdge> { sharedTriEdge, triEdge2, triEdge3 };
                    Triangle deformedTri1 = new Triangle(alterTri1.Index, updatedPoints, updatedEdges);
                    UpdateTriangle(alterTri1, deformedTri1);

                    var otherEdgesT2 = alterTri2.Edges.Where(edge => edge != e).ToList();
                    triEdge2 = new Edge(otherEdgesT2[0].Index, sharedPoint2, t2UnsharedPoint);
                    RemoveEdge((Edge)otherEdgesT2[0]);
                    AddEdge(triEdge2);
                    triEdge3 = new Edge(otherEdgesT2[1].Index, sharedPoint2, t1UnsharedPoint);
                    RemoveEdge((Edge)otherEdgesT2[1]);
                    AddEdge(triEdge3);
                    updatedPoints = new List<IPoint> { sharedPoint2, t1UnsharedPoint, t2UnsharedPoint };
                    updatedEdges = new List<IEdge> { sharedTriEdge, triEdge2, triEdge3 };
                    Triangle deformedTri2 = new Triangle(alterTri2.Index, updatedPoints, updatedEdges);
                    UpdateTriangle(alterTri2, deformedTri2);
                }
            }
        }

        for (int index = 0; index < 12; index++)
        {
            foreach (Point p in VertexPoints.Values)
            {
                HashSet<Edge> edgesWithPoint = HalfEdgesFrom[p];
                HashSet<Edge> edgesWithPointTo = HalfEdgesTo[p];
                HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();
                foreach (Edge e in edgesWithPoint)
                {
                    foreach (Triangle t in EdgeTriangles[e])
                    {
                        trianglesWithPoint.Add(t);
                    }
                }
                foreach (Edge e in edgesWithPointTo)
                {
                    foreach (Triangle t in EdgeTriangles[e])
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
                UpdatePoint(p, newPoint);
            }
            currentIndex++;
        }
    }
}
