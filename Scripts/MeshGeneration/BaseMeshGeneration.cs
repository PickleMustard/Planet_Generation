using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Structures;
using UtilityLibrary;
using PlanetGeneration;

namespace MeshGeneration;

/// <summary>
/// Handles the generation and deformation of base mesh structures for celestial bodies.
/// This class creates a dodecahedron as the starting mesh and then subdivides and deforms it
/// to create more complex planetary surfaces with configurable vertex distribution.
/// </summary>
public class BaseMeshGeneration
{
    /// <summary>
    /// Static counter used for vertex deformation calculations.
    /// </summary>
    private static int currentIndex = 0;
    private UnifiedCelestialMesh mesh;

    /// <summary>
    /// The golden ratio constant (1 + sqrt(5)) / 2, used for dodecahedron vertex calculations.
    /// </summary>
    static private float TAU = (1 + (float)Math.Sqrt(5)) / 2;

    /// <summary>
    /// Random number generator for procedural generation and deformation.
    /// </summary>
    private RandomNumberGenerator rand;

    /// <summary>
    /// Number of subdivision levels to apply to the base mesh.
    /// </summary>
    private int subdivide;

    /// <summary>
    /// Array specifying the number of vertices to generate per edge at each subdivision level.
    /// </summary>
    private int[] VerticesPerEdge;

    /// <summary>
    /// Reference to the configurable subdivider for mesh subdivision operations.
    /// </summary>
    private ConfigurableSubdivider _subdivider;

    /// <summary>
    /// Current vertex index counter for mesh generation.
    /// </summary>
    private int VertexIndex = 0;

    /// <summary>
    /// List of vertex normals for the generated mesh.
    /// </summary>
    private List<Vector3> normals;

    /// <summary>
    /// List of UV coordinates for texture mapping.
    /// </summary>
    private List<Vector2> uvs;

    /// <summary>
    /// List of triangle indices defining the mesh topology.
    /// </summary>
    private List<int> indices;

    /// <summary>
    /// List of faces that make up the current mesh state.
    /// </summary>
    private List<Face> faces;

    /// <summary>
    /// Reference to the structure database for managing mesh data.
    /// </summary>
    private StructureDatabase StrDb;

    /// <summary>
    /// Initializes a new instance of the BaseMeshGeneration class.
    /// </summary>
    /// <param name="rand">Random number generator for procedural generation</param>
    /// <param name="StrDb">Structure database for managing mesh data</param>
    /// <param name="subdivide">Number of times to subdivide the dodecahedron</param>
    /// <param name="VerticesPerEdge">Number of points to generate per edge at each subdivision level</param>
    public BaseMeshGeneration(RandomNumberGenerator rand, StructureDatabase StrDb, int subdivide, int[] VerticesPerEdge, UnifiedCelestialMesh mesh)
    {
        Logger.EnterFunction("BaseMeshGeneration::.ctor", $"subdivide={subdivide}, VPE=[{string.Join(",", VerticesPerEdge ?? Array.Empty<int>())}]");
        this.rand = rand;
        this.StrDb = StrDb;
        this.subdivide = subdivide;
        this.VerticesPerEdge = VerticesPerEdge;

        this.mesh = mesh;

        _subdivider = new ConfigurableSubdivider(StrDb, mesh);

        normals = new List<Vector3>();
        uvs = new List<Vector2>();
        indices = new List<int>();
        faces = new List<Face>();
        Logger.ExitFunction("BaseMeshGeneration::.ctor");
    }

