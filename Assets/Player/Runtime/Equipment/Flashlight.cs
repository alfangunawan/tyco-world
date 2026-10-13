using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Spot light under the camera: F toggles it with a click; the beam trails the view slightly.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class Flashlight : MonoBehaviour, IPlayerModule
    {
        [SerializeField] AudioSource clickSource;
        [SerializeField] AudioClip clickClip;

        Light spot;
        ISoundPlayer sound;
        Transform follow;
        Quaternion current;

        public int Order => 30;
        public bool IsOn => spot != null && spot.enabled;

        public void Configure(AudioSource source, AudioClip click)
        {
            clickSource = source;
            clickClip = click;
        }

        public void SetSoundPlayer(ISoundPlayer player) => sound = player;

        public void Initialize(FirstPersonPlayer player)
        {
            var t = player.Tuning.flashlight;
            spot = GetComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = t.outerAngle;
            spot.innerSpotAngle = t.innerAngle;
            spot.range = t.range;
            spot.intensity = t.intensity;
            spot.color = t.color;
            spot.shadows = LightShadows.Soft;
            spot.enabled = t.startOn;
            if (sound == null && clickSource != null) sound = new AudioSourceSoundPlayer(clickSource);
            follow = transform.parent;
            current = follow != null ? follow.rotation : transform.rotation;
        }

        public void Toggle()
        {
            if (spot == null) return;
            spot.enabled = !spot.enabled;
            if (sound != null) sound.Play(clickClip, 0.8f, spot.enabled ? 1.05f : 0.95f);
        }

        public void Tick(FirstPersonPlayer player, float dt)
        {
            if (player.Intent.Flashlight) Toggle();
            if (follow == null) return;
            current = Quaternion.Slerp(current, follow.rotation, 1f - Mathf.Exp(-player.Tuning.flashlight.lagSpeed * dt));
            transform.rotation = current;
        }
    }
}
