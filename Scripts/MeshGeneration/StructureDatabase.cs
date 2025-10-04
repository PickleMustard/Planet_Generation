using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;
using UtilityLibrary;

namespace MeshGeneration;

/// <summary>
/// Central database for managing mesh structure data including points, edges, triangles, and Voronoi cells.
/// Provides thread-safe operations for creating, updating, and querying mesh elements with support for
/// both legacy and canonical data structures. Manages mesh generation phases from ungenerated to dual mesh.
/// </summary>
public class StructureDatabase
{
    /// <summary>
    /// Gets the unique index identifier for this structure database instance.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Represents the current state of mesh generation progression.
    /// </summary>
    public enum MeshState
    {
        /// <summary>
        /// Initial state before any mesh generation has occurred.
        /// </summary>
        Ungenerated = 0,

        /// <summary>
        /// State after base mesh generation is complete.
        /// </summary>
        BaseMesh = 1,

        /// <summary>
        /// State after dual mesh (Voronoi) generation is complete.
        /// </summary>
        DualMesh = 2
    }



    /// <summary>
    /// Thread synchronization object for ensuring thread-safe operations on the database.
    /// </summary>
    private object lockObject = new object();

    public HashSet<Triangle> BaseTris = new HashSet<Triangle>();
    public HashSet<Edge> UsedEdges = new HashSet<Edge>();
    public HashSet<Point> UsedPoints = new HashSet<Point>();
    public Dictionary<Edge, List<Triangle>> EdgeTriangles = new Dictionary<Edge, List<Triangle>>();
    /// <summary>
    /// Gets or sets the current mesh generation state. Starts at Ungenerated and increments after each mesh generation phase.
    /// </summary>
    public MeshState state = MeshState.Ungenerated;

    /// <summary>
    /// Dictionary mapping circumcenter indices to Point objects.
    /// </summary>
    public Dictionary<int, Point> VoronoiVertices = new Dictionary<int, Point>();

    /// <summary>
    /// List of all Voronoi cells in the mesh.
    /// </summary>
    public List<VoronoiCell> VoronoiCells = new List<VoronoiCell>();

    /// <summary>
    /// Set of all vertices that are part of Voronoi cells.
    /// </summary>
    public HashSet<Point> VoronoiCellVertices = new HashSet<Point>();

    /// <summary>
    /// Dictionary mapping points to the Voronoi cells that contain them.
    /// </summary>
    public Dictionary<Point, HashSet<VoronoiCell>> CellMap = new Dictionary<Point, HashSet<VoronoiCell>>();

    /// <summary>
    /// Dictionary mapping edges to the Voronoi cells that contain them.
    /// </summary>
    public Dictionary<Edge, HashSet<VoronoiCell>> EdgeMap = new Dictionary<Edge, HashSet<VoronoiCell>>();

    /// <summary>
    /// Dictionary mapping points to triangles in Voronoi diagrams.
    /// </summary>
    public Dictionary<Point, HashSet<Triangle>> VoronoiTriMap = new Dictionary<Point, HashSet<Triangle>>();

    /// <summary>
    /// Dictionary mapping edges to triangles in Voronoi diagrams.
    /// </summary>
    public Dictionary<Edge, HashSet<Triangle>> VoronoiEdgeTriMap = new Dictionary<Edge, HashSet<Triangle>>();

    /// <summary>
    /// Dictionary mapping source points to destination points and their connecting edges for dual mesh.
    /// </summary>
    public Dictionary<Point, Dictionary<Point, Edge>> worldHalfEdgeMapFrom = new Dictionary<Point, Dictionary<Point, Edge>>();

    /// <summary>
    /// Dictionary mapping destination points to source points and their connecting edges for dual mesh.
    /// </summary>
    public Dictionary<Point, Dictionary<Point, Edge>> worldHalfEdgeMapTo = new Dictionary<Point, Dictionary<Point, Edge>>();

    // Canonical registries (Phase 0)
    /// <summary>
    /// Canonical registry mapping point indices to Point objects.
    /// </summary>
    internal Dictionary<int, Point> BaseVertices = new Dictionary<int, Point>();

    /// <summary>
    /// Canonical registry mapping half-edge IDs to HalfEdge objects.
    /// </summary>
    internal Dictionary<int, HalfEdge> HalfEdgeById = new Dictionary<int, HalfEdge>();

    /// <summary>
    /// Canonical registry mapping undirected edge keys to their indices.
    /// </summary>
    internal Dictionary<EdgeKey, Edge> UndirectedEdgeIndex = new Dictionary<EdgeKey, Edge>();

    /// <summary>
    /// Canonical registry mapping triangle IDs to Triangle objects.
    /// </summary>
    internal Dictionary<int, Triangle> TrianglesById = new Dictionary<int, Triangle>();

    /// <summary>
    /// Canonical registry mapping edge keys to lists of triangles that share that edge.
    /// </summary>
    internal Dictionary<EdgeKey, List<Triangle>> TrianglesByEdgeKey = new Dictionary<EdgeKey, List<Triangle>>();

    /// <summary>
    /// Canonical registry mapping points to their outgoing half-edges.
    /// </summary>
    internal Dictionary<Point, HashSet<HalfEdge>> OutHalfEdgesByPoint = new Dictionary<Point, HashSet<HalfEdge>>();

    /// <summary>
    /// Canonical registry mapping edge keys to Voronoi cells.
    /// </summary>
    internal Dictionary<EdgeKey, HashSet<VoronoiCell>> EdgeKeyCellMap = new Dictionary<EdgeKey, HashSet<VoronoiCell>>();

    // Phase 3–4: Internal containers and validation toggle
    /// <summary>
    /// Gets or sets whether validation is enabled. When true, validation checks are performed during operations.
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Gets the container for base mesh related data structures.
    /// </summary>
    internal BaseContainer Base { get; }

    /// <summary>
    /// Gets the container for dual mesh related data structures.
    /// </summary>
    internal DualContainer Dual { get; }

    /// <summary>
    /// Initializes a new instance of the StructureDatabase class with the specified index.
    /// </summary>
    /// <param name="index">The unique index identifier for this database instance.</param>
    public StructureDatabase(int index)
    {
        Index = index;
        Base = new BaseContainer(this);
        Dual = new DualContainer(this);
    }

