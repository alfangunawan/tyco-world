using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// An ojek fare getting on and off (spec #7 §4): they wait, walk to the back of the stopped bike, sit down onto the
    /// pillion (Sitting_Enter while sliding onto it), ride along with their hips held on the seat, and at the drop-off
    /// get off to the side (Sitting_Exit). In a car they walk to its side and get in instead.
    /// </summary>
    public sealed class PassengerRide
    {
        public enum Phase
        {
            Waiting,
            Approaching,
            Mounting,
            Riding,
            Dismounting,
            Off
        }

        public const float ReachRadius = 6f, GiveUpRadius = 9f, MaxBoardSpeed = 1f, WalkSpeed = 1.3f;
        public const float MountSeconds = 0.8f, DismountSeconds = 1f;

        readonly MissionContext context;
        readonly GameObject person;
        readonly CharacterRig rig;
        readonly Collider body;
        Motorbike bike;
        RideAnchors anchors;
        Transform anchor;
        bool inCar;
        Vector3 seatLocal, startLocal, startWorld, sidePoint;
        Quaternion startRotation;
        float t;

        public PassengerRide(MissionContext owner, GameObject passenger)
        {
            context = owner;
            person = passenger;
            rig = MissionContext.RigOf(passenger);
            body = passenger.GetComponent<Collider>();
        }

        public Phase State { get; private set; } = Phase.Waiting;
        public bool Boarded => State == Phase.Riding;
        public bool IsOff => State == Phase.Off;
        public Motorbike Bike => bike;
        public GameObject Person => person;

        static bool Slow(Motorbike b) => Mathf.Abs(b.Speed) <= MaxBoardSpeed;

        /// <summary>Beside the back wheel on the left (kerb) side, where people climb on.</summary>
        static Vector3 MountPoint(Motorbike b, bool car)
        {
            var t = b.transform;
            return car ? t.position - t.right * 1.4f : t.position - t.forward * 0.45f - t.right * 0.7f;
        }

        static bool IsCar(Motorbike b) => VehicleFactory.IsCar(b);

        public void Tick(float dt, bool wantOff)
        {
            switch (State)
            {
                case Phase.Waiting: Wait(); break;
                case Phase.Approaching: Approach(dt); break;
                case Phase.Mounting: Mount(dt); break;
                case Phase.Riding: Ride(wantOff); break;
                case Phase.Dismounting: Dismount(dt); break;
            }
        }

        void Wait()
        {
            if (rig != null) rig.Move(0f);
            var ridden = context.RiddenBike;
            var to = context.PlayerPosition - person.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f) person.transform.rotation = Quaternion.Slerp(person.transform.rotation, Quaternion.LookRotation(to), 0.08f);
            if (ridden == null || !Slow(ridden) || MissionContext.Flat(ridden.transform.position, person.transform.position) > ReachRadius) return;
            bike = ridden;
            inCar = IsCar(bike);
            State = Phase.Approaching;
        }

        void Approach(float dt)
        {
            if (context.RiddenBike != bike || MissionContext.Flat(bike.transform.position, person.transform.position) > GiveUpRadius)
            {
                State = Phase.Waiting;
                return;
            }
            var target = MountPoint(bike, inCar);
            target.y = context.Ground(target);
            var d = target - person.transform.position;
            d.y = 0f;
            float distance = d.magnitude;
            if (distance < 0.2f && Slow(bike))
            {
                BeginMount();
                return;
            }
            float step = Mathf.Min(distance, WalkSpeed * dt);
            var dir = distance > 1e-4f ? d / distance : person.transform.forward;
            var next = person.transform.position + dir * step;
            next.y = context.Ground(next);
            person.transform.SetPositionAndRotation(next, Quaternion.Slerp(person.transform.rotation, Quaternion.LookRotation(dir), 0.25f));
            if (rig != null) rig.Move(distance > 0.05f ? WalkSpeed : 0f);
        }

        void BeginMount()
        {
            if (body != null) body.enabled = false;
            if (inCar)
            {
                // Into the angkot: gone from view until the drop-off.
                person.SetActive(false);
                State = Phase.Riding;
                return;
            }
            var visuals = bike.Visuals;
            anchor = visuals != null && visuals.LeanPivot != null ? visuals.LeanPivot : bike.transform;
            anchors = RideAnchors.Of(bike);
            seatLocal = anchor.InverseTransformPoint(anchors.pillion.position);
            person.transform.SetParent(anchor, true);
            startLocal = person.transform.localPosition;
            startRotation = person.transform.localRotation;
            t = 0f;
            if (rig != null)
            {
                rig.Move(0f);
                rig.Sit(true);
            }
            State = Phase.Mounting;
        }

        void Mount(float dt)
        {
            t += dt;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / MountSeconds));
            person.transform.localPosition = Vector3.Lerp(startLocal, SeatRoot(), k);
            person.transform.localRotation = Quaternion.Slerp(startRotation, Quaternion.identity, k);
            if (t < MountSeconds) return;
            // Seated: feet onto the footrests.
            if (rig != null) rig.Ride(anchors, false);
            State = Phase.Riding;
        }

        /// <summary>Where the person's root goes so their hips rest on the pillion (from the current pose).</summary>
        Vector3 SeatRoot()
        {
            var hips = rig != null ? rig.Bone(HumanBodyBones.Hips) : null;
            if (hips == null) return seatLocal + Vector3.down * 0.45f;
            var hipsLocal = anchor.InverseTransformPoint(hips.position) - person.transform.localPosition;
            return seatLocal - hipsLocal;
        }

        void Ride(bool wantOff)
        {
            if (!inCar)
            {
                person.transform.localPosition = SeatRoot();
                person.transform.localRotation = Quaternion.identity;
            }
            if (!wantOff || !Slow(bike)) return;
            sidePoint = MountPoint(bike, inCar);
            sidePoint.y = context.Ground(sidePoint);
            if (inCar)
            {
                person.SetActive(true);
                person.transform.position = sidePoint;
                Finish();
                return;
            }
            person.transform.SetParent(context.Root, true);
            startWorld = person.transform.position;
            t = 0f;
            if (rig != null) rig.StopRiding();
            State = Phase.Dismounting;
        }

        void Dismount(float dt)
        {
            t += dt;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / DismountSeconds));
            person.transform.position = Vector3.Lerp(startWorld, sidePoint, k);
            if (t >= DismountSeconds) Finish();
        }

        void Finish()
        {
            if (body != null) body.enabled = true;
            State = Phase.Off;
        }

        /// <summary>Puts the passenger down at once (the job failed or ended while they were on).</summary>
        public void ForceOff()
        {
            if (State == Phase.Off) return;
            if (person == null) return;
            if (!person.activeSelf) person.SetActive(true);
            if (person.transform.parent != context.Root) person.transform.SetParent(context.Root, true);
            if (bike != null && (State == Phase.Mounting || State == Phase.Riding)) person.transform.position = context.OnGround(MountPoint(bike, inCar));
            person.transform.rotation = Quaternion.Euler(0f, person.transform.eulerAngles.y, 0f);
            if (rig != null) rig.StopRiding();
            Finish();
        }
    }
}
