using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MeshGeneration
{
    /// <summary>
    /// Reference implementation of Guibas-Stolfi Divide-and-Conquer Delaunay Triangulation
    /// Based on the original 1985 paper "Primitives for the Manipulation of General Subdivisions and the Computation of Voronoi Diagrams"
    /// </summary>
    public class DelaunayTriangulationReference
    {
        private class QuadEdge
        {
            public Vertex Origin { get; set; }
            public QuadEdge Next { get; set; }
            public QuadEdge Rot { get; set; }
            public bool Mark { get; set; }
            
            public Vertex Destination => Sym.Origin;
            public QuadEdge Sym => Rot.Rot;
            public QuadEdge InvRot => Rot.Rot.Rot;
            public QuadEdge Oprev => Rot.Next.Rot;
            public QuadEdge Onext => Next;
            public QuadEdge Lnext => InvRot.Onext.Rot;
            public QuadEdge Lprev => Next.Sym;
            public QuadEdge Rprev => Sym.Onext;
            public QuadEdge Rnext => Oprev.Sym;
            public QuadEdge Dnext => Sym.Onext.Sym;
            public QuadEdge Dprev => InvRot.Onext.InvRot;
        }
        
        private class Vertex
        {
            public Vector3 Position { get; set; }
            public int Index { get; set; }
            
            public Vertex(Vector3 pos, int idx)
            {
                Position = pos;
                Index = idx;
            }
        }
        
        private List<QuadEdge> edges = new List<QuadEdge>();
        
        /// <summary>
        /// Create a new edge between two vertices
        /// </summary>
        private QuadEdge MakeEdge(Vertex org, Vertex dest)
        {
            QuadEdge e = new QuadEdge();
            QuadEdge r = new QuadEdge();
            QuadEdge e_sym = new QuadEdge();
            QuadEdge r_sym = new QuadEdge();
            
            // Set up the quad-edge structure
            e.Rot = r;
            r.Rot = e_sym;
            e_sym.Rot = r_sym;
            r_sym.Rot = e;
            
            // Set next pointers to point to themselves initially
            e.Next = e;
            r.Next = r_sym;
            e_sym.Next = e_sym;
            r_sym.Next = r;
            
            // Set the vertices
            e.Origin = org;
            e_sym.Origin = dest;
            
            edges.Add(e);
            return e;
        }
        
        /// <summary>
        /// Splice two edges together or apart
        /// </summary>
        private void Splice(QuadEdge a, QuadEdge b)
        {
            QuadEdge alpha = a.Next.Rot;
            QuadEdge beta = b.Next.Rot;
            
            QuadEdge t1 = b.Next;
            QuadEdge t2 = a.Next;
            QuadEdge t3 = beta.Next;
            QuadEdge t4 = alpha.Next;
            
            a.Next = t1;
            b.Next = t2;
            alpha.Next = t3;
            beta.Next = t4;
        }
        
        /// <summary>
        /// Delete an edge from the triangulation
        /// </summary>
        private void DeleteEdge(QuadEdge e)
        {
            Splice(e, e.Oprev);
            Splice(e.Sym, e.Sym.Oprev);
            edges.Remove(e);
        }
        
        /// <summary>
        /// Connect two edges with a new edge
        /// </summary>
        private QuadEdge Connect(QuadEdge a, QuadEdge b)
        {
            QuadEdge e = MakeEdge(a.Destination, b.Origin);
            Splice(e, a.Lnext);
            Splice(e.Sym, b);
            return e;
        }
        
        /// <summary>
        /// Test if a point is to the right of an edge
        /// </summary>
        private bool RightOf(Vertex x, QuadEdge e)
        {
            return CCW(x, e.Destination, e.Origin);
        }
        
        /// <summary>
        /// Test if a point is to the left of an edge
        /// </summary>
        private bool LeftOf(Vertex x, QuadEdge e)
        {
            return CCW(x, e.Origin, e.Destination);
        }
        
        /// <summary>
        /// Counter-clockwise test
        /// </summary>
        private bool CCW(Vertex a, Vertex b, Vertex c)
        {
            float ax = a.Position.X;
            float ay = a.Position.Z;
            float bx = b.Position.X;
            float by = b.Position.Z;
            float cx = c.Position.X;
            float cy = c.Position.Z;
            
            return (bx - ax) * (cy - ay) - (by - ay) * (cx - ax) > 0;
        }
        
        /// <summary>
        /// InCircle test - returns true if point d is inside the circumcircle of triangle abc
        /// </summary>
        private bool InCircle(Vertex a, Vertex b, Vertex c, Vertex d)
        {
            float ax = a.Position.X;
            float ay = a.Position.Z;
            float bx = b.Position.X;
            float by = b.Position.Z;
            float cx = c.Position.X;
            float cy = c.Position.Z;
            float dx = d.Position.X;
            float dy = d.Position.Z;
            
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
        
        /// <summary>
        /// Test if an edge is valid relative to a base edge
        /// </summary>
        private bool Valid(QuadEdge e, QuadEdge basel)
        {
            return RightOf(e.Destination, basel);
        }
        
        /// <summary>
        /// Main Delaunay triangulation function
        /// </summary>
        public List<(int, int, int)> Triangulate(List<Vector3> points)
        {
            if (points.Count < 3)
                return new List<(int, int, int)>();
            
            // Create vertices and sort by x-coordinate
            List<Vertex> vertices = new List<Vertex>();
            for (int i = 0; i < points.Count; i++)
            {
                vertices.Add(new Vertex(points[i], i));
            }
            vertices.Sort((a, b) => {
                int cmp = a.Position.X.CompareTo(b.Position.X);
                if (cmp == 0) cmp = a.Position.Z.CompareTo(b.Position.Z);
                return cmp;
            });
            
            // Clear edges list
            edges.Clear();
            
            // Perform divide-and-conquer triangulation
            QuadEdge le, re;
            Delaunay(vertices, 0, vertices.Count - 1, out le, out re);
            
            // Extract triangles
            return ExtractTriangles();
        }
        
        /// <summary>
        /// Recursive divide-and-conquer Delaunay triangulation
        /// </summary>
        private void Delaunay(List<Vertex> sites, int sl, int sh, out QuadEdge le, out QuadEdge re)
        {
            int n = sh - sl + 1;
            
            if (n == 2)
            {
                // Two points - create a single edge
                le = MakeEdge(sites[sl], sites[sh]);
                re = le.Sym;
            }
            else if (n == 3)
            {
                // Three points - create a triangle
                QuadEdge a = MakeEdge(sites[sl], sites[sl + 1]);
                QuadEdge b = MakeEdge(sites[sl + 1], sites[sh]);
                Splice(a.Sym, b);
                
                if (CCW(sites[sl], sites[sl + 1], sites[sh]))
                {
                    Connect(b, a);
                    le = a;
                    re = b.Sym;
                }
                else if (CCW(sites[sl], sites[sh], sites[sl + 1]))
                {
                    QuadEdge c = Connect(b, a);
                    le = c.Sym;
                    re = c;
                }
                else
                {
                    // Collinear points
                    le = a;
                    re = b.Sym;
                }
            }
            else
            {
                // Four or more points - divide and conquer
                int sm = (sl + sh) / 2;
                QuadEdge ldo, ldi, rdi, rdo;
                
                // Recursively triangulate left and right halves
                Delaunay(sites, sl, sm, out ldo, out ldi);
                Delaunay(sites, sm + 1, sh, out rdi, out rdo);
                
                // Merge the two triangulations
                QuadEdge basel = ConnectHulls(ldi, rdi, sites);
                
                // Fix up the outer edges
                if (ldo.Origin == ldi.Origin) ldo = basel.Sym;
                if (rdo.Origin == rdi.Origin) rdo = basel;
                
                le = ldo;
                re = rdo;
                
                // Merge loop - add edges above the base edge
                while (true)
                {
                    // Locate the first L edge to be encountered by the rising bubble
                    QuadEdge lcand = basel.Sym.Onext;
                    if (Valid(lcand, basel))
                    {
                        while (InCircle(basel.Destination, basel.Origin, lcand.Destination, lcand.Onext.Destination))
                        {
                            QuadEdge t = lcand.Onext;
                            DeleteEdge(lcand);
                            lcand = t;
                        }
                    }
                    
                    // Locate the first R edge to be encountered by the rising bubble
                    QuadEdge rcand = basel.Oprev;
                    if (Valid(rcand, basel))
                    {
                        while (InCircle(basel.Destination, basel.Origin, rcand.Destination, rcand.Oprev.Destination))
                        {
                            QuadEdge t = rcand.Oprev;
                            DeleteEdge(rcand);
                            rcand = t;
                        }
                    }
                    
                    // If both are invalid, we're done
                    if (!Valid(lcand, basel) && !Valid(rcand, basel))
                        break;
                    
                    // Choose the next edge to add
                    if (!Valid(lcand, basel) || 
                        (Valid(rcand, basel) && InCircle(lcand.Destination, lcand.Origin, rcand.Origin, rcand.Destination)))
                    {
                        // Add cross edge from rcand
                        basel = Connect(rcand, basel.Sym);
                    }
                    else
                    {
                        // Add cross edge from lcand
                        basel = Connect(basel.Sym, lcand.Sym);
                    }
                }
            }
        }
        
        /// <summary>
        /// Connect the convex hulls of two triangulations
        /// </summary>
        private QuadEdge ConnectHulls(QuadEdge ldi, QuadEdge rdi, List<Vertex> sites)
        {
            // Find the lower common tangent of L and R
            while (true)
            {
                if (LeftOf(rdi.Origin, ldi))
                {
                    ldi = ldi.Lnext;
                }
                else if (RightOf(ldi.Origin, rdi))
                {
                    rdi = rdi.Rprev;
                }
                else
                {
                    break;
                }
            }
            
            // Create the base edge
            QuadEdge basel = Connect(rdi.Sym, ldi);
            
            // Adjust ldi and rdi for the upper common tangent
            if (ldi.Origin == basel.Origin) ldi = basel.Sym;
            if (rdi.Origin == basel.Destination) rdi = basel;
            
            return basel;
        }
        
        /// <summary>
        /// Extract triangles from the quad-edge structure
        /// </summary>
        private List<(int, int, int)> ExtractTriangles()
        {
            HashSet<(int, int, int)> triangles = new HashSet<(int, int, int)>();
            HashSet<QuadEdge> visited = new HashSet<QuadEdge>();
            
            foreach (QuadEdge e in edges)
            {
                if (visited.Contains(e)) continue;
                
                // Try to form a triangle starting from this edge
                QuadEdge e1 = e;
                QuadEdge e2 = e1.Lnext;
                QuadEdge e3 = e2.Lnext;
                
                // Check if we have a valid triangle
                if (e3.Lnext == e1 && e1.Origin != null && e2.Origin != null && e3.Origin != null)
                {
                    int a = e1.Origin.Index;
                    int b = e2.Origin.Index;
                    int c = e3.Origin.Index;
                    
                    // Ensure consistent winding order and avoid duplicates
                    if (a != b && b != c && a != c)
                    {
                        // Sort indices to create a canonical representation
                        int[] indices = new int[] { a, b, c };
                        
                        // Add with consistent winding (CCW)
                        if (CCW(e1.Origin, e2.Origin, e3.Origin))
                        {
                            triangles.Add((a, b, c));
                        }
                        else
                        {
                            triangles.Add((a, c, b));
                        }
                    }
                }
                
                visited.Add(e1);
                visited.Add(e2);
                visited.Add(e3);
            }
            
            return triangles.ToList();
        }
    }
}