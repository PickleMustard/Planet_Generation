using Godot;
using Registries;
using UtilityLibrary;

namespace PlayerInteraction;

/// <summary>
/// Plays ship gameplay sound effects in response to <see cref="AudioBus"/> dispatch signals.
/// Attached to the <c>AudioStreamPlayer3D</c> node (with an <c>AudioStreamPolyphonic</c> stream) in
/// <c>player_ship.tscn</c>, routed to the <c>game_sounds</c> bus. Sounds are data-driven: the
/// <see cref="Sounds"/> dictionary maps a dispatch id to an <see cref="AudioConfig"/> wrapper from
/// the <c>res://Audio</c> library, assigned in the editor.
///
/// One-shots (<see cref="AudioConfig.Loop"/> false, e.g. boost/capture) are queued on the
/// polyphonic stream via <see cref="AudioBus.PlaySound"/>. Looping clips (<c>Loop = true</c>, e.g.
/// the engine) can't be sustained by a polyphonic stream, so each gets a dedicated non-polyphonic
/// <see cref="AudioStreamPlayer3D"/> created on demand, started/stopped by
/// <see cref="AudioBus.StartLoopSound"/>/<see cref="AudioBus.StopLoopSound"/> and re-played on
/// <c>Finished</c> while active (loops regardless of the clip's import settings).
/// </summary>
public partial class ShipSoundEffects : AudioStreamPlayer3D
{
    /// <summary>Dispatch id → library clip. Filled in the editor; key should match the clip's <c>Id</c>.</summary>
    [Export]
    public Godot.Collections.Dictionary<StringName, AudioConfig> Sounds { get; set; } = new();

    private AudioStreamPlaybackPolyphonic? _playback;

    // Dedicated per-id players that sustain looping clips, created lazily on first start.
    private readonly System.Collections.Generic.Dictionary<StringName, AudioStreamPlayer3D> _loopPlayers = new();
    private readonly System.Collections.Generic.HashSet<StringName> _activeLoops = new();

    public override void _Ready()
    {
        // The polyphonic stream must be "playing" for individual clips to be queued onto it.
        Play();
        _playback = GetStreamPlayback() as AudioStreamPlaybackPolyphonic;
        if (_playback == null)
        {
            GameLogger.Warning(
                "[ShipSoundEffects] Stream playback is not AudioStreamPolyphonic; ship one-shot sounds disabled."
            );
        }

        if (AudioBus.Instance == null)
        {
            GameLogger.Warning("[ShipSoundEffects] AudioBus.Instance is null; ship sounds disabled.");
            return;
        }

        AudioBus.Instance.PlaySound += OnPlaySound;
        AudioBus.Instance.StartLoopSound += OnStartLoop;
        AudioBus.Instance.StopLoopSound += OnStopLoop;
    }

    public override void _ExitTree()
    {
        if (AudioBus.Instance != null)
        {
            AudioBus.Instance.PlaySound -= OnPlaySound;
            AudioBus.Instance.StartLoopSound -= OnStartLoop;
            AudioBus.Instance.StopLoopSound -= OnStopLoop;
        }
    }

    private void OnPlaySound(StringName id)
    {
        if (Sounds.TryGetValue(id, out var cfg) && !cfg.Loop)
            PlayConfig(cfg);
    }

    private void OnStartLoop(StringName id)
    {
        if (!Sounds.TryGetValue(id, out var cfg) || !cfg.Loop || cfg.Stream == null)
            return;

        var player = GetOrCreateLoopPlayer(id, cfg);
        _activeLoops.Add(id);
        player.Play();
    }

    private void OnStopLoop(StringName id)
    {
        _activeLoops.Remove(id);
        if (_loopPlayers.TryGetValue(id, out var player))
            player.Stop();
    }

    private AudioStreamPlayer3D GetOrCreateLoopPlayer(StringName id, AudioConfig cfg)
    {
        if (_loopPlayers.TryGetValue(id, out var existing))
            return existing;

        var player = new AudioStreamPlayer3D
        {
            Bus = Bus,
            Stream = cfg.Stream,
            VolumeDb = cfg.VolumeDb,
            PitchScale = cfg.Pitch,
        };
        // Re-play the clip while its loop is active, independent of the stream's import loop flag.
        player.Finished += () =>
        {
            if (_activeLoops.Contains(id))
                player.Play();
        };
        AddChild(player);
        _loopPlayers[id] = player;
        return player;
    }

    private void PlayConfig(AudioConfig? cfg)
    {
        if (_playback == null || cfg?.Stream == null)
            return;

        _playback.PlayStream(cfg.Stream, 0f, cfg.VolumeDb, cfg.Pitch);
    }
}
