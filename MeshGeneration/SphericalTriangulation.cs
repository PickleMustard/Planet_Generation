using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MeshGeneration;

// Ideal, dependency-free spherical Delaunay and Voronoi for Godot C#.
// Strategy:
// 1) Compute the 3D convex hull of unit points (QuickHull3D).
// 2) Triangulation on the sphere is the radial projection of hull facets.
// 3) Voronoi vertices are great-circle circumcenters from triangle triples.
// Notes:
// - Uses doubles internally for stability, returns Vector3.
// - Input points are normalized by default; antipodal duplicates are rejected.
// - Produces triangle indices into the deduplicated vertex array it stores.
public class SphericalTriangulation
{
    public IReadOnlyList<Vector3> Vertices => _vertices;
    public IReadOnlyList<(int a, int b, int c)> Triangles => _triangles;

    // Per-site Voronoi cell as ordered list of circumcenters (on sphere)
    // Index by site vertex index in Vertices
    public IReadOnlyList<IReadOnlyList<Vector3>> VoronoiCells => _voronoiCells;

    private readonly List<Vector3> _vertices = new();
    private readonly List<(int a, int b, int c)> _triangles = new();
    private readonly List<List<int>> _vertexToTriangles = new();
    private readonly List<IReadOnlyList<Vector3>> _voronoiCells = new();

    public struct BuildOptions
    {
        public bool NormalizeInput;     // Normalize input to unit sphere
        public float MergeEpsilon;      // Angle epsilon (radians) for merging nearly-identical points
        public float MinHullAreaEps;    // Minimum face area to keep during hull construction

        public static BuildOptions Default => new BuildOptions
        {
            NormalizeInput = true,
            MergeEpsilon = 1e-6f,
            MinHullAreaEps = 1e-10f,
        };
    }

    public void Build(IReadOnlyList<Vector3> input, BuildOptions options = default)
    {
        if (options.Equals(default(BuildOptions))) options = BuildOptions.Default;
        _vertices.Clear();
        _triangles.Clear();
        _vertexToTriangles.Clear();
        _voronoiCells.Clear();

        if (input == null || input.Count < 3)
            throw new ArgumentException("Need >= 3 points on sphere");

        // 1) Prepare unit input and merge near-duplicates
        var uniq = new List<Vector3>();
        foreach (var v in input)
        {
            Vector3 u = v;
            if (options.NormalizeInput)
            {
                if (u == Vector3.Zero) continue;
                u = u.Normalized();
            }
            if (!IsFinite(u)) continue;
            if (!ExistsNear(uniq, u, options.MergeEpsilon))
                uniq.Add(u);
        }
        if (uniq.Count < 3) throw new ArgumentException("Not enough unique unit points");

        _vertices.AddRange(uniq);
        for (int i = 0; i < _vertices.Count; i++) _vertexToTriangles.Add(new List<int>());

        // 2) Compute convex hull
        var hull = QuickHull3D.BuildHull(_vertices);
        if (hull.Faces.Count == 0) throw new InvalidOperationException("Convex hull failed");

        // 3) Emit triangles, ensure consistent orientation so outward normal points away from origin
        for (int fi = 0; fi < hull.Faces.Count; fi++)
        {
            var f = hull.Faces[fi];
            if (f.Indices.Length < 3) continue;
            // Triangulate polygon fan around v0
            int v0 = f.Indices[0];
            for (int k = 1; k + 1 < f.Indices.Length; k++)
            {
                int v1 = f.Indices[k];
                int v2 = f.Indices[k + 1];
                var a = _vertices[v0];
                var b = _vertices[v1];
                var c = _vertices[v2];
                var n = (b - a).Cross(c - a);
                // Outward normal should point away from origin. For points on unit sphere, dot(n, (a+b+c)/3) should be > 0
                if (n.Dot((a + b + c) / 3f) < 0f)
                {
                    // Flip winding
                    (v1, v2) = (v2, v1);
                }
                _vertexToTriangles[v0].Add(_triangles.Count);
                _vertexToTriangles[v1].Add(_triangles.Count);
                _vertexToTriangles[v2].Add(_triangles.Count);
                _triangles.Add((v0, v1, v2));
            }
        }

        // 4) Build Voronoi cells (dual). For each site, collect adjacent triangle circumcenters and order them on tangent plane.
        var triCircumcenters = new Vector3[_triangles.Count];
        for (int i = 0; i < _triangles.Count; i++)
        {
            var (a, b, c) = _triangles[i];
            triCircumcenters[i] = GreatCircleCircumcenter(_vertices[a], _vertices[b], _vertices[c]);
        }

        for (int vi = 0; vi < _vertices.Count; vi++)
        {
            var incident = _vertexToTriangles[vi];
            if (incident.Count == 0)
            {
                _voronoiCells.Add(Array.Empty<Vector3>());
                continue;
            }
            // Project circumcenters to tangent plane at site and order by polar angle
            var site = _vertices[vi];
            var u = PickTangent(site);
            var v = site.Cross(u);
            var ordered = incident
                .Select(ti => new { ti, cc = triCircumcenters[ti] })
                .Select(x => new { x.ti, cc = x.cc, p = new Vector2(x.cc.Dot(u), x.cc.Dot(v)) })
                .OrderBy(x => Mathf.Atan2(x.p.Y, x.p.X))
                .Select(x => x.cc)
                .ToList();
            _voronoiCells.Add(ordered);
        }
    }

