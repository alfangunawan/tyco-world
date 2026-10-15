using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// The player's own body on a bike (spec #7 §4): in the third-person ride camera the player is seen riding (sitting,
    /// hips on the front of the seat, hands on the grips and feet on the footboard; <see cref="RideAnchors"/>) instead
    /// of the bike's built-in primitive rider; on foot or in first person the body is hidden (the camera is the player's
    /// eyes). Cars hide their driver behind the hull.
    /// </summary>
    public sealed class RiderBodySystem : IKampungSystem
    {
        public const ulong PlayerSeed = 0x7A1C0UL;

        KampungDirector director;
        CharacterRig body;
        Motorbike riding;
        RideAnchors anchors;
        Transform anchor;
        Vector3 seatLocal;

        public int Order => 40;
        public CharacterRig Body => body;
        public bool Visible => body != null && body.Root.gameObject.activeSelf;

        public void Begin(KampungDirector owner)
        {
            director = owner;
            body = CharacterFactory.Spawn(owner.Characters, owner.ObjectsRoot, "PlayerBody", PlayerSeed, owner.Palette);
            if (body != null) body.Root.gameObject.SetActive(false);
        }

        public void End()
        {
            if (body != null)
            {
                if (Application.isPlaying) Object.Destroy(body.Root.gameObject);
                else Object.DestroyImmediate(body.Root.gameObject);
            }
            body = null;
        }

        Motorbike Ridden
        {
            get
            {
                var player = director.Player;
                if (player == null || !player.IsSeated || player.CurrentSeat == null) return null;
                return player.CurrentSeat.GetComponentInParent<Motorbike>();
            }
        }

        public void Tick(float dt)
        {
            if (body == null) return;
            var bike = Ridden;
            bool show = bike != null && bike.transform.Find("Hull") == null && bike.CameraState != null && bike.CameraState.Mode == RideCameraMode.ThirdPerson;
            if (!show)
            {
                if (body.Root.gameObject.activeSelf)
                {
                    body.StopRiding();
                    body.Root.gameObject.SetActive(false);
                }
                riding = null;
                return;
            }
            if (riding != bike)
            {
                riding = bike;
                var visuals = bike.Visuals;
                anchor = visuals != null && visuals.LeanPivot != null ? visuals.LeanPivot : bike.transform;
                anchors = RideAnchors.Of(bike);
                body.Root.SetParent(anchor, false);
                seatLocal = anchor.InverseTransformPoint(anchors.seat.position);
                // The bike's own primitive rider stays switched off while the real body rides.
                if (visuals != null && visuals.Rider != null)
                    foreach (var r in visuals.Rider.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
            if (!body.Root.gameObject.activeSelf) body.Root.gameObject.SetActive(true);
            if (body.Riding != anchors || !body.IsRider) body.Ride(anchors, true);
            body.Root.localRotation = Quaternion.identity;
            var hips = body.Bone(HumanBodyBones.Hips);
            if (hips != null)
            {
                var hipsLocal = anchor.InverseTransformPoint(hips.position) - body.Root.localPosition;
                body.Root.localPosition = seatLocal - hipsLocal;
            }
            else body.Root.localPosition = seatLocal + Vector3.down * 0.45f;
        }
    }
}
