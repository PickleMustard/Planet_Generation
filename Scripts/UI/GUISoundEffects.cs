using Godot;
using Registries;
using UtilityLibrary;

namespace UI;

/// <summary>
/// Plays GUI sound effects in response to <see cref="AudioBus.PlaySound"/>. Attached to the
/// <c>GUISoundEffects</c> <see cref="AudioStreamPlayer"/> node (with an
/// <c>AudioStreamPolyphonic</c> stream) in <c>MainGameUI.tscn</c>, so clips can overlap.
/// Sounds are data-driven: the <see cref="Sounds"/> dictionary maps a dispatch id to an
/// <see cref="AudioConfig"/> wrapper from the <c>res://Audio</c> library, assigned in the editor.
/// Adding a GUI sound needs no code here — add a library clip to the dict and emit its id.
/// </summary>
public partial class GUISoundEffects : AudioStreamPlayer
{
    /// <summary>Dispatch id → library clip. Filled in the editor; key should match the clip's <c>Id</c>.</summary>
    [Export]
    public Godot.Collections.Dictionary<StringName, AudioConfig> Sounds { get; set; } = new();

    private AudioStreamPlaybackPolyphonic? _playback;

    public override void _Ready()
    {
        // The polyphonic stream must be "playing" for individual clips to be queued onto it.
        Play();
        _playback = GetStreamPlayback() as AudioStreamPlaybackPolyphonic;
        if (_playback == null)
        {
            GameLogger.Warning(
                "[GUISoundEffects] Stream playback is not AudioStreamPolyphonic; GUI sounds disabled."
            );
        }

        if (AudioBus.Instance == null)
        {
            GameLogger.Warning("[GUISoundEffects] AudioBus.Instance is null; GUI sounds disabled.");
            return;
        }

        AudioBus.Instance.PlaySound += OnPlaySound;
    }

    public override void _ExitTree()
    {
        if (AudioBus.Instance == null)
            return;

        AudioBus.Instance.PlaySound -= OnPlaySound;
    }

    private void OnPlaySound(StringName id)
    {
        if (Sounds.TryGetValue(id, out var cfg))
            PlayConfig(cfg);
    }

    private void PlayConfig(AudioConfig? cfg)
    {
        if (_playback == null || cfg?.Stream == null)
            return;

        _playback.PlayStream(cfg.Stream, 0f, cfg.VolumeDb, cfg.Pitch);
    }
}
