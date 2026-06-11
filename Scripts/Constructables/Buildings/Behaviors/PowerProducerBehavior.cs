using System.Collections.Generic;
using System.Linq;
using Constructables.Power;
using Godot;
using ProceduralGeneration;
using Structures.Enums;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.GameMath.Orbital;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Generates power per tick at the rate declared by the active recipe's
/// <c>power</c> output. Acts as a <see cref="IGridContributor"/>: its
/// <see cref="Radius"/> seeds and extends the surrounding <see cref="PowerGrid"/>.
/// </summary>
public partial class PowerProducerBehavior : RefCounted, IBuildingBehavior, IGridContributor, IBehaviorConfigurable
{
    private Building? _owner;

    public Building? Owner => _owner;

    public int Radius { get; set; }

    /// <summary>
    /// Default recipe read from YAML behavior config. Used to resolve power output
    /// when <see cref="ManufacturingBehavior"/> is not present on the building.
    /// </summary>
    public string? DefaultRecipe { get; private set; }

    /// <summary>
    /// Production speed multiplier read from YAML behavior config. Affects
    /// manufacturing cycle duration when the producer drives its own cycles.
    /// Defaults to 1.0.
    /// </summary>
    public float ProductionSpeed { get; private set; } = 1.0f;

    /// <summary>
    /// Optional environment-driven output scaler, sampled at OnRegister.
    /// When present, <see cref="EnvScaleFactor"/> is applied to <see cref="EffectiveOutput"/>.
    /// </summary>
    private EnvironmentalModifier? _environmentalModifier;

    /// <summary>
    /// Cached factor from the environmental modifier; 1.0 when none configured.
    /// Multiplies <see cref="EffectiveOutput"/> so renewables (solar/wind/geothermal)
    /// report their environment-adjusted magnitude.
    /// </summary>
    public float EnvScaleFactor { get; private set; } = 1f;

    /// <summary>
    /// Rated (pre-scaling) power output, read live from the active recipe's
    /// <c>power</c> output entry. Falls back to the building's <see cref="Building.ActiveRecipeId"/>
    /// or <see cref="ManufacturingBehavior.DefaultRecipe"/> when no cycle is currently in flight
    /// (so renewables show their rated number even between cycles).
    /// </summary>
    public virtual float Output => ResolveRecipePower();

    /// <summary>
    /// Per-tick power contribution to the grid. Equals the recipe-rated <see cref="Output"/>
    /// scaled by the owning <see cref="ManufacturingBehavior.EnvScaleFactor"/> so renewables
    /// (solar/wind/geothermal) report their environment-adjusted magnitude here.
    /// </summary>
    public virtual float EffectiveOutput => Output * GetEnvScaleFactor();

    private float GetEnvScaleFactor()
    {
        // Prefer ManufacturingBehavior's factor when present (backward compat),
        // otherwise use this behavior's own cached factor.
        if (_owner != null)
        {
            var mfg = _owner.GetBehavior<ManufacturingBehavior>();
            if (mfg != null)
                return mfg.EnvScaleFactor;
        }
        return EnvScaleFactor;
    }

    /// <summary>
    /// Total work required for the current production cycle. Delegates to
    /// <see cref="ManufacturingBehavior"/> when present; otherwise 0 (no cycle for
    /// standalone/renewable producers).
    /// </summary>
    public float WorkRequired
    {
        get
        {
            if (_owner == null) return 0f;
            var mfg = _owner.GetBehavior<ManufacturingBehavior>();
            return mfg?.WorkRequired ?? 0f;
        }
    }

    /// <summary>
    /// Work remaining in the current production cycle. Delegates to
    /// <see cref="ManufacturingBehavior"/> when present; otherwise 0 (no cycle for
    /// standalone/renewable producers).
    /// </summary>
    public float WorkRemaining
    {
        get
        {
            if (_owner == null) return 0f;
            var mfg = _owner.GetBehavior<ManufacturingBehavior>();
            if (mfg == null || mfg.WorkRequired <= 0f) return 0f;
            return Mathf.Max(mfg.WorkRequired - mfg.WorkProgress, 0f);
        }
    }

    /// <summary>
    /// Renewable producers (solar, wind, geothermal) generate continuously while powered on,
    /// independent of any manufacturing cycle. Fueled plants (default) gate generation on the
    /// owner's <see cref="ManufacturingState.Manufacturing"/> state so they only output while
    /// burning fuel.
    /// </summary>
    public bool IsRenewable { get; set; } = false;

    // Producers carry no battery storage.
    public float BatteryCapacity => 0f;
    public float BatteryStored
    {
        get => 0f;
        set { /* no-op for producers */ }
    }

    /// <summary>
    /// Producers contribute power only while a manufacturing cycle is running (fueled plants),
    /// or continuously while powered on (renewables). Brownout / construction always disables.
    /// </summary>
    public bool IsProducing
    {
        get
        {
            if (_owner == null || !_owner.PoweredOn || _owner.IsUnderConstruction)
                return false;
            if (IsRenewable)
                return true;
            var mfg = _owner.GetBehavior<ManufacturingBehavior>();
            if (mfg != null)
                return mfg.State == ManufacturingState.Manufacturing;
            // Standalone non-renewable producer without ManufacturingBehavior:
            // producing when powered and not under construction.
            return true;
        }
    }

