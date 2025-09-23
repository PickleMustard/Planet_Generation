using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;


namespace MeshGeneration
{
    using EdgeList = System.Collections.Generic.List<Edge>;
    using PointList = System.Collections.Generic.List<Point>;

    public class DelaunayTriangulation
    {
        private static int renderTriCounter = 0;
        private static List<Edge> Edges = new List<Edge>();

        private static void Kill(Edge e)
        {
            //GD.PrintRaw($"Killing Edge: {e}\n");
            Edge.Splice(e, e.Oprev());
            Edge.Splice(e.Sym(), e.Sym().Oprev());
            Edges.Remove(e.Sym());
            Edges.Remove(e);
        }

        private static (EdgeList, EdgeList) LinePrimitive(Point p, Point q)
        {
            Edge e = Edge.MakeEdge(p, q);
            Edges.Add(e);
            Edges.Add(e.Sym());
            //GD.PrintRaw($"LinePrimitive: {e}\n");
            return (new EdgeList { e }, new EdgeList { e.Sym() });
        }

        private static (EdgeList, EdgeList) TrianglePrimitive(Point a, Point b, Point c)
        {
            Edge a1 = Edge.MakeEdge(a, b);
            Edge b1 = Edge.MakeEdge(b, c);
            Edges.Add(a1);
            Edges.Add(b1);
            Edges.Add(b1.Sym());
            Edges.Add(a1.Sym());
            Edge.Splice(a1.Sym(), b1);
            if (CCW(a, b, c))
            {
                Edge c1 = Edge.Connect(b1, a1);
                Edges.Add(c1);
                Edges.Add(c1.Sym());
                if (renderTriCounter < 100)
                {
                    var tri = new Triangle(0, new List<Point> { StructureDatabase.circumcenters[a.Index], StructureDatabase.circumcenters[b.Index], StructureDatabase.circumcenters[c.Index] }, new List<Edge> { a1, b1, c1 });
                    PolygonRendererSDL.RenderTriangleAndConnections(GenerateDocArrayMesh.instance, 10, tri);
                }
                return (new EdgeList { a1 }, new EdgeList { b1.Sym() });
            }
            else if (CCW(a, c, b))
            {
                Edge c1 = Edge.Connect(b1, a1);
                Edges.Add(c1);
                Edges.Add(c1.Sym());
                if (renderTriCounter < 100)
                {
                    var tri = new Triangle(0, new List<Point> { StructureDatabase.circumcenters[a.Index], StructureDatabase.circumcenters[b.Index], StructureDatabase.circumcenters[c.Index] }, new List<Edge> { a1, b1, c1 });
                    PolygonRendererSDL.RenderTriangleAndConnections(GenerateDocArrayMesh.instance, 10, tri);
                }
                return (new EdgeList { c1.Sym() }, new EdgeList { c1 });
            }
            else
            {
                //Points are collinear
                return (new EdgeList { a1 }, new EdgeList { b1.Sym() });
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
            float[][] m = new float[][] {
                new float[] { a.X, b.X, c.X, d.X },
                new float[] { a.Y, b.Y, c.Y, d.Y },
                new float[] { a.ToVector3().LengthSquared(), b.ToVector3().LengthSquared(), c.ToVector3().LengthSquared(), d.ToVector3().LengthSquared() },
                new float[] { 1.0f, 1.0f, 1.0f, 1.0f } };
            return Det4x4(m) > 0.0f;
        }

        private static Edge LowestCommonTangent(Edge leftInner, Edge rightInner)
        {
            //GD.PrintRaw($"Finding Lowest Common Tangent\n");
            while (true)
            {
                //GD.PrintRaw($"Left Inner: {leftInner}, Right Inner: {rightInner}\n");
                if (LeftOf(leftInner, rightInner.Origin))
                {
                    leftInner = leftInner.Lnext();
                }
                else if (RightOf(rightInner, leftInner.Origin))
                {
                    rightInner = rightInner.Rprev();
                }
                else
                {
                    break;
                }
            }

            Edge baseEdge = Edge.Connect(rightInner.Sym(), leftInner);
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

        private static void MergeHulls(Edge baseEdge)
        {
            GD.PrintRaw($"Merging Hulls\n");
            while (true)
            {
                Edge leftCandidate = LeftCandidate(baseEdge);
                GD.PrintRaw($"Left Candidate: {leftCandidate}\n");
                Edge rightCandidate = RightCandidate(baseEdge);
                GD.PrintRaw($"Right Candidate: {rightCandidate}\n");

                if (!Valid(leftCandidate, baseEdge) && !Valid(rightCandidate, baseEdge))
                {
                    break;
                }
                else if (!Valid(leftCandidate, baseEdge) || (Valid(rightCandidate, baseEdge) && InCircle(leftCandidate.Origin, leftCandidate.Destination, rightCandidate.Origin, rightCandidate.Destination)))
                {
                    baseEdge = Edge.Connect(rightCandidate, baseEdge.Sym());
                    Edges.Add(baseEdge);
                }
                else
                {
                    baseEdge = Edge.Connect(baseEdge.Sym(), leftCandidate.Sym());
                    Edges.Add(baseEdge);
                }
            }
        }

        public static List<Triangle> TriangulateCell(List<Point> points, Vector3 unitNormal)
        {
            Edges.Clear();
            var result = Triangulate(points);
            var triangles = ExtractTriangles(result.Item1, result.Item2, unitNormal);
            if (renderTriCounter < 100)
            {
                //foreach (var triangle in triangles)
                //{
                //    PolygonRendererSDL.DrawTriangle(GenerateDocArrayMesh.instance, 7, StructureDatabase.circumcenters[triangle.Points[0].Index].ToVector3(), StructureDatabase.circumcenters[triangle.Points[2].Index].ToVector3(), StructureDatabase.circumcenters[triangle.Points[1].Index].ToVector3());
                //}
                renderTriCounter++;
            }
            GD.PrintRaw($"Edges: {Edges.Count}\n");

            return triangles;
        }

        /// <summary>
        /// Performs Delaunay triangulation on a set of 2D projected points
        /// </summary>
        /// <param name="points">List of 2D projected points</param>
        /// <param name="unitNormal">Unit normal vector of the plane</param>
        /// <returns>List of triangles forming the Delaunay triangulation</returns>
        public static (EdgeList, EdgeList) Triangulate(List<Point> points)
        {
            if (points.Count < 2) GD.PrintRaw($"Points division with <2 points");
            if (points.Count == 2)
            {
                GD.PrintRaw($"Points division with 2 points");
                var result = LinePrimitive(points[0], points[1]);
                PolygonRendererSDL.DrawLine(GenerateDocArrayMesh.instance, 10, StructureDatabase.circumcenters[result.Item1[0].Origin.Index].ToVector3(), StructureDatabase.circumcenters[result.Item1[0].Destination.Index].ToVector3());
                //GD.PrintRaw($"meow\nLinePrimitive: {result.Item1[0]} | {result.Item2[0]}\n");
                return result;
            }

            if (points.Count == 3)
            {
                GD.PrintRaw($"Points division with 3 points");
                var result = TrianglePrimitive(points[0], points[1], points[2]);
                //PolygonRendererSDL.DrawTriangle(GenerateDocArrayMesh.instance, 10, StructureDatabase.circumcenters[result.Item1[0].Index].ToVector3(), StructureDatabase.circumcenters[result.Item1[1].Index].ToVector3(), StructureDatabase.circumcenters[result.Item1[2].Index].ToVector3());
                return result;
            }

            var PointsPartition = SplitPoints(points);
            GD.PrintRaw($"Points Partition: {PointsPartition.Item1.Count}, {PointsPartition.Item2.Count}\n");
            var left = Triangulate(PointsPartition.Item1);
            var right = Triangulate(PointsPartition.Item2);

            Edge rightInner = right.Item1[0];
            Edge leftInner = left.Item2[0];

            Edge leftOuter = left.Item1[0];
            Edge rightOuter = right.Item2[0];

            Edge baseEdge = LowestCommonTangent(leftInner, rightInner);
            if (leftInner.Origin == leftOuter.Origin)
            {
                leftOuter = baseEdge.Sym();
            }
            if (rightInner.Origin == rightOuter.Origin)
            {
                rightOuter = baseEdge;
            }

            MergeHulls(baseEdge);
            return (new EdgeList { leftOuter }, new EdgeList { rightOuter });

        }

        private static List<Triangle> ExtractTriangles(EdgeList LeftEdges, EdgeList RightEdges, Vector3 unitNormal)
        {
            HashSet<Triangle> triangles = new HashSet<Triangle>();
            HashSet<Edge> processedEdges = new HashSet<Edge>();

            //List<Triangle> triangles = new List<Triangle>();
            //HashSet<Edge> visitedEdges = new HashSet<Edge>();

            HashSet<Edge> allEdges = new HashSet<Edge>();
            foreach (Edge edge in LeftEdges)
            {
                CollectAllEdges(edge, allEdges);
            }
            foreach (Edge edge in RightEdges)
            {
                CollectAllEdges(edge, allEdges);
            }
            foreach (Edge e in allEdges)
            {
                if (!processedEdges.Contains(e))
                {
                    var leftTriangle = tryFormTriangle(e, triangles.Count);
                    if (leftTriangle != null)
                    {
                        triangles.Add(leftTriangle);
                    }
                    var rightTriangle = tryFormTriangle(e.Sym(), triangles.Count);
                    {
                        if (rightTriangle != null)
                        {
                            triangles.Add(rightTriangle);
                        }
                    }
                }
                processedEdges.Add(e);
                processedEdges.Add(e.Sym());
            }
            //foreach (Edge edge in allEdges)
            //{
            //    if (!visitedEdges.Contains(edge))
            //    {
            //        List<Edge> edges = TryFormTriangle(edge, visitedEdges);
            //        if (edges != null && edges.Count == 3)
            //        {
            //            List<Point> points = new List<Point> { edges[0].Origin, edges[1].Origin, edges[2].Origin };
            //            Triangle triangle = new Triangle(triangles.Count, points, edges);
            //            triangles.Add(triangle);
            //        }
            //    }
            //}
            List<Triangle> triangles2 = new List<Triangle>(triangles);
            //ValidateWindings(triangles2, unitNormal);
            return triangles2;
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
                    Triangle triangle = new Triangle(index, points, edges);
                    return triangle;
                }
            }
            return null;
        }
        private static void ValidateWindings(List<Triangle> triangles, Vector3 unitNormal)
        {
            foreach (var triangle in triangles)
            {
                var p1 = StructureDatabase.circumcenters[triangle.Points[0].Index].ToVector3();
                var p2 = StructureDatabase.circumcenters[triangle.Points[1].Index].ToVector3();
                var p3 = StructureDatabase.circumcenters[triangle.Points[2].Index].ToVector3();

                var v1 = p2 - p1;
                var v2 = p3 - p1;
                var triangleNormal = -v1.Cross(v2).Normalized();
                var triangleCenter = (p1 + p2 + p3) / 3f;
                float angleTriangleFace = Mathf.Acos(triangleNormal.Dot(unitNormal));
                if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90)
                {
                    var temp = triangle.Points[1];
                    triangle.Points[1] = triangle.Points[2];
                    triangle.Points[2] = temp;
                    if (triangle.Edges != null && triangle.Edges.Count >= 3)
                    {
                        var tempEdge = triangle.Edges[0];
                        triangle.Edges[0] = triangle.Edges[2];
                        triangle.Edges[2] = tempEdge;
                    }
                }
            }
        }


