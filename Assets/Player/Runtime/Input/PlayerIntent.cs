using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>What the player wants this frame, independent of the device (keyboard, script, cutscene).</summary>
    public struct PlayerIntent
    {
        /// <summary>x = strafe right, y = forward; magnitude ≤ 1.</summary>
        public Vector2 Move;
        /// <summary>x = yaw delta, y = pitch delta (positive looks up), in degrees for this frame.</summary>
        public Vector2 LookDegrees;
        /// <summary>Held.</summary>
        public bool Sprint;
        /// <summary>Pressed this frame.</summary>
        public bool Jump, CrouchToggle, Interact, Flashlight;
    }
}
