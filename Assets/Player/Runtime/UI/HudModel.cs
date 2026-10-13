using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Everything the HUD shows, without any UI code (rendered by HudView).</summary>
    public sealed class HudModel : IPlayerFeedback
    {
        readonly HudTuning t;
        float messageTimeLeft;
        float sinceFull;

        public HudModel(HudTuning tuning)
        {
            t = tuning;
            sinceFull = tuning.staminaHideDelay;
        }

        public string Prompt { get; private set; } = "";
        public string Message { get; private set; } = "";
        public float StaminaFill { get; private set; } = 1f;
        public bool StaminaVisible { get; private set; }
        public bool CrosshairVisible { get; set; } = true;
        public string Clock { get; private set; } = "";
        public bool ClockVisible { get; private set; }

        public void SetPrompt(string prompt) => Prompt = prompt ?? "";

        public void SetClock(string text, bool visible)
        {
            Clock = text ?? "";
            ClockVisible = visible;
        }

        public void ShowMessage(string text, float seconds = -1f)
        {
            Message = text ?? "";
            messageTimeLeft = seconds > 0f ? seconds : t.messageSeconds;
        }

        public void Tick(float dt, float stamina01)
        {
            if (messageTimeLeft > 0f)
            {
                messageTimeLeft -= dt;
                if (messageTimeLeft <= 0f) Message = "";
            }
            StaminaFill = Mathf.Clamp01(stamina01);
            if (StaminaFill < 0.999f)
            {
                sinceFull = 0f;
                StaminaVisible = true;
            }
            else
            {
                sinceFull += dt;
                StaminaVisible = sinceFull < t.staminaHideDelay;
            }
        }
    }
}
