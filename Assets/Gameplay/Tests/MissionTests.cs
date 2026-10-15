using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.Player;
using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    public class MissionStepTests
    {
        sealed class TwoStepJob : Mission
        {
            public bool A, B;
            public int Entered;
            public GameObject Prop;
            public override string Id => "uji";
            public override string Title => "Uji";
            public override string Detail => "";
            public override string RewardText => "";
            public override float StartHour => 21f;
            public override float EndHour => 3f;

            protected override bool Build(MissionContext context)
            {
                Reward = 1234;
                Prop = Own(new GameObject("UjiProp"));
                Add(new MissionStep { Objective = "satu", Done = () => A, Enter = () => Entered++ });
                Add(new MissionStep { Objective = "dua", Done = () => B, Enter = () => Entered++, TimeLimit = 5f });
                return true;
            }
        }

        [Test]
        public void Steps_RunInOrder_TimeOutFails_AndOwnedObjectsGo()
        {
            var job = new TwoStepJob();
            Assert.IsTrue(job.Start(null));
            Assert.AreEqual("satu", job.Objective);
            Assert.AreEqual(1, job.Entered);
            Assert.AreEqual(-1f, job.TimeLeft);
            job.Tick(1f);
            Assert.AreEqual(0, job.StepIndex);
            job.A = true;
            job.Tick(1f);
            Assert.AreEqual("dua", job.Objective);
            Assert.AreEqual(2, job.Entered);
            Assert.AreEqual(5f, job.TimeLeft, 1e-4f);
            job.Tick(5f);
            Assert.AreEqual(MissionStatus.Failed, job.Status);
            Assert.IsTrue(job.Prop == null, "the mission's props are destroyed when it ends");

            Assert.IsTrue(job.Start(null));
            job.A = true;
            job.Tick(0.1f);
            job.B = true;
            job.Tick(0.1f);
            Assert.AreEqual(MissionStatus.Succeeded, job.Status);
            Assert.AreEqual(1234, job.Reward);
        }

        [Test]
        public void Window_WrapsMidnight()
        {
            var job = new TwoStepJob();
            Assert.IsTrue(job.InWindow(22f));
            Assert.IsTrue(job.InWindow(1f));
            Assert.IsFalse(job.InWindow(3f));
            Assert.IsFalse(job.InWindow(12f));
            Assert.AreEqual("21.00-03.00", job.WindowText);
        }
    }

    public class MissionSystemTests
    {
        static Vector3 O => KampungFixture.Origin;

        KampungFixture fx;
        MissionSystem jobs;

        /// <summary>A 480 m grid of lanes 30 m apart with gates, two shops and the landmarks the jobs ask for.</summary>
        public static void World(KampungData d)
        {
            KampungFixture.StreetGrid(d, 16, 30f);
            // The phone map's road lines, for routes (the GPS and the ojek's time).
            d.mapRoads = d.walkLanes;
            var spots = new List<Spot>
            {
                new Spot { kind = SpotKind.Kos, position = O + new Vector3(0f, 0f, -30f) },
                new Spot { kind = SpotKind.Cemetery, position = O + new Vector3(150f, 0f, 150f) },
                new Spot { kind = SpotKind.PosRonda, position = O + new Vector3(90f, 0f, 0f) },
                new Spot { kind = SpotKind.PosRonda, position = O + new Vector3(150f, 0f, 60f) },
                new Spot { kind = SpotKind.PosRonda, position = O + new Vector3(210f, 0f, -30f) },
                new Spot { kind = SpotKind.CampusGate, position = O + new Vector3(0f, 0f, 240f) },
                new Spot { kind = SpotKind.Mosque, position = O + new Vector3(-210f, 0f, 90f) }
            };
            foreach (float r in new[] { 60f, 90f, 120f, 150f, 180f, 210f, 240f })
            {
                spots.Add(new Spot { kind = SpotKind.Door, position = O + new Vector3(r, 0f, 15f), yaw = 180f });
                spots.Add(new Spot { kind = SpotKind.Door, position = O + new Vector3(-r, 0f, -15f), yaw = 0f });
            }
            d.spots = spots.ToArray();
            d.shops = new[]
            {
                KampungFixture.Site("warung", ShopType.Warung, new Vector3(30f, 0f, 2f), 180f, "Warung Bu Uji"),
                KampungFixture.Site("mini", ShopType.Minimarket, new Vector3(-60f, 0f, 2f), 180f, "Minimarket Uji")
            };
        }

        [SetUp]
        public void SetUp()
        {
            Interactable.ResetStatics();
            fx = new KampungFixture(World);
            jobs = new MissionSystem();
            fx.Director.AddSystem(jobs);
            fx.Director.JumpClock(10f);
            fx.Director.State.Wallet.Set(100000);
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        void Go(Vector3 p, float yaw = 0f)
        {
            fx.Player.Teleport(p, yaw);
            fx.Fx.Sync();
            Tick();
        }

        void Tick(int frames = 2)
        {
            for (int i = 0; i < frames; i++) fx.Director.Tick(0.05f);
        }

        static bool Interact(GameObject go, KampungFixture f)
        {
            var target = go.GetComponent<ActionInteractable>();
            Assert.IsNotNull(target, go.name + " is an E target");
            return target.Interact(f.Context);
        }

        [Test]
        public void Listings_ShowSixJobs_ByTheHour()
        {
            var day = jobs.Listings().ToDictionary(l => l.Id);
            Assert.AreEqual(6, day.Count);
            CollectionAssert.AreEquivalent(new[] { "ojek", "galon", "titipan", "kucing" }, day.Values.Where(l => l.Available).Select(l => l.Id));
            fx.Director.JumpClock(23f);
            var night = jobs.Listings().Where(l => l.Available).Select(l => l.Id).ToList();
            CollectionAssert.AreEquivalent(new[] { "ronda", "kuburan" }, night);
            StringAssert.Contains("21.00-02.00", jobs.Listings().First(l => l.Id == "ronda").Detail);
        }

        [Test]
        public void Galon_FetchCarryDeliver_PaysAndFreesTheLegs()
        {
            var galon = jobs.Get<GalonMission>();
            Assert.IsTrue(jobs.StartJob(galon));
            Assert.AreSame(galon, jobs.Active);
            Assert.AreEqual("warung", galon.Shop.id, "the nearest open warung");
            Tick(1);
            StringAssert.Contains("Ambil galon di Warung Bu Uji", fx.Director.Hud.ObjectiveText);
            Assert.IsTrue(jobs.Beacon.activeSelf);
            StringAssert.EndsWith(" m", fx.Director.Hud.CompassText);
            Assert.IsFalse(Interact(galon.Customer, fx), "nothing to hand over yet");

            Go(galon.Shop.counter + new Vector3(0f, 0f, 1.5f));
            Assert.IsTrue(galon.Carrying);
            Assert.AreEqual(0, fx.Director.State.Bag.Count(ItemCatalog.Galon), "carried, never in the saved bag");
            Assert.IsFalse(fx.Player.Locks.Allows(PlayerAbility.Sprint), "too heavy to run");

            long before = fx.Director.State.Wallet.Balance;
            Go(galon.Customer.transform.position + new Vector3(0f, 0f, 1.5f));
            Assert.IsTrue(Interact(galon.Customer, fx));
            Tick();
            Assert.IsNull(jobs.Active);
            Assert.AreEqual(MissionStatus.Succeeded, galon.Status);
            Assert.AreEqual(before + GalonMission.Pay, fx.Director.State.Wallet.Balance);
            Assert.AreEqual(0, fx.Director.State.Bag.Count(ItemCatalog.Galon));
            Assert.IsTrue(fx.Player.Locks.Allows(PlayerAbility.Sprint));
            Assert.IsFalse(jobs.Beacon.activeSelf);
            Assert.AreEqual("", fx.Director.Hud.ObjectiveText);
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.StartsWith("Kerjaan selesai: Antar Galon")));
            Assert.IsTrue(fx.Director.State.IsMissionDone("galon"));
        }

        [Test]
        public void Titipan_BuyThenHandOver_RefundsPlusTip()
        {
            var titip = jobs.Get<TitipanMission>();
            Assert.IsTrue(jobs.StartJob(titip));
            Assert.That(titip.Items.Count, Is.InRange(2, 3));
            Assert.IsFalse(Interact(titip.Friend, fx), "empty-handed");
            long spent = 0;
            foreach (var item in titip.Items)
            {
                Assert.AreEqual(PurchaseResult.Bought, fx.Director.State.Buy(item));
                spent += item.price;
            }
            Tick();
            Assert.AreEqual(1, titip.StepIndex);
            long before = fx.Director.State.Wallet.Balance;
            Go(titip.Friend.transform.position + new Vector3(1.5f, 0f, 0f));
            Assert.IsTrue(Interact(titip.Friend, fx));
            Tick();
            Assert.AreEqual(MissionStatus.Succeeded, titip.Status);
            Assert.AreEqual(before + spent + TitipanMission.Tip, fx.Director.State.Wallet.Balance);
            Assert.IsTrue(titip.Items.All(i => fx.Director.State.Bag.Count(i.id) == 0));
        }

        [Test]
        public void Kucing_HiddenUntilClose_CatchAndReturn()
        {
            var kucing = jobs.Get<KucingMission>();
            Assert.IsTrue(jobs.StartJob(kucing));
            Assert.IsFalse(Interact(kucing.Cat, fx), "first ask Bu Yati");
            Go(kucing.Owner.transform.position + new Vector3(0f, 0f, 1.5f));
            Assert.IsTrue(Interact(kucing.Owner, fx));
            Tick();
            Assert.AreEqual(1, kucing.StepIndex);
            Assert.Greater(Vector3.Distance(fx.Player.transform.position, kucing.CatPosition), KucingMission.RevealWithin);
            Assert.IsFalse(jobs.Beacon.activeSelf, "a search: no beacon from afar");
            StringAssert.StartsWith("sekitar", fx.Director.Hud.CompassText);

            Go(kucing.CatPosition + new Vector3(1.2f, 0f, 0f));
            Assert.IsTrue(jobs.Beacon.activeSelf);
            Assert.AreEqual(kucing.CatPosition, kucing.Target.Value);
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.Contains("MEONG")));
            Assert.IsTrue(Interact(kucing.Cat, fx));
            Tick();
            Assert.IsFalse(kucing.Cat.activeSelf);
            Go(kucing.Owner.transform.position + new Vector3(0f, 0f, 1.5f));
            StringAssert.Contains("Serahkan", kucing.Owner.GetComponent<ActionInteractable>().GetPrompt(fx.Context));
            Assert.IsTrue(Interact(kucing.Owner, fx));
            Tick();
            Assert.AreEqual(MissionStatus.Succeeded, kucing.Status);
        }

        Motorbike Bike()
        {
            var tuning = fx.Fx.Track(ScriptableObject.CreateInstance<BikeTuning>());
            var bike = fx.Fx.CreateBike(new Vector3(3f, 0f, 0f), 0f, tuning);
            fx.Fx.Sync();
            return bike;
        }

        /// <summary>Ticks until the condition holds (or the time is up); returns the time it took.</summary>
        float TickUntil(System.Func<bool> condition, float seconds)
        {
            float t = 0f;
            while (!condition() && t < seconds)
            {
                fx.Director.Tick(0.05f);
                fx.Player.Tick(0.02f);
                fx.Fx.Sync();
                t += 0.05f;
            }
            return t;
        }

        void Ride(Motorbike bike, Vector3 to)
        {
            bike.Teleport(to, 0f);
            fx.Player.Tick(0.02f);
            fx.Fx.Sync();
            Tick();
        }

        [Test]
        public void Ojek_OnTheMotor_PicksUpAndDropsOff_ForTheFare()
        {
            var ojek = jobs.Get<OjekMission>();
            Assert.IsTrue(jobs.StartJob(ojek));
            float trip = MissionContext.Flat(ojek.Pickup, ojek.Drop);
            Assert.That(trip, Is.InRange(OjekMission.TripMin, OjekMission.TripMax));
            Assert.AreEqual(OjekMission.BaseFare + OjekMission.PerMetre * Mathf.RoundToInt(ojek.RouteLength) / 500 * 500, ojek.Reward,
                "the fare goes by the road distance");

            Go(ojek.Pickup + new Vector3(1f, 0f, 0f));
            Tick(20);
            Assert.AreEqual(0, ojek.StepIndex, "walking up does not count: it is an ojek");
            Assert.AreEqual(PassengerRide.Phase.Waiting, ojek.Ride.State);
            var bike = Bike();
            Assert.IsTrue(bike.Mount(fx.Player));
            Ride(bike, ojek.Pickup + new Vector3(2.5f, 0f, 0f));
            Assert.AreEqual(PassengerRide.Phase.Approaching, ojek.Ride.State, "the fare walks over to the stopped bike");
            Assert.AreEqual(0, ojek.StepIndex);
            float waited = TickUntil(() => ojek.StepIndex == 1, 8f);
            Assert.AreEqual(1, ojek.StepIndex, "on the pillion after walking over and sitting down");
            Assert.Greater(waited, PassengerRide.MountSeconds, "not in an instant");
            Assert.IsTrue(ojek.Passenger.activeSelf, "visible, riding along");
            Assert.IsTrue(ojek.Passenger.transform.IsChildOf(bike.transform), "sits on the bike");
            Assert.Less(Vector3.Distance(ojek.Passenger.transform.position, bike.transform.position), 1.2f);
            Assert.IsFalse(ojek.Passenger.GetComponent<Collider>().enabled, "no capsule in the bike's way");
            Assert.Greater(ojek.TimeLeft, 0f);
            long before = fx.Director.State.Wallet.Balance;
            Ride(bike, ojek.Drop + new Vector3(2f, 0f, 0f));
            Assert.AreEqual(MissionStatus.Running, ojek.Status, "getting off takes a moment");
            TickUntil(() => ojek.Status != MissionStatus.Running, 4f);
            Assert.AreEqual(MissionStatus.Succeeded, ojek.Status);
            Assert.AreEqual(before + ojek.Reward, fx.Director.State.Wallet.Balance);
            Assert.IsNotNull(ojek.Passenger, "walks away instead of vanishing");
            Assert.IsFalse(ojek.Passenger.transform.IsChildOf(bike.transform));
            Assert.AreEqual(1, jobs.LeaverCount);
            TickUntil(() => jobs.LeaverCount == 0, 10f);
            Assert.IsTrue(ojek.Passenger == null, "gone after walking off");
        }

        [Test]
        public void Ojek_Time_FollowsTheRouteOnTheRoads_NotTheCrowFlight()
        {
            var ojek = jobs.Get<OjekMission>();
            Assert.IsTrue(jobs.StartJob(ojek));
            var route = fx.Director.Roads.Route(new Vector2(ojek.Pickup.x, ojek.Pickup.z), new Vector2(ojek.Drop.x, ojek.Drop.z));
            Assert.IsNotNull(route);
            float length = RoadGraph.Length(route);
            Assert.AreEqual(length, ojek.RouteLength, 0.5f);
            Assert.GreaterOrEqual(ojek.RouteLength, MissionContext.Flat(ojek.Pickup, ojek.Drop) - 0.5f, "roads are never shorter than the crow flies");
            Assert.AreEqual(length / OjekMission.TripSpeed + OjekMission.TripMargin, ojek.TripSeconds, 0.01f);
            Assert.LessOrEqual(OjekMission.TripSpeed, 5f, "an average a rider keeps through kampung turns and gang (18 km/h)");
            Assert.GreaterOrEqual(OjekMission.TripMargin, 60f, "time to stop, let the fare on and find the way");
        }

        [Test]
        public void Ojek_Destinations_Vary_AndNeverRepeatTheLastThree()
        {
            var ojek = jobs.Get<OjekMission>();
            var drops = new List<Vector3>();
            var names = new HashSet<string>();
            for (int i = 0; i < 9; i++)
            {
                Assert.IsTrue(jobs.StartJob(ojek), "run " + i);
                foreach (var earlier in drops.Skip(System.Math.Max(0, drops.Count - 3)))
                    Assert.Greater(MissionContext.Flat(earlier, ojek.Drop), OjekMission.RepeatRadius, "run " + i + " goes somewhere new");
                drops.Add(ojek.Drop);
                names.Add(ojek.DropName);
                jobs.Cancel();
                Tick();
            }
            Assert.GreaterOrEqual(names.Count, 3, "gates, mosques, shops, houses...: " + string.Join(", ", names));
        }

        [Test]
        public void Ojek_TooSlow_ThePassengerGivesUp()
        {
            var ojek = jobs.Get<OjekMission>();
            Assert.IsTrue(jobs.StartJob(ojek));
            var bike = Bike();
            Assert.IsTrue(bike.Mount(fx.Player));
            Ride(bike, ojek.Pickup + new Vector3(2f, 0f, 0f));
            TickUntil(() => ojek.StepIndex == 1, 8f);
            float limit = ojek.TimeLeft;
            for (float t = 0f; t < limit + 1f; t += 0.5f) fx.Director.Tick(0.5f);
            Assert.AreEqual(MissionStatus.Failed, ojek.Status);
            Assert.IsNull(jobs.Active);
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.Contains("Telat")));
        }

        [Test]
        public void Ronda_PostsInOrder_SomeoneFollows_ThenPaid()
        {
            fx.Director.JumpClock(22f);
            var ronda = jobs.Get<RondaMission>();
            Assert.IsTrue(jobs.StartJob(ronda));
            Assert.AreEqual(3, ronda.Posts.Count);
            var posts = fx.Director.ObjectsRoot.GetComponentsInChildren<ActionInteractable>().Where(a => a.name.StartsWith("Kentongan")).OrderBy(a => a.name).ToList();
            Assert.AreEqual(3, posts.Count);
            Assert.IsFalse(posts[1].Interact(fx.Context), "pos 2 before pos 1 does nothing");
            Assert.IsTrue(posts[0].Interact(fx.Context));
            Tick();
            Go(ronda.Posts[1], 0f);
            Assert.IsTrue(posts[1].Interact(fx.Context));
            Tick();
            Assert.IsNotNull(ronda.Follower);
            Assert.IsTrue(ronda.Follower.activeSelf);
            Assert.Greater(Vector3.Distance(ronda.Follower.transform.position, fx.Player.transform.position), 5f, "behind you on the road");
            // Turn around: it is gone a moment later.
            var to = ronda.Follower.transform.position - fx.Player.transform.position;
            Go(fx.Player.transform.position, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            for (int i = 0; i < 20; i++) Tick(1);
            Assert.IsTrue(ronda.FollowerSeen);
            Assert.IsFalse(ronda.Follower.activeSelf);
            long before = fx.Director.State.Wallet.Balance;
            Assert.IsTrue(posts[2].Interact(fx.Context));
            Tick();
            Assert.AreEqual(MissionStatus.Succeeded, ronda.Status);
            Assert.AreEqual(before + RondaMission.Pay, fx.Director.State.Wallet.Balance);
        }

        [Test]
        public void Kuburan_TheRadio_TheWatcher_TheStory_OnlyOnce()
        {
            fx.Director.JumpClock(23f);
            var kuburan = jobs.Get<KuburanMission>();
            Assert.IsTrue(jobs.StartJob(kuburan), "start");
            Go(O + new Vector3(150f, 0f, 145f));
            Assert.AreEqual(1, kuburan.StepIndex);
            Go(kuburan.RadioPosition + new Vector3(0f, 0f, -1.5f));
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.Contains("kamar tiga")), "static by the radio: " + string.Join(" | ", fx.Director.Hud.Toasts));
            Assert.IsTrue(Interact(kuburan.Radio, fx), "switch off");
            Tick();
            Assert.IsNotNull(kuburan.Watcher);
            Assert.IsTrue(kuburan.Watcher.activeSelf, "someone was listening");
            for (int i = 0; i < 60; i++) Tick(1);
            Assert.IsFalse(kuburan.Watcher.activeSelf);
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.Contains("Kos Raflesia")), "the story: " + string.Join(" | ", fx.Director.Hud.Toasts));
            Go(O + new Vector3(0f, 0f, -28f));
            Assert.AreEqual(MissionStatus.Succeeded, kuburan.Status);
            Assert.IsFalse(jobs.IsAvailable(kuburan), "a story happens once");
            Assert.IsFalse(jobs.StartJob(kuburan));
        }

        [Test]
        public void PhoneCancel_AndSleep_EndTheJob()
        {
            var galon = jobs.Get<GalonMission>();
            Assert.IsTrue(jobs.StartJob(galon));
            Assert.IsFalse(jobs.StartJob(jobs.Get<KucingMission>()), "one job at a time");
            jobs.Toggle("galon");
            Tick();
            Assert.IsNull(jobs.Active);
            Assert.AreEqual(MissionStatus.Failed, galon.Status);
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.Contains("Dibatalkan")));

            Assert.IsTrue(jobs.StartJob(galon));
            Go(galon.Shop.counter + new Vector3(0f, 0f, 1.5f));
            Assert.IsTrue(galon.Carrying);
            fx.Director.Faint();
            Tick();
            Assert.IsNull(jobs.Active);
            Assert.IsFalse(galon.Carrying, "the galon goes back");
            Assert.IsTrue(fx.Player.Locks.Allows(PlayerAbility.Sprint));
        }

        [Test]
        public void Galon_ABoughtGalon_IsNotTheJob_AndSurvivesACancel()
        {
            fx.Director.State.Bag.Add(ItemCatalog.Galon);
            var galon = jobs.Get<GalonMission>();
            Assert.IsTrue(jobs.StartJob(galon));
            Go(galon.Customer.transform.position + new Vector3(0f, 0f, 1.5f));
            Assert.IsFalse(Interact(galon.Customer, fx), "step 1: fetch the job's galon first");
            jobs.Toggle("galon");
            Tick();
            Assert.AreEqual(1, fx.Director.State.Bag.Count(ItemCatalog.Galon), "the player's own galon stays");
        }

        [Test]
        public void Shutdown_MidJob_FreesTheLegs()
        {
            var galon = jobs.Get<GalonMission>();
            Assert.IsTrue(jobs.StartJob(galon));
            Go(galon.Shop.counter + new Vector3(0f, 0f, 1.5f));
            Assert.IsFalse(fx.Player.Locks.Allows(PlayerAbility.Sprint));
            fx.Director.Shutdown();
            Assert.IsTrue(fx.Player.Locks.Allows(PlayerAbility.All));
        }

        [Test]
        public void WithoutData_TheRunningJob_CanStillBeCancelledFromThePhone()
        {
            var galon = jobs.Get<GalonMission>();
            Assert.IsTrue(jobs.StartJob(galon));
            // The first day's free package runs out at midnight.
            fx.Director.State.NextDay();
            Assert.IsFalse(fx.Director.State.HasData);
            fx.Director.TogglePhone();
            fx.Director.Phone.Select(PhoneTab.Kerjaan);
            Tick();
            Assert.IsTrue(fx.Director.Phone.Click(PhoneView.JobPrefix + "galon"), "Batalkan is shown without data");
            Tick();
            Assert.IsNull(jobs.Active);
            Assert.IsFalse(fx.Director.Phone.Click(PhoneView.JobPrefix + "ojek"), "new jobs still need data");
        }

        [Test]
        public void ANewGame_ComesWithData_AndSaysWhereTheJobsAre()
        {
            Assert.IsTrue(fx.Director.State.HasData, "day 1's package is free");
            Assert.IsTrue(fx.Director.Hud.Toasts.Contains(KampungDirector.FirstDayJobsToast));
            fx.Director.TogglePhone();
            fx.Director.Phone.Select(PhoneTab.Kerjaan);
            Tick();
            Assert.IsTrue(fx.Director.Phone.Click(PhoneView.JobPrefix + "galon"), "jobs can be taken straight away");
            Assert.AreSame(jobs.Get<GalonMission>(), jobs.Active);
        }

        [Test]
        public void PakRt_ComesOutAtNine_WithALitPost_AndSaysSo()
        {
            fx.Director.JumpClock(20.9f);
            Tick();
            Assert.IsTrue(jobs.PakRtPost == null || !jobs.PakRtPost.activeSelf);
            fx.Director.JumpClock(21.1f);
            Tick();
            Assert.IsTrue(jobs.PakRt.activeSelf);
            Assert.IsTrue(jobs.PakRtPost.activeSelf);
            var lamp = jobs.PakRtPost.GetComponentInChildren<Light>();
            Assert.IsNotNull(lamp, "a lantern lights him up at night");
            Assert.Less(Vector3.Distance(lamp.transform.position, jobs.PakRt.transform.position), 4f);
            Assert.IsTrue(fx.Director.Hud.Toasts.Contains(MissionSystem.PakRtOutToast));
            int toasts = fx.Director.Hud.Toasts.Count(t => t == MissionSystem.PakRtOutToast);
            Tick(5);
            Assert.AreEqual(toasts, fx.Director.Hud.Toasts.Count(t => t == MissionSystem.PakRtOutToast), "said once");
        }

        [Test]
        public void PakRt_ShowsOnTheMap_WhileHeIsOut()
        {
            fx.Director.JumpClock(21.5f);
            Tick();
            var marker = fx.Director.MarkerSources.SelectMany(s => s()).Where(m => m.Color == MapColors.PakRt).ToList();
            Assert.AreEqual(1, marker.Count);
            Assert.Less(Vector2.Distance(marker[0].Position, new Vector2(jobs.PakRt.transform.position.x, jobs.PakRt.transform.position.z)), 0.01f);
        }

        [Test]
        public void PakRt_BeforeTheWatchStarts_IsNotYetOut()
        {
            fx.Director.JumpClock(20.5f);
            Tick();
            Assert.IsTrue(jobs.PakRt == null || !jobs.PakRt.activeSelf, "the watch starts at 21.00");
        }

        [Test]
        public void PakRt_AtNight_ByThePost_OffersTheWatch()
        {
            Tick();
            Assert.IsTrue(jobs.PakRt == null || !jobs.PakRt.activeSelf, "Pak RT sits at home by day");
            fx.Director.JumpClock(21.5f);
            Tick();
            Assert.IsNotNull(jobs.PakRt);
            Assert.IsTrue(jobs.PakRt.activeSelf);
            Assert.Less(Vector3.Distance(jobs.PakRt.transform.position, O + new Vector3(90f, 0f, 0f)), 4f, "by the nearest post");
            Assert.AreEqual(MissionSystem.PakRtPrompt, jobs.PakRt.GetComponent<ActionInteractable>().GetPrompt(fx.Context));
            Assert.IsTrue(Interact(jobs.PakRt, fx));
            Assert.AreSame(jobs.Get<RondaMission>(), jobs.Active);
            Tick();
            Assert.IsFalse(jobs.PakRt.activeSelf, "he waits for you to finish");
        }
    }
}