    public static Vector3 GreatCircleCircumcenter(in Vector3 a, in Vector3 b, in Vector3 c)
    {
        // Intersect the planes (a,b) and (a,c): n1 = a x b, n2 = a x c; center dir = n1 x n2
        var n1 = a.Cross(b);
        var n2 = a.Cross(c);
        var dir = n1.Cross(n2);
        if (dir == Vector3.Zero)
        {
            // Degenerate (collinear on great circle): return average normalized
            var avg = (a + b + c) / 3f;
            return avg == Vector3.Zero ? a : avg.Normalized();
        }
        dir = dir.Normalized();
        // Pick the solution consistent with triangle's outward orientation
        if (dir.Dot((a + b + c) / 3f) < 0f) dir = -dir;
        return dir;
    }

    private static bool ExistsNear(List<Vector3> pts, Vector3 v, float eps)
    {
        // Merge by angular epsilon (use dot threshold)
        foreach (var p in pts)
        {
            float d = p.Dot(v);
            if (d > 1f - eps) return true;
            if (d < -1f + eps) return true; // treat antipodal as duplicate (degenerate for Delaunay)
        }
        return false;
    }

    private static bool IsFinite(Vector3 v)
    {
        return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
    }
    private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

    private static Vector3 PickTangent(in Vector3 n)
    {
        // Choose a stable tangent vector not parallel to n
        Vector3 t = MathF.Abs(n.X) > 0.5f ? new Vector3(-n.Y, n.X, 0f) : new Vector3(0f, -n.Z, n.Y);
        if (t == Vector3.Zero) t = new Vector3(1, 0, 0);
        return t.Normalized();
    }

    // Lightweight QuickHull for unit points
    private class QuickHull3D
    {
        public struct Hull
        {
            public List<Face> Faces;
        }
        public struct Face
        {
            public int[] Indices; // CCW around outward normal
        }

        private struct HalfEdge
        {
            public int From;
            public int To;
        }

