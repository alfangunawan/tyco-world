using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Side jobs (spec #6 §5): the phone's Kerjaan list, one running job at a time, its objective, beacon, compass and
    /// map marker, and Pak RT at the first pos ronda who asks for help with the night watch. A job ends when it
    /// succeeds (paid), runs out of time, is cancelled from the phone, or the screen fades (sleep, fainting, caught).
    /// </summary>
    public sealed class MissionSystem : IKampungSystem
    {
        /// <summary>Pak RT keeps the hours of the Ronda Malam job he offers.</summary>
        public const float PakRtFrom = 21f, PakRtUntil = 2f;
        public const string PakRtPrompt = "[E] Ngobrol dengan Pak RT";
        public const string PakRtOutToast = "Pak RT jaga di pos ronda dekat kos (lentera, titik hijau di Peta). Ada kerjaan ronda.";

        readonly List<Mission> missions = new List<Mission>();
        readonly List<JobListing> listings = new List<JobListing>();
        KampungDirector director;
        Transform root;
        MissionContext context;
        GameObject beacon;
        GameObject pakRt, pakRtPost;
        bool wasPakRtHours;
        float time, routeTimer;
        int started;
        List<Vector2> route;
        Vector2 routeFrom, routeTo;

        public int Order => 50;
        public IReadOnlyList<Mission> Missions => missions;
        public Mission Active { get; private set; }
        public MissionContext Context => context;
        public GameObject Beacon => beacon;
        public GameObject PakRt => pakRt;
        /// <summary>The way along the roads to the current target, refreshed about once a second; null without a target.</summary>
        public IReadOnlyList<Vector2> Route => route;
        /// <summary>The lantern and kentongan at his post, shown with him.</summary>
        public GameObject PakRtPost => pakRtPost;
        /// <summary>Raised when a job ends either way (tests, sounds later).</summary>
        public event Action<Mission> Ended;

        public MissionSystem(IEnumerable<Mission> jobs = null)
        {
            missions.AddRange(jobs ?? DefaultJobs());
        }

        public static IEnumerable<Mission> DefaultJobs() => new Mission[]
        {
            new OjekMission(), new GalonMission(), new TitipanMission(), new KucingMission(), new RondaMission(), new KuburanMission()
        };

        public T Get<T>() where T : Mission
        {
            foreach (var m in missions)
                if (m is T t) return t;
            return null;
        }

        public Mission Find(string id)
        {
            foreach (var m in missions)
                if (m.Id == id) return m;
            return null;
        }

        public void Begin(KampungDirector owner)
        {
            director = owner;
            root = new GameObject("Missions").transform;
            root.SetParent(owner.ObjectsRoot, false);
            context = new MissionContext(owner, root, Seed(0));
            beacon = MissionProps.Beacon(root, owner.Palette);
            beacon.SetActive(false);
            owner.Jobs = Listings;
            owner.RouteSource = () => route;
            owner.MarkerSources.Add(Markers);
            if (owner.Phone != null) owner.Phone.JobRequested += Toggle;
        }

        public void End()
        {
            if (Active != null) Active.Fail("");
            Active = null;
            if (director != null)
            {
                if (director.Phone != null) director.Phone.JobRequested -= Toggle;
                director.MarkerSources.Remove(Markers);
                director.Jobs = null;
                director.RouteSource = null;
            }
            if (root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
                else UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        ulong Seed(int salt) => SeededRandom.Mix((ulong)director.State.Day * 7919UL + (ulong)salt, 0x30B5UL);

        public bool IsAvailable(Mission m) =>
            Active == null && m.InWindow(director.Hours) && (m.Repeatable || !director.State.IsMissionDone(m.Id));

        public IReadOnlyList<JobListing> Listings()
        {
            listings.Clear();
            foreach (var m in missions)
            {
                bool done = !m.Repeatable && director.State.IsMissionDone(m.Id);
                listings.Add(new JobListing
                {
                    Id = m.Id,
                    Title = m.Title,
                    Detail = m.Detail + " (jam " + m.WindowText + ")" + (done ? " Sudah selesai." : ""),
                    Reward = m.RewardText,
                    Active = m == Active,
                    Available = IsAvailable(m)
                });
            }
            return listings;
        }

        /// <summary>The phone's Ambil/Batalkan button.</summary>
        public void Toggle(string id)
        {
            var m = Find(id);
            if (m == null) return;
            if (m == Active) Cancel();
            else StartJob(m);
        }

        public bool StartJob(Mission m)
        {
            if (m == null || !IsAvailable(m)) return false;
            context.Reseed(Seed(++started));
            if (!m.Start(context))
            {
                director.Toast("Kerjaan \"" + m.Title + "\" tidak bisa diambil di sekitar sini sekarang.");
                return false;
            }
            Active = m;
            director.ClosePhone();
            director.Toast("Kerjaan diambil: " + m.Title + ".");
            return true;
        }

        public void Cancel()
        {
            if (Active == null) return;
            Active.Fail("Dibatalkan.");
        }

        public void Tick(float dt)
        {
            time += dt;
            if (Active != null)
            {
                // Sleep, fainting and being caught all fade the screen: the job is lost.
                if (director.IsFading && Active.Status == MissionStatus.Running) Active.Fail("Kerjaan gagal: kamu tidak sempat menyelesaikannya.");
                else Active.Tick(dt);
                if (Active.Status != MissionStatus.Running) Finish(Active);
            }
            TickPakRt();
            UpdateRoute(dt);
            Render();
        }

        void Finish(Mission m)
        {
            Active = null;
            if (m.Status == MissionStatus.Succeeded)
            {
                context.Pay(m.Reward);
                director.State.CompleteMission(m.Id);
                director.Toast("Kerjaan selesai: " + m.Title + ". +" + RupiahFormat.Format(m.Reward));
                director.SaveNow();
            }
            else if (!string.IsNullOrEmpty(m.FailReason)) director.Toast(m.FailReason);
            Ended?.Invoke(m);
        }

        void Render()
        {
            var hud = director.Hud;
            var target = Active != null ? Active.Target : null;
            var step = Active != null ? Active.Current : null;
            bool reveal = target.HasValue && (step == null || step.RevealWithin <= 0f || context.Distance(target.Value) <= step.RevealWithin);
            if (beacon != null)
            {
                beacon.SetActive(target.HasValue && reveal);
                if (target.HasValue && reveal)
                {
                    beacon.transform.position = target.Value;
                    MissionProps.Animate(beacon, time);
                }
            }
            if (hud == null) return;
            if (Active == null)
            {
                hud.SetObjective("");
                hud.SetCompass(null, 0f);
                return;
            }
            float left = Active.TimeLeft;
            hud.SetObjective(Active.Title + ": " + Active.Objective + (left >= 0f ? "  " + Clock(left) : ""));
            if (!target.HasValue)
            {
                hud.SetCompass(null, 0f);
                return;
            }
            var to = target.Value - context.PlayerPosition;
            float distance = new Vector2(to.x, to.z).magnitude;
            float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            var forward = context.ViewForward;
            float heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            string text = reveal ? Mathf.RoundToInt(distance).ToString(CultureInfo.InvariantCulture) + " m" : "sekitar " + Mathf.RoundToInt(distance / 10f) * 10 + " m";
            hud.SetCompass(text, Mathf.DeltaAngle(heading, bearing));
        }

        /// <summary>
        /// Re-plans the way to the target when it moved, the player strayed, or a second passed; the start is cut to
        /// where the player now is so the line always begins at their feet.
        /// </summary>
        void UpdateRoute(float dt)
        {
            var target = Active != null ? Active.Target : null;
            if (!target.HasValue)
            {
                route = null;
                return;
            }
            var to = new Vector2(target.Value.x, target.Value.z);
            var me = context.PlayerPosition;
            var from = new Vector2(me.x, me.z);
            routeTimer -= dt;
            bool stale = route == null || (to - routeTo).sqrMagnitude > 4f || (from - routeFrom).sqrMagnitude > 64f || routeTimer <= 0f;
            if (!stale) return;
            routeTimer = 1f;
            routeFrom = from;
            routeTo = to;
            route = director.Roads.Route(from, to);
            if (route != null) route.Insert(0, from);
        }

        public void RefreshRoute()
        {
            route = null;
            UpdateRoute(0f);
        }

        static string Clock(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return (s / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        IEnumerable<MapMarker> Markers()
        {
            var target = Active != null ? Active.Target : null;
            if (target.HasValue)
                yield return new MapMarker { Position = new Vector2(target.Value.x, target.Value.z), Color = MapColors.Target, Size = 8f, Pin = true };
            if (pakRt != null && pakRt.activeSelf)
                yield return new MapMarker { Position = new Vector2(pakRt.transform.position.x, pakRt.transform.position.z), Color = MapColors.PakRt, Size = 6f, Pin = Active == null };
        }

        // ---------- Pak RT ----------

        bool PakRtHours
        {
            get
            {
                float h = director.Hours;
                return h >= PakRtFrom || h < PakRtUntil;
            }
        }

        void TickPakRt()
        {
            var ronda = Get<RondaMission>();
            // His post: the one nearest the kos (fixed, so he never jumps between posts as you walk).
            var kos = context.NearestLandmark(SpotKind.Kos, context.PlayerPosition);
            var post = ronda != null ? context.NearestLandmark(SpotKind.PosRonda, kos != null ? kos.position : context.PlayerPosition) : null;
            bool hours = PakRtHours;
            bool want = post != null && hours && Active != ronda;
            // He comes out at nine: say so once (only when the player is free to help).
            if (hours && !wasPakRtHours && post != null && Active == null) director.Toast(PakRtOutToast);
            wasPakRtHours = hours;
            if (!want)
            {
                if (pakRt != null) pakRt.SetActive(false);
                if (pakRtPost != null) pakRtPost.SetActive(false);
                return;
            }
            if (pakRtPost == null)
            {
                pakRtPost = new GameObject("PosRonda");
                pakRtPost.transform.SetParent(root, false);
                pakRtPost.transform.position = context.OnGround(post.position);
                var lantern = MissionProps.Lantern(pakRtPost.transform, director.Palette);
                lantern.transform.localPosition = new Vector3(0.6f, 0f, 2.2f);
                MissionProps.Kentongan(pakRtPost.transform, director.Palette).transform.localPosition = new Vector3(2.6f, 0f, 0.6f);
            }
            if (!pakRtPost.activeSelf) pakRtPost.SetActive(true);
            if (pakRt == null)
            {
                var look = PersonLook.From(0xA7A7UL);
                look.Hijab = false;
                // A batik shirt and a sarung: light enough to read under the lantern.
                look.Shirt = new Color32(196, 128, 52, 255);
                look.Pants = new Color32(60, 96, 140, 255);
                pakRt = context.Person("PakRT", context.OnGround(post.position + new Vector3(1.6f, 0f, 1.6f)), Vector3.forward, look, () => PakRtPrompt, TalkToPakRt);
                Physics.SyncTransforms();
            }
            if (!pakRt.activeSelf) pakRt.SetActive(true);
            var at = context.OnGround(post.position + new Vector3(1.6f, 0f, 1.6f));
            if ((pakRt.transform.position - at).sqrMagnitude > 0.01f) pakRt.transform.position = at;
            if (context.Distance(at) < 12f)
            {
                var face = context.FacePlayer(at);
                pakRt.transform.rotation = Quaternion.Slerp(pakRt.transform.rotation, Quaternion.LookRotation(face), 0.1f);
            }
        }

        void TalkToPakRt()
        {
            var ronda = Get<RondaMission>();
            if (Active != null)
            {
                director.Toast("Pak RT: \"Selesaikan dulu urusanmu, nanti bantu ronda.\"");
                return;
            }
            if (!IsAvailable(ronda))
            {
                director.Toast("Pak RT: \"Ronda mulai jam sembilan. Nanti ke sini lagi, ya.\"");
                return;
            }
            director.Toast("Pak RT: \"Malam ini kurang orang. Bantu ronda keliling, ya? Nanti ada uang rokok.\"");
            StartJob(ronda);
        }
    }
}
