using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>Freezes Time.timeScale and pauses every AudioSource; resuming restores the previous time scale.</summary>
    public sealed class UnityPauseEffects : IPauseEffects
    {
        float previousScale = 1f;

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                previousScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            else
            {
                Time.timeScale = previousScale;
                AudioListener.pause = false;
            }
        }
    }
}
