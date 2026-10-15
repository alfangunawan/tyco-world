using System;
using System.Collections.Generic;
using TycoWorld.Player;
using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Warga vehicles and the consequences of taking them (spec #6 §2): parked bikes and cars appear on their baked spots
    /// near the player; breaking a lock takes a few seconds standing still (moving off gives up; a car also sounds its
    /// alarm); warga who see it
    /// shout "MALING!", the heat rises and the nearest of them give chase. Caught: a fine, the vehicle goes back and you are
    /// walked home. Out of sight the heat cools; a repaint at a bengkel clears it at once.
    /// </summary>
    public sealed class VehicleSystem : IKampungSystem
    {
        public const float BikeRange = 110f, CarRange = 130f, Hysteresis = 25f, ReturnBeyond = 220f;
        public const int MaxBikes = 14, MaxCars = 5;
        public const float BikeHotwireSeconds = 3f, CarHotwireSeconds = 4f;
        public const float WitnessRadius = PedestrianSystem.WitnessRadius, HoodedFactor = 0.7f, AlarmFactor = 2f;
        public const long Fine = 100000;
        public const float CheckEvery = 0.5f;
        public const float RepaintRadius = 15f;
        /// <summary>Stepping further than this from where the break-in started gives it up (so a seen thief can still run).</summary>
        public const float HotwireDrift = 0.6f;
        /// <summary>A taken vehicle holds its parking spot until it is this far from it (no copy spawns inside it).</summary>
        public const float SpotRelease = 6f;
        /// <summary>A spot this close to any active vehicle is occupied and gets no new one.</summary>
        public const float SpotClearance = 2.5f;
        public const string NothingToPaintText = "Tidak ada kendaraan yang perlu dicat di sini.";
        public const string HuntedSleepText = "Warga masih mencarimu. Tidak bisa tidur sekarang.";
        public const string GaveUpToast = "Batal membobol kunci.";
        /// <summary>A riderless vehicle standing still this long stops ticking (its Motorbike component sleeps).</summary>
        public const float SleepAfter = 1.5f;
        public const string AlarmToast = "NGIUNG NGIUNG! Alarm mobil meraung.";
        public const string ShoutToast = "Warga: \"MALING! MALING!\"";

        readonly List<WorldVehicle> vehicles = new List<WorldVehicle>();
        readonly Dictionary<int, WorldVehicle> bySpot = new Dictionary<int, WorldVehicle>();
        readonly List<int> candidates = new List<int>();
        readonly object stealLock = new object();
        KampungDirector director;
        Transform root;
        BikeTuning carTuning;
        PedestrianSystem people;
        float spawnTimer, checkTimer, witnessTimer;
        WorldVehicle hotwiring;
        float hotwireTime;
        Vector3 hotwireFrom;
        bool hotwireLocked, caughtPending, attemptSeen;
        FirstPersonPlayer lockedPlayer;

        public int Order => 20;
        public HeatModel Heat { get; } = new HeatModel();
        public IReadOnlyList<WorldVehicle> Vehicles => vehicles;
        public WorldVehicle Hotwiring => hotwiring;
        public float HotwireProgress => hotwiring != null ? Mathf.Clamp01(hotwireTime / Duration(hotwiring)) : 0f;
        public bool IsCaughtPending => caughtPending;
        public int Caught { get; private set; }

        public void Begin(KampungDirector owner)
        {
            director = owner;
            root = new GameObject("Vehicles").transform;
            root.SetParent(owner.ObjectsRoot, false);
            var prefabBike = owner.BikePrefab != null ? owner.BikePrefab.GetComponent<Motorbike>() : null;
            carTuning = VehicleFactory.CarTuning(prefabBike != null ? prefabBike.Tuning : null);
            owner.Serviced += OnServiced;
            owner.ServiceText = ServiceText;
            owner.RefuseService = RefuseService;
            owner.SleepBlockers.Add(SleepBlocker);
            owner.MarkerSources.Add(Markers);
            spawnTimer = 0f;
        }

        public void End()
        {
            CancelHotwire();
            if (people != null) people.Caught -= OnCaught;
            if (director != null)
            {
                director.Serviced -= OnServiced;
                director.ServiceText = null;
                director.RefuseService = null;
                director.SleepBlockers.Remove(SleepBlocker);
                director.MarkerSources.Remove(Markers);
            }
            if (root != null) DestroyNow(root.gameObject);
            if (carTuning != null) DestroyNow(carTuning);
            vehicles.Clear();
            bySpot.Clear();
        }

        static void DestroyNow(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        PedestrianSystem People
        {
            get
            {
                if (people == null && director != null)
                {
                    people = director.GetSystem<PedestrianSystem>();
                    if (people != null) people.Caught += OnCaught;
                }
                return people;
            }
        }

        Vector3 PlayerPosition => director.Player != null ? director.Player.transform.position : Vector3.zero;

        /// <summary>The vehicle the player is on (any bike or car, theirs or not), or null.</summary>
        public Motorbike Ridden
        {
            get
            {
                var player = director.Player;
                if (player == null || !player.IsSeated || player.CurrentSeat == null) return null;
                return player.CurrentSeat.GetComponentInParent<Motorbike>();
            }
        }

        public WorldVehicle Find(Motorbike body)
        {
            foreach (var v in vehicles)
                if (v.Body == body) return v;
            return null;
        }

        public void Tick(float dt)
        {
            _ = People;
            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                spawnTimer = CheckEvery;
                UpdateSpawns();
            }
            TickHotwire(dt);
            TickSleep(dt);
            checkTimer -= dt;
            if (checkTimer <= 0f)
            {
                checkTimer = CheckEvery;
                TickHeat(CheckEvery);
            }
            if (director.Hud != null) director.Hud.SetStars(Heat.Level);
        }

        // ---------- parked vehicles ----------

        int Count(VehicleKind kind)
        {
            int n = 0;
            foreach (var v in vehicles)
                if (v.Active && v.Kind == kind && v.Spot >= 0) n++;
            return n;
        }

        void UpdateSpawns()
        {
            var viewer = PlayerPosition;
            var spots = director.Data.spots;
            // Far away: parked ones go; a stolen one left far behind is found by its owner.
            foreach (var v in vehicles)
            {
                // A taken vehicle that has left its spot frees it.
                if (v.Active && v.Moved && v.Spot >= 0 && MissionContext.Flat(v.Position, spots[v.Spot].position) > SpotRelease) ReleaseSpot(v);
                if (!v.Active || v.Body.IsMounted || v == hotwiring) continue;
                float d = MissionContext.Flat(v.Position, viewer);
                if (v.Moved ? d > ReturnBeyond : d > RangeOf(v.Kind) + Hysteresis) Despawn(v);
            }
            candidates.Clear();
            for (int i = 0; i < spots.Length; i++)
            {
                var kind = spots[i].kind;
                if (kind != SpotKind.ParkingBike && kind != SpotKind.ParkingCar) continue;
                if (bySpot.ContainsKey(i)) continue;
                if (MissionContext.Flat(spots[i].position, viewer) <= RangeOf(KindOf(kind)) && !Occupied(spots[i].position)) candidates.Add(i);
            }
            candidates.Sort((a, b) => MissionContext.Flat(spots[a].position, viewer).CompareTo(MissionContext.Flat(spots[b].position, viewer)));
            int bikes = Count(VehicleKind.Bike), cars = Count(VehicleKind.Car);
            foreach (int i in candidates)
            {
                var kind = KindOf(spots[i].kind);
                if (kind == VehicleKind.Bike ? bikes >= MaxBikes : cars >= MaxCars) continue;
                Spawn(i, kind);
                if (kind == VehicleKind.Bike) bikes++;
                else cars++;
            }
        }

        /// <summary>
        /// Parked vehicles do not need the controller's per-frame physics: a riderless one that stands still is put to
        /// sleep (component off) and woken just before someone gets on.
        /// </summary>
        void TickSleep(float dt)
        {
            foreach (var v in vehicles)
            {
                if (!v.Active || !v.Body.enabled) continue;
                if (v.Body.IsMounted || v == hotwiring || Mathf.Abs(v.Body.Speed) > 0.05f)
                {
                    v.Idle = 0f;
                    continue;
                }
                v.Idle += dt;
                if (v.Idle >= SleepAfter) v.Body.enabled = false;
            }
        }

        static void Wake(WorldVehicle v)
        {
            v.Idle = 0f;
            v.Body.enabled = true;
        }

        /// <summary>Some active vehicle (a taken one parked back here, say) already stands on the spot.</summary>
        bool Occupied(Vector3 spot)
        {
            foreach (var v in vehicles)
                if (v.Active && MissionContext.Flat(v.Position, spot) < SpotClearance) return true;
            return false;
        }

        void ReleaseSpot(WorldVehicle v)
        {
            if (v.Spot >= 0 && bySpot.TryGetValue(v.Spot, out var holder) && holder == v) bySpot.Remove(v.Spot);
            v.Spot = -1;
        }

        static VehicleKind KindOf(SpotKind kind) => kind == SpotKind.ParkingCar ? VehicleKind.Car : VehicleKind.Bike;
        static float RangeOf(VehicleKind kind) => kind == VehicleKind.Car ? CarRange : BikeRange;

        void Spawn(int spotIndex, VehicleKind kind)
        {
            var spot = director.Data.spots[spotIndex];
            WorldVehicle v = null;
            foreach (var pooled in vehicles)
                if (!pooled.Active && pooled.Kind == kind)
                {
                    v = pooled;
                    break;
                }
            int colour = (int)(SeededRandom.Mix((ulong)spotIndex, 0xC0105UL) % 6UL);
            if (v == null)
            {
                var body = kind == VehicleKind.Bike
                    ? VehicleFactory.Bike(root, director.BikePrefab, director.Palette, VehicleFactory.BikeColour(colour))
                    : VehicleFactory.Car(root, carTuning, director.Palette, VehicleFactory.CarColour(colour));
                v = new WorldVehicle(kind, body, this);
                vehicles.Add(v);
            }
            else
            {
                VehicleFactory.Activate(v.Root);
            }
            v.Spot = spotIndex;
            v.Unlocked = v.Moved = v.Hot = false;
            v.Colour = colour;
            VehicleFactory.Paint(v.Root, director.Palette, kind == VehicleKind.Bike ? VehicleFactory.BikeColour(colour) : VehicleFactory.CarColour(colour));
            var at = spot.position;
            at.y = Ground(at);
            v.Body.Teleport(at, spot.yaw);
            // Parked on the ground found above: no need to fall or tick until someone gets on.
            v.Body.enabled = false;
            v.Idle = 0f;
            bySpot[spotIndex] = v;
        }

        float Ground(Vector3 p)
        {
            float level = director.GroundLevel;
            return Physics.Raycast(new Vector3(p.x, level + 0.5f, p.z), Vector3.down, out var hit, 1.5f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : level;
        }

        void Despawn(WorldVehicle v)
        {
            ReleaseSpot(v);
            v.Unlocked = v.Moved = v.Hot = false;
            v.Root.SetActive(false);
        }

        // ---------- breaking in ----------

        public bool CanUse(WorldVehicle v) =>
            v != null && v.Active && !v.Body.IsMounted && hotwiring == null && !caughtPending && !director.IsFading;

        public void Use(WorldVehicle v)
        {
            if (!CanUse(v) || director.Player == null) return;
            if (v.Unlocked)
            {
                Wake(v);
                v.Body.Mount(director.Player);
                return;
            }
            hotwiring = v;
            hotwireTime = 0f;
            witnessTimer = 0f;
            attemptSeen = false;
            hotwireFrom = PlayerPosition;
            lockedPlayer = director.Player;
            // Hands busy (no E, no torch); the legs are free, and moving off gives up.
            lockedPlayer.Locks.Acquire(stealLock, PlayerAbility.Interact | PlayerAbility.Flashlight);
            hotwireLocked = true;
            if (v.Kind == VehicleKind.Car) director.Toast(AlarmToast);
            Report(v);
        }

        static float Duration(WorldVehicle v) => v.Kind == VehicleKind.Car ? CarHotwireSeconds : BikeHotwireSeconds;

        void TickHotwire(float dt)
        {
            if (hotwiring == null) return;
            if (director.IsFading || !hotwiring.Active)
            {
                CancelHotwire();
                return;
            }
            if (MissionContext.Flat(PlayerPosition, hotwireFrom) > HotwireDrift)
            {
                CancelHotwire();
                director.Toast(GaveUpToast);
                return;
            }
            hotwireTime += dt;
            witnessTimer -= dt;
            if (witnessTimer <= 0f)
            {
                witnessTimer = 1f;
                Report(hotwiring);
            }
            if (director.Hud != null) director.Hud.SetProgress("Membobol kunci " + hotwiring.Noun + "...", HotwireProgress);
            if (hotwireTime < Duration(hotwiring)) return;
            var v = hotwiring;
            // Done, not given up: detach it first so CancelHotwire only frees the hands and the HUD.
            hotwiring = null;
            CancelHotwire();
            v.Unlocked = true;
            v.Moved = true;
            // It keeps its spot until it has left it (UpdateSpawns frees it), so no copy spawns underneath.
            Wake(v);
            v.Body.Mount(director.Player);
            director.Toast(attemptSeen ? "Kunci jebol. Kabur!" : "Kunci jebol. Tidak ada yang lihat... sepertinya.");
        }

        void CancelHotwire()
        {
            // A lock left half-broken: the vehicle stays parked and is no one's stolen vehicle.
            if (hotwiring != null && !hotwiring.Moved) hotwiring.Hot = false;
            hotwiring = null;
            hotwireTime = 0f;
            if (hotwireLocked && lockedPlayer != null) lockedPlayer.Locks.Release(stealLock);
            hotwireLocked = false;
            lockedPlayer = null;
            if (director != null && director.Hud != null) director.Hud.SetProgress(null, -1f);
        }

        float SeeRadius(bool alarm)
        {
            float r = WitnessRadius * (alarm ? AlarmFactor : 1f);
            return director.State.IsHooded ? r * HoodedFactor : r;
        }

        /// <summary>Warga who can see the theft raise the alarm (once per attempt): heat, shouting, a chase.</summary>
        void Report(WorldVehicle v)
        {
            var crowd = People;
            if (crowd == null || attemptSeen) return;
            var witnesses = crowd.Witnesses(v.Position, SeeRadius(v.Kind == VehicleKind.Car), v.Root.transform);
            if (witnesses.Count == 0) return;
            attemptSeen = true;
            v.Hot = true;
            Heat.Crime(v.Kind == VehicleKind.Car ? 2 : 1);
            crowd.Alarm(v.Position, witnesses, Mathf.Max(0, Heat.Level - crowd.ChaserCount));
            director.Toast(ShoutToast);
        }

        // ---------- heat, the chase, getting caught ----------

        void TickHeat(float dt)
        {
            if (!Heat.IsHot) return;
            var crowd = People;
            var ridden = Ridden;
            var witnesses = crowd != null
                ? crowd.Witnesses(PlayerPosition, SeeRadius(false), ridden != null ? ridden.transform : null)
                : new List<Pedestrian>();
            bool seen = witnesses.Count > 0 || (crowd != null && crowd.ChaserCount > 0 && NearestChaser(crowd) < WitnessRadius);
            int before = Heat.Level;
            Heat.Tick(dt, seen);
            if (crowd == null) return;
            if (Heat.Level == 0)
            {
                crowd.CalmDown();
                if (before > 0) director.Toast("Sepertinya warga sudah berhenti mencarimu.");
                return;
            }
            int want = Heat.Level - crowd.ChaserCount;
            if (want > 0 && witnesses.Count > 0) crowd.Alarm(PlayerPosition, witnesses, want);
        }

        float NearestChaser(PedestrianSystem crowd)
        {
            float best = float.PositiveInfinity;
            foreach (var p in crowd.Pool)
                if (p.Active && p.State == PedestrianState.Chase) best = Mathf.Min(best, MissionContext.Flat(p.Position, PlayerPosition));
            return best;
        }

        void OnCaught(Pedestrian by)
        {
            if (caughtPending || director.IsFading) return;
            caughtPending = true;
            CancelHotwire();
            director.Toast("Warga: \"Ketangkep! Bawa ke pos ronda!\"");
            director.FadeThrough(() =>
            {
                long taken = director.State.Wallet.TakeUpTo(Fine);
                ReturnStolen();
                Heat.Clear();
                if (People != null) People.CalmDown();
                director.SendHome();
                Caught++;
                director.Toast("Kamu digiring warga pulang ke kos. Denda " + RupiahFormat.Format(taken) + ".");
                director.SaveNow();
                caughtPending = false;
            });
        }

        /// <summary>Every taken vehicle goes back to its owner (the rider is set down first); the slate is wiped clean.</summary>
        void ReturnStolen()
        {
            foreach (var v in vehicles)
            {
                // Settled at the pos ronda: even a half-broken lock is forgotten.
                v.Hot = false;
                if (!v.Active || !v.Moved) continue;
                var rider = v.Body.Rider;
                if (rider != null) rider.Teleport(v.Position - v.Body.transform.right * v.Body.Tuning.dismountSideOffset, v.Body.Yaw);
                Despawn(v);
            }
        }

        // ---------- bengkel ----------

        /// <summary>The stolen vehicle being serviced: the one ridden, else a taken one parked near the player.</summary>
        public WorldVehicle ServiceTarget
        {
            get
            {
                var ridden = Ridden;
                var v = ridden != null ? Find(ridden) : null;
                if (v != null) return v;
                WorldVehicle best = null;
                float bestDistance = RepaintRadius;
                foreach (var w in vehicles)
                {
                    if (!w.Active || !w.Moved) continue;
                    float d = MissionContext.Flat(w.Position, PlayerPosition);
                    if (d > bestDistance) continue;
                    bestDistance = d;
                    best = w;
                }
                return best;
            }
        }

        string RefuseService(ItemDefinition item) =>
            item != null && item.id == ItemCatalog.CatUlang && ServiceTarget == null && !Heat.IsHot ? NothingToPaintText : null;

        string SleepBlocker() => Heat.IsHot || (People != null && People.ChaserCount > 0) ? HuntedSleepText : null;

        string ServiceText()
        {
            var v = ServiceTarget;
            if (v != null) return "Beres. " + (v.Kind == VehicleKind.Bike ? "Motor" : "Mobil") + "nya dicat ulang. Warga tidak akan mengenalinya.";
            return Heat.IsHot ? "Beres. Motormu dicat ulang; warga jadi ragu itu kamu." : "Beres. Motormu dicat ulang, kinclong.";
        }

        void OnServiced(ItemDefinition item)
        {
            if (item == null || item.id != ItemCatalog.CatUlang) return;
            var v = ServiceTarget;
            if (v != null)
            {
                v.Colour = (v.Colour + 1 + (int)(SeededRandom.Mix((ulong)v.Colour, 7UL) % 4UL)) % 6;
                VehicleFactory.Paint(v.Root, director.Palette, v.Kind == VehicleKind.Bike ? VehicleFactory.BikeColour(v.Colour) : VehicleFactory.CarColour(v.Colour));
                v.Hot = false;
            }
            Heat.Clear();
            if (People != null) People.CalmDown();
        }

        IEnumerable<MapMarker> Markers()
        {
            foreach (var v in vehicles)
                if (v.Active && v.Moved && !v.Body.IsMounted)
                    yield return new MapMarker { Position = new Vector2(v.Position.x, v.Position.z), Color = MapColors.Stolen, Size = 6f, Pin = true };
            // Warga on your heels while the heat is on.
            var crowd = People;
            if (crowd != null && Heat.IsHot)
                foreach (var p in crowd.Pool)
                    if (p.Active && p.State == PedestrianState.Chase)
                        yield return new MapMarker { Position = new Vector2(p.Position.x, p.Position.z), Color = MapColors.Stolen, Size = 3f };
        }
    }
}
