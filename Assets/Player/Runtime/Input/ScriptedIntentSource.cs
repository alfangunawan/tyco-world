using System.Collections.Generic;

namespace TycoWorld.Player
{
    /// <summary>Intent source driven by code: tests, MCP verification, and later cutscenes.</summary>
    public sealed class ScriptedIntentSource : IPlayerIntentSource
    {
        struct Segment
        {
            public PlayerIntent Intent;
            public float Remaining;
        }

        readonly List<Segment> segments = new List<Segment>();
        PlayerIntent edges;
        bool hasEdges;

        public bool IsDone => segments.Count == 0 && !hasEdges;

        /// <summary>Holds Move and Sprint for <paramref name="seconds"/>; LookDegrees is read as degrees per second. Edge flags are ignored.</summary>
        public void Hold(PlayerIntent held, float seconds)
        {
            if (seconds > 0f) segments.Add(new Segment { Intent = held, Remaining = seconds });
        }

        /// <summary>Fires Jump/CrouchToggle/Interact/Flashlight and an instant look delta on the next Read.</summary>
        public void Press(PlayerIntent pressed)
        {
            edges.Jump |= pressed.Jump;
            edges.CrouchToggle |= pressed.CrouchToggle;
            edges.Interact |= pressed.Interact;
            edges.Flashlight |= pressed.Flashlight;
            edges.LookDegrees += pressed.LookDegrees;
            hasEdges = true;
        }

        public void Clear()
        {
            segments.Clear();
            edges = default;
            hasEdges = false;
        }

        public PlayerIntent Read(float dt)
        {
            var result = new PlayerIntent();
            if (segments.Count > 0)
            {
                var segment = segments[0];
                result.Move = segment.Intent.Move;
                result.Sprint = segment.Intent.Sprint;
                result.LookDegrees = segment.Intent.LookDegrees * dt;
                segment.Remaining -= dt;
                if (segment.Remaining <= 0f) segments.RemoveAt(0);
                else segments[0] = segment;
            }
            if (hasEdges)
            {
                result.Jump = edges.Jump;
                result.CrouchToggle = edges.CrouchToggle;
                result.Interact = edges.Interact;
                result.Flashlight = edges.Flashlight;
                result.LookDegrees += edges.LookDegrees;
                edges = default;
                hasEdges = false;
            }
            return result;
        }
    }
}
