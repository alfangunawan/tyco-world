using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Drives one person's Kampung animator (spec #7): walking speed, sitting, riding, talking, picking up, and holding
    /// a carried item with both hands through humanoid IK. On a bike the person sits with their feet on the footboard
    /// (or the passenger's footrests) and a rider leans in with their hands on the grips, all by IK onto the bike's
    /// <see cref="RideAnchors"/>. The game moves the person; the animations stay in place. Runs in edit mode too, so
    /// tests that update the animator get the IK.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterRig : MonoBehaviour
    {
        public static readonly int SpeedId = Animator.StringToHash("Speed");
        public static readonly int SitId = Animator.StringToHash("Sit");
        public static readonly int DriveId = Animator.StringToHash("Drive");
        public static readonly int TalkId = Animator.StringToHash("Talk");
        public static readonly int PickUpId = Animator.StringToHash("PickUp");

        /// <summary>How far a rider leans toward the handlebar (degrees).</summary>
        public const float RiderLean = 28f;

        Animator animator;
        Transform held;
        Vector3 leftGrip, rightGrip;
        RideAnchors riding;
        bool rider;

        public Animator Animator
        {
            get
            {
                if (animator == null) animator = GetComponent<Animator>();
                return animator;
            }
        }

        public Transform Held => held;
        /// <summary>The holder to place and turn (the model under it is turned to face its +Z).</summary>
        public Transform Root => transform.parent != null ? transform.parent : transform;
        public bool IsSitting => Animator.GetBool(SitId);
        public bool IsDriving => Animator.GetBool(DriveId);
        public RideAnchors Riding => riding;
        /// <summary>How many times the animator's IK pass ran for this person.</summary>
        public int IkPasses { get; private set; }
        public bool IsRider => riding != null && rider;

        public Transform Bone(HumanBodyBones bone) => Animator.isHuman ? Animator.GetBoneTransform(bone) : null;

        /// <summary>Walking speed in m/s (0 stands, about 1.3 walks, 2.6 jogs, 5 sprints).</summary>
        public void Move(float metresPerSecond) => Animator.SetFloat(SpeedId, Mathf.Max(0f, metresPerSecond));

        public void Sit(bool on) => Animator.SetBool(SitId, on);
        public void Drive(bool on) => Animator.SetBool(DriveId, on);
        public void Talk(bool on) => Animator.SetBool(TalkId, on);
        public void PickUp() => Animator.SetTrigger(PickUpId);

        /// <summary>Holds <paramref name="item"/> with both hands at its local grip points (IK each frame).</summary>
        public void Hold(Transform item, Vector3 leftLocal, Vector3 rightLocal)
        {
            held = item;
            leftGrip = leftLocal;
            rightGrip = rightLocal;
        }

        public void Release() => held = null;

        /// <summary>
        /// Sits on a bike: feet on the footboard (a rider, who also leans in and takes the grips) or on the passenger's
        /// footrests. A rider drops straight into the sitting pose; a passenger sits down.
        /// </summary>
        public void Ride(RideAnchors anchors, bool asRider)
        {
            riding = anchors;
            rider = asRider;
            Animator.SetBool(DriveId, false);
            Sit(anchors != null);
            if (anchors != null && asRider && Animator.isActiveAndEnabled && Animator.runtimeAnimatorController != null) Animator.Play("SitIdle", 0, 0f);
        }

        public void StopRiding()
        {
            riding = null;
            Sit(false);
        }

        /// <summary>Where each hand goes while holding (tests read it).</summary>
        public bool TryGetGrips(out Vector3 left, out Vector3 right)
        {
            left = right = default;
            if (held == null) return false;
            left = held.TransformPoint(leftGrip);
            right = held.TransformPoint(rightGrip);
            return true;
        }

        void OnAnimatorIK(int layerIndex)
        {
            var a = Animator;
            IkPasses++;
            if (riding != null)
            {
                RideIK(a);
                return;
            }
            Goal(a, AvatarIKGoal.LeftFoot, null);
            Goal(a, AvatarIKGoal.RightFoot, null);
            if (!TryGetGrips(out var left, out var right))
            {
                a.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                a.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                a.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                a.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                return;
            }
            a.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
            a.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            a.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            a.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            a.SetIKPosition(AvatarIKGoal.LeftHand, left);
            a.SetIKPosition(AvatarIKGoal.RightHand, right);
        }

        void RideIK(Animator a)
        {
            var bike = riding.transform;
            if (rider) a.bodyRotation = Quaternion.AngleAxis(RiderLean, bike.right) * a.bodyRotation;
            var feetUp = Quaternion.LookRotation(bike.forward, bike.up);
            Goal(a, AvatarIKGoal.LeftFoot, rider ? riding.footLeft : riding.pillionFootLeft, feetUp);
            Goal(a, AvatarIKGoal.RightFoot, rider ? riding.footRight : riding.pillionFootRight, feetUp);
            // Knees forward and a little out, so the legs fold over the footboard instead of through the body panels.
            Hint(a, AvatarIKHint.LeftKnee, rider ? riding.footLeft : riding.pillionFootLeft, bike, -1f, 0.35f, 0.3f);
            Hint(a, AvatarIKHint.RightKnee, rider ? riding.footRight : riding.pillionFootRight, bike, 1f, 0.35f, 0.3f);
            if (rider)
            {
                var hands = Quaternion.LookRotation(bike.forward, bike.up);
                Goal(a, AvatarIKGoal.LeftHand, riding.gripLeft, hands);
                Goal(a, AvatarIKGoal.RightHand, riding.gripRight, hands);
                Hint(a, AvatarIKHint.LeftElbow, riding.gripLeft, bike, -1f, -0.3f, -0.15f);
                Hint(a, AvatarIKHint.RightElbow, riding.gripRight, bike, 1f, -0.3f, -0.15f);
            }
            else
            {
                Goal(a, AvatarIKGoal.LeftHand, null);
                Goal(a, AvatarIKGoal.RightHand, null);
            }
        }

        static void Goal(Animator a, AvatarIKGoal goal, Transform target, Quaternion rotation = default)
        {
            float w = target != null ? 1f : 0f;
            a.SetIKPositionWeight(goal, w);
            a.SetIKRotationWeight(goal, w);
            if (target == null) return;
            a.SetIKPosition(goal, target.position);
            a.SetIKRotation(goal, rotation);
        }

        /// <summary>A knee or elbow hint: from the hand/foot target, <paramref name="forward"/> metres ahead, up and out.</summary>
        static void Hint(Animator a, AvatarIKHint hint, Transform from, Transform bike, float side, float forward, float up)
        {
            if (from == null)
            {
                a.SetIKHintPositionWeight(hint, 0f);
                return;
            }
            a.SetIKHintPositionWeight(hint, 1f);
            a.SetIKHintPosition(hint, from.position + bike.forward * forward + bike.up * up + bike.right * (0.15f * side));
        }
    }
}
