using System.Globalization;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    /// <summary>Dev helper: drives the real player in Play mode from Unity MCP snippets (domain reload is off, so statics persist).</summary>
    public static class McpPlayerDriver
    {
        sealed class CountingSoundPlayer : ISoundPlayer
        {
            readonly ISoundPlayer inner;
            public int Count;
            public CountingSoundPlayer(ISoundPlayer inner) { this.inner = inner; }

            public void Play(AudioClip clip, float volume, float pitch)
            {
                Count++;
                if (inner != null) inner.Play(clip, volume, pitch);
            }
        }

        static ScriptedIntentSource source;
        static CountingSoundPlayer gateSound, doorSound, motorSound;

        static FirstPersonPlayer Player => Object.FindAnyObjectByType<FirstPersonPlayer>();

        public static string Install()
        {
            var p = Player;
            if (p == null) return "NO PLAYER";
            source = new ScriptedIntentSource();
            p.SetIntentSource(source);
            gateSound = Wrap(Object.FindAnyObjectByType<GateInteractable>());
            doorSound = Wrap(Object.FindAnyObjectByType<LockedDoorInteractable>());
            motorSound = Wrap(Object.FindAnyObjectByType<PlaceholderVehicleInteractable>());
            return Status();
        }

        public static string Hold(float x, float y, bool sprint, float seconds)
        {
            source.Hold(new PlayerIntent { Move = new Vector2(x, y), Sprint = sprint }, seconds);
            return Status();
        }

        public static string Press(bool interact, bool jump, bool crouch, bool flashlight)
        {
            source.Press(new PlayerIntent { Interact = interact, Jump = jump, CrouchToggle = crouch, Flashlight = flashlight });
            return Status();
        }

        /// <summary>Turns the view toward a world point (yaw and pitch) on the next frame.</summary>
        public static string Face(float x, float y, float z)
        {
            var p = Player;
            var dir = new Vector3(x, y, z) - p.View.position;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            source.Press(new PlayerIntent { LookDegrees = new Vector2(Mathf.DeltaAngle(p.Yaw, yaw), pitch - p.Pitch) });
            return Status();
        }

        public static string TeleportTo(float x, float y, float z, float yaw)
        {
            Player.Teleport(new Vector3(x, y, z), yaw);
            return Status();
        }

        public static string Spawn()
        {
            var point = Object.FindAnyObjectByType<PlayerSpawnPoint>();
            return string.Format(CultureInfo.InvariantCulture, "spawn=({0:F2},{1:F2},{2:F2}) yaw={3:F1}", point.Feet.x, point.Feet.y, point.Feet.z, point.Yaw);
        }

        public static string FlashlightLuminance()
        {
            var p = Player;
            var cam = p.View.GetComponent<Camera>();
            var flashlight = p.GetComponentInChildren<Flashlight>(true);
            return string.Format(CultureInfo.InvariantCulture, "flashlight={0} lum={1:F4}", flashlight.IsOn, McpRender.CenterLuminance(cam));
        }

        public static string Status()
        {
            var p = Player;
            if (p == null) return "NO PLAYER";
            var spawner = p.GetComponent<PlayerSpawner>();
            var steps = p.GetComponent<FootstepAudio>();
            var interactor = p.GetComponentInChildren<Interactor>(true);
            var flashlight = p.GetComponentInChildren<Flashlight>(true);
            var hud = p.GetComponent<PlayerHud>();
            var gate = Object.FindAnyObjectByType<GateInteractable>();
            var pos = p.transform.position;
            return string.Format(CultureInfo.InvariantCulture,
                "state={0} waited={1:F2} pos=({2:F2},{3:F2},{4:F2}) yaw={5:F1} grounded={6} surface={7} feature={8} kind={9} stamina={10:F2} staminaVisible={11} " +
                "prompt='{12}' message='{13}' steps={14} footsteps={15} lastSurface={16} gate={17} gateAngle={18:F0} gateSounds={19} doorSounds={20} motorSounds={21} " +
                "flashlight={22} focus={23} hudBuilt={24} scriptDone={25} frame={26} crouched={27}",
                spawner != null ? spawner.State.ToString() : "-", spawner != null ? spawner.WaitedSeconds : 0f,
                pos.x, pos.y, pos.z, p.Yaw, p.IsGrounded, p.Ground.Surface,
                p.Ground.Feature != null ? p.Ground.Feature.OsmId : 0, p.Ground.Feature != null ? p.Ground.Feature.Kind.ToString() : "-",
                p.Stamina.Normalized, p.Hud.StaminaVisible, p.Hud.Prompt, p.Hud.Message, p.StepCount,
                steps != null ? steps.PlayedCount : -1, steps != null ? steps.LastSurface.ToString() : "-",
                gate != null ? gate.State.ToString() : "-", gate != null ? gate.AngleDeg : 0f,
                gateSound != null ? gateSound.Count : -1, doorSound != null ? doorSound.Count : -1, motorSound != null ? motorSound.Count : -1,
                flashlight != null && flashlight.IsOn, interactor != null && interactor.Focus != null ? interactor.Focus.name : "-",
                hud != null && hud.View != null, source == null || source.IsDone, Time.frameCount, p.IsCrouched);
        }

        static CountingSoundPlayer Wrap(Interactable target)
        {
            if (target == null) return null;
            var counter = new CountingSoundPlayer(target.AudioOutput != null ? new AudioSourceSoundPlayer(target.AudioOutput) : null);
            target.SetSoundPlayer(counter);
            return counter;
        }
    }
}
