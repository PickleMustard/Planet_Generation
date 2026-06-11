using Godot;

namespace Registries;

/// <summary>
/// Godot-native wrapper resource for an audio clip. Stored as a <c>.tres</c> so the exporter
/// tracks (and keeps) the embedded <see cref="AudioStream"/>; configuration points at the
/// <c>.tres</c> path instead of a raw <c>.ogg</c>/<c>.wav</c>/<c>.mp3</c> file.
/// </summary>
[GlobalClass]
public partial class AudioConfig : Resource
{
    /// <summary>Identifier for the clip.</summary>
    [Export]
    public StringName Id { get; set; } = "";

    /// <summary>The wrapped audio stream.</summary>
    [Export]
    public AudioStream? Stream { get; set; }

    /// <summary>Default playback volume in decibels.</summary>
    [Export]
    public float VolumeDb { get; set; } = 0.0f;

    /// <summary>Default pitch scale.</summary>
    [Export]
    public float Pitch { get; set; } = 1.0f;

    /// <summary>
    /// When true the clip is a sustained loop (played on a dedicated non-polyphonic player and
    /// re-played on finish) rather than a fire-and-forget one-shot. Driven by
    /// <c>StartLoopSound</c>/<c>StopLoopSound</c> on the <c>AudioBus</c>.
    /// </summary>
    [Export]
    public bool Loop { get; set; } = false;
}
