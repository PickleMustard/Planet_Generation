using Godot;

namespace Registries;

/// <summary>
/// Boot-scene node that anchors the asset manifests into Godot's export dependency graph.
/// YAML config references asset wrappers by string path only, so the exporter never learns those
/// <c>.tres</c> files are needed and strips them. By exporting the three manifests here — and
/// placing this node in <c>DataLoading.tscn</c> — the dependency chain
/// scene → AssetManifests → manifest <c>.tres</c> → wrapper <c>.tres</c> → texture/scene/stream
/// is complete, so everything ships.
///
/// The node has no runtime behaviour; it is freed when the boot scene transitions away. Export
/// dependency tracking is static analysis of the scene file and is unaffected by that lifetime.
/// Developer tooling loads the manifests by the path constants below rather than via the live node.
/// </summary>
public partial class AssetManifests : Node
{
    /// <summary>Canonical on-disk location of the icon manifest.</summary>
    public const string IconManifestPath = "res://Configuration/AssetManifests/IconManifest.tres";

    /// <summary>Canonical on-disk location of the model manifest.</summary>
    public const string ModelManifestPath = "res://Configuration/AssetManifests/ModelManifest.tres";

    /// <summary>Canonical on-disk location of the audio manifest.</summary>
    public const string AudioManifestPath = "res://Configuration/AssetManifests/AudioManifest.tres";

    [Export]
    public IconManifest? Icons { get; set; }

    [Export]
    public ModelManifest? Models { get; set; }

    [Export]
    public AudioManifest? Audio { get; set; }
}
