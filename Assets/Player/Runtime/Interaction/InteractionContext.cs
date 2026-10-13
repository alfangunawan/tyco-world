using UnityEngine;

namespace TycoWorld.Player
{
    public readonly struct InteractionContext
    {
        public readonly FirstPersonPlayer Player;
        public readonly IPlayerFeedback Feedback;
        public readonly Vector3 ViewOrigin;

        public InteractionContext(FirstPersonPlayer player, IPlayerFeedback feedback, Vector3 viewOrigin)
        {
            Player = player;
            Feedback = feedback;
            ViewOrigin = viewOrigin;
        }
    }
}