        public static Hull BuildHull(IReadOnlyList<Vector3> pts)
        {
            if (pts.Count < 4)
            {
                // Create a single triangle fan around the first point to form a minimal hull on sphere
                var faces = new List<Face>();
                int v0 = 0;
                for (int i = 1; i + 1 < pts.Count; i++)
                {
                    faces.Add(new Face { Indices = new[] { v0, i, i + 1 } });
                }
                return new Hull { Faces = faces };
            }

            // Convert to double for robustness
            var P = pts.Select(p => new double[] { p.X, p.Y, p.Z }).ToArray();

            // 1) Find initial tetrahedron with non-zero volume
            if (!FindInitialTetra(P, out int a, out int b, out int c, out int d))
            {
                // Almost coplanar: fall back to fan
                var faces = new List<Face>();
                int v0 = 0;
                for (int i = 1; i + 1 < pts.Count; i++)
                    faces.Add(new Face { Indices = new[] { v0, i, i + 1 } });
                return new Hull { Faces = faces };
            }

            // Oriented so that ABC has D on positive side
            if (SignedVolume(P[a], P[b], P[c], P[d]) < 0)
                (b, c) = (c, b);

            // Current hull faces as oriented triangles
            var facesList = new List<(int i0, int i1, int i2)> {
                (a,b,c), (a,d,b), (b,d,c), (c,d,a)
            };

            // Assign points to faces' outside sets
            var outside = new Dictionary<int, List<int>>();
            for (int i = 0; i < facesList.Count; i++) outside[i] = new List<int>();
            var assigned = new HashSet<int> { a, b, c, d };

            for (int i = 0; i < P.Length; i++)
            {
                if (assigned.Contains(i)) continue;
                int bestFace = -1;
                double bestDist = 1e-12;
                for (int fi = 0; fi < facesList.Count; fi++)
                {
                    var f = facesList[fi];
                    double dist = DistanceToPlane(P[i], P[f.i0], P[f.i1], P[f.i2]);
                    if (dist > bestDist)
                    {
                        bestDist = dist; bestFace = fi;
                    }
                }
                if (bestFace >= 0)
                {
                    outside[bestFace].Add(i);
                }
            }

            // Main loop: expand hull with farthest points from faces
            while (true)
            {
                // Pick a face with outside points
                int pickFace = -1; int pickPoint = -1; double maxDist = 0;
                for (int fi = 0; fi < facesList.Count; fi++)
                {
                    if (outside[fi].Count == 0) continue;
                    var f = facesList[fi];
                    int far = -1; double farDist = -1;
                    foreach (var pi in outside[fi])
                    {
                        var dist = DistanceToPlane(P[pi], P[f.i0], P[f.i1], P[f.i2]);
                        if (dist > farDist) { farDist = dist; far = pi; }
                    }
                    if (far != -1 && farDist > maxDist)
                    {
                        maxDist = farDist; pickFace = fi; pickPoint = far;
                    }
                }
                if (pickFace == -1) break; // done

                // Find all faces visible from pickPoint
                var visible = new HashSet<int>();
                var stack = new Stack<int>();
                stack.Push(pickFace);
                while (stack.Count > 0)
                {
                    var fi = stack.Pop();
                    if (visible.Contains(fi)) continue;
                    var f = facesList[fi];
                    double dist = DistanceToPlane(P[pickPoint], P[f.i0], P[f.i1], P[f.i2]);
                    if (dist > 1e-12)
                    {
                        visible.Add(fi);
                        // Explore neighbors by edges (approximate since we don't keep adjacency: check all faces sharing an edge)
                        for (int fj = 0; fj < facesList.Count; fj++)
                        {
                            if (visible.Contains(fj)) continue;
                            var g = facesList[fj];
                            int shared = SharedEdgeCount(f, g);
                            if (shared >= 2) stack.Push(fj);
                        }
                    }
                }

                // Horizon edges: boundary between visible and non-visible
                var horizon = new List<(int from, int to)>();
                for (int fi = 0; fi < facesList.Count; fi++)
                {
                    if (!visible.Contains(fi)) continue;
                    var f = facesList[fi];
                    var edges = new (int from, int to)[] { (f.i0, f.i1), (f.i1, f.i2), (f.i2, f.i0) };
                    foreach (var e in edges)
                    {
                        bool isSharedByInvisible = false;
                        for (int fj = 0; fj < facesList.Count; fj++)
                        {
                            if (visible.Contains(fj)) continue;
                            var g = facesList[fj];
                            if (HasDirectedEdge(g, e.from, e.to)) { isSharedByInvisible = true; break; }
                            if (HasDirectedEdge(g, e.to, e.from)) { isSharedByInvisible = true; break; }
                        }
                        if (!isSharedByInvisible)
                        {
                            horizon.Add((e.from, e.to));
                        }
                    }
                }

                // Remove visible faces
                var newFaces = new List<(int i0, int i1, int i2)>();
                var newOutside = new Dictionary<int, List<int>>();
                for (int fi = 0; fi < facesList.Count; fi++)
                {
                    if (!visible.Contains(fi))
                    {
                        newOutside[newFaces.Count] = outside[fi];
                        newFaces.Add(facesList[fi]);
                    }
                }
                facesList = newFaces;
                outside = newOutside;

                // Create new faces from horizon with pickPoint, orient CCW so that pickPoint is on positive side
                int baseIndex = facesList.Count;
                foreach (var e in horizon)
                {
                    var f = (e.from, e.to, pickPoint);
                    // Ensure orientation: pickPoint should be outside
                    if (DistanceToPlane(P[pickPoint], P[f.from], P[f.to], P[f.pickPoint]) < 0)
                        f = (e.to, e.from, pickPoint);
                    facesList.Add((f.from, f.to, f.pickPoint));
                    outside[facesList.Count - 1] = new List<int>();
                }

                // Reassign points from removed visible faces to new faces if still outside
                var candidates = new HashSet<int>();
                foreach (var fi in visible)
                    foreach (var pi in outside.ContainsKey(fi) ? outside[fi] : new List<int>()) candidates.Add(pi);
                candidates.Add(pickPoint);

                foreach (var pi in candidates)
                {
                    if (assigned.Contains(pi)) assigned.Remove(pi);
                }
                for (int fi = baseIndex; fi < facesList.Count; fi++)
                {
                    var f = facesList[fi];
                    foreach (var pi in candidates)
                    {
                        var dist = DistanceToPlane(P[pi], P[f.i0], P[f.i1], P[f.i2]);
                        if (dist > 1e-12)
                        {
                            outside[fi].Add(pi);
                        }
                    }
                }
            }

            // Build result faces (merge coplanar triangles into polygons could be added; we leave as triangles)
            var result = new Hull { Faces = new List<Face>() };
            foreach (var f in facesList)
            {
                result.Faces.Add(new Face { Indices = new[] { f.i0, f.i1, f.i2 } });
            }
            return result;
        }

