using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.DayCycle
{
    /// <summary>
    /// Debug keys for the Editor and development builds: T jumps 06:30 ↔ 19:30, [ / ] change the speed, F3 shows the
    /// HUD clock. Reads the keyboard directly, so it keeps working while player controls are locked.
    /// </summary>
    [RequireComponent(typeof(DayCycleDirector))]
    public sealed class DayCycleDebug : MonoBehaviour
    {
        readonly DebugTimeControls controls = new DebugTimeControls();
        DayCycleDirector director;

        public static bool Allowed => Application.isEditor || Debug.isDebugBuild;
        public DebugTimeControls Controls => controls;

        void Awake()
        {
            if (!Allowed) enabled = false;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            ApplyKeys(new DebugKeys
            {
                JumpTime = keyboard.tKey.wasPressedThisFrame,
                SlowerSpeed = keyboard.leftBracketKey.wasPressedThisFrame,
                FasterSpeed = keyboard.rightBracketKey.wasPressedThisFrame,
                ToggleClock = keyboard.f3Key.wasPressedThisFrame
            });
        }

        public bool ApplyKeys(DebugKeys keys)
        {
            if (director == null) director = GetComponent<DayCycleDirector>();
            if (director == null || !director.IsInitialized) return false;
            if (!controls.Apply(keys, director.Clock)) return false;
            director.ShowClock = controls.ClockVisible;
            if (controls.LastMessage.Length > 0) director.ShowMessage(controls.LastMessage);
            return true;
        }
    }
}
