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
        /// <summary>Time for the trip: the straight distance at this speed plus a margin.</summary>
        public const float TripSpeed = 6.5f, TripMargin = 45f;

        GameObject passenger;
        Vector3 pickup, drop;
        string dropName = "";

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

        protected override bool Build(MissionContext c)
        {
            var from = c.LanePoint(c.PlayerPosition, PickupMin, PickupMax);
            if (!from.HasValue) return false;
            pickup = from.Value;
            if (!PickDrop(c)) return false;
            float trip = MissionContext.Flat(pickup, drop);
            Reward = BaseFare + PerMetre * Mathf.RoundToInt(trip) / 500 * 500;
            var look = PersonLook.From((ulong)c.Range(1, 1 << 20));
            passenger = Own(c.Person("Penumpang", pickup, c.FacePlayer(pickup), look));
            Add(new MissionStep
            {
                Objective = "Jemput penumpang di pinggir jalan (naik motor)",
                Target = () => pickup,
                Done = () => c.IsRiding && c.Near(pickup, PickupRadius)
            });
            Add(new MissionStep
            {
                Objective = "Antar penumpang ke " + dropName,
                Target = () => drop,
                Enter = () =>
                {
                    if (passenger != null) passenger.SetActive(false);
                    c.Toast("Penumpang naik: \"Ke " + dropName + " ya, a. Agak buru-buru.\"");
                },
                Done = () => c.IsRiding && c.Near(drop, DropRadius),
                TimeLimit = trip / TripSpeed + TripMargin
            });
            return true;
        }

        bool PickDrop(MissionContext c)
        {
            // Somewhere a student would go: the campus gate, the mosque, a shop; else any lane point far enough.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int kind = c.Range(0, 3);
                if (kind == 0)
                {
                    var gate = c.NearestLandmark(SpotKind.CampusGate, pickup);
                    if (gate != null && Within(gate.position)) return Set(c, gate.position, "gerbang kampus");
                }
                else if (kind == 1)
                {
                    var mosque = c.NearestLandmark(SpotKind.Mosque, pickup);
                    if (mosque != null && Within(mosque.position)) return Set(c, mosque.position, "masjid");
                }
                else if (c.Data.shops.Length > 0)
                {
                    var shop = c.Data.shops[c.Range(0, c.Data.shops.Length)];
                    if (Within(shop.counter)) return Set(c, shop.counter, shop.name);
                }
            }
            var any = c.LanePoint(pickup, TripMin, TripMax);
            return any.HasValue && Set(c, any.Value, "tujuan");
        }

        bool Within(Vector3 p)
        {
            float d = MissionContext.Flat(p, pickup);
            return d >= TripMin && d <= TripMax;
        }

        bool Set(MissionContext c, Vector3 p, string name)
        {
            drop = c.OnGround(p);
            dropName = name;
            return true;
        }

        protected override string TimeoutText => "Penumpang turun di jalan: \"Telat, a. Saya jalan kaki aja.\"";
    }
}
