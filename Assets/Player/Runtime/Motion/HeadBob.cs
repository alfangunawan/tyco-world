using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Camera bob: dips at each foot contact (phase 0 and 0.5), sways sideways once per two steps.</summary>
    public static class HeadBob
    {
        public static Vector3 Offset(float cyclePhase01, float amplitude, float lateralRatio)
        {
            float s = Mathf.Sin(2f * Mathf.PI * cyclePhase01);
            return new Vector3(amplitude * lateralRatio * s, amplitude * Mathf.Abs(s), 0f);
        }
    }
}
