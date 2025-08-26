using Godot;
using Structures;
using System;
using System.Collections.Generic;
using System.Linq;

public class Delaunator3D
{
    private readonly float EPSILON = Mathf.Pow(2, -52);
    private readonly int[] EDGE_STACK = new int[512];

    /// <summary>
    /// One value per half-edge, containing the point index of where a given half edge starts.
    /// </summary>
    public int[] Triangles { get; private set; }

    /// <summary>
    /// One value per half-edge, containing the opposite half-edge in the adjacent triangle, or -1 if there is no adjacent triangle
    /// </summary>
    public int[] Halfedges { get; private set; }

    /// <summary>
    /// The initial points Delaunator was constructed with.
    /// </summary>
    public Point[] Points { get; private set; }

    /// <summary>
    /// A list of point indices that traverses the hull of the points.
    /// </summary>
    public int[] Hull { get; private set; }

    private readonly int hashSize;
    private readonly int[] hullPrev;
    private readonly int[] hullNext;
    private readonly int[] hullTri;
    private readonly int[] hullHash;

    private float cx;
    private float cy;
    private float cz;

    private int trianglesLen;
    private readonly float[] coords;
    private readonly int hullStart;
    private readonly int hullSize;

    public Delaunator3D(Point[] points)
    {
        if (points.Length < 3)
        {
            throw new ArgumentOutOfRangeException("Need at least 3 points");
        }

        Points = points;
        coords = new float[Points.Length * 3];
        GD.Print(coords.Length);

        for (var i = 0; i < Points.Length; i++)
        {
            var p = Points[i];
            coords[3 * i] = p.X;
            coords[3 * i + 1] = p.Y;
            coords[3 * i + 2] = p.Z;
        }

        var n = points.Length;
        var maxTriangles = 2 * n - 5;

        Triangles = new int[maxTriangles * 3];

        Halfedges = new int[maxTriangles * 3];
        //hashSize = (int)Math.Ceiling(Math.Sqrt(n));
        hashSize = n*n;

        hullPrev = new int[n];
        hullNext = new int[n];
        hullTri = new int[n];
        hullHash = new int[hashSize];

        var ids = new int[n];

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var minZ = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        var maxZ = float.NegativeInfinity;

        for (var i = 0; i < n; i++)
        {
            var x = coords[2 * i];
            var y = coords[2 * i + 1];
            var z = coords[2 * i + 2];
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (z < minZ) minZ = z;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
            if (z > maxZ) maxZ = z;
            ids[i] = i;
        }

        var cx = (minX + maxX) / 2;
        var cy = (minY + maxY) / 2;
        var cz = (minZ + maxZ) / 2;

        var minDist = float.PositiveInfinity;
        var i0 = 0;
        var i1 = 0;
        var i2 = 0;

        // pick a seed point close to the center
        for (int i = 0; i < n; i++)
        {
            var d = Dist(cx, cy, cz, coords[2 * i], coords[2 * i + 1], coords[2 * i + 2]);
            if (d < minDist)
            {
                i0 = i;
                minDist = d;
            }
        }
        var i0x = coords[2 * i0];
        var i0y = coords[2 * i0 + 1];
        var i0z = coords[2 * i0 + 2];

        minDist = float.PositiveInfinity;

        // find the point closest to the seed
        for (int i = 0; i < n; i++)
        {
            if (i == i0) continue;
            var d = Dist(i0x, i0y, i0z, coords[2 * i], coords[2 * i + 1], coords[2 * i + 2]);
            if (d < minDist && d > 0)
            {
                i1 = i;
                minDist = d;
            }
        }

        var i1x = coords[2 * i1];
        var i1y = coords[2 * i1 + 1];
        var i1z = coords[2 * i1 + 2];

        var minRadius = float.PositiveInfinity;

        // find the third point which forms the smallest circumcircle with the first two
        for (int i = 0; i < n; i++)
        {
            if (i == i0 || i == i1) continue;
            var r = Circumradius(i0x, i0y, i0z, i1x, i1y, i1z, coords[2 * i], coords[2 * i + 1], coords[2 * i + 2]);
            if (r < minRadius)
            {
                i2 = i;
                minRadius = r;
            }
        }
        var i2x = coords[2 * i2];
        var i2y = coords[2 * i2 + 1];
        var i2z = coords[2 * i2 + 2];

        if (minRadius == float.PositiveInfinity)
        {
            throw new Exception("No Delaunay triangulation exists for this input.");
        }

        /*if (Orient(i0x, i0y, i1x, i1y, i2x, i2y))
        {
            var i = i1;
            var x = i1x;
            var y = i1y;
            i1 = i2;
            i1x = i2x;
            i1y = i2y;
            i2 = i;
            i2x = x;
            i2y = y;
        }*/

        var center = Circumcenter(i0x, i0y, i0z, i1x, i1y, i1z, i2x, i2y, i2z);
        this.cx = center.X;
        this.cy = center.Y;

        var dists = new float[n];
        for (var i = 0; i < n; i++)
        {
            dists[i] = Dist(coords[2 * i], coords[2 * i + 1], coords[2 * i + 2], center.X, center.Y, center.Z);
        }

        // sort the points by distance from the seed triangle circumcenter
        Quicksort(ids, dists, 0, n - 1);

        // set up the seed triangle as the starting hull
        hullStart = i0;
        hullSize = 3;

        hullNext[i0] = hullPrev[i2] = i1;
        hullNext[i1] = hullPrev[i0] = i2;
        hullNext[i2] = hullPrev[i1] = i0;

        hullTri[i0] = 0;
        hullTri[i1] = 1;
        hullTri[i2] = 2;

        GD.Print($"{i0x},{i0y},{i0z}");
        GD.Print(HashKey(i0x, i0y, i0z));
        hullHash[HashKey(i0x, i0y, i0z)] = i0;
        hullHash[HashKey(i1x, i1y, i1z)] = i1;
        hullHash[HashKey(i2x, i2y, i2z)] = i2;

        trianglesLen = 0;
        AddTriangle(i0, i1, i2, -1, -1, -1);

        float xp = 0;
        float yp = 0;
        float zp = 0;

        for (var k = 0; k < ids.Length; k++)
        {
            var i = ids[k];
            var x = coords[2 * i];
            var y = coords[2 * i + 1];
            var z = coords[2 * i + 2];

            // skip near-duplicate points
            if (k > 0 && Math.Abs(x - xp) <= EPSILON && Math.Abs(y - yp) <= EPSILON && Math.Abs(z - zp) <= EPSILON) continue;
            xp = x;
            yp = y;
            zp = z;

            // skip seed triangle points
            if (i == i0 || i == i1 || i == i2) continue;

            // find a visible edge on the convex hull using edge hash
            var start = 0;
            for (var j = 0; j < hashSize; j++)
            {
                var key = HashKey(x, y, z);
                GD.Print(key);
                GD.Print(j);
                start = hullHash[(key + j) % hashSize];
                if (start != -1 && start != hullNext[start]) break;
            }


            start = hullPrev[start];
            var e = start;
            var q = hullNext[e];

            while (!Orient(x, y, coords[2 * e], coords[2 * e + 1], coords[2 * q], coords[2 * q + 1]))
            {
                e = q;
                if (e == start)
                {
                    e = int.MaxValue;
                    break;
                }

                q = hullNext[e];
            }

            if (e == int.MaxValue) continue; // likely a near-duplicate point; skip it

            // add the first triangle from the point
            var t = AddTriangle(e, i, hullNext[e], -1, -1, hullTri[e]);

            // recursively flip triangles from the point until they satisfy the Delaunay condition
            hullTri[i] = Legalize(t + 2);
            hullTri[e] = t; // keep track of boundary triangles on the hull
            hullSize++;

            // walk forward through the hull, adding more triangles and flipping recursively
            var next = hullNext[e];
            q = hullNext[next];

            while (Orient(x, y, coords[2 * next], coords[2 * next + 1], coords[2 * q], coords[2 * q + 1]))
            {
                t = AddTriangle(next, i, q, hullTri[i], -1, hullTri[next]);
                hullTri[i] = Legalize(t + 2);
                hullNext[next] = next; // mark as removed
                hullSize--;
                next = q;

                q = hullNext[next];
            }

            // walk backward from the other side, adding more triangles and flipping
            if (e == start)
            {
                q = hullPrev[e];

                while (Orient(x, y, coords[2 * q], coords[2 * q + 1], coords[2 * e], coords[2 * e + 1]))
                {
                    t = AddTriangle(q, i, e, -1, hullTri[e], hullTri[q]);
                    Legalize(t + 2);
                    hullTri[q] = t;
                    hullNext[e] = e; // mark as removed
                    hullSize--;
                    e = q;

                    q = hullPrev[e];
                }
            }

            // update the hull indices
            hullStart = hullPrev[i] = e;
            hullNext[e] = hullPrev[next] = i;
            hullNext[i] = next;

            // save the two new edges in the hash table
            hullHash[HashKey(x, y, z)] = i;
            hullHash[HashKey(coords[2 * e], coords[2 * e + 1], coords[2 * e + 2])] = e;
        }


        Hull = new int[hullSize];
        var s = hullStart;
        for (var i = 0; i < hullSize; i++)
        {
            Hull[i] = s;
            s = hullNext[s];
        }

        hullPrev = hullNext = hullTri = null; // get rid of temporary arrays

        //// trim typed triangle mesh arrays
        Triangles = Triangles.Take(trianglesLen).ToArray();
        Halfedges = Halfedges.Take(trianglesLen).ToArray();
    }

