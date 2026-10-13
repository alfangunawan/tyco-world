using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Player
{
    public enum GateEvent { Unlatch, Creak, Latch }

    /// <summary>Hinged gate state machine. Angle is around the hinge's Y axis; sign picks the swing direction.</summary>
    public sealed class GateSwing
    {
        public enum State { Closed, Opening, Open, Closing }

        readonly float openAngle;
        readonly float speed;
        readonly List<GateEvent> pending = new List<GateEvent>();
        readonly List<GateEvent> emitted = new List<GateEvent>();
        float sign = 1f;

        public GateSwing(float openAngleDeg = 100f, float speedDegPerSec = 120f)
        {
            openAngle = openAngleDeg;
            speed = speedDegPerSec;
        }

        public State Current { get; private set; } = State.Closed;
        public float AngleDeg { get; private set; }
        public bool IsMoving => Current == State.Opening || Current == State.Closing;

        public bool Toggle(float swingSign)
        {
            if (IsMoving) return false;
            if (Current == State.Closed)
            {
                sign = swingSign >= 0f ? 1f : -1f;
                Current = State.Opening;
                pending.Add(GateEvent.Unlatch);
                pending.Add(GateEvent.Creak);
            }
            else
            {
                Current = State.Closing;
                pending.Add(GateEvent.Creak);
            }
            return true;
        }

        public IReadOnlyList<GateEvent> Tick(float dt)
        {
            emitted.Clear();
            emitted.AddRange(pending);
            pending.Clear();
            if (Current == State.Opening)
            {
                AngleDeg = Mathf.MoveTowards(AngleDeg, openAngle * sign, speed * dt);
                if (Mathf.Approximately(AngleDeg, openAngle * sign)) Current = State.Open;
            }
            else if (Current == State.Closing)
            {
                AngleDeg = Mathf.MoveTowards(AngleDeg, 0f, speed * dt);
                if (Mathf.Approximately(AngleDeg, 0f))
                {
                    AngleDeg = 0f;
                    Current = State.Closed;
                    emitted.Add(GateEvent.Latch);
                }
            }
            return emitted;
        }
    }
}