    /// <summary>
    /// Increments the mesh generation state to the next phase.
    /// </summary>
    /// <remarks>
    /// Progresses from Ungenerated -> BaseMesh -> DualMesh.
    /// This method should be called after completing each mesh generation phase.
    /// </remarks>
    public void IncrementMeshState()
    {
        state = (MeshState)((int)state + 1);
    }

    // ===== Phase 1: Facade APIs =====
    /// <summary>
    /// Gets an existing point at the specified position or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="pos">The 3D position of the point.</param>
    /// <returns>The existing or newly created Point object.</returns>
    /// <remarks>
    /// This method is thread-safe and uses the point's position coordinates to determine its index.
    /// If a point with the same calculated index already exists, it is returned instead of creating a duplicate.
    /// </remarks>
    public Point GetOrCreatePoint(Vector3 pos)
    {
        int idx = Point.DetermineIndex(pos.X, pos.Y, pos.Z);
        return GetOrCreatePoint(idx, pos);
    }

    /// <summary>
    /// Gets an existing point with the specified index or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="index">The unique index of the point.</param>
    /// <param name="pos">The 3D position of the point.</param>
    /// <returns>The existing or newly created Point object.</returns>
    /// <remarks>
    /// This method is thread-safe. If a point with the specified index already exists in the canonical registry,
    /// it is returned. Otherwise, a new Point is created and added to both canonical and legacy registries.
    /// </remarks>
    public Point GetOrCreatePoint(int index, Vector3 pos)
    {
        lock (lockObject)
        {
            if (BaseVertices.TryGetValue(index, out var existing))
            {
                return existing;
            }
            else
            {
                Point p = new Point(pos, index);
                BaseVertices[index] = p;
                return p;
            }
        }
    }

    /// <summary>
    /// Attempts to get an edge between two specified points.
    /// </summary>
    /// <param name="a">The starting point of the edge.</param>
    /// <param name="b">The ending point of the edge.</param>
    /// <param name="edge">When this method returns, contains the Edge object if found; otherwise, null.</param>
    /// <returns>true if the edge was found; otherwise, false.</returns>
    /// <remarks>
    /// This method is thread-safe and searches in multiple locations with fallback logic:
    /// 1. First checks canonical half-edges
    /// 2. Falls back to legacy HalfEdgesFrom dictionary
    /// 3. Finally checks legacy Edges by index
    /// </remarks>
    public bool TryGetEdge(Point a, Point b, out Edge edge)
    {
        lock (lockObject)
        {
            // Prefer canonical half-edges
            if (OutHalfEdgesByPoint.TryGetValue(a, out var set))
            {
                foreach (var h in set)
                {
                    if (h.Twin.From == b)
                    {
                        int idx = Edge.DefineIndex(a, b);
                        break;
                    }
                }
            }
            edge = null;
            return false;
        }
    }

    /// <summary>
    /// Gets an existing edge between two points or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="a">The starting point of the edge.</param>
    /// <param name="b">The ending point of the edge.</param>
    /// <returns>The existing or newly created Edge object.</returns>
    /// <remarks>
    /// This method is thread-safe. If an edge between the specified points already exists,
    /// it is returned. Otherwise, a new Edge is created and registered in both canonical and legacy systems.
    /// </remarks>
    public Edge GetOrCreateEdge(Point a, Point b)
    {
        lock (lockObject)
        {
            if (TryGetEdge(a, b, out var found))
            {
                return found;
            }
            // Create new legacy edge and mirror canonicals
            var e = Edge.MakeEdge(a, b);
            RegisterEdgeCanonicalAndLegacy(e);
            return e;
        }
    }

    /// <summary>
    /// Gets an existing edge between two points or creates a new one with the specified index if it doesn't exist.
    /// </summary>
    /// <param name="a">The starting point of the edge.</param>
    /// <param name="b">The ending point of the edge.</param>
    /// <param name="index">The specific index to use for the edge if creating a new one.</param>
    /// <returns>The existing or newly created Edge object.</returns>
    /// <remarks>
    /// This method is thread-safe. If an edge between the specified points already exists,
    /// it is returned. Otherwise, a new Edge is created with the specified index and registered
    /// in both canonical and legacy systems.
    /// </remarks>
    public Edge GetOrCreateEdge(Point a, Point b, int index)
    {
        Logger.EnterFunction("GetOrCreateEdge", $"Point a={a},Point b={b},index={index}");
        lock (lockObject)
        {
            if (TryGetEdge(a, b, out var found)) return found;
            var e = Edge.MakeEdge(index, a, b);
            RegisterEdgeCanonicalAndLegacy(e);
            Logger.ExitFunction("GetOrCreateEdge");
            return e;
        }
    }

    /// <summary>
    /// Gets all half-edges incident to the specified point.
    /// </summary>
    /// <param name="p">The point for which to get incident edges.</param>
    /// <returns>An array of Edge objects representing all edges connected to the point.</returns>
    /// <remarks>
    /// This method is thread-safe and returns legacy directed Edge objects.
    /// The search strategy depends on the current mesh state:
    /// - BaseMesh: Uses HalfEdgesFrom dictionary
    /// - DualMesh: Uses worldHalfEdgeMapFrom and worldHalfEdgeMapTo dictionaries
    /// Returns an empty array if an exception occurs.
    /// </remarks>
    public Edge[] GetIncidentHalfEdges(Point p)
    {
        // Temporary facade that returns legacy directed Edge objects
        Logger.EnterFunction("GetIncidentHalfEdges", $"pIndex={p.Index}");
        try
        {
            lock (lockObject)
            {
                HashSet<Edge> result = new HashSet<Edge>();
                // Prefer HalfEdgesFrom when available
                if (OutHalfEdgesByPoint.TryGetValue(p, out var edgesFrom) && edgesFrom.Count > 0)
                {
                    foreach (var e in edgesFrom) result.Add(UndirectedEdgeIndex[e.Key]);
                }
                else
                {
                    // Preserve current logic using world maps in DualMesh
                    switch (state)
                    {
                        case MeshState.BaseMesh:
                            if (OutHalfEdgesByPoint.TryGetValue(p, out var baseEdges))
                            {
                                foreach (var e in baseEdges) result.Add(UndirectedEdgeIndex[e.Key]);
                            }
                            break;
                        case MeshState.DualMesh:
                            if (worldHalfEdgeMapFrom.TryGetValue(p, out var fromDict))
                            {
                                foreach (var e in fromDict.Values) result.Add(e);
                            }
                            if (worldHalfEdgeMapTo.TryGetValue(p, out var toDict))
                            {
                                foreach (var e in toDict.Values) result.Add(e);
                            }
                            break;
                    }
                }
                var arr = result.ToArray();
                Logger.ExitFunction("GetIncidentHalfEdges", $"returned {arr.Length} edges");
                return arr;
            }
        }
        catch
        {
            Logger.ExitFunction("GetIncidentHalfEdges", "returned 0 edges (exception)");
            return Array.Empty<Edge>();
        }
    }