    private int Legalize(int a)
    {
        var i = 0;
        int ar;

        // recursion eliminated with a fixed-size stack
        while (true)
        {
            var b = Halfedges[a];

            /* if the pair of triangles doesn't satisfy the Delaunay condition
             * (p1 is inside the circumcircle of [p0, pl, pr]), flip them,
             * then do the same check/flip recursively for the new pair of triangles
             *
             *           pl                    pl
             *          /||\                  /  \
             *       al/ || \bl            al/    \a
             *        /  ||  \              /      \
             *       /  a||b  \    flip    /___ar___\
             *     p0\   ||   /p1   =>   p0\---bl---/p1
             *        \  ||  /              \      /
             *       ar\ || /br             b\    /br
             *          \||/                  \  /
             *           pr                    pr
             */
            int a0 = a - a % 3;
            ar = a0 + (a + 2) % 3;

            if (b == -1)
            { // convex hull edge
                if (i == 0) break;
                a = EDGE_STACK[--i];
                continue;
            }

            var b0 = b - b % 3;
            var al = a0 + (a + 1) % 3;
            var bl = b0 + (b + 2) % 3;

            var p0 = Triangles[ar];
            var pr = Triangles[a];
            var pl = Triangles[al];
            var p1 = Triangles[bl];

            var illegal = InCircle(
                coords[2 * p0], coords[2 * p0 + 1],
                coords[2 * pr], coords[2 * pr + 1],
                coords[2 * pl], coords[2 * pl + 1],
                coords[2 * p1], coords[2 * p1 + 1]);

            if (illegal)
            {
                Triangles[a] = p1;
                Triangles[b] = p0;

                var hbl = Halfedges[bl];

                // edge swapped on the other side of the hull (rare); fix the halfedge reference
                if (hbl == -1)
                {
                    var e = hullStart;
                    do
                    {
                        if (hullTri[e] == bl)
                        {
                            hullTri[e] = a;
                            break;
                        }
                        e = hullPrev[e];
                    } while (e != hullStart);
                }
                Link(a, hbl);
                Link(b, Halfedges[ar]);
                Link(ar, bl);

                var br = b0 + (b + 1) % 3;

                // don't worry about hitting the cap: it can only happen on extremely degenerate input
                if (i < EDGE_STACK.Length)
                {
                    EDGE_STACK[i++] = br;
                }
            }
            else
            {
                if (i == 0) break;
                a = EDGE_STACK[--i];
            }
        }

        return ar;
    }

