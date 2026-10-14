using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Collects one FrameStats window after a warm-up (performance probe over MCP).</summary>
    public sealed class FrameStatsSampler : MonoBehaviour
    {
        FrameStats stats;
        float warmup;

        public bool IsDone => stats != null && stats.HasResult;
        public FrameStats Stats => stats;

        public void Begin(float seconds, float warmupSeconds = 1f)
        {
            stats = new FrameStats(seconds);
            warmup = warmupSeconds;
        }

        void Update()
        {
            if (stats == null || stats.HasResult) return;
            float dt = Time.unscaledDeltaTime;
            if (warmup > 0f)
            {
                warmup -= dt;
                return;
            }
            stats.Sample(dt);
        }
    }
}
