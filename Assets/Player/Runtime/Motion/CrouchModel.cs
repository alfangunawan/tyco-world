using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Toggle crouch; standing up needs headroom (checked by the caller). Body and eye height move together.</summary>
    public sealed class CrouchModel
    {
        readonly CrouchTuning t;
        readonly float standingHeight;

        public CrouchModel(CrouchTuning tuning, float standingHeight)
        {
            t = tuning;
            this.standingHeight = standingHeight;
            Height = standingHeight;
            EyeHeight = t.standingEyeHeight;
        }

        public bool Crouched { get; private set; }
        public float Height { get; private set; }
        public float EyeHeight { get; private set; }
        public float TargetHeight => Crouched ? t.crouchHeight : standingHeight;

        public void RequestToggle(bool hasHeadroomToStand)
        {
            if (!Crouched) Crouched = true;
            else if (hasHeadroomToStand) Crouched = false;
        }

        public void Tick(float dt)
        {
            Height = Mathf.MoveTowards(Height, TargetHeight, t.transitionSpeed * dt);
            float k = Mathf.InverseLerp(t.crouchHeight, standingHeight, Height);
            EyeHeight = Mathf.Lerp(t.crouchEyeHeight, t.standingEyeHeight, k);
        }
    }
}