        private static bool FindInitialTetra(double[][] P, out int a, out int b, out int c, out int d)
        {
            a = b = c = d = -1;
            if (P.Length < 4) return false;

            a = 0;
            // b: farthest from a
            double best = -1;
            for (int i = 1; i < P.Length; i++)
            {
                double d2 = Dist2(P[a], P[i]);
                if (d2 > best) { best = d2; b = i; }
            }
            if (b == -1) return false;
            // c: farthest from line ab
            best = -1; c = -1;
            for (int i = 0; i < P.Length; i++)
            {
                if (i == a || i == b) continue;
                double area2 = Norm2(Cross(Sub(P[b], P[a]), Sub(P[i], P[a])));
                if (area2 > best) { best = area2; c = i; }
            }
            if (c == -1 || best < 1e-14) return false;
            // d: farthest from plane abc
            best = -1; d = -1;
            for (int i = 0; i < P.Length; i++)
            {
                if (i == a || i == b || i == c) continue;
                double vol6 = Math.Abs(SignedVolume(P[a], P[b], P[c], P[i]));
                if (vol6 > best) { best = vol6; d = i; }
            }
            return d != -1 && best > 1e-18;
        }

        private static double DistanceToPlane(double[] p, double[] a, double[] b, double[] c)
        {
            var n = Cross(Sub(b, a), Sub(c, a));
            var pa = Sub(p, a);
            double num = Dot(n, pa);
            double den = Math.Sqrt(Norm2(n)) + 1e-24;
            return num / den;
        }

        private static int SharedEdgeCount((int i0, int i1, int i2) f, (int j0, int j1, int j2) g)
        {
            int count = 0;
            if (f.i0 == g.j0 || f.i0 == g.j1 || f.i0 == g.j2) count++;
            if (f.i1 == g.j0 || f.i1 == g.j1 || f.i1 == g.j2) count++;
            if (f.i2 == g.j0 || f.i2 == g.j1 || f.i2 == g.j2) count++;
            return count;
        }
        private static bool HasDirectedEdge((int i0, int i1, int i2) f, int from, int to)
        {
            return (f.i0 == from && f.i1 == to) || (f.i1 == from && f.i2 == to) || (f.i2 == from && f.i0 == to);
        }

        private static double SignedVolume(double[] a, double[] b, double[] c, double[] d)
        {
            var ab = Sub(b, a);
            var ac = Sub(c, a);
            var ad = Sub(d, a);
            return Dot(Cross(ab, ac), ad);
        }

        private static double Dist2(double[] a, double[] b)
        {
            double dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
            return dx * dx + dy * dy + dz * dz;
        }
        private static double[] Sub(double[] a, double[] b) => new double[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        private static double[] Cross(double[] a, double[] b) => new double[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        private static double Norm2(double[] a) => Dot(a, a);
    }
}

// Optional helper to visualize triangulation with ImmediateMesh lines
public class SphericalTriangulationDebug
{
    public static void DrawEdges(Node parent, IReadOnlyList<Vector3> verts, IReadOnlyList<(int a, int b, int c)> tris, Color? color = null)
    {
        Color col = color ?? Colors.Gold;
        foreach (var (a, b, c) in tris)
        {
            DrawLine(parent, verts[a], verts[b], col);
            DrawLine(parent, verts[b], verts[c], col);
            DrawLine(parent, verts[c], verts[a], col);
        }
    }

    private static void DrawLine(Node parent, Vector3 p1, Vector3 p2, Color color)
    {
        var meshInstance = new MeshInstance3D();
        var immediateMesh = new ImmediateMesh();
        var material = new StandardMaterial3D();
        material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
        material.AlbedoColor = color;
        immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        immediateMesh.SurfaceAddVertex(p1);
        immediateMesh.SurfaceAddVertex(p2);
        immediateMesh.SurfaceEnd();
        meshInstance.Mesh = immediateMesh;
        meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        if (parent is Node3D n3)
        {
            n3.AddChild(meshInstance);
        }
        else if (parent is Node n)
        {
            n.AddChild(meshInstance);
        }
    }
}
