using System.Collections.Generic;

namespace TycoWorld.Vehicles
{
    /// <summary>Replays held inputs and one-shot presses for tests and MCP verification (same shape as the player's scripted source).</summary>
    public sealed class ScriptedVehicleIntentSource : IVehicleIntentSource
    {
        struct Segment
        {
            public VehicleIntent Intent;
            public float Remaining;
        }

        readonly List<Segment> segments = new List<Segment>();
        VehicleIntent edges;
        bool hasEdges;

        public bool IsDone => segments.Count == 0 && !hasEdges;

        /// <summary>Holds Throttle, Steer and Brake for <paramref name="seconds"/>; LookDegrees is read as degrees per second. Edge flags are ignored.</summary>
        public void Hold(VehicleIntent held, float seconds)
        {
            if (seconds > 0f) segments.Add(new Segment { Intent = held, Remaining = seconds });
        }

        /// <summary>Fires Horn, ToggleHeadlight, ToggleCamera, Exit and an instant look delta on the next Read.</summary>
        public void Press(VehicleIntent pressed)
        {
            edges.Horn |= pressed.Horn;
            edges.ToggleHeadlight |= pressed.ToggleHeadlight;
            edges.ToggleCamera |= pressed.ToggleCamera;
            edges.Exit |= pressed.Exit;
            edges.LookDegrees += pressed.LookDegrees;
            hasEdges = true;
        }

        public void Clear()
        {
            segments.Clear();
            edges = default;
            hasEdges = false;
        }

        public VehicleIntent Read(float dt)
        {
            var result = new VehicleIntent();
            if (segments.Count > 0)
            {
                var segment = segments[0];
                result.Throttle = segment.Intent.Throttle;
                result.Steer = segment.Intent.Steer;
                result.Brake = segment.Intent.Brake;
                result.LookDegrees = segment.Intent.LookDegrees * dt;
                segment.Remaining -= dt;
                if (segment.Remaining <= 0f) segments.RemoveAt(0);
                else segments[0] = segment;
            }
            if (hasEdges)
            {
                result.Horn = edges.Horn;
                result.ToggleHeadlight = edges.ToggleHeadlight;
                result.ToggleCamera = edges.ToggleCamera;
                result.Exit = edges.Exit;
                result.LookDegrees += edges.LookDegrees;
                edges = default;
                hasEdges = false;
            }
            return result;
        }
    }
}