    public virtual void OnAttach(Building owner) => _owner = owner;
    public virtual void OnRegister()
    {
        if (_environmentalModifier == null)
        {
            EnvScaleFactor = 1f;
            return;
        }
        EnvScaleFactor = _environmentalModifier.ComputeFactor(_owner);
        GameLogger.Debug(
            $"PowerProducerBehavior: env modifier {_environmentalModifier.Type} -> scale {EnvScaleFactor:F3}");
    }
    public virtual void OnUnregister() { }
    public virtual void OnDetach() => _owner = null;

    public virtual void OnManufactureTick(float delta, Building owner) { }

    public virtual void Configure(Dictionary<string, object> config)
    {
        Radius = BehaviorConfigHelper.ReadInt(config, "grid_radius", 0);
        IsRenewable = BehaviorConfigHelper.ReadBool(config, "is_renewable", false);
        DefaultRecipe = BehaviorConfigHelper.ReadString(config, "default_recipe", null);
        ProductionSpeed = BehaviorConfigHelper.ReadFloat(config, "production_speed", 1.0f);
        _environmentalModifier = EnvironmentalModifier.FromConfig(config);
    }

    /// <summary>
    /// Placement-preview snapshot of what a power producer would generate on a given body,
    /// computed statically from the building definition (no live <see cref="Building"/> exists
    /// during placement). Mirrors <see cref="ExtractionBehavior.GetExtractableDeposits"/>.
    /// </summary>
    public readonly struct PowerPreview
    {
        public readonly float Rated;       // recipe power before scaling
        public readonly float Scale;       // env factor (1.0 when no modifier)
        public readonly float Effective;   // Rated * Scale
        public readonly EnvironmentalModifier.ModifierType ModifierType;
        public readonly float Atmosphere;  // body atmosphere (for the why-line)
        public readonly float DistanceAU;  // body distance to star in AU (for the why-line)
        public readonly bool HasOutput;    // false when no PowerProducerBehavior / no recipe power

        public PowerPreview(
            float rated, float scale, EnvironmentalModifier.ModifierType modifierType,
            float atmosphere, float distanceAU, bool hasOutput)
        {
            Rated = rated;
            Scale = scale;
            Effective = rated * scale;
            ModifierType = modifierType;
            Atmosphere = atmosphere;
            DistanceAU = distanceAU;
            HasOutput = hasOutput;
        }
    }

    /// <summary>
    /// Computes the power a producer would generate if placed on <paramref name="body"/> over
    /// <paramref name="cells"/>, including any environmental scaling. Returns a preview with
    /// <see cref="PowerPreview.HasOutput"/> false when the definition has no PowerProducerBehavior
    /// or its recipe declares no <c>power</c> output.
    /// </summary>
    public static PowerPreview GetPlacementPreview(
        BuildingDefinition def, IOrbitalBody? body, IReadOnlyList<VoronoiCell>? cells)
    {
        var entry = def?.BehaviorEntries
            .FirstOrDefault(e => e.BehaviorId == "PowerProducerBehavior");
        if (entry == null)
            return default;

        string? recipeId = BehaviorConfigHelper.ReadString(entry.Config, "default_recipe", null);
        float rated = 0f;
        if (!string.IsNullOrEmpty(recipeId)
            && RecipeDatabase.Instance != null
            && RecipeDatabase.Instance.TryGetRecipe(recipeId, out var recipe))
        {
            rated = recipe.OutputResources.TryGetValue("power", out var p) ? p : 0f;
        }

        var mod = EnvironmentalModifier.FromConfig(entry.Config);
        float scale = mod?.ComputeFactor(body, cells) ?? 1f;
        var modType = mod?.Type ?? EnvironmentalModifier.ModifierType.None;

        float atmosphere = body?.Atmosphere ?? 0f;
        float distanceAU = body != null
            ? OrbitalMath.ConvertUnitsToAU(body.DistanceToParentStar())
            : 0f;

        return new PowerPreview(rated, scale, modType, atmosphere, distanceAU, rated > 0f);
    }

    private float ResolveRecipePower()
    {
        var recipe = ResolveActiveRecipe();
        if (recipe == null)
            return 0f;
        return recipe.OutputResources.TryGetValue("power", out var p) ? p : 0f;
    }

    private RecipeDefinition? ResolveActiveRecipe()
    {
        if (_owner == null)
            return null;

        // Try ManufacturingBehavior first (existing behavior for fueled plants).
        var mfg = _owner.GetBehavior<ManufacturingBehavior>();
        if (mfg != null)
        {
            if (mfg.CurrentRecipe != null)
                return mfg.CurrentRecipe;
            string? mfgRecipe = _owner.ActiveRecipeId ?? mfg.DefaultRecipe;
            if (!string.IsNullOrEmpty(mfgRecipe) && RecipeDatabase.Instance != null)
                return RecipeDatabase.Instance.TryGetRecipe(mfgRecipe, out var r) ? r : null;
        }

        // Fall back to this behavior's own DefaultRecipe (renewable / standalone producers).
        string? recipeId = _owner.ActiveRecipeId ?? DefaultRecipe;
        if (string.IsNullOrEmpty(recipeId) || RecipeDatabase.Instance == null)
            return null;
        return RecipeDatabase.Instance.TryGetRecipe(recipeId, out var recipe) ? recipe : null;
    }
}
