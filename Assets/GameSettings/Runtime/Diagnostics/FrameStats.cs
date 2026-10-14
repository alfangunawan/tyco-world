using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Frame-time statistics over a fixed window of real time (FPS counter, performance probe).</summary>
    public sealed class FrameStats
    {
        readonly float window;
        float elapsed, worst;
        int frames;

        public FrameStats(float windowSeconds) => window = Mathf.Max(0.01f, windowSeconds);

        public float WindowSeconds => window;
        public bool HasResult { get; private set; }
        public float AverageFps { get; private set; }
        public float AverageMs { get; private set; }
        public float WorstMs { get; private set; }

        /// <summary>Adds one frame; returns true when a window just completed and the averages were refreshed.</summary>
        public bool Sample(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f) return false;
            elapsed += unscaledDeltaTime;
            frames++;
            if (unscaledDeltaTime > worst) worst = unscaledDeltaTime;
            if (elapsed < window) return false;
            AverageFps = frames / elapsed;
            AverageMs = elapsed * 1000f / frames;
            WorstMs = worst * 1000f;
            HasResult = true;
            elapsed = 0f;
            worst = 0f;
            frames = 0;
            return true;
        }

        public void Clear()
        {
            elapsed = 0f;
            worst = 0f;
            frames = 0;
            HasResult = false;
            AverageFps = 0f;
            AverageMs = 0f;
            WorstMs = 0f;
        }
    }
}
