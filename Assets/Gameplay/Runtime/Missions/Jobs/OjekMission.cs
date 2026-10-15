using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>Ojek Kampus (spec #6 §5): pick up a fare by the road on your motor and drop them off before time runs out.</summary>
    public sealed class OjekMission : Mission
    {
        public const long BaseFare = 8000;
        public const long PerMetre = 6;
        public const float PickupMin = 60f, PickupMax = 220f, TripMin = 180f, TripMax = 650f;
        public const float PickupRadius = 4.5f, DropRadius = 7f;
        /// <summary>
        /// Time for the trip: the road route at this average (18 km/h, through turns and gang) plus a margin to stop, let
        /// the fare on and find the way.
        /// </summary>
        public const float TripSpeed = 5f, TripMargin = 60f;
        /// <summary>A new drop-off is at least this far from each of the last <see cref="RecentDrops"/>.</summary>
        public const float RepeatRadius = 25f;
        public const int RecentDrops = 3;

        GameObject passenger;
        PassengerRide ride;
        Vector3 pickup, drop;
        string dropName = "";
        readonly List<Vector3> recent = new List<Vector3>();

        public override string Id => "ojek";
        public override string Title => "Ojek Kampus";
        public override string Detail => "Jemput mahasiswa di pinggir jalan dan antar ke tujuannya. Harus naik motor.";
        public override string RewardText => "Rp 8.000 + jarak";
        public override float StartHour => 6f;
        public override float EndHour => 21f;
        public string DropName => dropName;
        public Vector3 Pickup => pickup;
        public Vector3 Drop => drop;
        public GameObject Passenger => passenger;
        /// <summary>Metres along the roads from the pickup to the drop-off.</summary>
        public float RouteLength { get; private set; }
        /// <summary>Seconds allowed for the ride.</summary>
        public float TripSeconds { get; private set; }
        public PassengerRide Ride => ride;

        protected override bool Build(MissionContext c)
        {
            var from = c.LanePoint(c.PlayerPosition, PickupMin, PickupMax);
            if (!from.HasValue) return false;
            pickup = from.Value;
            if (!PickDrop(c)) return false;
            recent.Add(drop);
            if (recent.Count > RecentDrops) recent.RemoveAt(0);
            RouteLength = c.RouteLength(pickup, drop);
            TripSeconds = RouteLength / TripSpeed + TripMargin;
            Reward = BaseFare + PerMetre * Mathf.RoundToInt(RouteLength) / 500 * 500;
            var look = PersonLook.From((ulong)c.Range(1, 1 << 20));
            passenger = Own(c.Person("Penumpang", pickup, c.FacePlayer(pickup), look));
            ride = new PassengerRide(c, passenger);
            Add(new MissionStep
            {
                Objective = "Jemput penumpang (naik motor, berhenti di dekatnya)",
                Target = () => pickup,
                Done = () => ride.Boarded
            });
            Add(new MissionStep
            {
                Objective = "Antar penumpang ke " + dropName,
                Target = () => drop,
                Enter = () => c.Toast("Penumpang naik: \"Ke " + dropName + " ya, a. Agak buru-buru.\""),
                Done = () => ride.IsOff,
                TimeLimit = TripSeconds
            });
            return true;
        }

        /// <summary>
        /// Somewhere people go: any campus gate, mosque, shop, kos, pos ronda or house in range, the kind picked first
        /// (so houses, the most numerous, do not crowd out the rest), never near the last few drop-offs; else any lane
        /// point far enough.
        /// </summary>
        bool PickDrop(MissionContext c)
        {
            var kinds = new List<List<(Vector3 at, string name)>>();
            void Kind(IEnumerable<(Vector3 at, string name)> places)
            {
                var ok = new List<(Vector3 at, string name)>();
                foreach (var p in places)
                    if (Within(p.at) && Fresh(p.at)) ok.Add(p);
                if (ok.Count > 0) kinds.Add(ok);
            }
            Kind(Spots(c, SpotKind.CampusGate, "gerbang kampus"));
            Kind(Spots(c, SpotKind.Mosque, "masjid"));
            Kind(Spots(c, SpotKind.Kos, "kos-kosan"));
            Kind(Spots(c, SpotKind.PosRonda, "pos ronda"));
            Kind(Spots(c, SpotKind.Door, "rumah warga"));
            var shops = new List<(Vector3 at, string name)>();
            foreach (var shop in c.Data.shops) shops.Add((shop.counter, shop.name));
            Kind(shops);
            if (kinds.Count > 0)
            {
                var pick = kinds[c.Range(0, kinds.Count)];
                var place = pick[c.Range(0, pick.Count)];
                return Set(c, place.at, place.name);
            }
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var any = c.LanePoint(pickup, TripMin, TripMax);
                if (any.HasValue && Fresh(any.Value)) return Set(c, any.Value, "tujuan");
            }
            return false;
        }

        static IEnumerable<(Vector3 at, string name)> Spots(MissionContext c, SpotKind kind, string name)
        {
            foreach (var s in c.Landmarks(kind)) yield return (s.position, name);
        }

        bool Within(Vector3 p)
        {
            float d = MissionContext.Flat(p, pickup);
            return d >= TripMin && d <= TripMax;
        }

        bool Fresh(Vector3 p)
        {
            foreach (var r in recent)
                if (MissionContext.Flat(p, r) <= RepeatRadius) return false;
            return true;
        }

        bool Set(MissionContext c, Vector3 p, string name)
        {
            drop = c.OnGround(p);
            dropName = name;
            return true;
        }

        protected override void OnTick(float dt)
        {
            if (ride == null) return;
            // Off at the drop-off once the bike stops there.
            bool atDrop = StepIndex == 1 && Context.IsRiding && Context.Near(drop, DropRadius);
            ride.Tick(dt, atDrop);
        }

        protected override void OnSucceeded() => WalkOff();

        protected override void OnFailed()
        {
            if (ride != null) ride.ForceOff();
            WalkOff();
        }

        /// <summary>The fare walks away from the road instead of vanishing.</summary>
        void WalkOff()
        {
            if (passenger == null) return;
            Release(passenger);
            var away = passenger.transform.position - Context.PlayerPosition;
            Context.Leave(passenger, away.sqrMagnitude > 0.01f ? away : passenger.transform.forward, 6f);
        }

        protected override string TimeoutText => "Penumpang turun di jalan: \"Telat, a. Saya jalan kaki aja.\"";
    }
}
