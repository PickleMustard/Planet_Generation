using Godot;

namespace Registries;

/// <summary>
/// Editor-configured handle to a library sound. An item exports a <c>SoundEffect</c>
/// (<c>[Export] SoundEffect? BoostSfx</c>) and points its <see cref="Clip"/> at an
/// <see cref="AudioConfig"/> wrapper from the <c>res://Audio</c> library in the inspector. Scripts
/// trigger it as a template via the <c>AudioBus</c> helpers (<c>AudioBus.Instance?.Play(BoostSfx)</c>),
/// so the played sound is swappable in the editor with no code change.
///
/// Data-only by convention — playback behavior lives on the <c>AudioBus</c>/playback nodes, not here.
/// </summary>
[GlobalClass]
public partial class SoundEffect : Resource
{
    /// <summary>Library clip this effect plays; drag an <see cref="AudioConfig"/> .tres from res://Audio.</summary>
    [Export]
    public AudioConfig? Clip { get; set; }

    /// <summary>Dispatch id resolved from the assigned clip.</summary>
    public StringName Id => Clip?.Id ?? default;

    /// <summary>Whether the assigned clip is a sustained loop.</summary>
    public bool IsLoop => Clip?.Loop ?? false;
}
