using Godot;

namespace Registries;

/// <summary>
/// Export anchor for every <see cref="AudioConfig"/> <c>.tres</c> wrapper in the project.
/// YAML config references audio by string path, which Godot's exporter cannot follow; holding
/// the wrappers in an <c>[Export]</c> array — reachable from the boot scene via
/// <see cref="AssetManifests"/> — forces the exporter to keep them (and their embedded streams).
/// Populated by the developer-tool manifest builder; kept as a committed <c>.tres</c>.
/// </summary>
[GlobalClass]
public partial class AudioManifest : Resource
{
    /// <summary>Every audio wrapper that must ship in the build.</summary>
    [Export]
    public Godot.Collections.Array<AudioConfig> Audio { get; set; } = new();
}
