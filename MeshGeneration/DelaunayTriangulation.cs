using System;
using System.Collections.Generic;
using Godot;
using Structures;
using UtilityLibrary;


namespace MeshGeneration
{
    using PointList = System.Collections.Generic.List<Point>;

    public class DelaunayTriangulation
    {
        private static int renderTriCounter = 0;
        private static List<Edge> Edges = new List<Edge>();

        private static void Kill(Edge e)
        {
            Logger.Edge($"Killing Edge: {e}");
            Edge.Splice(e, e.Oprev());
            Edge.Splice(e.Sym(), e.Sym().Oprev());
            Edges.Remove(e.Sym());
            Edges.Remove(e);
            Logger.Debug($"Edge killed successfully", "Kill");
        }

        private static (Edge, Edge) LinePrimitive(Point p, Point q)
        {
            Logger.EnterFunction("LinePrimitive", $"p={p}, q={q}");
            Edge e = Edge.MakeEdge(p, q);
            Edges.Add(e);
            Edges.Add(e.Sym());
            PolygonRendererSDL.DrawLine(GenerateDocArrayMesh.instance, 10, StructureDatabase.circumcenters[e.Origin.Index].ToVector3(), StructureDatabase.circumcenters[e.Destination.Index].ToVector3());
            Logger.Edge($"LinePrimitive created edge: {e}");
            Logger.ExitFunction("LinePrimitive", $"Returning ({e}, {e.Sym()})");
            return (e, e.Sym());
        }

        private static (Edge, Edge) TrianglePrimitive(Point a, Point b, Point c)
        {
            Logger.EnterFunction("TrianglePrimitive", $"a={a}, b={b}, c={c}");
            Edge a1 = Edge.MakeEdge(a, b);
            Edge b1 = Edge.MakeEdge(b, c);
            Edges.Add(a1);
            Edges.Add(a1.Sym());
            Edges.Add(b1);
            Edges.Add(b1.Sym());
            Edge.Splice(a1.Sym(), b1);
            Logger.Edge($"TrianglePrimitive created edges: a1={a1}, b1={b1}");

            if (CCW(a, b, c))
            {
                Logger.Debug("Points are CCW ordered", "TrianglePrimitive");
                Edge c1 = Edge.Connect(b1, a1);
                Edges.Add(c1);
                Edges.Add(c1.Sym());
                Logger.Edge($"Connected edges to form triangle: c1={c1}");
                if (renderTriCounter < 000)
                {
                    var tri = new Triangle(new List<Point> { StructureDatabase.circumcenters[a.Index], StructureDatabase.circumcenters[b.Index], StructureDatabase.circumcenters[c.Index] }, new List<Edge> { a1, b1, c1 });
                    PolygonRendererSDL.RenderTriangleAndConnections(GenerateDocArrayMesh.instance, 10, tri);
                }
                Logger.ExitFunction("TrianglePrimitive", $"Returning CCW case: ({a1}, {b1.Sym()})");
                return (a1, b1.Sym());
            }
            else if (CCW(a, c, b))
            {
                Logger.Debug("Points are CW ordered", "TrianglePrimitive");
                Edge c1 = Edge.Connect(b1, a1);
                Edges.Add(c1);
                Edges.Add(c1.Sym());
                Logger.Edge($"Connected edges to form triangle: c1={c1}");
                if (renderTriCounter < 000)
                {
                    var tri = new Triangle(new List<Point> { StructureDatabase.circumcenters[a.Index], StructureDatabase.circumcenters[b.Index], StructureDatabase.circumcenters[c.Index] }, new List<Edge> { a1, b1, c1 });
                    PolygonRendererSDL.RenderTriangleAndConnections(GenerateDocArrayMesh.instance, 10, tri);
                }
                Logger.ExitFunction("TrianglePrimitive", $"Returning CW case: ({c1.Sym()}, {c1})");
                return (c1.Sym(), c1);
            }
            else
            {
                Logger.Debug("Points are collinear", "TrianglePrimitive");
                Logger.ExitFunction("TrianglePrimitive", $"Returning collinear case: ({a1}, {b1.Sym()})");
                return (a1, b1.Sym());
            }
        }