    /// <summary>
    /// Adds a new triangle to the mesh structure with the specified vertices.
    /// </summary>
    /// <param name="points">A list of exactly 3 points that form the triangle vertices.</param>
    /// <returns>The newly created Triangle object.</returns>
    /// <exception cref="ArgumentException">Thrown when points is null or does not contain exactly 3 points.</exception>
    /// <remarks>
    /// This method is thread-safe and performs the following operations:
    /// - Ensures all points exist in registries
    /// - Creates or retrieves edges between the points
    /// - Allocates a unique triangle ID
    /// - Registers the triangle in both canonical and le 3, 10, 2,gacy systems
    /// - Updates edge-triangle relationships
    /// </remarks>
    public Triangle AddTriangle(List<Point> points)
    {
        // Wires edges, updates registries + legacy (base maps)
        if (points == null || points.Count != 3) throw new ArgumentException("Triangle requires exactly 3 points");
        lock (lockObject)
        {
            // Ensure points exist
            foreach (var p in points)
            {
                if (!BaseVertices.ContainsKey(p.Index)) BaseVertices[p.Index] = p;
            }
            // Create/wire edges in order
            var e0 = GetOrCreateEdge(points[0], points[1]);
            var e1 = GetOrCreateEdge(points[1], points[2]);
            var e2 = GetOrCreateEdge(points[2], points[0]);

            // Allocate next non-negative triangle id
            int newId = 0;
            while (newId < int.MaxValue && TrianglesById.ContainsKey(newId)) newId++;
            var tri = new Triangle(newId, new List<Point>(points), new List<Edge> { e0, e1, e2 });

            RegisterTriangleInRegistriesOnly(tri);

            // Mirror legacy maps for base mesh
            BaseTris.Add(tri);
            if (!EdgeTriangles.ContainsKey(e0)) EdgeTriangles[e0] = new List<Triangle>();
            if (!EdgeTriangles.ContainsKey(e1)) EdgeTriangles[e1] = new List<Triangle>();
            if (!EdgeTriangles.ContainsKey(e2)) EdgeTriangles[e2] = new List<Triangle>();
            EdgeTriangles[e0].Add(tri);
            EdgeTriangles[e1].Add(tri);
            EdgeTriangles[e2].Add(tri);

            return tri;
        }
    }