        private static void CollectAllEdges(Edge edge, HashSet<Edge> allEdges)
        {
            Color[] color = new Color[] { Colors.Aqua, Colors.Red, Colors.Green, Colors.Blue, Colors.White, Colors.Black, Colors.Gray, Colors.Pink };
            Queue<Edge> queue = new Queue<Edge>();
            queue.Enqueue(edge);
            int counter = 0;
            while (queue.Count > 0)
            {
                Edge current = queue.Dequeue();
                if (renderTriCounter < 100)
                {
                    PolygonRendererSDL.DrawLine(GenerateDocArrayMesh.instance, 10, StructureDatabase.circumcenters[current.Origin.Index].ToVector3(), StructureDatabase.circumcenters[current.Destination.Index].ToVector3(), color[counter % color.Length]);
                    counter++;
                }
                if (allEdges.Contains(current)) continue;
                allEdges.Add(current);
                Edge next = current.Onext();
                Edge sym = current.Sym();
                Edge prev = current.Oprev();
                Edge lnext = current.Lnext();
                Edge rnext = current.Rnext();

                if (!queue.Contains(next)) queue.Enqueue(next);
                if (!queue.Contains(sym)) queue.Enqueue(sym);
                if (!queue.Contains(prev)) queue.Enqueue(prev);
                if (!queue.Contains(lnext)) queue.Enqueue(lnext);
                if (!queue.Contains(rnext)) queue.Enqueue(rnext);
            }
        }

        private static List<Edge> TryFormTriangle(Edge edge, HashSet<Edge> visitedEdges)
        {
            List<Edge> edges = new List<Edge>();
            Edge current = edge;

            do
            {
                edges.Add(current);
                visitedEdges.Add(current);
                current = current.Lnext();
                if (edges.Count > 10) break;
            } while (current != edge && edges.Count < 4);
            if (edges.Count == 3)
            {
                return edges;
            }
            return null;
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
