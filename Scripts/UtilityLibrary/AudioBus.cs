using Godot;
using Registries;

namespace UtilityLibrary
{
    /// <summary>
    /// Global sound dispatcher, parallel to <see cref="SignalBus"/> but dedicated to sound
    /// effects. Items trigger a sound with no reference to the player — preferably via an
    /// editor-configured <see cref="Registries.SoundEffect"/> template
    /// (<c>AudioBus.Instance?.Play(BoostSfx)</c>), or by id
    /// (<c>AudioBus.Instance?.EmitPlaySound(SfxIds.Click)</c>). Playback nodes
    /// (<c>GUISoundEffects</c>, <c>ShipSoundEffects</c>) hold an editor-assigned
    /// <c>Dictionary&lt;StringName, AudioConfig&gt;</c> of library clips and play the entry whose
    /// key matches the dispatched id (silently ignoring ids they don't own).
    ///
    /// One-shots use <see cref="PlaySound"/>; sustained loops use
    /// <see cref="StartLoopSound"/>/<see cref="StopLoopSound"/>. The <c>SoundEffect</c> overloads
    /// (<see cref="Play(Registries.SoundEffect)"/> etc.) resolve a template to its id and dispatch.
    /// All signals are main-thread only —
    /// GUI sounds fire from the main thread and the ship emits from <c>_PhysicsProcess</c>, which
    /// also runs on the main thread.
    /// </summary>
    public partial class AudioBus : Node
    {
        public static AudioBus? Instance { get; private set; }

        public override void _Ready()
        {
            Instance = this;
        }

        /// <summary>Fire-and-forget one-shot identified by its library id.</summary>
        [Signal]
        public delegate void PlaySoundEventHandler(StringName id);

        public void EmitPlaySound(StringName id) => EmitSignal(SignalName.PlaySound, id);

        /// <summary>Start a sustained loop (an <see cref="Registries.AudioConfig"/> with <c>Loop = true</c>).</summary>
        [Signal]
        public delegate void StartLoopSoundEventHandler(StringName id);

        public void EmitStartLoopSound(StringName id) => EmitSignal(SignalName.StartLoopSound, id);

        /// <summary>Stop a sustained loop previously started with <see cref="StartLoopSound"/>.</summary>
        [Signal]
        public delegate void StopLoopSoundEventHandler(StringName id);

        public void EmitStopLoopSound(StringName id) => EmitSignal(SignalName.StopLoopSound, id);

        // --- SoundEffect template helpers ---
        // Resolve an editor-configured SoundEffect to its clip id and dispatch. Null-safe so an
        // unconfigured export (or null effect) is a silent no-op.

        /// <summary>Play a one-shot from a configured <see cref="SoundEffect"/> template.</summary>
        public void Play(SoundEffect? fx)
        {
            if (fx?.Clip != null)
                EmitPlaySound(fx.Clip.Id);
        }

        /// <summary>Start a configured loop <see cref="SoundEffect"/> (clip with <c>Loop = true</c>).</summary>
        public void StartLoop(SoundEffect? fx)
        {
            if (fx?.Clip != null)
                EmitStartLoopSound(fx.Clip.Id);
        }

        /// <summary>Stop a configured loop <see cref="SoundEffect"/> previously started.</summary>
        public void StopLoop(SoundEffect? fx)
        {
            if (fx?.Clip != null)
                EmitStopLoopSound(fx.Clip.Id);
        }
    }
}
