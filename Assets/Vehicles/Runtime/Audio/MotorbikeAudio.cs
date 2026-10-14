using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// Engine loop (pitch and volume follow speed), starter, horn and bump one-shots (spec #5 §7). Level is the
    /// settings "Efek" volume; the engine volume is rewritten every frame, so the generic effects volume skips these sources.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MotorbikeAudio : MonoBehaviour
    {
        [SerializeField] AudioSource engineSource;
        [SerializeField] AudioSource oneShotSource;
        [SerializeField] AudioClip engineClip;
        [SerializeField] AudioClip starterClip;
        [SerializeField] AudioClip hornClip;
        [SerializeField] AudioClip bumpClip;

        float level = 1f;
        float starterLeft;
        float sinceHorn = float.MaxValue;
        ISoundPlayer oneShots;

        public float Level
        {
            get => level;
            set => level = Mathf.Clamp01(value);
        }

        public bool EngineRunning { get; private set; }
        public AudioSource EngineSource => engineSource;
        public AudioSource OneShotSource => oneShotSource;

        /// <summary>Where one-shots go; defaults to the one-shot AudioSource. Tests swap in a recorder.</summary>
        public ISoundPlayer OneShots
        {
            get
            {
                if (oneShots == null && oneShotSource != null) oneShots = new AudioSourceSoundPlayer(oneShotSource);
                return oneShots;
            }
            set => oneShots = value;
        }

        public void Configure(AudioSource engine, AudioSource oneShot, AudioClip engineLoop, AudioClip starter, AudioClip horn, AudioClip bump)
        {
            engineSource = engine;
            oneShotSource = oneShot;
            engineClip = engineLoop;
            starterClip = starter;
            hornClip = horn;
            bumpClip = bump;
            oneShots = null;
        }

        public void StartEngine()
        {
            if (EngineRunning) return;
            EngineRunning = true;
            Play(starterClip, 1f);
            starterLeft = starterClip != null ? starterClip.length : 0f;
            if (engineSource == null) return;
            engineSource.clip = engineClip;
            engineSource.loop = true;
        }

        public void StopEngine()
        {
            EngineRunning = false;
            starterLeft = 0f;
            if (engineSource != null && engineSource.isPlaying) engineSource.Stop();
        }

        public void Tick(float speed01, float throttle, float dt, BikeTuning t)
        {
            if (sinceHorn < float.MaxValue) sinceHorn += dt;
            if (engineSource == null) return;
            engineSource.pitch = EngineSound.Pitch(speed01, throttle, t);
            engineSource.volume = EngineSound.Volume(speed01, t) * level;
            if (!EngineRunning) return;
            if (starterLeft > 0f)
            {
                starterLeft -= dt;
                return;
            }
            if (Application.isPlaying && engineSource.clip != null && !engineSource.isPlaying) engineSource.Play();
        }

        /// <summary>Honks unless the last honk was less than hornCooldown ago.</summary>
        public bool Horn(BikeTuning t)
        {
            if (sinceHorn < t.hornCooldown) return false;
            sinceHorn = 0f;
            Play(hornClip, 1f);
            return true;
        }

        public void Bump(float strength01) => Play(bumpClip, Mathf.Clamp01(strength01));

        void Play(AudioClip clip, float volume)
        {
            var player = OneShots;
            if (player != null && clip != null) player.Play(clip, volume * level, 1f);
        }
    }
}