        private static (PointList, PointList) SplitPoints(List<Point> points)
        {
            String pointsStr = "Points: ";
            foreach (Point p in points)
            {
                pointsStr += $"{p}, ";
            }
            GD.PrintRaw($"{pointsStr}\n");
            int halfway = points.Count / 2;
            PointList a = new PointList(points.GetRange(0, halfway));
            PointList b = new PointList(points.GetRange(halfway, points.Count - halfway));
            GD.PrintRaw($"Split Points: {a.Count}, {b.Count}| Original List: {points.Count}\n");
            return (a, b);
        }

        private static bool LeftOf(Edge e, Point p)
        {
            return CCW(p, e.Origin, e.Destination);
        }

        private static bool RightOf(Edge e, Point p)
        {
            return CCW(p, e.Destination, e.Origin);
        }

        private static bool Valid(Edge e, Edge baseEdge)
        {
            return RightOf(baseEdge, e.Destination);
        }

        private static bool InCircle(Point a, Point b, Point c, Point d)
        {
            // Using the lifted coordinates method from Guibas-Stolfi
            float ax = a.X;
            float ay = a.Y;
            float bx = b.X;
            float by = b.Y;
            float cx = c.X;
            float cy = c.Y;
            float dx = d.X;
            float dy = d.Y;

            float adx = ax - dx;
            float ady = ay - dy;
            float bdx = bx - dx;
            float bdy = by - dy;
            float cdx = cx - dx;
            float cdy = cy - dy;

            float abdet = adx * bdy - bdx * ady;
            float bcdet = bdx * cdy - cdx * bdy;
            float cadet = cdx * ady - adx * cdy;

            float alift = adx * adx + ady * ady;
            float blift = bdx * bdx + bdy * bdy;
            float clift = cdx * cdx + cdy * cdy;

            return alift * bcdet + blift * cadet + clift * abdet < 0;
        }

        private static Edge LowestCommonTangent(Edge leftInner, Edge rightInner)
        {
            Logger.EnterFunction("LowestCommonTangent", $"leftInner={leftInner}, rightInner={rightInner}");
            Logger.Debug("Finding Lowest Common Tangent", "LowestCommonTangent");
            while (true)
            {
                Logger.Debug($"Current edges - Left: {leftInner}, Right: {rightInner}", "LowestCommonTangent");
                if (LeftOf(leftInner, rightInner.Origin))
                {
                    Logger.Debug("Moving left inner edge forward", "LowestCommonTangent");
                    leftInner = leftInner.Lnext();
                }
                else if (RightOf(rightInner, leftInner.Origin))
                {
                    Logger.Debug("Moving right inner edge backward", "LowestCommonTangent");
                    rightInner = rightInner.Rprev();
                }
                else
                {
                    Logger.Debug("Found lowest common tangent", "LowestCommonTangent");
                    break;
                }
            }

            Edge baseEdge = Edge.Connect(rightInner.Sym(), leftInner);
            Edges.Add(baseEdge);
            Edges.Add(baseEdge.Sym()); // Add symmetric edge
            Logger.Edge($"Connected edges to form base edge: {baseEdge}");
            Logger.ExitFunction("LowestCommonTangent", $"Returning base edge: {baseEdge}");
            return baseEdge;
        }

        private static Edge LeftCandidate(Edge baseEdge)
        {
            Edge leftCandidate = baseEdge.Sym().Onext();
            if (Valid(leftCandidate, baseEdge))
            {
                while (InCircle(baseEdge.Destination, baseEdge.Origin, leftCandidate.Destination, leftCandidate.Onext().Destination))
                {
                    Edge t = leftCandidate.Onext();
                    Kill(leftCandidate);
                    leftCandidate = t;
                }
            }
            return leftCandidate;
        }

