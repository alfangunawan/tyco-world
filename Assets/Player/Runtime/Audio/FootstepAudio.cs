using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Plays one footstep per step event (plus jump and landing) for the surface under the player.</summary>
    public sealed class FootstepAudio : MonoBehaviour, IPlayerModule
    {
        public const float LandingVolumeScale = 1.6f;
        public const float CrouchVolumeScale = 0.6f;
        public const float SprintVolumeScale = 1.2f;

        [SerializeField] FootstepBank bank;
        [SerializeField] AudioSource source;

        ISoundPlayer sound;
        NoRepeatPicker picker = new NoRepeatPicker(0x5EED5EEDu);

        public int Order => 10;
        public FootstepBank Bank => bank;
        public SurfaceType LastSurface { get; private set; } = SurfaceType.Default;
        public int PlayedCount { get; private set; }

        public void Configure(FootstepBank footstepBank, AudioSource audioSource)
        {
            bank = footstepBank;
            source = audioSource;
        }

        public void SetSoundPlayer(ISoundPlayer player) => sound = player;

        public void Initialize(FirstPersonPlayer player)
        {
            if (sound == null && source != null) sound = new AudioSourceSoundPlayer(source);
        }

        public void Tick(FirstPersonPlayer player, float dt)
        {
            var surface = player.Ground.Hit ? player.Ground.Surface : SurfaceType.Default;
            float mode = player.IsCrouched ? CrouchVolumeScale : player.IsSprinting ? SprintVolumeScale : 1f;
            if (player.LandedThisFrame) PlayFor(surface, LandingVolumeScale);
            if (player.JumpedThisFrame) PlayFor(surface, mode);
            for (int i = 0; i < player.StepsThisFrame; i++) PlayFor(surface, mode);
        }

        void PlayFor(SurfaceType surface, float volumeScale)
        {
            if (bank == null || sound == null) return;
            var set = bank.Resolve(surface);
            if (set == null) return;
            int index = picker.Next(set.clips.Length);
            sound.Play(set.clips[index], set.volume * volumeScale, picker.Range(set.pitchMin, set.pitchMax));
            LastSurface = surface;
            PlayedCount++;
        }
    }
}