    /// <summary>
    /// Initializes the mesh data structures by creating a dodecahedron as the base mesh.
    /// This method generates the 12 vertices of a dodecahedron and creates the initial
    /// 20 triangular faces that define the base mesh structure.
    /// </summary>
    /// <remarks>
    /// The dodecahedron vertices are calculated using the golden ratio (TAU) to ensure
    /// proper geometric proportions. Each vertex is normalized and scaled to a radius of 100 units.
    /// </remarks>
    public void PopulateArrays()
    {
        Logger.EnterFunction("PopulateArrays");
        List<Point> cartesionPoints = new List<Point> {
                        new Point(new Vector3(0, 1, TAU) * 100f),
                        new Point( new Vector3(0, -1, TAU) * 100f),
                        new Point( new Vector3(0, -1, -TAU) * 100f),
                        new Point( new Vector3(0, 1, -TAU) * 100f),
                        new Point(new Vector3(1, TAU, 0) * 100f),
                        new Point( new Vector3(-1, TAU, 0) * 100f),
                        new Point( new Vector3(-1, -TAU, 0) * 100f),
                        new Point( new Vector3(1, -TAU, 0) * 100f),
                        new Point(new Vector3(TAU, 0, 1) * 100f),
                        new Point( new Vector3(TAU, 0, -1) * 100f),
                        new Point( new Vector3(-TAU, 0, -1) * 100f),
                        new Point( new Vector3(-TAU, 0, 1) * 100f)
        };
        VertexIndex = 12;
        normals = new List<Vector3>();
        uvs = new List<Vector2>();
        for (int i = 0; i < cartesionPoints.Count; i++)
        {
            var p = cartesionPoints[i];
            var pos = p.Position;
            var rp = StrDb.GetOrCreatePoint(p.Index, pos);
            cartesionPoints[i] = rp;
            normals.Add(new Vector3(rp.Position.X, rp.Position.Y, rp.Position.Z));
            Logger.Point($"Point: {rp}");
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
        Logger.Info($"PopulateArrays: vertices={StrDb.BaseVertices.Count}, faces={faces.Count}, indices={indices.Count}");
        Logger.ExitFunction("PopulateArrays");
    }


    /// <summary>
    /// Generates non-deformed faces by subdividing the base mesh according to the specified parameters.
    /// This method performs multiple levels of subdivision, each time increasing the complexity
    /// of the mesh by adding more vertices and faces.
    /// </summary>
    /// <param name="distribution">The vertex distribution method to use during subdivision (defaults to Linear)</param>
    /// <remarks>
    /// The subdivision process works iteratively, with each level potentially using a different
    /// number of vertices per edge as specified in the VerticesPerEdge array. After the first
    /// subdivision level, the structure database is reset to BaseMesh state.
    /// </remarks>
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
            faces = new List<Face>(tempFaces);
            tempFaces.Clear();
            Logger.Info($"After level {level + 1}: faces={faces.Count}");
        }
        Logger.ExitFunction("GenerateNonDeformedFaces");
    }

    /// <summary>
    /// Converts the generated faces into a triangle list and stores them in the structure database.
    /// This method processes all faces and creates corresponding triangle objects with proper
    /// edge connectivity, establishing the final mesh topology.
    /// </summary>
    /// <remarks>
    /// Each face is converted to a triangle and added to the structure database. The method
    /// handles edge creation and connectivity automatically through the database facade.
    /// This is typically called after mesh subdivision and before deformation.
    /// </remarks>
    public void GenerateTriangleList()
    {
        Logger.EnterFunction("GenerateTriangleList");
        Logger.Info($"Structure Database: {StrDb.Index}");
        int added = 0;
        foreach (Face f in faces)
        {
            List<Point> points = f.v.ToList();
            // Delegate wiring to DB facade
            var newTri = StrDb.AddTriangle(points);
            // Legacy edge additions are handled internally by AddTriangle
            added++;
        }
        Logger.Info($"GenerateTriangleList: added={added} triangles, BaseTris={StrDb.BaseTris.Count}");
        Logger.ExitFunction("GenerateTriangleList");
    }

    /// <summary>
    /// Initiates the mesh deformation process to create more natural-looking planetary surfaces.
    /// This method runs multiple deformation cycles using the thread pool to optimize the mesh topology
    /// by performing edge flips and vertex smoothing operations.
    /// </summary>
    /// <param name="numDeformationCycles">Number of deformation cycles to execute</param>
    /// <param name="numAbberations">Number of edge flip operations to perform per cycle</param>
    /// <param name="optimalSideLength">Target edge length for deformation decisions</param>
    /// <remarks>
    /// The deformation process involves two main phases:
    /// 1. Edge flipping: Randomly selects edges and flips them if it improves triangle quality
    /// 2. Vertex smoothing: Moves vertices toward the average center of adjacent triangles
    ///
    /// This process helps create more evenly distributed triangles and reduces mesh artifacts.
    /// The method uses the thread pool for controlled parallel processing to prevent system overload.
    /// </remarks>
    public async Task InitiateDeformation(int numDeformationCycles, int numAbberations, float optimalSideLength)
    {
        Logger.EnterFunction("InitiateDeformation", $"cycles={numDeformationCycles}, abberations={numAbberations}, optimalSideLength={optimalSideLength}");
        
        var tasks = new List<Task>();

        for (int i = 0; i < numDeformationCycles; i++)
        {
            var taskId = $"{mesh.Name}_deform_{i}";
            var task = MeshGenerationThreadPool.Instance.EnqueueTask(
                () => DeformMesh(numAbberations, optimalSideLength),
                taskId,
                TaskPriority.Medium,
                mesh.Name
            );
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
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
                Edge[] allEdgesWithPoint = StrDb.GetIncidentHalfEdges(randomPoint);
                List<Edge> allEdges = allEdgesWithPoint.ToList();
                bool EnoughEdges = allEdgesWithPoint.Length > 5;

                if (allEdgesWithPoint.Length > 0)
                {
                    Edge e = allEdgesWithPoint[rand.RandiRange(0, allEdgesWithPoint.Length - 1)];
                    List<Triangle> trisWithEdge = StrDb.GetTrianglesByEdge(e);
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
                    if (Mathf.Abs(sharedEdgeLength - newEdgeLength) > optimalSideLength / .5f)
                    {
                        continue;
                    }
                    StrDb.RemoveEdge(e);
                    Edge sharedTriEdge = StrDb.GetOrCreateEdge(t1UnsharedPoint, t2UnsharedPoint);


                    var otherEdgesT1 = alterTri1.Edges.Where(edge => edge != e).ToList();
                    index = otherEdgesT1[0].Index;
                    StrDb.RemoveEdge((Edge)otherEdgesT1[0]);
                    Edge triEdge2 = StrDb.GetOrCreateEdge(sharedPoint1, t1UnsharedPoint);
                    index = otherEdgesT1[1].Index;
                    StrDb.RemoveEdge((Edge)otherEdgesT1[1]);
                    Edge triEdge3 = StrDb.GetOrCreateEdge(sharedPoint1, t2UnsharedPoint);
                    List<Point> updatedPoints = new List<Point> { t2UnsharedPoint, t1UnsharedPoint, sharedPoint1 };
                    List<Edge> updatedEdges = new List<Edge> { triEdge3, triEdge2, sharedTriEdge };
                    Triangle deformedTri1 = new Triangle(alterTri1.Index, updatedPoints, updatedEdges);
                    StrDb.UpdateTriangle(alterTri1, deformedTri1);

                    var otherEdgesT2 = alterTri2.Edges.Where(edge => edge != e).ToList();
                    index = otherEdgesT2[0].Index;
                    StrDb.RemoveEdge((Edge)otherEdgesT2[0]);
                    triEdge2 = StrDb.GetOrCreateEdge(sharedPoint2, t2UnsharedPoint);
                    index = otherEdgesT2[1].Index;
                    StrDb.RemoveEdge((Edge)otherEdgesT2[1]);
                    triEdge3 = StrDb.GetOrCreateEdge(sharedPoint2, t1UnsharedPoint);
                    updatedPoints = new List<Point> { t2UnsharedPoint, t1UnsharedPoint, sharedPoint2 };
                    updatedEdges = new List<Edge> { triEdge3, triEdge2, sharedTriEdge };
                    Triangle deformedTri2 = new Triangle(alterTri2.Index, updatedPoints, updatedEdges);
                    StrDb.UpdateTriangle(alterTri2, deformedTri2);

                    PolygonRendererSDL.RenderTriangleAndConnections(mesh, 5, alterTri1);
                    PolygonRendererSDL.RenderTriangleAndConnections(mesh, 5, alterTri2);

                    List<Point> usedPoints = new List<Point> { t1UnsharedPoint, t2UnsharedPoint, sharedPoint1, sharedPoint2 };
                    StrDb.RemoveFromRandom(usedPoints);
                }
            }

            //for (int index = 0; index < 1; index++)
            //{
            //    foreach (Point p in StrDb.VertexPoints.Values)
            //    {
            //        Edge[] edgesWithPoint = StrDb.GetIncidentHalfEdges(p);
            //        HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();
            //        //foreach (Edge e in edgesWithPoint)
            //        //{
            //        //    foreach (Triangle t in StrDb.GetTrianglesByEdgeIndex(e.Index))
            //        //    {
            //        //        trianglesWithPoint.Add(t);
            //        //    }
            //        //}
            //        Vector3 average = new Vector3(0, 0, 0);
            //        foreach (Triangle t in trianglesWithPoint)
            //        {
            //            Vector3 triCenter = new Vector3(0, 0, 0);
            //            triCenter = new Vector3(((Point)t.Points[0]).Position.X, ((Point)t.Points[0]).Position.Y, ((Point)t.Points[0]).Position.Z);
            //            triCenter += new Vector3(((Point)t.Points[1]).Position.X, ((Point)t.Points[1]).Position.Y, ((Point)t.Points[1]).Position.Z);
            //            triCenter += new Vector3(((Point)t.Points[2]).Position.X, ((Point)t.Points[2]).Position.Y, ((Point)t.Points[2]).Position.Z);
            //            triCenter /= 3f;
            //            average += triCenter;
            //        }
            //        average /= trianglesWithPoint.Count;

            //        Point newPoint = new Point(p.Position + (average - p.Position) / currentIndex, p.Index);
            //        StrDb.UpdatePointBaseMesh(p, newPoint);
            //    }
            //}
            currentIndex++;
            Logger.ExitFunction("DeformMesh", $"updatedVertices={StrDb.BaseVertices.Count}");
        }
        catch (Exception e)
        {
            GD.PrintRaw($"\u001b[2J\u001b[H");
            Logger.Error($"DeformMesh Error: {e.Message}\n{e.StackTrace}", "ERROR");
            GD.PrintErr($"Error in DeformMesh: {e.Message}\n{e.StackTrace}");
        }
    }
}
