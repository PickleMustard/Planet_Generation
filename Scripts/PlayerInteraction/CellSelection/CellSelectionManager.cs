using Godot;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;

namespace PlayerInteraction.CellSelection
{
    /// <summary>
    /// Manages Voronoi cell selection state and shader-based highlight rendering.
    /// When a cell is selected, this manager updates the cell selection highlight shader
    /// uniform on the target body's mesh material, producing a GPU-driven outline and fill
    /// effect. Works with both <see cref="CelestialBody"/> and <see cref="SatelliteBody"/>.
    /// Registered as an autoload singleton.
    /// </summary>
    public partial class CellSelectionManager : Node
    {
        public static CellSelectionManager? Instance { get; private set; }

        private VoronoiCell? _selectedCell;
        private Node3D? _selectedBody;
        private Continent? _selectedContinent;
        private int _selectedContinentIndex = -1;

        public VoronoiCell? SelectedCell => _selectedCell;
        public Node3D? SelectedBody => _selectedBody;
        public Continent? SelectedContinent => _selectedContinent;
        public int SelectedContinentIndex => _selectedContinentIndex;

        /// <summary>
        /// Emitted when a cell is selected. The body parameter is the Node3D
        /// that implements <see cref="ISelectableBody"/> (either CelestialBody or SatelliteBody).
        /// The continent parameter may be null for satellite bodies without continent data.
        /// </summary>
        [Signal]
        public delegate void CellSelectedEventHandler(
            VoronoiCell cell,
            Node3D body,
            Continent continent
        );

        [Signal]
        public delegate void SelectionClearedEventHandler();

        [Signal]
        public delegate void ContinentSelectedEventHandler(int continentIndex, Node3D body);

        [Signal]
        public delegate void ContinentSelectionClearedEventHandler();

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

        /// <summary>
        /// Selects a Voronoi cell on the given body. Clears any previous selection
        /// and updates shader uniforms on both the old and new body meshes.
        /// </summary>
        /// <param name="cell">The Voronoi cell to highlight.</param>
        /// <param name="continent">The continent the cell belongs to (may be null).</param>
        /// <param name="body">The Node3D body (must implement ISelectableBody).</param>
        public void SelectCell(VoronoiCell cell, Continent? continent, Node3D body)
        {
            if (cell == null || body == null)
            {
                ClearSelection();
                return;
            }

            // Clear the highlight on the previously selected body's shader
            ClearShaderHighlight();

            _selectedCell = cell;
            _selectedBody = body;
            _selectedContinent = continent;

            // Set the highlight on the new body's shader
            ApplyShaderHighlight(cell, body);

            EmitSignal(SignalName.CellSelected, cell, body, continent!);
        }

        /// <summary>
        /// Clears the current cell selection and resets the shader highlight.
        /// </summary>
        public void ClearSelection()
        {
            ClearShaderHighlight();

            _selectedCell = null;
            _selectedBody = null;
            _selectedContinent = null;

            EmitSignal(SignalName.SelectionCleared);
        }

        /// <summary>
        /// Updates the cell selection highlight shader uniform on the target body's mesh.
        /// Sets selected_cell_id to the cell's Index so the shader can highlight it.
        /// </summary>
        private void ApplyShaderHighlight(VoronoiCell cell, Node3D body)
        {
            if (body is ISelectableBody selectable && selectable.Mesh != null)
            {
                selectable.Mesh.SetSelectedCell((float)cell.Index);
            }
        }

        /// <summary>
        /// Resets the cell selection highlight shader on the currently selected body.
        /// Sets selected_cell_id to -1.0 so no cell is highlighted.
        /// </summary>
        private void ClearShaderHighlight()
        {
            if (_selectedBody is ISelectableBody selectable && selectable.Mesh != null)
            {
                selectable.Mesh.SetSelectedCell(-1.0f);
            }
        }

        /// <summary>
        /// Selects a continent on the given body. Highlights the entire continent.
        /// </summary>
        /// <param name="continentIndex">The continent index to highlight.</param>
        /// <param name="body">The Node3D body containing the continent.</param>
        public void SelectContinent(int continentIndex, Node3D body)
        {
            if (continentIndex < 0 || body == null)
            {
                ClearContinentSelection();
                return;
            }

            // Clear previous continent highlight
            ClearContinentShaderHighlight();

            _selectedContinentIndex = continentIndex;
            _selectedBody = body;

            // Get continent from body's mesh
            if (body is ISelectableBody selectable && selectable.Mesh?.Continents != null)
            {
                selectable.Mesh.Continents.TryGetValue(continentIndex, out _selectedContinent);
            }

            // Apply shader highlight
            ApplyContinentShaderHighlight(continentIndex, body);

            EmitSignal(SignalName.ContinentSelected, continentIndex, body);
        }

        /// <summary>
        /// Clears the current continent selection.
        /// </summary>
        public void ClearContinentSelection()
        {
            ClearContinentShaderHighlight();
            _selectedContinentIndex = -1;
            _selectedContinent = null;
            EmitSignal(SignalName.ContinentSelectionCleared);
        }

        /// <summary>
        /// Updates the continent selection highlight shader uniform on the target body's mesh.
        /// </summary>
        private void ApplyContinentShaderHighlight(int continentIndex, Node3D body)
        {
            if (body is ISelectableBody selectable && selectable.Mesh != null)
            {
                selectable.Mesh.SetSelectedContinent(continentIndex);
            }
        }

        /// <summary>
        /// Resets the continent selection highlight shader on the currently selected body.
        /// </summary>
        private void ClearContinentShaderHighlight()
        {
            if (_selectedBody is ISelectableBody selectable && selectable.Mesh != null)
            {
                selectable.Mesh.ClearSelectedContinent();
            }
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                ClearShaderHighlight();
                ClearContinentShaderHighlight();
                Instance = null;
            }
            base._ExitTree();
        }
    }
}
