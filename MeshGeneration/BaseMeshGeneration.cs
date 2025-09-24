using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Structures;

using static MeshGeneration.StructureDatabase;

namespace MeshGeneration;
public class BaseMeshGeneration
{
    private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
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
                        new Point( new Vector3(-TAU, 0, 1))};
        VertexIndex = 12;
        normals = new List<Vector3>();
        uvs = new List<Vector2>();
        foreach (Point p in cartesionPoints)
        {
            p.Position = p.Position.Normalized() * 100f;
            normals.Add(p.ToVector3());
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
                        Edge.MakeEdge(cartesionPoints[indices[i]], cartesionPoints[indices[i + 1]]),
                        Edge.MakeEdge(cartesionPoints[indices[i + 1]], cartesionPoints[indices[i + 2]]),
                        Edge.MakeEdge(cartesionPoints[indices[i + 2]], cartesionPoints[indices[i]])));
        }

    }


    public void GenerateNonDeformedFaces(VertexDistribution distribution = VertexDistribution.Linear)
    {
        List<Face> tempFaces = new List<Face>();
        for (int level = 0; level < subdivide; level++)
        {
            Edges.Clear();
            StructureDatabase.ClearEdges();
            var verticesToGenerate = level < VerticesPerEdge.Length ? VerticesPerEdge[level] : VerticesPerEdge[VerticesPerEdge.Length - 1];
            foreach (Face face in faces)
            {
                tempFaces.AddRange(_subdivider.SubdivideFace(face, verticesToGenerate, distribution));
            }
            faces.Clear();
            faces = new List<Face>(tempFaces);
            tempFaces.Clear();
        }
    }

    public void GenerateTriangleList()
    {
        foreach (Face f in faces)
        {
            Triangle newTri = new Triangle(BaseTris.Count, f.v.ToList(), f.e.ToList());
            BaseTris.Add((f.v[0], f.v[1], f.v[2]), newTri);
            StructureDatabase.AddTriangle(newTri);
            foreach (Edge edge in f.e)
            {
                AddEdge(edge);
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
            //DeformMesh(numAbberations, optimalSideLength);
            Task firstPass = Task.Factory.StartNew(() => DeformMesh(numAbberations, optimalSideLength));
            deformationPasses.Add(firstPass);
        }
        Task.WaitAll(deformationPasses.ToArray());
    }

    private void DeformMesh(int numAbberations, float optimalSideLength)
    {
        try
        {
            int alteredIndex = 0;
            for (int i = 0; i < numAbberations; i++)
            {
                Point p = SelectRandomPoint(rand);
                semaphore.Wait();
                try
                {
                    Edge[] edgesFrom = GetHalfEdges(p);
                    if (edgesFrom == null) continue;
                    HashSet<Edge> allEdgesWithPoint = new HashSet<Edge>(edgesFrom);
                    bool EnoughEdges = allEdgesWithPoint.Count > 5;

                    if (allEdgesWithPoint.Count > 0)
                    {
                        foreach (Edge e in allEdgesWithPoint)
                        {
                            HashSet<Triangle> trisWithEdge = EdgeTriangles[e];
                            if (trisWithEdge.Count < 2) continue;
                            Triangle alterTri1 = trisWithEdge.ElementAt(0);
                            Triangle alterTri2 = trisWithEdge.ElementAt(1);
                            alteredIndex = alterTri1.Index;
                            var points1 = alterTri1.Points;
                            var points2 = alterTri2.Points;

                            Point sharedPoint1 = e.Q;
                            Point sharedPoint2 = e.P;
                            Point t1UnsharedPoint = alterTri1.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
                            Point t2UnsharedPoint = alterTri2.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
                            float sharedEdgeLength = (e.P.ToVector3() - e.Q.ToVector3()).Length();
                            float newEdgeLength = (t1UnsharedPoint.ToVector3() - t2UnsharedPoint.ToVector3()).Length();
                            Edge sharedTriEdge = Edge.MakeEdge(sharedPoint1, sharedPoint2, alteredIndex);
                            if (Mathf.Abs(sharedEdgeLength - newEdgeLength) > optimalSideLength / .5f)
                            {
                                continue;
                            }

                            UpdateEdge(e, sharedTriEdge);
                            //RemoveEdge(e);
                            //AddEdge(sharedTriEdge);

                            GD.PrintRaw($"Alter Tri 1: {alterTri1}\n");
                            var otherEdgesT1 = alterTri1.Edges.Where(edge => edge != e).ToList();
                            GD.PrintRaw($"Other Edges T1: {otherEdgesT1.Count}\n");

                            Edge triEdge2 = Edge.MakeEdge(sharedPoint1, t1UnsharedPoint, otherEdgesT1[0].Index);
                            UpdateEdge(otherEdgesT1[0], triEdge2);
                            //RemoveEdge(otherEdgesT1[0]);
                            //AddEdge(triEdge2);
                            Edge triEdge3 = Edge.MakeEdge(sharedPoint1, t2UnsharedPoint, otherEdgesT1[1].Index);
                            UpdateEdge(otherEdgesT1[1], triEdge3);
                            //RemoveEdge(otherEdgesT1[1]);
                            //AddEdge(triEdge3);
                            List<Point> updatedPoints = new List<Point> { sharedPoint1, t1UnsharedPoint, t2UnsharedPoint };
                            List<Edge> updatedEdges = new List<Edge> { sharedTriEdge, triEdge2, triEdge3 };
                            Triangle deformedTri1 = new Triangle(alterTri1.Index, updatedPoints, updatedEdges);
                            UpdateTriangle(alterTri1, deformedTri1);

                            var otherEdgesT2 = alterTri2.Edges.Where(edge => edge != e).ToList();
                            //triEdge2 = new Edge(otherEdgesT2[0].Index, sharedPoint2, t2UnsharedPoint);
                            triEdge2 = Edge.MakeEdge(sharedPoint2, t2UnsharedPoint, otherEdgesT2[0].Index);
                            UpdateEdge(otherEdgesT2[0], triEdge2);
                            //RemoveEdge(otherEdgesT2[0]);
                            //AddEdge(triEdge2);
                            triEdge3 = Edge.MakeEdge(sharedPoint2, t1UnsharedPoint, otherEdgesT2[1].Index);
                            //triEdge3 = new Edge(otherEdgesT2[1].Index, sharedPoint2, t1UnsharedPoint);
                            UpdateEdge(otherEdgesT2[1], triEdge3);
                            //RemoveEdge(otherEdgesT2[1]);
                            //AddEdge(triEdge3);
                            updatedPoints = new List<Point> { sharedPoint2, t1UnsharedPoint, t2UnsharedPoint };
                            updatedEdges = new List<Edge> { sharedTriEdge, triEdge2, triEdge3 };
                            Triangle deformedTri2 = new Triangle(alterTri2.Index, updatedPoints, updatedEdges);
                            UpdateTriangle(alterTri2, deformedTri2);
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }

            semaphore.Wait();
            try
            {
                for (int index = 0; index < 12; index++)
                {
                    GD.PrintRaw($"Deforming, index: {index} \n");
                    foreach (Point p in VertexPoints.Values)
                    {
                        Edge[] edgesWithPoint = GetHalfEdges(p);
                        if (edgesWithPoint == null) continue;
                        HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();
                        foreach (Edge e in edgesWithPoint)
                        {
                            foreach (Triangle t in GetTrianglesFromEdge(e))
                            {
                                trianglesWithPoint.Add(t);
                            }
                        }
                        Vector3 average = new Vector3(0, 0, 0);
                        foreach (Triangle t in trianglesWithPoint)
                        {
                            Vector3 triCenter = new Vector3(0, 0, 0);
                            triCenter = t.Points[0].ToVector3();
                            triCenter += t.Points[1].ToVector3();
                            triCenter += t.Points[2].ToVector3();
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
            finally
            {
                semaphore.Release();
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Deform Mesh Error: {e.Message}\n{e.StackTrace}\n");
        }
    }
}