        private static Edge RightCandidate(Edge baseEdge)
        {
            Edge rightCandidate = baseEdge.Oprev();
            if (Valid(rightCandidate, baseEdge))
            {
                //GD.PrintRaw($"Right Candidate: {rightCandidate}\n");
                while (InCircle(baseEdge.Origin, baseEdge.Destination, rightCandidate.Destination, rightCandidate.Oprev().Destination))
                {
                    //GD.PrintRaw($"Right Candidate: {rightCandidate}\n");
                    Edge t = rightCandidate.Oprev();
                    Kill(rightCandidate);
                    rightCandidate = t;
                }
            }
            return rightCandidate;
        }

        private static Edge MergeHulls(Edge ldi, Edge rdi)
        {
            Logger.Debug("Merging Hulls", "MergeHulls");

            // Find the lowest common tangent
            while (true)
            {
                if (LeftOf(ldi, rdi.Origin))
                {
                    ldi = ldi.Lnext();
                }
                else if (RightOf(rdi, ldi.Origin))
                {
                    rdi = rdi.Rprev();
                }
                else break;
            }

            Edge baseEdge = Edge.Connect(rdi.Sym(), ldi);
            Edges.Add(baseEdge);
            Edges.Add(baseEdge.Sym());

            if (ldi.Origin == baseEdge.Origin) ldi = baseEdge.Sym();
            if (rdi.Origin == baseEdge.Destination) rdi = baseEdge;

            // Merge the two hulls
            while (true)
            {
                Edge lcand = baseEdge.Sym().Onext();
                if (Valid(lcand, baseEdge))
                {
                    while (InCircle(baseEdge.Destination, baseEdge.Origin, lcand.Destination, lcand.Onext().Destination))
                    {
                        var t = lcand.Onext();
                        Kill(lcand);
                        lcand = t;
                    }
                }

                Edge rcand = baseEdge.Oprev();
                if (Valid(rcand, baseEdge))
                {
                    while (InCircle(baseEdge.Destination, baseEdge.Origin, rcand.Destination, rcand.Oprev().Destination))
                    {
                        var t = rcand.Oprev();
                        Kill(rcand);
                        rcand = t;
                    }
                }

                if (!Valid(lcand, baseEdge) && !Valid(rcand, baseEdge)) break;

                if (!Valid(lcand, baseEdge) || (Valid(rcand, baseEdge) &&
                    InCircle(lcand.Destination, lcand.Origin, rcand.Origin, rcand.Destination)))
                {
                    baseEdge = Edge.Connect(rcand, baseEdge.Sym());
                    Edges.Add(baseEdge);
                    Edges.Add(baseEdge.Sym());
                }
                else
                {
                    baseEdge = Edge.Connect(baseEdge.Sym(), lcand.Sym());
                    Edges.Add(baseEdge);
                    Edges.Add(baseEdge.Sym());
                }
            }

            return ldi;
        }