    private static bool InCircle(double ax, double ay, double bx, double by, double cx, double cy, double px, double py)
    {
        var dx = ax - px;
        var dy = ay - py;
        var ex = bx - px;
        var ey = by - py;
        var fx = cx - px;
        var fy = cy - py;

        var ap = dx * dx + dy * dy;
        var bp = ex * ex + ey * ey;
        var cp = fx * fx + fy * fy;

        return dx * (ey * cp - bp * fy) -
               dy * (ex * cp - bp * fx) +
               ap * (ex * fy - ey * fx) < 0;
    }

    private int AddTriangle(int i0, int i1, int i2, int a, int b, int c)
    {
        var t = trianglesLen;

        Triangles[t] = i0;
        Triangles[t + 1] = i1;
        Triangles[t + 2] = i2;

        Link(t, a);
        Link(t + 1, b);
        Link(t + 2, c);

        trianglesLen += 3;
        return t;
    }

    private void Link(int a, int b)
    {
        Halfedges[a] = b;
        if (b != -1) Halfedges[b] = a;
    }
    private int HashKey(float x, float y, float z) => (int)(Math.Floor(PseudoAngle(x - cx, y - cy, z - cz) * hashSize) % hashSize);
    private static float PseudoAngle(float dx, float dy, float dz)
    {
        //var p = dx / (Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz));
        //return (dy > 0 ? 3 - p : 1 + p) / 4; // [0..1]
        var v3 = new Vector3(dx, dy, dz);
        return v3.Dot(Vector3.Back);

    }
    private static void Quicksort(int[] ids, float[] dists, int left, int right)
    {
        if (right - left <= 20)
        {
            for (var i = left + 1; i <= right; i++)
            {
                var temp = ids[i];
                var tempDist = dists[temp];
                var j = i - 1;
                while (j >= left && dists[ids[j]] > tempDist) ids[j + 1] = ids[j--];
                ids[j + 1] = temp;
            }
        }
        else
        {
            var median = (left + right) >> 1;
            var i = left + 1;
            var j = right;
            Swap(ids, median, i);
            if (dists[ids[left]] > dists[ids[right]]) Swap(ids, left, right);
            if (dists[ids[i]] > dists[ids[right]]) Swap(ids, i, right);
            if (dists[ids[left]] > dists[ids[i]]) Swap(ids, left, i);

            var temp = ids[i];
            var tempDist = dists[temp];
            while (true)
            {
                do i++; while (dists[ids[i]] < tempDist);
                do j--; while (dists[ids[j]] > tempDist);
                if (j < i) break;
                Swap(ids, i, j);
            }
            ids[left + 1] = ids[j];
            ids[j] = temp;

            if (right - i + 1 >= j - left)
            {
                Quicksort(ids, dists, i, right);
                Quicksort(ids, dists, left, j - 1);
            }
            else
            {
                Quicksort(ids, dists, left, j - 1);
                Quicksort(ids, dists, i, right);
            }
        }
    }
    private static void Swap(int[] arr, int i, int j)
    {
        var tmp = arr[i];
        arr[i] = arr[j];
        arr[j] = tmp;
    }
    private static Point Circumcenter(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz)
    {
        var a = new Vector3(ax, ay, az);
        var b = new Vector3(bx, by, bz);
        var c = new Vector3(cx, cy, cz);

        var cc = (b - a).Cross(c - a).Normalized();

        return new Point(a + cc);
    }
    private static float Dist(float ax, float ay, float az, float bx, float by, float bz)
    {
        var dx = ax - bx;
        var dy = ay - by;
        var dz = az - bz;
        return dx * dx + dy * dy + dz * dz;
    }
    //Sum over edges (x2-x1)(y2-y1), if result is positive -> curve is anticlockwise
    private static bool Orient(float px, float py, float qx, float qy, float rx, float ry) => (qy - py) * (rx - qx) - (qx - px) * (ry - qy) < 0;
    private static float Circumradius(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz)
    {
        var a = new Vector3(ax, ay, az);
        var b = new Vector3(bx, by, bz);
        var c = new Vector3(cx, cy, cz);

        var cc = (b - a).Cross(c - a).Normalized();

        return cc.X * cc.X + cc.Y * cc.Y + cc.Z * cc.Z;
    }
    public IEnumerable<ITriangle> GetTriangles()
    {
        for (var t = 0; t < Triangles.Length / 3; t++)
        {
            yield return new Triangle(t, GetTrianglePoints(t).ToList(), new List<Edge>());
        }
    }
    public Point[] GetTrianglePoints(int t)
    {
        var points = new List<Point>();
        foreach (var p in PointsOfTriangle(t))
        {
            points.Add(Points[p]);
        }
        return points.ToArray();
    }
    public IEnumerable<int> PointsOfTriangle(int t)
    {
        foreach (var edge in EdgesOfTriangle(t))
        {
            yield return Triangles[edge];
        }
    }
    public static int[] EdgesOfTriangle(int t) => new int[] { 3 * t, 3 * t + 1, 3 * t + 2 };
    public IEnumerable<IEdge> GetVoronoiEdges(Func<int, Point> triangleVerticeSelector = null)
    {
        if (triangleVerticeSelector == null) triangleVerticeSelector = x => GetCentroid(x);
        for (var e = 0; e < Triangles.Length; e++)
        {
            if (e < Halfedges[e])
            {
                var p = triangleVerticeSelector(TriangleOfEdge(e));
                var q = triangleVerticeSelector(TriangleOfEdge(Halfedges[e]));
                yield return new Edge(e, p, q);
            }
        }
    }
    public Point GetCentroid(int t)
    {
        var vertices = GetTrianglePoints(t);
        return GetCentroid(vertices);
    }
    public static Point GetCentroid(Point[] points)
    {
        float accumulatedArea = 0.0f;
        float centerX = 0.0f;
        float centerY = 0.0f;
        float centerZ = 0.0f;

        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            //var temp = points[i].X * points[j].Y - points[j].X * points[i].Y;
            var temp = points[i].ToVector3().Cross(points[j].ToVector3()).Length();
            accumulatedArea += temp;
            centerX += (points[i].X + points[j].X) * temp;
            centerY += (points[i].Y + points[j].Y) * temp;
            centerZ += (points[i].Z + points[j].Z) * temp;
        }

        if (Math.Abs(accumulatedArea) < 1E-7f)
            return new Point();

        accumulatedArea *= 3f;
        return new Point(centerX / accumulatedArea, centerY / accumulatedArea, centerZ / accumulatedArea);
    }
    public static int TriangleOfEdge(int e) { return e / 3; }
    public void ForEachTriangle(Action<ITriangle> callback)
    {
        foreach (var triangle in GetTriangles())
        {
            callback?.Invoke(triangle);
        }
    }
    public void ForEachVoronoiEdge(Action<IEdge> callback)
    {
        foreach (var edge in GetVoronoiEdges())
        {
            callback?.Invoke(edge);
        }
    }
}
