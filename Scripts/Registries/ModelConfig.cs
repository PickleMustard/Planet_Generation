using Godot;

namespace Registries;

/// <summary>
/// Godot-native wrapper resource for a 3D model. Stored as a <c>.tres</c> so the exporter
/// tracks (and keeps) the embedded <see cref="PackedScene"/>; configuration points at the
/// <c>.tres</c> path instead of a raw <c>.glb</c>/<c>.gltf</c> file.
/// </summary>
[GlobalClass]
public partial class ModelConfig : Resource
{
    /// <summary>Identifier for the model (matches the asset stem, e.g. <c>factory_a</c>).</summary>
    [Export]
    public StringName Id { get; set; } = "";

    /// <summary>The wrapped model scene.</summary>
    [Export]
    public PackedScene? Model { get; set; }

    /// <summary>Default uniform scale multiplier.</summary>
    [Export]
    public float Scale { get; set; } = 1.0f;

    /// <summary>Default rotation offset in degrees.</summary>
    [Export]
    public Vector3 RotationOffset { get; set; } = Vector3.Zero;
}