        public static List<Triangle> TriangulateCell(List<Point> points, Vector3 unitNormal)
        {
            Edges.Clear();

            // Validate input
            if (points.Count < 3)
            {
                Logger.Debug($"Cannot triangulate with {points.Count} points", "TriangulateCell");
                return new List<Triangle>();
            }

            List<Point> vertices = new List<Point>();
            for (int i = 0; i < points.Count; i++)
            {
                vertices.Add(new Point(points[i].Position, points[i].Index));
            }

            // Sort vertices lexicographically
            vertices.Sort((a, b) =>
            {
                int cmp = a.Position.X.CompareTo(b.Position.X);
                if (cmp == 0) cmp = a.Position.Y.CompareTo(b.Position.Y);
                return cmp;
            });

            var result = Triangulate(0, vertices.Count - 1, vertices);
            var triangles = ExtractTriangles(result.Item1, result.Item2, unitNormal);

            // Debug rendering
            if (renderTriCounter < 400)
            {
                foreach (var triangle in triangles)
                {
                    Triangle tri = new Triangle(new List<Point> { StructureDatabase.circumcenters[triangle.Points[0].Index], StructureDatabase.circumcenters[triangle.Points[2].Index], StructureDatabase.circumcenters[triangle.Points[1].Index] }, new List<Edge> { triangle.Edges[0], triangle.Edges[2], triangle.Edges[1] });
                    PolygonRendererSDL.RenderTriangleAndConnections(GenerateDocArrayMesh.instance, 10, tri);
                    PolygonRendererSDL.DrawTriangle(GenerateDocArrayMesh.instance, 7,
                        StructureDatabase.circumcenters[triangle.Points[0].Index].ToVector3() / 10f,
                        StructureDatabase.circumcenters[triangle.Points[2].Index].ToVector3() / 10f,
                        StructureDatabase.circumcenters[triangle.Points[1].Index].ToVector3() / 10f);
                }
                renderTriCounter++;
            }

            Logger.Debug($"Triangulated {points.Count} points into {triangles.Count} triangles", "TriangulateCell");
            GD.PrintRaw($"Triangles: {triangles.Count}\n");

            return triangles;
        }

        /// <summary>
        /// Performs Delaunay triangulation on a set of 2D projected points
        /// </summary>
        /// <param name="points">List of 2D projected points</param>
        /// <param name="unitNormal">Unit normal vector of the plane</param>
        /// <returns>List of triangles forming the Delaunay triangulation</returns>
        public static (Edge, Edge) Triangulate(int start, int end, List<Point> points)
        {
            int n = end - start + 1;
            if (n < 2) GD.PrintRaw($"Points division with <2 points");
            if (n == 2)
            {
                GD.PrintRaw($"Points division with 2 points at {start} and {end}\n");
                var result = LinePrimitive(points[start], points[end]);
                //PolygonRendererSDL.DrawLine(GenerateDocArrayMesh.instance, 10, StructureDatabase.circumcenters[result.Item1[0].Origin.Index].ToVector3() / 10f, StructureDatabase.circumcenters[result.Item1[0].Destination.Index].ToVector3() / 10f);
                //GD.PrintRaw($"meow\nLinePrimitive: {result.Item1[0]} | {result.Item2[0]}\n");
                return result;
            }

            if (n == 3)
            {
                GD.PrintRaw($"Points division with 3 points\n");
                var result = TrianglePrimitive(points[start], points[start + 1], points[end]);
                //PolygonRendererSDL.DrawTriangle(GenerateDocArrayMesh.instance, 10, StructureDatabase.circumcenters[result.Item1[0].Index].ToVector3(), StructureDatabase.circumcenters[result.Item1[1].Index].ToVector3(), StructureDatabase.circumcenters[result.Item1[2].Index].ToVector3());
                return result;
            }

            int sm = (start + end) / 2;
            var left = Triangulate(start, sm, points);
            var right = Triangulate(sm + 1, end, points);

            Edge leftOut = MergeHulls(left.Item2, right.Item1);
            Edge rightOut = right.Item2;

            if (left.Item1.Origin == left.Item2.Origin) leftOut = left.Item1;
            if (right.Item1.Origin == right.Item2.Origin) rightOut = right.Item1;

            Logger.Debug($"Merged hulls, returning edges", "Triangulate");

            return (leftOut, rightOut);

        }

