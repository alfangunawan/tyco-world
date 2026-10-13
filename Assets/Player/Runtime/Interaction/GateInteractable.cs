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

        public override bool CanInteract(in InteractionContext context) => !Swing.IsMoving;

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
