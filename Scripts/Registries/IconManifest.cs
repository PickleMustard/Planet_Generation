using Godot;

namespace Registries;

/// <summary>
/// Export anchor for every <see cref="IconConfig"/> <c>.tres</c> wrapper in the project.
/// YAML config references icons by string path, which Godot's exporter cannot follow; holding
/// the wrappers in an <c>[Export]</c> array — reachable from the boot scene via
/// <see cref="AssetManifests"/> — forces the exporter to keep them (and their embedded textures).
/// Populated by the developer-tool manifest builder; kept as a committed <c>.tres</c>.
/// </summary>
[GlobalClass]
public partial class IconManifest : Resource
{
    /// <summary>Every icon wrapper that must ship in the build.</summary>
    [Export]
    public Godot.Collections.Array<IconConfig> Icons { get; set; } = new();
}