        private static List<Triangle> ExtractTriangles(Edge LeftEdges, Edge RightEdges, Vector3 unitNormal)
        {
            HashSet<Triangle> triangles = new HashSet<Triangle>();
            HashSet<Edge> visitedEdges = new HashSet<Edge>();
            HashSet<string> triangleHashes = new HashSet<string>();

            // Process each edge exactly once
            foreach (Edge e in Edges)
            {
                if (visitedEdges.Contains(e)) continue;
                visitedEdges.Add(e);

                // Try to form a triangle from this edge using Lnext navigation
                Edge e1 = e;
                Edge e2 = e1.Lnext();
                Edge e3 = e2.Lnext();

                // Check if we have a valid closed triangle
                if (e3.Lnext() == e1)
                {
                    Point v1 = e1.Origin;
                    Point v2 = e2.Origin;
                    Point v3 = e3.Origin;

                    // Ensure we have 3 distinct points
                    if (v1 != null && v2 != null && v3 != null &&
                        v1 != v2 && v2 != v3 && v3 != v1)
                    {
                        // Create a unique hash for this triangle to avoid duplicates
                        List<int> indices = new List<int> { v1.Index, v2.Index, v3.Index };
                        indices.Sort();
                        string hash = $"{indices[0]}_{indices[1]}_{indices[2]}";

                        if (!triangleHashes.Contains(hash))
                        {
                            triangleHashes.Add(hash);

                            // Verify triangle is CCW in 2D projection
                            if (CCW(v1, v2, v3))
                            {
                                List<Point> points = new List<Point> { v1, v2, v3 };
                                List<Edge> edges = new List<Edge> { e1, e2, e3 };
                                Triangle triangle = new Triangle(points, edges);
                                triangles.Add(triangle);
                            }
                        }
                    }
                }
            }

            List<Triangle> triangleList = new List<Triangle>(triangles);
            ValidateWindings(triangleList, unitNormal);
            return triangleList;
        }

        private static Triangle tryFormTriangle(Edge edge, int index)
        {
            var v1 = edge.Origin;
            var v2 = edge.Destination;

            Edge nextEdge = edge.Lnext();
            Edge thirdEdge = nextEdge.Lnext();

            // Check if we have a valid triangle (3 edges that close)
            if (thirdEdge.Lnext() == edge && thirdEdge.Destination == v1)
            {
                var v3 = nextEdge.Destination;

                // Ensure we have 3 distinct points
                if (v1 != v2 && v2 != v3 && v3 != v1)
                {
                    List<Point> points = new List<Point> { v1, v2, v3 };
                    List<Edge> edges = new List<Edge> { edge, nextEdge, thirdEdge };
                    Triangle triangle = new Triangle(points, edges);
                    return triangle;
                }
            }
            return null;
        }
        private static void ValidateWindings(List<Triangle> triangles, Vector3 unitNormal)
        {
            foreach (var triangle in triangles)
            {
                GD.PrintRaw($"Validating Triangle: {triangle}\n");
                var p1 = StructureDatabase.circumcenters[triangle.Points[0].Index].ToVector3();
                var p2 = StructureDatabase.circumcenters[triangle.Points[1].Index].ToVector3();
                var p3 = StructureDatabase.circumcenters[triangle.Points[2].Index].ToVector3();

                var v1 = p2 - p1;
                var v2 = p3 - p1;
                var triangleNormal = -v1.Cross(v2).Normalized();
                var triangleCenter = (p1 + p2 + p3) / 3f;
                float angleTriangleFace = Mathf.Acos(triangleNormal.Dot(unitNormal));
                if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90f)
                {
                    var temp = triangle.Points[1];
                    triangle.Points[1] = triangle.Points[2];
                    triangle.Points[2] = temp;
                    if (triangle.Edges != null && triangle.Edges.Count >= 3)
                    {
                        var tempEdge = triangle.Edges[1];
                        triangle.Edges[1] = triangle.Edges[2];
                        triangle.Edges[2] = tempEdge;
                    }
                }
                else
                {
                    var temp = triangle.Points[1];
                    triangle.Points[1] = triangle.Points[2];
                    triangle.Points[2] = temp;
                }
            }
        }

