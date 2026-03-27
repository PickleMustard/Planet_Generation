using System.Collections.Generic;
using Godot;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Defines a building type with construction requirements, placement constraints, and production capabilities.
/// Follows data-driven design pattern similar to ResourceDefinition.
/// </summary>
public class BuildingDefinition
{
    /// <summary>
    /// Unique identifier name for the building.
    /// </summary>
    public string? IdName { get; set; }

    /// <summary>
    /// Display name shown to players in UI.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description shown in building tooltips and UI.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Building category (Agriculture, Extraction, Power, etc.)
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Time required to construct the building in seconds.
    /// </summary>
    public float BuildingTime { get; set; } = 60.0f;

    /// <summary>
    /// Total work units required for construction.
    /// </summary>
    public float WorkRequired { get; set; } = 100.0f;

    /// <summary>
    /// Placement requirements and constraints.
    /// </summary>
    public PlacementRequirements Placement { get; set; } = new();

    /// <summary>
    /// Resources required for construction.
    /// </summary>
    public Dictionary<string, int> RequiredResources { get; set; } = new();

    /// <summary>
    /// Production capabilities of the building.
    /// </summary>
    public ProductionDefinition Production { get; set; } = new();

    /// <summary>
    /// Visual representation settings.
    /// </summary>
    public VisualDefinition Visual { get; set; } = new();

    /// <summary>
    /// Defines placement requirements for a building.
    /// </summary>
    public class PlacementRequirements
    {
        /// <summary>
        /// Allowed biome types for placement.
        /// </summary>
        public List<Biome.BiomeType> Biomes { get; set; } = new();

        /// <summary>
        /// Minimum elevation (0-1) for placement.
        /// </summary>
        public float MinElevation { get; set; } = 0.0f;

        /// <summary>
        /// Maximum elevation (0-1) for placement.
        /// </summary>
        public float MaxElevation { get; set; } = 1.0f;

        /// <summary>
        /// Maximum slope angle in degrees for placement.
        /// </summary>
        public float MaxSlope { get; set; } = 45.0f;

        /// <summary>
        /// Number of Voronoi cells required for building footprint.
        /// </summary>
        public int CellCount { get; set; } = 1;

        /// <summary>
        /// Whether building requires adjacent cells to be available.
        /// </summary>
        public bool RequiresAdjacent { get; set; } = false;
    }

    /// <summary>
    /// Defines production capabilities of a building.
    /// </summary>
    public class ProductionDefinition
    {
        /// <summary>
        /// Resource extraction rate per second (for extraction buildings).
        /// </summary>
        public float ExtractionRate { get; set; } = 0.0f;

        /// <summary>
        /// Resources produced by the building.
        /// </summary>
        public List<string> Resources { get; set; } = new();

        /// <summary>
        /// Recipe IDs that can be produced by the building.
        /// </summary>
        public List<string> Recipes { get; set; } = new();
    }

    /// <summary>
    /// Defines visual representation of a building.
    /// </summary>
    public class VisualDefinition
    {
        /// <summary>
        /// Path to 3D model resource.
        /// </summary>
        public string? ModelPath { get; set; }

        /// <summary>
        /// Scale factor for the model.
        /// </summary>
        public float Scale { get; set; } = 1.0f;

        /// <summary>
        /// Rotation offset in degrees (Euler angles).
        /// </summary>
        public Vector3 RotationOffset { get; set; } = Vector3.Zero;
    }
}