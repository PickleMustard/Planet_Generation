namespace Structures.Logistics;

/// <summary>
/// Represents an engine configuration definition from YAML files.
/// Used for loading engine data from configuration files.
/// This is a simple data transfer object for config loading.
/// For runtime engine calculations with modifiers, see the full EngineDefinition class.
/// </summary>
public class EngineConfigDefinition
{
    /// <summary>
    /// The unique name of the engine.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Specific impulse (Isp) in seconds. Higher values indicate more fuel-efficient engines.
    /// </summary>
    public float SpecificImpulse { get; set; }

    /// <summary>
    /// Thrust output in Newtons. Higher values indicate more powerful engines.
    /// </summary>
    public float Thrust { get; set; }

    /// <summary>
    /// Human-readable description of the engine type.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