    /// <summary>
    /// Gets all triangles that are incident to the specified point.
    /// </summary>
    /// <param name="p">The point for which to get incident triangles.</param>
    /// <returns>An enumerable collection of Triangle objects that contain the specified point.</returns>
    /// <remarks>
    /// This method is thread-safe. It works by first getting all edges incident to the point,
    /// then finding all triangles that contain each of those edges. Uses a HashSet to ensure
    /// no duplicate triangles are returned.
    /// </remarks>
    public IEnumerable<Triangle> GetIncidentTriangles(Point p)
    {
        lock (lockObject)
        {
            HashSet<Triangle> result = new HashSet<Triangle>();
            // Prefer canonical via incident edges
            var edges = GetIncidentHalfEdges(p);
            foreach (var e in edges)
            {
                EdgeKey eKey = EdgeKey.From(e.P, e.Q);
                if (TrianglesByEdgeKey.TryGetValue(eKey, out var list))
                {
                    foreach (var t in list) result.Add(t);
                }
                else if (EdgeTriangles.TryGetValue(e, out list))
                {
                    foreach (var t in list) result.Add(t);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Gets all triangles that contain the edge with the specified index.
    /// </summary>
    /// <param name="index">The index of the edge.</param>
    /// <returns>A list of Triangle objects that contain the specified edge.</returns>
    /// <remarks>
    /// This method is thread-safe. It first tries to find triangles using the canonical registry
    /// via EdgeKey. If that fails, it falls back to the legacy system by checking both directions
    /// of the edge and combining the results.
    /// </remarks>
    public List<Triangle> GetTrianglesByEdge(Edge e)
    {
        lock (lockObject)
        {
            List<Triangle> result = new List<Triangle>();
            if (TrianglesByEdgeKey.TryGetValue(e.key, out var list))
            {
                result.AddRange(list);
                return result;
            }
            return result;
        }
    }

    /// <summary>
    /// Attempts to get the two triangles that share the specified edge for edge flipping operations.
    /// </summary>
    /// <param name="e">The edge to check for adjacent triangles.</param>
    /// <param name="t1">When this method returns, contains the first triangle if found; otherwise, null.</param>
    /// <param name="t2">When this method returns, contains the second triangle if found; otherwise, null.</param>
    /// <returns>true if exactly two triangles share the edge; otherwise, false.</returns>
    /// <remarks>
    /// This method is thread-safe and is used for edge flipping in Delaunay triangulation.
    /// It first checks the canonical registry via EdgeKey, then falls back to the legacy system.
    /// Returns true only if exactly two triangles share the edge, which is required for flipping.
    /// </remarks>
    public bool FlipEdge(Edge e, out Triangle t1, out Triangle t2)
    {
        lock (lockObject)
        {
            var key = EdgeKey.From(e.P, e.Q);
            t1 = null; t2 = null;
            if (TrianglesByEdgeKey.TryGetValue(key, out var list))
            {
                if (list.Count == 2)
                {
                    t1 = list[0];
                    t2 = list[1];
                    return true;
                }
                if (list.Count == 1)
                {
                    t1 = list[0];
                }
                return false;
            }
            // Legacy fallback
            List<Triangle> tmp = new List<Triangle>();
            if (EdgeTriangles.TryGetValue(e, out var l1)) tmp.AddRange(l1);
            //var rev = e.ReverseEdge();
            //if (EdgeTriangles.TryGetValue(rev, out var l2)) tmp.AddRange(l2);
            if (tmp.Count == 2)
            {
                t1 = tmp[0]; t2 = tmp[1];
                return true;
            }
            if (tmp.Count == 1) t1 = tmp[0];
            return false;
        }
    }

    // ===== Legacy shims routed through registries =====
    /// <summary>
    /// Legacy method to get all edges connected to a specific point.
    /// </summary>
    /// <param name="p">The point for which to get connected edges.</param>
    /// <returns>An array of Edge objects connected to the specified point.</returns>
    /// <remarks>
    /// This is a legacy shim method that delegates to GetIncidentHalfEdges.
    /// It maintains backward compatibility with existing code that uses this method name.
    /// </remarks>
    public Edge[] GetEdgesFromPoint(Point p)
    {
        Logger.EnterFunction("GetEdgesFromPoint", $"pIndex={p.Index}");

        var edges = GetIncidentHalfEdges(p);
        Logger.ExitFunction("GetEdgesFromPoint", $"returned {edges.Length} edges");
        return edges;
    }

    /// <summary>
    /// Adds a point to the mesh structure.
    /// </summary>
    /// <param name="point">The Point object to add.</param>
    /// <remarks>
    /// This method is thread-safe. It adds the point to both canonical and legacy registries.
    /// The behavior varies by mesh state, though currently only the Ungenerated state
    /// performs additional operations. The point is added to PointsById and VertexPoints dictionaries.
    /// </remarks>
    public void AddPoint(Point point)
    {
        Logger.EnterFunction("AddPoint", $"MeshState: {state}, pointIndex={point.Index}");
        lock (lockObject)
        {
            // Registry first
            if (!BaseVertices.ContainsKey(point.Index)) BaseVertices[point.Index] = point;

            // Preserve legacy structure/actions by state (currently only Ungenerated branch mutates)
            switch (state)
            {
                case MeshState.Ungenerated:
                    // Legacy path already mirrored above
                    break;
                case MeshState.BaseMesh:
                    break;
                case MeshState.DualMesh:
                    break;
            }
        }
    }

    /// <summary>
    /// Adds an edge between two points to the mesh structure.
    /// </summary>
    /// <param name="p1">The starting point of the edge.</param>
    /// <param name="p2">The ending point of the edge.</param>
    /// <returns>The Edge object that was added or retrieved.</returns>
    /// <remarks>
    /// This method delegates to GetOrCreateEdge, ensuring no duplicate edges are created.
    /// It maintains backward compatibility with existing code.
    /// </remarks>
    public Edge AddEdge(Point p1, Point p2)
    {
        Logger.EnterFunction("AddEdge", $"MeshState: {state}, From {p1} to {p2}");
        Edge returnEdge = GetOrCreateEdge(p1, p2);
        Logger.ExitFunction("AddEdge", $"edgeIndex={returnEdge.Index}");
        return returnEdge;
    }

    /// <summary>
    /// Adds an edge between two points with a specific index to the mesh structure.
    /// </summary>
    /// <param name="p1">The starting point of the edge.</param>
    /// <param name="p2">The ending point of the edge.</param>
    /// <param name="index">The specific index to use for the edge.</param>
    /// <returns>The Edge object that was added or retrieved.</returns>
    /// <remarks>
    /// This method delegates to GetOrCreateEdge with the specified index,
    /// ensuring no duplicate edges are created while respecting the requested index.
    /// </remarks>
    public Edge AddEdge(Point p1, Point p2, int index)
    {
        Logger.EnterFunction("AddEdge", $"MeshState: {state}, From {p1} to {p2} with index {index}");
        Edge returnEdge = GetOrCreateEdge(p1, p2, index);
        Logger.ExitFunction("AddEdge", $"edgeIndex={returnEdge.Index}");
        return returnEdge;
    }

    /// <summary>
    /// Adds a pre-existing edge to the mesh structure.
    /// </summary>
    /// <param name="edge">The Edge object to add.</param>
    /// <remarks>
    /// This method is thread-safe. It registers the edge in both canonical and legacy systems
    /// using RegisterEdgeCanonicalAndLegacy. The edge must already be properly constructed
    /// with valid points.
    /// </remarks>
    public void AddEdge(Edge edge)
    {
        Logger.EnterFunction("AddEdge", $"MeshState: {state}, edgeIndex={edge.Index}");
        lock (lockObject)
        {
            RegisterEdgeCanonicalAndLegacy(edge);
        }
        Logger.ExitFunction("AddEdge");
    }
    /// <summary>
    /// Adds a pre-existing triangle to the mesh structure.
    /// </summary>
    /// <param name="triangle">The Triangle object to add.</param>
    /// <remarks>
    /// This method is thread-safe. It registers the triangle in canonical registries first,
    /// then performs legacy mirroring based on the current mesh state:
    /// - Ungenerated: Adds to BaseTris and EdgeTriangles for base mesh operations
    /// - BaseMesh: Updates Voronoi-related mappings for dual mesh operations
    /// - DualMesh: Currently no additional operations
    /// </remarks>
    public void AddTriangle(Triangle triangle)
    {
        Logger.EnterFunction("AddTriangle", $"MeshState: {state}, triIndex={triangle.Index}");
        lock (lockObject)
        {
            // Registry first
            RegisterTriangleInRegistriesOnly(triangle);

            // Legacy mirroring by state to preserve behavior
            switch (state)
            {
                case MeshState.Ungenerated:
                    //BaseTris.Add(((Point)triangle.Points[0], (Point)triangle.Points[1], (Point)triangle.Points[2]), triangle);
                    if (!EdgeTriangles.ContainsKey((Edge)triangle.Edges[0])) EdgeTriangles[(Edge)triangle.Edges[0]] = new List<Triangle>();
                    if (!EdgeTriangles.ContainsKey((Edge)triangle.Edges[1])) EdgeTriangles[(Edge)triangle.Edges[1]] = new List<Triangle>();
                    if (!EdgeTriangles.ContainsKey((Edge)triangle.Edges[2])) EdgeTriangles[(Edge)triangle.Edges[2]] = new List<Triangle>();
                    EdgeTriangles[(Edge)triangle.Edges[0]].Add(triangle);
                    EdgeTriangles[(Edge)triangle.Edges[1]].Add(triangle);
                    EdgeTriangles[(Edge)triangle.Edges[2]].Add(triangle);
                    Logger.Info($"Added triangle {triangle.Index} to BaseTris and EdgeTriangles");
                    break;
                case MeshState.BaseMesh:
                    foreach (Point p in triangle.Points)
                    {
                        var found = VoronoiTriMap.TryGetValue(p, out HashSet<Triangle> value);
                        if (!found) { VoronoiTriMap.Add(p, new HashSet<Triangle>()); }
                        VoronoiTriMap[p].Add(triangle);
                    }
                    foreach (Edge e in triangle.Edges)
                    {
                        var found = VoronoiEdgeTriMap.TryGetValue(e, out HashSet<Triangle> value);
                        if (!found) { VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>()); }
                        VoronoiEdgeTriMap[e].Add(triangle);
                        UpdateWorldEdgeMap((Point)e.P, (Point)e.Q);
                    }
                    break;
                case MeshState.DualMesh:
                    break;
            }
        }
        Logger.ExitFunction("AddTriangle");
    }

    /// <summary>
    /// Updates a point in the base mesh with a new point object.
    /// </summary>
    /// <param name="point">The original point to be replaced.</param>
    /// <param name="newPoint">The new point object to replace the original.</param>
    /// <remarks>
    /// This method is thread-safe. It updates both the legacy VertexPoints dictionary
    /// and the canonical PointsById dictionary. The original point's index is used as the key,
    /// and the new point is stored at that index.
    /// </remarks>
    public void UpdatePointBaseMesh(Point point, Point newPoint)
    {
        Logger.EnterFunction("UpdatePointBaseMesh", $"pointIndex={point.Index} -> newIndex={newPoint.Index}");
        lock (lockObject)
        {
            BaseVertices[newPoint.Index] = newPoint;
        }
        Logger.ExitFunction("UpdatePointBaseMesh");
    }

    /// <summary>
    /// Updates the world edge mapping for dual mesh operations.
    /// </summary>
    /// <param name="p1">The first point of the edge.</param>
    /// <param name="p2">The second point of the edge.</param>
    /// <returns>The Edge object that was updated or created.</returns>
    /// <remarks>
    /// This method ensures that the edge exists in both canonical and legacy systems,
    /// then updates the worldHalfEdgeMapFrom and worldHalfEdgeMapTo dictionaries for dual mesh operations.
    /// It maintains bidirectional mapping between points and their connecting edges.
    /// </remarks>
    public Edge UpdateWorldEdgeMap(Point p1, Point p2)
    {
        Logger.EnterFunction("UpdateWorldEdgeMap", $"p1={p1.Index}, p2={p2.Index}");
        Edge generatedEdge = Edge.MakeEdge(p1, p2);
        // Ensure canonical/legacy half-edges exist
        AddEdge(generatedEdge);

        Edge toUpdate = null;
        if (!MapContains(worldHalfEdgeMapTo, p1))
        {
            worldHalfEdgeMapTo.Add(p1, new Dictionary<Point, Edge>());
        }
        if (!MapContains(worldHalfEdgeMapFrom, p2))
        {
            worldHalfEdgeMapFrom.Add(p2, new Dictionary<Point, Edge>());
        }
        if (MapContains(worldHalfEdgeMapTo, p1, p2))
        {
            toUpdate = worldHalfEdgeMapTo[p1][p2];
        }
        else
        {
            worldHalfEdgeMapTo[p1].Add(p2, generatedEdge);
            toUpdate = generatedEdge;
        }
        if (MapContains(worldHalfEdgeMapFrom, p2, p1))
        {
            toUpdate = worldHalfEdgeMapFrom[p2][p1];
        }
        else
        {
            worldHalfEdgeMapFrom[p2].Add(p1, generatedEdge);
            toUpdate = generatedEdge;
        }
        Logger.ExitFunction("UpdateWorldEdgeMap", $"returned edgeIndex={toUpdate.Index}");
        return toUpdate;
    }

    /// <summary>
    /// Updates an existing edge with a new edge object.
    /// </summary>
    /// <param name="edge">The original edge to be replaced.</param>
    /// <param name="newEdge">The new edge object to replace the original.</param>
    /// <remarks>
    /// This method is thread-safe. It performs a comprehensive update by:
    /// - Removing canonical half-edges for the old edge
    /// - Updating legacy HalfEdgesFrom and Edges dictionaries
    /// - Adding the new edge to legacy structures
    /// - Recreating canonical half-edges for the new edge
    /// This ensures consistency across both canonical and legacy systems.
    /// </remarks>
    public void UpdateEdge(Edge edge, Edge newEdge)
    {
        Logger.EnterFunction("UpdateEdge", $"edgeIndex={edge.Index}  -> newIndex={newEdge.Index}");
        lock (lockObject)
        {
            // Remove canonical half-edges for old edge
            RemoveCanonicalHalfEdges(edge.P, edge.Q);

            // Recreate canonical half-edges for new edge
            RegisterEdgeCanonicalAndLegacy(newEdge);
        }
        Logger.ExitFunction("UpdateEdge");
    }

    /// <summary>
    /// Updates an existing triangle with a new triangle object.
    /// </summary>
    /// <param name="triangle">The original triangle to be replaced.</param>
    /// <param name="newTriangle">The new triangle object to replace the original.</param>
    /// <remarks>
    /// This method is thread-safe. It updates both canonical and legacy systems:
    /// - Canonical: Replaces the triangle in TrianglesById and updates edge-key mappings
    /// - Legacy: Removes the old triangle from EdgeTriangles, updates BaseTris,
    ///   and adds the new triangle to EdgeTriangles
    /// This ensures consistency across all triangle references in the mesh structure.
    /// </remarks>
    public void UpdateTriangle(Triangle triangle, Triangle newTriangle)
    {
        Logger.EnterFunction("UpdateTriangle", $"triIndex={triangle.Index} -> newIndex={newTriangle.Index}");
        lock (lockObject)
        {
            // Canonical: replace in registry and edge-key map
            if (TrianglesById.ContainsKey(triangle.Index))
            {
                TrianglesById[triangle.Index] = newTriangle;
            }
            foreach (var e in triangle.Edges)
            {
                var key = EdgeKey.From(e.P, e.Q);
                if (TrianglesByEdgeKey.TryGetValue(key, out var list))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].Index == triangle.Index) { list[i] = newTriangle; }
                    }
                }
                else
                {
                    TrianglesByEdgeKey.Add(key, new List<Triangle> { newTriangle });
                }
            }

            // Legacy mirrors
            //EdgeTriangles[(Edge)triangle.Edges[0]].Remove(triangle);
            //EdgeTriangles[(Edge)triangle.Edges[1]].Remove(triangle);
            //EdgeTriangles[(Edge)triangle.Edges[2]].Remove(triangle);
            BaseTris.Add(triangle);
            if (!EdgeTriangles.ContainsKey((Edge)triangle.Edges[0])) EdgeTriangles[(Edge)triangle.Edges[0]] = new List<Triangle>();
            if (!EdgeTriangles.ContainsKey((Edge)triangle.Edges[1])) EdgeTriangles[(Edge)triangle.Edges[1]] = new List<Triangle>();
            if (!EdgeTriangles.ContainsKey((Edge)triangle.Edges[2])) EdgeTriangles[(Edge)triangle.Edges[2]] = new List<Triangle>();
            EdgeTriangles[(Edge)triangle.Edges[0]].Add(newTriangle);
            EdgeTriangles[(Edge)triangle.Edges[1]].Add(newTriangle);
            EdgeTriangles[(Edge)triangle.Edges[2]].Add(newTriangle);
        }
        Logger.ExitFunction("UpdateTriangle");
    }

    /// <summary>
    /// Selects a random point from the mesh structure.
    /// </summary>
    /// <param name="rand">A RandomNumberGenerator instance to use for random selection.</param>
    /// <returns>A randomly selected Point object from the VertexPoints dictionary.</returns>
    /// <remarks>
    /// This method is thread-safe. It creates a list from the VertexPoints values
    /// and uses the provided RandomNumberGenerator to select a random point.
    /// Returns null if no points are available.
    /// </remarks>
    public Point SelectRandomPoint(RandomNumberGenerator rand)
    {
        Logger.EnterFunction("SelectRandomPoint");
        lock (lockObject)
        {
            List<Point> points = new List<Point>(BaseVertices.Values.Where(p => !UsedPoints.Contains(p)));
            Point selected = points[rand.RandiRange(0, points.Count - 1)];
            Logger.ExitFunction("SelectRandomPoint", $"returned pointIndex={selected.Index}");
            return selected;
        }
    }

    public void RemoveFromRandom(List<Point> points)
    {
        foreach (Point p in points)
        {
            UsedPoints.Add(p);
        }
    }

    /// <summary>
    /// Removes an edge from the mesh structure.
    /// </summary>
    /// <param name="edge">The Edge object to remove.</param>
    /// <remarks>
    /// This method is thread-safe. It performs comprehensive cleanup by:
    /// - Removing canonical half-edges for both directions
    /// - Removing the edge and its reverse from the Edges dictionary
    /// - Removing edge-triangle relationships from EdgeTriangles
    /// - Removing the edge from HalfEdgesFrom for both endpoints
    /// This ensures the edge is completely removed from all data structures.
    /// </remarks>
    public void RemoveEdge(Edge edge)
    {
        Logger.EnterFunction("RemoveEdge", $"edgeIndex={edge.Index}");
        lock (lockObject)
        {
            // Canonical removal
            RemoveCanonicalHalfEdges(edge.P, edge.Q);
        }
        Logger.ExitFunction("RemoveEdge");
    }

    // ===== Phase 1: Minimal helpers for later phases =====
    /// <summary>
    /// Gets an existing circumcenter with the specified index or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="index">The unique index of the circumcenter point.</param>
    /// <param name="pos">The 3D position of the circumcenter.</param>
    /// <returns>The existing or newly created circumcenter Point object.</returns>
    /// <remarks>
    /// This method is thread-safe. Circumcenters are special points used in Delaunay triangulation
    /// and Voronoi diagram generation. The method stores circumcenters in a dedicated dictionary
    /// while also maintaining them in the canonical and legacy point registries.
    /// </remarks>
    public Point GetOrCreateCircumcenter(int index, Vector3 pos)
    {
        lock (lockObject)
        {
            if (VoronoiVertices.TryGetValue(index, out var p)) return p;
            var point = new Point(pos, index);
            VoronoiVertices[index] = point;
            if (!BaseVertices.ContainsKey(index)) BaseVertices[index] = point;
            return point;
        }
    }

    /// <summary>
    /// Adds a Voronoi cell to the mapping for a specific vertex point.
    /// </summary>
    /// <param name="p">The vertex point to associate with the Voronoi cell.</param>
    /// <param name="cell">The VoronoiCell object to add.</param>
    /// <remarks>
    /// This method is thread-safe. It maintains the relationship between vertices and
    /// the Voronoi cells that contain them. The point is also added to VoronoiCellVertices
    /// to track all vertices that participate in Voronoi cells.
    /// </remarks>
    public void AddCellForVertex(Point p, VoronoiCell cell)
    {
        lock (lockObject)
        {
            if (!CellMap.TryGetValue(p, out var set))
            {
                set = new HashSet<VoronoiCell>();
                CellMap[p] = set;
            }
            set.Add(cell);
            VoronoiCellVertices.Add(p);
        }
    }

    /// <summary>
    /// Adds a Voronoi cell to the mapping for a specific edge key.
    /// </summary>
    /// <param name="key">The EdgeKey representing the undirected edge.</param>
    /// <param name="cell">The VoronoiCell object to add.</param>
    /// <remarks>
    /// This method is thread-safe. It maintains the relationship between edges and
    /// the Voronoi cells that contain them. It also mirrors the relationship to the
    /// legacy EdgeMap if matching edge instances exist. The method searches for both
    /// directions of the edge (A->B and B->A) in the legacy system.
    /// </remarks>
    public void AddCellForEdge(EdgeKey key, VoronoiCell cell)
    {
        lock (lockObject)
        {
            if (!EdgeKeyCellMap.TryGetValue(key, out var set))
            {
                set = new HashSet<VoronoiCell>();
                EdgeKeyCellMap[key] = set;
            }
            set.Add(cell);

            // Mirror to legacy EdgeMap only if a matching legacy key (same endpoints, any instance) exists
            if (BaseVertices.TryGetValue(key.A, out var pa) && BaseVertices.TryGetValue(key.B, out var pb))
            {
                Edge matchAB = null;
                Edge matchBA = null;
                foreach (var kv in EdgeMap)
                {
                    var e = kv.Key;
                    if (e.P == pa && e.Q == pb) matchAB = e;
                    else if (e.P == pb && e.Q == pa) matchBA = e;
                    if (matchAB != null && matchBA != null) break;
                }
                if (matchAB != null)
                {
                    if (!EdgeMap[matchAB].Contains(cell)) EdgeMap[matchAB].Add(cell);
                }
                if (matchBA != null)
                {
                    if (!EdgeMap[matchBA].Contains(cell)) EdgeMap[matchBA].Add(cell);
                }
            }
        }
    }

    // ===== Phase 3–4: Phase reset hook =====
    /// <summary>
    /// Resets the mesh structure to prepare for a specific generation phase.
    /// </summary>
    /// <param name="target">The target MeshState to prepare for.</param>
    /// <remarks>
    /// This method is thread-safe and performs cleanup operations based on the target phase:
    /// - Ungenerated: No operation (initial state)
    /// - BaseMesh: Clears all dual mesh related containers to prepare for base mesh generation
    /// - DualMesh: No destructive reset (dual mesh builds on base mesh)
    /// This ensures clean state transitions between mesh generation phases.
    /// </remarks>
    public void ResetPhase(MeshState target)
    {
        Logger.EnterFunction("ResetPhase", $"target={target}");
        lock (lockObject)
        {
            switch (target)
            {
                case MeshState.Ungenerated:
                    // No-op
                    break;
                case MeshState.BaseMesh:
                    // Reset dual-related containers
                    worldHalfEdgeMapFrom.Clear();
                    worldHalfEdgeMapTo.Clear();
                    VoronoiCells.Clear();
                    VoronoiCellVertices.Clear();
                    CellMap.Clear();
                    EdgeMap.Clear();
                    VoronoiTriMap.Clear();
                    VoronoiEdgeTriMap.Clear();
                    break;
                case MeshState.DualMesh:
                    // No destructive reset at entry to dual
                    break;
            }
        }
        Logger.ExitFunction("ResetPhase");
    }

    // ===== Helpers =====
    /// <summary>
    /// Registers an edge in both canonical and legacy data structures.
    /// </summary>
    /// <param name="e">The edge to register.</param>
    /// <remarks>
    /// This method performs comprehensive registration by:
    /// - Ensuring points exist in both canonical and legacy registries
    /// - Creating canonical half-edges in both directions with twin linking
    /// - Adding the edge to the undirected edge index registry
    /// - Adding both edge directions to the legacy Edges dictionary
    /// - Maintaining symmetry in the legacy HalfEdgesFrom dictionary
    /// </remarks>
    private void RegisterEdgeCanonicalAndLegacy(Edge e)
    {
        // Points registry
        if (!BaseVertices.ContainsKey(e.P.Index)) BaseVertices[e.P.Index] = e.P;
        if (!BaseVertices.ContainsKey(e.Q.Index)) BaseVertices[e.Q.Index] = e.Q;

        // Canonical half-edges (both directions, twin-linked)
        EnsureHalfEdgePair(e.P, e.Q);

        // Canonical undirected index (do not overwrite once present)
        var key = EdgeKey.From(e.P, e.Q);
        if (!UndirectedEdgeIndex.ContainsKey(key)) UndirectedEdgeIndex[key] = e;
    }

    /// <summary>
    /// Registers a triangle in canonical registries only.
    /// </summary>
    /// <param name="tri">The triangle to register.</param>
    /// <remarks>
    /// This method performs canonical registration by:
    /// - Adding the triangle to the TrianglesById registry
    /// - Ensuring all edges have canonical entries via RegisterEdgeCanonicalAndLegacy
    /// - Registering the triangle in TrianglesByEdgeKey for each edge
    /// - Setting the Left property of forward-oriented half-edges to reference this triangle
    /// </remarks>
    private void RegisterTriangleInRegistriesOnly(Triangle tri)
    {
        if (!TrianglesById.ContainsKey(tri.Index)) TrianglesById[tri.Index] = tri;
        // Ensure all edges have canonical entries and register edge->triangle
        for (int i = 0; i < 3; i++)
        {
            var e = tri.Edges[i];
            RegisterEdgeCanonicalAndLegacy(e);
            var key = EdgeKey.From(e.P, e.Q);
            if (!TrianglesByEdgeKey.TryGetValue(key, out var list))
            {
                list = new List<Triangle>();
                TrianglesByEdgeKey[key] = list;
            }
            list.Add(tri);

            // Ensure half-edges exist and optionally set Left for forward orientation
            var hf = FindHalfEdge(e.P, e.Q);
            if (hf != null && hf.Left == null) hf.Left = tri;
        }
    }

    /// <summary>
    /// Ensures that a pair of twin half-edges exists between two points.
    /// </summary>
    /// <param name="a">The starting point of the forward half-edge.</param>
    /// <param name="b">The ending point of the forward half-edge.</param>
    /// <remarks>
    /// This method checks for existing half-edges in both directions and creates missing ones.
    /// It ensures that the half-edges are properly linked as twins. If one half-edge exists
    /// but its twin is null, the twin relationship is established.
    /// </remarks>
    private void EnsureHalfEdgePair(Point a, Point b)
    {
        var fwd = FindHalfEdge(a, b);
        var rev = FindHalfEdge(b, a);
        if (fwd != null && rev != null)
        {
            if (fwd.Twin == null) fwd.Twin = rev;
            if (rev.Twin == null) rev.Twin = fwd;
            return;
        }

        // Create missing ones
        if (fwd == null) fwd = CreateHalfEdge(a, b);
        if (rev == null) rev = CreateHalfEdge(b, a);
        fwd.Twin = rev;
        rev.Twin = fwd;
    }

    /// <summary>
    /// Creates a new half-edge from one point to another.
    /// </summary>
    /// <param name="from">The starting point of the half-edge.</param>
    /// <param name="to">The ending point of the half-edge.</param>
    /// <returns>The newly created HalfEdge object.</returns>
    /// <remarks>
    /// This method creates a half-edge with a unique ID and registers it in the canonical registries.
    /// The half-edge is added to both HalfEdgeById and OutHalfEdgesByPoint dictionaries.
    /// </remarks>
    private HalfEdge CreateHalfEdge(Point from, Point to)
    {
        int id = HalfEdgeById.Count;
        EdgeKey key = EdgeKey.From(from, to);
        var h = new HalfEdge(from, to, key);
        HalfEdgeById[id] = h;
        if (!OutHalfEdgesByPoint.TryGetValue(from, out var set))
        {
            set = new HashSet<HalfEdge>();
            OutHalfEdgesByPoint[from] = set;
        }
        set.Add(h);
        return h;
    }

    /// <summary>
    /// Finds a half-edge from one point to another.
    /// </summary>
    /// <param name="from">The starting point of the half-edge to find.</param>
    /// <param name="to">The ending point of the half-edge to find.</param>
    /// <returns>The HalfEdge object if found; otherwise, null.</returns>
    /// <remarks>
    /// This method searches the OutHalfEdgesByPoint dictionary for a half-edge
    /// that starts at the specified 'from' point and ends at the specified 'to' point.
    /// </remarks>
    private HalfEdge FindHalfEdge(Point from, Point to)
    {
        if (OutHalfEdgesByPoint.TryGetValue(from, out var set))
        {
            foreach (var h in set)
            {
                if (h.Twin.From == to) return h;
            }
        }
        return null;
    }

    /// <summary>
    /// Removes canonical half-edges between two points in both directions.
    /// </summary>
    /// <param name="a">The first point of the edge.</param>
    /// <param name="b">The second point of the edge.</param>
    /// <remarks>
    /// This method removes both forward and reverse half-edges from the canonical registries.
    /// It removes the half-edges from both OutHalfEdgesByPoint and HalfEdgeById dictionaries.
    /// This ensures complete cleanup of canonical half-edge data.
    /// </remarks>
    private void RemoveCanonicalHalfEdges(Point a, Point b)
    {
        var fwd = FindHalfEdge(a, b);
        var rev = FindHalfEdge(b, a);
        if (fwd != null)
        {
            if (OutHalfEdgesByPoint.TryGetValue(a, out var set)) set.Remove(fwd);
            //HalfEdgeById.Remove(fwd.Id);
        }
        if (rev != null)
        {
            if (OutHalfEdgesByPoint.TryGetValue(b, out var set2)) set2.Remove(rev);
            //HalfEdgeById.Remove(rev.Id);
        }
    }

    /// <summary>
    /// Checks if a nested dictionary contains a specific point as a key.
    /// </summary>
    /// <param name="map">The nested dictionary to check.</param>
    /// <param name="p1">The point to check for existence as a top-level key.</param>
    /// <returns>true if the point exists as a key in the top-level dictionary; otherwise, false.</returns>
    private static bool MapContains(Dictionary<Point, Dictionary<Point, Edge>> map, Point p1)
    {
        return map.ContainsKey(p1);
    }

    /// <summary>
    /// Checks if a nested dictionary contains a specific edge between two points.
    /// </summary>
    /// <param name="map">The nested dictionary to check.</param>
    /// <param name="p1">The starting point of the edge.</param>
    /// <param name="p2">The ending point of the edge.</param>
    /// <returns>true if the edge exists in the nested dictionary; otherwise, false.</returns>
    private static bool MapContains(Dictionary<Point, Dictionary<Point, Edge>> map, Point p1, Point p2)
    {
        if (map.ContainsKey(p1))
        {
            return map[p1].ContainsKey(p2);
        }
        return false;
    }

    /// <summary>
    /// Checks if an index has been created for a specific point in a nested dictionary.
    /// </summary>
    /// <param name="map">The nested dictionary to check.</param>
    /// <param name="p1">The point to check for index creation.</param>
    /// <returns>true if an index exists for the point; otherwise, false.</returns>
    private static bool MapIndexCreated(Dictionary<Point, Dictionary<Point, Edge>> map, Point p1)
    {
        return map[p1] != null;
    }


    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<Edge, List<Triangle>> LegacyEdgeTriangles => new System.Collections.ObjectModel.ReadOnlyDictionary<Edge, List<Triangle>>(EdgeTriangles);

    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>> LegacyWorldHalfEdgeMapFrom => new System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>>(worldHalfEdgeMapFrom);

    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>> LegacyWorldHalfEdgeMapTo => new System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>>(worldHalfEdgeMapTo);

    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<int, Point> LegacyCircumcenters => new System.Collections.ObjectModel.ReadOnlyDictionary<int, Point>(VoronoiVertices);

    // ===== Phase 3–4: Internal container types =====
    /// <summary>
    /// Container for base mesh related data structures providing read-only access.
    /// </summary>
    public sealed class BaseContainer
    {
        /// <summary>
        /// Reference to the parent StructureDatabase instance.
        /// </summary>
        private readonly StructureDatabase db;

        /// <summary>
        /// Initializes a new instance of the BaseContainer class.
        /// </summary>
        /// <param name="db">The parent StructureDatabase instance.</param>
        internal BaseContainer(StructureDatabase db) { this.db = db; }

        /// <summary>
        /// Gets a read-only dictionary of base triangles keyed by their three vertex points.
        /// </summary>
        public System.Collections.ObjectModel.ReadOnlyCollection<Triangle> Triangles =>
            new System.Collections.ObjectModel.ReadOnlyCollection<Triangle>(db.BaseTris.ToList());

    }

    /// <summary>
    /// Container for dual mesh related data structures providing read-only access.
    /// </summary>
    public sealed class DualContainer
    {
        /// <summary>
        /// Reference to the parent StructureDatabase instance.
        /// </summary>
        private readonly StructureDatabase db;

        /// <summary>
        /// Initializes a new instance of the DualContainer class.
        /// </summary>
        /// <param name="db">The parent StructureDatabase instance.</param>
        internal DualContainer(StructureDatabase db) { this.db = db; }

        /// <summary>
        /// Gets a read-only dictionary mapping source points to destination points and their connecting edges for dual mesh.
        /// </summary>
        public System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>> WorldFrom =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>>(db.worldHalfEdgeMapFrom);

        /// <summary>
        /// Gets a read-only dictionary mapping destination points to source points and their connecting edges for dual mesh.
        /// </summary>
        public System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>> WorldTo =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>>(db.worldHalfEdgeMapTo);

        /// <summary>
        /// Gets a read-only dictionary mapping edges to the Voronoi cells that contain them.
        /// </summary>
        public System.Collections.ObjectModel.ReadOnlyDictionary<Edge, HashSet<VoronoiCell>> EdgeCells =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<Edge, HashSet<VoronoiCell>>(db.EdgeMap);

        /// <summary>
        /// Gets a read-only dictionary mapping points to the Voronoi cells that contain them.
        /// </summary>
        public System.Collections.ObjectModel.ReadOnlyDictionary<Point, HashSet<VoronoiCell>> CellMap =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<Point, HashSet<VoronoiCell>>(db.CellMap);
    }
}

