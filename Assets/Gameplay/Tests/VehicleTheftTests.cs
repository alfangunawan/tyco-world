using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.Player;
using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    public class HeatModelTests
    {
        [Test]
        public void Crime_Stacks_ToThree_HoldsWhileSeen_CoolsOneStarPerSpellUnseen()
        {
            var heat = new HeatModel();
            Assert.IsFalse(heat.IsHot);
            heat.Tick(100f, false);
            Assert.AreEqual(0, heat.Level);
            heat.Crime(2);
            heat.Crime(2);
            Assert.AreEqual(HeatModel.Max, heat.Level);
            heat.Tick(60f, true);
            Assert.AreEqual(3, heat.Level, "seen: the heat holds");
            heat.Tick(HeatModel.CoolSeconds - 1f, false);
            Assert.AreEqual(3, heat.Level);
            heat.Tick(1f, false);
            Assert.AreEqual(2, heat.Level);
            heat.Tick(10f, false);
            heat.Tick(1f, true);
            heat.Tick(HeatModel.CoolSeconds - 1f, false);
            Assert.AreEqual(2, heat.Level, "being seen restarts the cooling");
            heat.Clear();
            Assert.AreEqual(0, heat.Level);
        }

        [Test]
        public void CarTuning_IsWiderSlowerAndLevel()
        {
            var bike = ScriptableObject.CreateInstance<BikeTuning>();
            var car = VehicleFactory.CarTuning(bike);
            try
            {
                Assert.AreEqual(0f, car.maxLean);
                Assert.Greater(car.bodyRadius, bike.bodyRadius);
                Assert.Greater(car.blockHalfExtents.z, bike.blockHalfExtents.z);
                Assert.Less(car.maxForwardSpeed, bike.maxForwardSpeed);
                Assert.Greater(car.dismountSideOffset, bike.dismountSideOffset);
            }
            finally
            {
                Object.DestroyImmediate(car);
                Object.DestroyImmediate(bike);
            }
        }
    }

    public class VehicleSystemTests
    {
        static Vector3 O => KampungFixture.Origin;

        KampungFixture fx;
        PedestrianSystem people;
        VehicleSystem vehicles;

        /// <summary>A lane grid, a bike spot 4 m from the player, a far bike spot, a car spot and a bengkel.</summary>
        static void World(KampungData d)
        {
            KampungFixture.StreetGrid(d, 16, 30f);
            d.spots = new[]
            {
                new Spot { kind = SpotKind.Kos, position = O + new Vector3(0f, 0f, -30f) },
                new Spot { kind = SpotKind.ParkingBike, position = O + new Vector3(4f, 0f, 0f), yaw = 0f },
                new Spot { kind = SpotKind.ParkingBike, position = O + new Vector3(200f, 0f, 0f), yaw = 0f },
                new Spot { kind = SpotKind.ParkingCar, position = O + new Vector3(-6f, 0f, 8f), yaw = 90f }
            };
            d.shops = new[] { KampungFixture.Site("bengkel", ShopType.Bengkel, new Vector3(0f, 0f, 12f), 180f, "Bengkel Uji") };
        }

        [SetUp]
        public void SetUp()
        {
            Interactable.ResetStatics();
            fx = new KampungFixture(World);
            people = new PedestrianSystem { AutoSpawn = false };
            vehicles = new VehicleSystem();
            fx.Director.AddSystem(people);
            fx.Director.AddSystem(vehicles);
            fx.Director.JumpClock(10f);
            fx.Director.State.Wallet.Set(250000);
            Tick();
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        void Tick(int frames = 2, float dt = 0.05f)
        {
            for (int i = 0; i < frames; i++)
            {
                fx.Director.Tick(dt);
                fx.Fx.Sync();
            }
        }

        void Run(float seconds) => Tick(Mathf.CeilToInt(seconds / 0.05f));

        WorldVehicle Near(VehicleKind kind) =>
            vehicles.Vehicles.Where(v => v.Active && v.Kind == kind).OrderBy(v => Vector3.Distance(v.Position, fx.Player.transform.position)).FirstOrDefault();

        /// <summary>A passer-by standing at <paramref name="local"/> (from the origin) looking at the player.</summary>
        Pedestrian Witness(Vector3 local)
        {
            var near = new List<int>();
            people.Graph.Near(new Vector2(O.x, O.z), 40f, near);
            var p = people.SpawnAt(near[0], 0f, 1);
            p.Place(O + local, -local);
            p.State = PedestrianState.Wait;
            p.Timer = 999f;
            fx.Fx.Sync();
            return p;
        }

        [Test]
        public void ParkedVehicles_ComeAndGo_WithThePlayer()
        {
            Assert.AreEqual(1, vehicles.Vehicles.Count(v => v.Active && v.Kind == VehicleKind.Bike), "the near bike, not the one 200 m away");
            Assert.AreEqual(1, vehicles.Vehicles.Count(v => v.Active && v.Kind == VehicleKind.Car));
            var bike = Near(VehicleKind.Bike);
            Assert.Less(Vector3.Distance(bike.Position, O + new Vector3(4f, 0f, 0f)), 0.6f, "on its spot");
            Assert.IsNull(bike.Root.GetComponent<MotorbikeInteractable>(), "a warga bike cannot simply be ridden");
            StringAssert.StartsWith("[E] Bobol kunci motor", bike.Lock.GetPrompt(fx.Context));
            fx.Player.Teleport(O + new Vector3(190f, 0f, 0f), 0f);
            Tick(12);
            var active = vehicles.Vehicles.Where(v => v.Active && v.Kind == VehicleKind.Bike).ToList();
            Assert.AreEqual(1, active.Count, "the near spot's bike is put away, the far spot's appears");
            Assert.AreEqual(2, active[0].Spot);
            Assert.Less(Vector3.Distance(active[0].Position, O + new Vector3(200f, 0f, 0f)), 0.6f);
            Assert.LessOrEqual(vehicles.Vehicles.Count(v => v.Kind == VehicleKind.Bike), 1, "pooled: the same object moved");
        }

        [Test]
        public void BreakingIn_Unseen_TakesThreeSecondsStill_ThenYouRide()
        {
            var bike = Near(VehicleKind.Bike);
            Assert.IsTrue(bike.Lock.Interact(fx.Context));
            Assert.AreSame(bike, vehicles.Hotwiring);
            Assert.IsFalse(fx.Player.Locks.Allows(PlayerAbility.Interact), "hands busy with the lock");
            Assert.IsTrue(fx.Player.Locks.Allows(PlayerAbility.Move), "the legs are free: moving off gives up");
            Run(VehicleSystem.BikeHotwireSeconds - 0.5f);
            Assert.IsFalse(bike.Body.IsMounted);
            Assert.Greater(vehicles.HotwireProgress, 0.7f);
            Run(1f);
            Assert.IsTrue(bike.Body.IsMounted);
            Assert.AreSame(fx.Player, bike.Body.Rider);
            Assert.IsTrue(bike.Unlocked);
            Assert.AreEqual(0, vehicles.Heat.Level, "nobody saw");
            Assert.AreEqual("", fx.Director.Hud.StarsText);
            Assert.IsNull(vehicles.Hotwiring);
            Assert.AreEqual("[E] Naik motor", bike.Lock.GetPrompt(fx.Context));
        }

        [Test]
        public void BreakingIn_Seen_Maling_TheNearestChases_AndCatchesYou()
        {
            var watcher = Witness(new Vector3(4f, 0f, 9f));
            var bike = Near(VehicleKind.Bike);
            Assert.IsTrue(bike.Lock.Interact(fx.Context));
            Assert.AreEqual(1, vehicles.Heat.Level);
            Assert.AreEqual(PedestrianState.Chase, watcher.State);
            Assert.IsTrue(fx.Director.Hud.Toasts.Contains(VehicleSystem.ShoutToast));
            Tick(1);
            StringAssert.StartsWith("MALING!", fx.Director.Hud.StarsText);
            long before = fx.Director.State.Wallet.Balance;
            // Standing at the lock, the chaser reaches you.
            Run(3f);
            Assert.AreEqual(1, vehicles.Caught + (vehicles.IsCaughtPending ? 1 : 0), "caught");
            fx.Director.FinishFade();
            Tick();
            Assert.AreEqual(1, vehicles.Caught);
            Assert.AreEqual(before - VehicleSystem.Fine, fx.Director.State.Wallet.Balance);
            Assert.AreEqual(0, vehicles.Heat.Level);
            Assert.AreEqual(0, people.ChaserCount);
            Assert.IsFalse(bike.Body.IsMounted);
            Assert.IsTrue(fx.Player.Locks.Allows(PlayerAbility.Move));
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.Contains("Denda Rp 100.000")));
            Assert.IsFalse(bike.Hot, "settled: the next try starts clean");
        }

        [Test]
        public void MovingOff_GivesUpTheBreakIn_SoASeenThiefCanRun()
        {
            Witness(new Vector3(4f, 0f, 16f));
            var bike = Near(VehicleKind.Bike);
            Assert.IsTrue(bike.Lock.Interact(fx.Context));
            Assert.AreEqual(1, vehicles.Heat.Level);
            Run(1f);
            fx.Player.Teleport(fx.Player.transform.position + new Vector3(-2f, 0f, -2f), 180f);
            Tick();
            Assert.IsNull(vehicles.Hotwiring);
            Assert.IsFalse(bike.Body.IsMounted);
            Assert.IsTrue(fx.Player.Locks.Allows(PlayerAbility.All));
            Assert.IsTrue(fx.Director.Hud.Toasts.Contains(VehicleSystem.GaveUpToast));
            Assert.AreEqual(1, vehicles.Heat.Level, "they still saw you");
        }

        [Test]
        public void TheFine_TakesWhatYouHave_WhenYouHaveLess()
        {
            fx.Director.State.Wallet.Set(30000);
            Witness(new Vector3(4f, 0f, 6f));
            Assert.IsTrue(Near(VehicleKind.Bike).Lock.Interact(fx.Context));
            Run(3f);
            fx.Director.FinishFade();
            Tick();
            Assert.AreEqual(0, fx.Director.State.Wallet.Balance);
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.Contains("Denda Rp 30.000")));
        }

        [Test]
        public void AHood_KeepsYouUnseen_FromFurtherAway()
        {
            fx.Director.State.Wallet.Set(500000);
            Assert.AreEqual(PurchaseResult.Worn, fx.Director.State.Buy(fx.Director.Catalog.Find(ItemCatalog.Hoodie)));
            Assert.IsTrue(fx.Director.State.IsHooded);
            var watcher = Witness(new Vector3(4f, 0f, 15f));
            Assert.IsTrue(Near(VehicleKind.Bike).Lock.Interact(fx.Context));
            Assert.AreEqual(0, vehicles.Heat.Level, "15 m is beyond 18 m x 0.7 with the hood up");
            Assert.AreNotEqual(PedestrianState.Chase, watcher.State);
        }

        [Test]
        public void ACar_TakesLonger_SoundsTheAlarm_AndCountsDouble()
        {
            Witness(new Vector3(-6f, 0f, 8f + 26f));
            var car = Near(VehicleKind.Car);
            fx.Player.Teleport(car.Position + car.Body.transform.right * 2.2f, 0f);
            fx.Fx.Sync();
            StringAssert.StartsWith("[E] Bobol kunci mobil", car.Lock.GetPrompt(fx.Context));
            Assert.IsTrue(car.Lock.Interact(fx.Context));
            Assert.IsTrue(fx.Director.Hud.Toasts.Contains(VehicleSystem.AlarmToast));
            Assert.AreEqual(2, vehicles.Heat.Level, "the alarm carries 36 m: the warga at 26 m hears it");
            Run(VehicleSystem.BikeHotwireSeconds + 0.2f);
            Assert.IsFalse(car.Body.IsMounted, "four seconds for a car");
        }

        [Test]
        public void OutOfSight_TheHeatCools_AndTheChaseStops()
        {
            var watcher = Witness(new Vector3(4f, 0f, 9f));
            var bike = Near(VehicleKind.Bike);
            Assert.IsTrue(bike.Lock.Interact(fx.Context));
            Assert.AreEqual(1, vehicles.Heat.Level);
            // The warga is called away (out of sight) before reaching you.
            watcher.Root.SetActive(false);
            Run(VehicleSystem.BikeHotwireSeconds + 0.5f);
            Assert.IsTrue(bike.Body.IsMounted);
            float waited = VehicleSystem.BikeHotwireSeconds + 0.5f;
            while (vehicles.Heat.Level > 0 && waited < 40f)
            {
                Tick(1);
                waited += 0.05f;
            }
            Assert.AreEqual(0, vehicles.Heat.Level);
            Assert.That(waited, Is.InRange(HeatModel.CoolSeconds - 1f, HeatModel.CoolSeconds + 1f), "one star cools in 25 s unseen");
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.Contains("berhenti mencarimu")));
        }

        [Test]
        public void ARepaint_AtTheBengkel_ClearsTheHeat_AndChangesTheColour()
        {
            var watcher = Witness(new Vector3(4f, 0f, 9f));
            var bike = Near(VehicleKind.Bike);
            Assert.IsTrue(bike.Lock.Interact(fx.Context));
            watcher.Root.SetActive(false);
            Run(VehicleSystem.BikeHotwireSeconds + 0.5f);
            Assert.IsTrue(bike.Hot);
            int colour = bike.Colour;
            fx.Player.Teleport(O + new Vector3(0f, 0f, 10f), 0f);
            bike.Body.Teleport(O + new Vector3(2f, 0f, 10f), 0f);
            fx.Fx.Sync();
            Assert.AreSame(bike, vehicles.ServiceTarget);
            fx.Director.OpenShop(fx.Director.Stalls[0]);
            Assert.IsTrue(fx.Director.Shop.ClickBuy(ItemCatalog.CatUlang));
            StringAssert.Contains("dicat ulang", fx.Director.Shop.MessageText);
            Assert.AreEqual(0, vehicles.Heat.Level);
            Assert.IsFalse(bike.Hot);
            Assert.AreNotEqual(colour, bike.Colour);
        }
    }
}
