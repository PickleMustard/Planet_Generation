using System;
using Godot;
using Godot.Collections;
using Structures.MeshGeneration;

namespace Structures.GameState;

public partial class Octree<[MustBeVariant] T> : Resource where T : Point
{
    private System.Collections.Generic.HashSet<T> _points;
    public OctreeNode<T> root { get; private set; }

    public Octree(Aabb boundary)
    {
        root = new OctreeNode<T>(boundary.Abs());
        _points = new System.Collections.Generic.HashSet<T>();
        GD.Print($"Root is leaf: {root.IsLeaf()}");
    }

    public void Grow(float factor)
    {
        GD.Print($"Growing Octree by {factor} | Original {root.boundary}");
        root.Grow(factor);
        GD.Print($"New Octree: {root.boundary}");
    }

    public Array<T> GetPoints()
    {
        Array<T> result = new Array<T>();
        foreach (var p in _points)
        {
            result.Add(p);
        }
        return result;
    }

    public bool Insert(T point)
    {
        if (!root.Contains(point)) return false;
        _points.Add(point);
        try
        {
            return root.Insert(point);
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error in Insert: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    public Array<T> queryRange(Aabb range)
    {
        Array<T> result = new Array<T>();
        queryRangeRecursive(root, range, result);
        return result;
    }

    public T FindNearest(T query)
    {
        T nearest = null;
        float bestDistSq = float.MaxValue;
        int level = 0;
        FindNearestRecursive(root, query, ref bestDistSq, ref nearest, ref level);
        GD.Print($"Searched through: {level}");
        return nearest;
    }

    private void FindNearestRecursive(OctreeNode<T> node, T query, ref float bestDistSq, ref T nearest, ref int level)
    {
        if (node.DistanceToPointSq(query) >= bestDistSq) return;
        foreach (var point in node.points)
        {
            float distSq = query.Position.DistanceSquaredTo(point.Position);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = point;
            }
            level++;
        }
        for (int i = 0; i < 8; i++)
        {
            if (node.children[i] != null)
            {
                FindNearestRecursive(node.children[i], query, ref bestDistSq, ref nearest, ref level);
            }
        }

    }

    private void queryRangeRecursive(OctreeNode<T> node, Aabb range, Array<T> result)
    {
        if (!node.Intersects(range)) return;
        foreach (var point in node.points)
        {
            if (range.HasPoint(point.Position)) result.Add(point);
        }
        for (int i = 0; i < 8; i++)
        {
            if (node.children[i] != null)
            {
                queryRangeRecursive(node.children[i], range, result);
            }
        }
    }

}

public class OctreeNode<[MustBeVariant] T> where T : Point
{
    const int NODE_CAPACITY = 16;

    public Aabb boundary { get; private set; }
    public Array<T> points { get; private set; }
    public OctreeNode<T>[] children { get; private set; }

    public OctreeNode(Aabb boundary)
    {
        this.boundary = boundary;
        points = new Array<T>();
        children = new OctreeNode<T>[8];
    }

    public void Grow(float factor)
    {
        Vector3 start = boundary.Position + boundary.Position * (factor / 2f);
        Vector3 size = boundary.Size + boundary.Size * (factor);
        boundary = new Aabb(start, size).Abs();
        foreach (var child in children)
        {
            if (child != null)
            {
                child.Grow(factor / 2f);
            }
        }
    }

    public bool IsLeaf()
    {
        bool isLeaf = true;
        foreach (var child in children)
        {
            if (child != null) isLeaf = false;
        }
        return isLeaf;
    }

    public int GetOctant(T point)
    {
        int octant = 0;
        if (point.Position.X >= boundary.GetCenter().X) octant |= 1;
        if (point.Position.Y >= boundary.GetCenter().Y) octant |= 2;
        if (point.Position.Z >= boundary.GetCenter().Z) octant |= 4;
        return octant;
    }

    public float DistanceToPointSq(T Point)
    {
        Vector3 halfDim = boundary.Size / 2f;
        float dx = 0f;
        if (Point.Position.X < boundary.GetCenter().X - halfDim.X) dx = Point.Position.X - (boundary.GetCenter().X - halfDim.X);
        else if (Point.Position.X >= boundary.GetCenter().X + halfDim.X) dx = Point.Position.X - (boundary.GetCenter().X + halfDim.X);
        float dy = 0f;
        if (Point.Position.Y < boundary.GetCenter().Y - halfDim.Y) dy = Point.Position.Y - (boundary.GetCenter().Y - halfDim.Y);
        else if (Point.Position.Y >= boundary.GetCenter().Y + halfDim.Y) dy = Point.Position.Y - (boundary.GetCenter().Y + halfDim.Y);
        float dz = 0f;
        if (Point.Position.Z < boundary.GetCenter().Z - halfDim.Z) dz = Point.Position.Z - (boundary.GetCenter().Z - halfDim.Z);
        else if (Point.Position.Z >= boundary.GetCenter().Z + halfDim.Z) dz = Point.Position.Z - (boundary.GetCenter().Z + halfDim.Z);
        return dx * dx + dy * dy + dz * dz;
    }

    public bool Intersects(Aabb range)
    {
        return boundary.Intersects(range);
    }

    public bool Contains(T point)
    {
        return boundary.HasPoint(point.Position);
    }

    public void Subdivide()
    {
        Vector3 childHalfDim = boundary.Size / 4f;

        for (int i = 0; i < 8; i++)
        {
            Vector3 childCenter = boundary.GetCenter();
            childCenter.X += ((i & 1) == 1) ? childHalfDim.X : -childHalfDim.X;
            childCenter.Y += ((i & 2) == 2) ? childHalfDim.Y : -childHalfDim.Y;
            childCenter.Z += ((i & 4) == 4) ? childHalfDim.Z : -childHalfDim.Z;

            Vector3 aabbStart = childCenter - childHalfDim;
            Vector3 size = childHalfDim * 2f;
            children[i] = new OctreeNode<T>(new Aabb(aabbStart, size).Abs());
        }

        //foreach (var point in points)
        //{
        //    int octant = GetOctant(point);
        //    children[octant].Insert(point);
        //}

        //points.Clear();
    }

    public bool Insert(T point)
    {
        if (!boundary.HasPoint(point.Position)) return false;
        if (IsLeaf() && points.Count < NODE_CAPACITY)
        {
            points.Add(point);
            return true;
        }
        if (IsLeaf())
            Subdivide();
        int octant = GetOctant(point);
        return children[octant].Insert(point);
    }
}
