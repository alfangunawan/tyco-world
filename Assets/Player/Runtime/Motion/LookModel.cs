using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Yaw wraps to [0, 360); pitch (positive = up) is clamped.</summary>
    public static class LookModel
    {
        public static Vector2 Apply(Vector2 yawPitch, Vector2 deltaDegrees, float pitchLimit) =>
            new Vector2(Mathf.Repeat(yawPitch.x + deltaDegrees.x, 360f),
                Mathf.Clamp(yawPitch.y + deltaDegrees.y, -pitchLimit, pitchLimit));

        public static Quaternion PitchRotation(float pitch) => Quaternion.Euler(-pitch, 0f, 0f);
    }
}