        private static bool CCW(Point a, Point b, Point c)
        {
            float[][] m = new float[][] { new float[] { a.X, b.X, c.X }, new float[] { a.Y, b.Y, c.Y }, new float[] { 1.0f, 1.0f, 1.0f } };
            return Det3x3(m) > 0.0f;
        }

        private static float Det4x4(float[][] mat)
        {
            float a = mat[0][0];
            float b = mat[1][0];
            float c = mat[2][0];
            float d = mat[3][0];
            float e = mat[0][1];
            float f = mat[1][1];
            float g = mat[2][1];
            float h = mat[3][1];
            float i = mat[0][2];
            float j = mat[1][2];
            float k = mat[2][2];
            float l = mat[3][2];
            float m = mat[0][3];
            float n = mat[1][3];
            float o = mat[2][3];
            float p = mat[3][3];

            float adet = a * ((f * k * p) - (f * l * o) - (g * j * p) + (g * l * n) + (h * j * o) - (h * k * n));
            float bdet = b * ((e * k * p) - (e * l * o) - (g * i * p) + (g * l * m) + (h * i * o) - (h * k * m));
            float cdet = c * ((e * j * p) - (e * l * n) - (f * i * p) + (f * l * m) + (h * i * n) - (h * j * m));
            float ddet = d * ((e * j * o) - (e * k * n) - (f * i * o) + (f * k * m) + (g * i * n) - (g * j * m));

            float det = adet - bdet + cdet - ddet;
            return det;
        }
        private static float Det3x3(float[][] m)
        {
            float a = m[0][0];
            float b = m[1][0];
            float c = m[2][0];
            float d = m[0][1];
            float e = m[1][1];
            float f = m[2][1];
            float g = m[0][2];
            float h = m[1][2];
            float i = m[2][2];

            float det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
            return det;
        }

        /// <summary>
        /// Validates and fixes triangle orientations to match the unit normal
        /// </summary>
        /// <param name="triangles">List of triangles to validate</param>
        /// <param name="unitNormal">Target unit normal vector</param>
        private static void ValidateTriangleOrientations(List<Triangle> triangles, Vector3 unitNormal)
        {
            foreach (var triangle in triangles)
            {
                // Calculate triangle normal
                var p1 = triangle.Points[0].ToVector3();
                var p2 = triangle.Points[1].ToVector3();
                var p3 = triangle.Points[2].ToVector3();

                var v1 = p2 - p1;
                var v2 = p3 - p1;
                var triangleNormal = v1.Cross(v2).Normalized();

                // Check if orientation matches unit normal
                if (triangleNormal.Dot(unitNormal) < 0)
                {
                    // Flip triangle orientation
                    var temp = triangle.Points[1];
                    triangle.Points[1] = triangle.Points[2];
                    triangle.Points[2] = temp;
                }
            }
        }

        /// <summary>
        /// Checks if two edges are equal (same endpoints)
        /// </summary>
        /// <param name="e1">First edge</param>
        /// <param name="e2">Second edge</param>
        /// <returns>True if edges are equal</returns>
        private static bool EdgesAreEqual(Edge e1, Edge e2)
        {
            return e1 == e2 || e1.Sym() == e2;
        }

        /// <summary>
        /// Marks a point as a super triangle vertex
        /// </summary>
        /// <param name="point">Point to mark</param>
        private static void MarkAsSuperTriangleVertex(Point point)
        {
            // In a more complete implementation, we would add a flag to the Point class
            // For now, we'll use a special index value to identify super triangle vertices
            // This is a simplification for this implementation
        }

        /// <summary>
        /// Checks if a point is a super triangle vertex
        /// </summary>
        /// <param name="point">Point to check</param>
        /// <returns>True if point is a super triangle vertex</returns>
        private static bool IsSuperTriangleVertex(Point point)
        {
            // In a more complete implementation, we would check a flag on the Point class
            // For now, we'll use a special index value to identify super triangle vertices
            // This is a simplification for this implementation
            return false;
        }
    }
}
