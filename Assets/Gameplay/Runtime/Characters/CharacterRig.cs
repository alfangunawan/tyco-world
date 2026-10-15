using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Drives one person's Kampung animator (spec #7): walking speed, sitting, riding, talking, picking up, and holding
    /// a carried item with both hands through humanoid IK. The game moves the person; the animations stay in place.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterRig : MonoBehaviour
    {
        public static readonly int SpeedId = Animator.StringToHash("Speed");
        public static readonly int SitId = Animator.StringToHash("Sit");
        public static readonly int DriveId = Animator.StringToHash("Drive");
        public static readonly int TalkId = Animator.StringToHash("Talk");
        public static readonly int PickUpId = Animator.StringToHash("PickUp");

        Animator animator;
        Transform held;
        Vector3 leftGrip, rightGrip;

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
            if (!TryGetGrips(out var left, out var right))
            {
                a.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                a.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                return;
            }
            a.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
            a.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            a.SetIKPosition(AvatarIKGoal.LeftHand, left);
            a.SetIKPosition(AvatarIKGoal.RightHand, right);
        }
    }
}
