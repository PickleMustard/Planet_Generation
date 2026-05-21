#if DEBUG
using System;
using System.Collections.Generic;
using Debug.DatabaseViewer;

namespace ProceduralGeneration.MeshGeneration;

public partial class StructureDatabase : IDebugDataProvider
{
    string IDataProvider.Name => "Structure Database";
    string IDataProvider.Category => "Mesh Generation";
    bool IDataProvider.NeedsRefresh => true;
    object IDebugDataProvider.SourceObject => this;
    string IDebugDataProvider.InstanceNamespace => $"StructureDatabase.{Index}";
    bool IDebugDataProvider.IsSourceValid => true;

    DebugDataNode IDataProvider.GetData()
    {
        lock (lockObject)
        {
            var node = new DebugDataNode("Structure Database")
                .AddProperty("Index", Index)
                .AddProperty("Mesh State", state.ToString())
                .AddProperty("Vertices", BaseVertices.Count)
                .AddProperty("Half Edges", HalfEdgeById.Count)
                .AddProperty("Triangles", TrianglesById.Count)
                .AddProperty("Voronoi Cells", VoronoiCells.Count)
                .AddProperty("Undirected Edges", UndirectedEdgeIndex.Count);

            var baseMeshNode = node.AddChild("Base Mesh");
            baseMeshNode.AddProperty("Base Triangles", BaseTris.Count);
            baseMeshNode.AddProperty("Used Edges", UsedEdges.Count);
            baseMeshNode.AddProperty("Used Points", UsedPoints.Count);

            var dualMeshNode = node.AddChild("Dual Mesh");
            dualMeshNode.AddProperty("Voronoi Vertices", VoronoiVertices.Count);
            dualMeshNode.AddProperty("Voronoi Cell Vertices", VoronoiCellVertices.Count);
            dualMeshNode.AddProperty("Cell Map Entries", CellMap.Count);
            dualMeshNode.AddProperty("Edge Map Entries", EdgeMap.Count);

            return node;
        }
    }

    void IDataProvider.Refresh() { }

    IEnumerable<string> IDataProvider.Search(string pattern)
    {
        var results = new List<string>();
        lock (lockObject)
        {
            if (Index.ToString().Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                results.Add($"StructureDatabase.{Index}");
            }
        }
        return results;
    }
}
#endif
