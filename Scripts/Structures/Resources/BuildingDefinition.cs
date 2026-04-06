using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.Transfers;

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
    /// Maximum number of this building allowed. -1 for no limit.
    /// </summary>
    public int BuildingLimit { get; set; } = -1;

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
    /// Sound effect settings.
    /// </summary>
    public SoundDefinition Sound { get; set; } = new();

    /// <summary>
    /// Transfer station capabilities. Null for non-logistics buildings.
    /// </summary>
    public TransferStationDefinition? TransferStation { get; set; }

    /// <summary>
    /// Defines placement requirements for a building.
    /// </summary>
    public class PlacementRequirements
    {
        /// <summary>
        /// Allowed biome types for placement.
        /// Use empty list with AllowAnyBiome=true for any biome.
        /// </summary>
        public List<Biome.BiomeType> Biomes { get; set; } = new();

        /// <summary>
        /// When true, building can be placed in any biome.
        /// Set to true when biomes list contains "*" wildcard.
        /// </summary>
        public bool AllowAnyBiome { get; set; } = false;

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
        /// Default recipe ID used by this building.
        /// </summary>
        public string? DefaultRecipe { get; set; }

        /// <summary>
        /// Alternative recipe IDs available for this building.
        /// </summary>
        public List<string> AlternativeRecipes { get; set; } = new();

        /// <summary>
        /// Maximum input storage capacity.
        /// </summary>
        public int InputStorageAmount { get; set; } = 0;

        /// <summary>
        /// Maximum output storage capacity.
        /// </summary>
        public int OutputStorageAmount { get; set; } = 0;

        /// <summary>
        /// Production speed multiplier.
        /// </summary>
        public float ProductionSpeed { get; set; } = 1.0f;
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
        /// Path to material resource.
        /// </summary>
        public string? ModelMaterial { get; set; }

        /// <summary>
        /// Path to animation resource.
        /// </summary>
        public string? AnimationPath { get; set; }

        /// <summary>
        /// Scale factor for the model.
        /// </summary>
        public float Scale { get; set; } = 1.0f;

        /// <summary>
        /// Rotation offset in degrees (Euler angles).
        /// </summary>
        public Vector3 RotationOffset { get; set; } = Vector3.Zero;
    }

    /// <summary>
    /// Defines sound effects for a building.
    /// </summary>
    public class SoundDefinition
    {
        /// <summary>
        /// Sound played during construction.
        /// </summary>
        public string? Building { get; set; }

        /// <summary>
        /// Sound played when construction finishes.
        /// </summary>
        public string? Finished { get; set; }

        /// <summary>
        /// Sound played while building is idle.
        /// </summary>
        public string? Idle { get; set; }

        /// <summary>
        /// Sound played while building is fabricating.
        /// </summary>
        public string? Fabricating { get; set; }
    }
}