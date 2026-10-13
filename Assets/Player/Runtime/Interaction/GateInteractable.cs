using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Iron gate on a hinge: swings away from whoever opens it; unlatch + creak when opening, creak + latch when closing.</summary>
    public sealed class GateInteractable : Interactable
    {
        [SerializeField] Transform leaf;
        [SerializeField] AudioClip unlatchClip;
        [SerializeField] AudioClip creakClip;
        [SerializeField] AudioClip latchClip;
        [SerializeField] float leafLength = 2f;

        GateSwing swing;

        GateSwing Swing => swing ?? (swing = new GateSwing());

        public GateSwing.State State => Swing.Current;
        public float AngleDeg => Swing.AngleDeg;

        public void Configure(Transform leafPivot, AudioClip unlatch, AudioClip creak, AudioClip latch)
        {
            leaf = leafPivot;
            unlatchClip = unlatch;
            creakClip = creak;
            latchClip = latch;
        }

        public override string GetPrompt(in InteractionContext context) =>
            State == GateSwing.State.Closed || State == GateSwing.State.Closing ? "[E] Buka pagar" : "[E] Tutup pagar";

        public const float SweepClearance = 0.45f;

        /// <summary>Closing moves the leaf back through the yard; refuse while the viewer stands in that arc (a moved static collider would shove or clip the player).</summary>
        public override bool CanInteract(in InteractionContext context)
        {
            if (Swing.IsMoving) return false;
            if (Swing.Current != GateSwing.State.Open || leaf == null) return true;
            var local = transform.InverseTransformPoint(context.ViewOrigin) - leaf.localPosition;
            return !IsInSweep(new Vector2(local.x, local.z), Swing.AngleDeg, 0f, leafLength, SweepClearance);
        }

        /// <summary>
        /// True when a point (hinge space, x/z) is within <paramref name="clearance"/> of the leaf at any angle between the two;
        /// a positive Y angle points the leaf at (cos a, -sin a).
        /// </summary>
        public static bool IsInSweep(Vector2 pointXZ, float fromDeg, float toDeg, float length, float clearance)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) / 5f));
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / steps) * Mathf.Deg2Rad;
                var tip = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * length;
                float t = Mathf.Clamp01(Vector2.Dot(pointXZ, tip) / tip.sqrMagnitude);
                if (Vector2.Distance(pointXZ, tip * t) < clearance) return true;
            }
            return false;
        }

        protected override void OnInteract(in InteractionContext context)
        {
            // Leaf extends along local +x from the hinge; a positive Y angle moves its free end toward -z.
            Vector3 viewer = transform.InverseTransformPoint(context.ViewOrigin);
            Swing.Toggle(viewer.z >= 0f ? 1f : -1f);
        }

        void Update()
        {
            if (Application.isPlaying) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            foreach (var e in Swing.Tick(dt))
            {
                var clip = e == GateEvent.Unlatch ? unlatchClip : e == GateEvent.Creak ? creakClip : latchClip;
                var output = Sound;
                if (output != null) output.Play(clip, 0.9f, 1f);
            }
            if (leaf != null) leaf.localRotation = Quaternion.Euler(0f, Swing.AngleDeg, 0f);
        }
    }
}
