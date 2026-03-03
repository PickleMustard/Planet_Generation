#if DEBUG
using System.Collections.Generic;
using Godot;
using Structures.GameState;
using Structures.MeshGeneration;
using ProceduralGeneration.PlanetGeneration;
using ProceduralGeneration.MeshGeneration;
using UtilityLibrary;

namespace PlayerInteraction.CellSelection
{
    public partial class CellSelectionManager : Node
    {
        public static CellSelectionManager Instance { get; private set; }

        private VoronoiCell _selectedCell;
        private CelestialBody _selectedBody;
        private Continent _selectedContinent;
        private List<MeshInstance3D> _highlightMeshes = new List<MeshInstance3D>();

        public VoronoiCell SelectedCell => _selectedCell;
        public CelestialBody SelectedBody => _selectedBody;
        public Continent SelectedContinent => _selectedContinent;

        [Signal]
        public delegate void CellSelectedEventHandler(
            VoronoiCell cell,
            CelestialBody body,
            Continent continent
        );

        [Signal]
        public delegate void SelectionClearedEventHandler();

        public override void _EnterTree()
        {
            if (Instance == null)
            {
                GD.Print("CellSelectionManager._EnterTree");
                Instance = this;
            }
            else
            {
                GD.PrintErr(
                    "CellSelectionManager already initialized. Duplicate instance detected."
                );
                QueueFree();
            }
        }

        public void SelectCell(VoronoiCell cell, Continent continent, CelestialBody body)
        {
            if (cell == null || body == null)
            {
                ClearSelection();
                return;
            }

            ClearHighlight();

            _selectedCell = cell;
            _selectedBody = body;

            _selectedContinent = continent;

            DrawCellHighlight(cell, body);

            EmitSignal(SignalName.CellSelected, cell, body, continent);
        }

        public void ClearSelection()
        {
            ClearHighlight();

            _selectedCell = null;
            _selectedBody = null;
            _selectedContinent = null;

            EmitSignal(SignalName.SelectionCleared);
        }

        private void DrawCellHighlight(VoronoiCell cell, CelestialBody body)
        {
            if (cell?.Edges == null || body == null)
                return;

            var root = GetTree().Root;
            Color highlightColor = new Color(1.0f, 0.8f, 0.0f, 1.0f);

            foreach (var edge in cell.Edges)
            {
                if (edge?.P == null || edge?.Q == null)
                    continue;

                Vector3 worldPos1 = body.ToGlobal(
                    edge.P.Position.Normalized() * (body.Mesh.size + cell.Height)
                );
                Vector3 worldPos2 = body.ToGlobal(
                    edge.Q.Position.Normalized() * (body.Mesh.size + cell.Height)
                );

                var lineMesh = PolygonRendererSDL.DrawLine(
                    root,
                    1.0f,
                    worldPos1,
                    worldPos2,
                    highlightColor
                );
                lineMesh.Name = $"CellHighlight_{cell.Index}";
                _highlightMeshes.Add(lineMesh);
            }
        }

        private void ClearHighlight()
        {
            foreach (var mesh in _highlightMeshes)
            {
                if (IsInstanceValid(mesh))
                {
                    mesh.QueueFree();
                }
            }
            _highlightMeshes.Clear();
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                ClearHighlight();
                Instance = null;
            }
            base._ExitTree();
        }
    }
}
#endif
