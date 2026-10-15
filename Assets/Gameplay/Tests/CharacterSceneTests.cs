using System.Linq;
using NUnit.Framework;
using TycoWorld.Player;
using TycoWorld.Vehicles;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    /// <summary>The downloaded people, animations and the galon at work (spec #7), on the real CharacterLibrary asset.</summary>
    public class CharacterSceneTests
    {
        const string LibraryPath = "Assets/Gameplay/Data/CharacterLibrary.asset";

        KampungFixture fx;
        MissionSystem jobs;
        CharacterLibrary library;

        [SetUp]
        public void SetUp()
        {
            library = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(LibraryPath);
            if (library == null || !library.HasPeople) Assert.Ignore("Run Tyco World > Gameplay > Build Character Art first.");
            Interactable.ResetStatics();
            fx = new KampungFixture(MissionSystemTests.World, characters: library);
            jobs = new MissionSystem();
            fx.Director.AddSystem(jobs);
            fx.Director.AddSystem(new RiderBodySystem());
            fx.Director.JumpClock(10f);
        }

        [TearDown]
        public void TearDown()
        {
            if (fx != null) fx.Dispose();
        }

        void Tick(int frames = 2)
        {
            for (int i = 0; i < frames; i++)
            {
                fx.Director.Tick(0.05f);
                fx.Player.Tick(0.02f);
                fx.Fx.Sync();
            }
        }

        void TickUntil(System.Func<bool> condition, float seconds)
        {
            for (float t = 0f; t < seconds && !condition(); t += 0.05f) Tick(1);
        }

        void Go(Vector3 p, float yaw = 0f)
        {
            fx.Player.Teleport(p, yaw);
            fx.Fx.Sync();
            Tick();
        }

        Motorbike Bike(Vector3 local)
        {
            var bike = fx.Fx.CreateBike(local, 0f, fx.Fx.Track(ScriptableObject.CreateInstance<BikeTuning>()));
            fx.Fx.Sync();
            return bike;
        }

        [Test]
        public void ThePeople_AreTheDownloadedBodies_WithTheKampungAnimator()
        {
            var keeper = fx.Director.Stalls[0].KeeperRig;
            Assert.IsNotNull(keeper, "the shop keeper is a real body");
            Assert.AreSame(library.controller, keeper.Animator.runtimeAnimatorController);
            Assert.IsTrue(keeper.Animator.isHuman);
            var smr = keeper.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(smr);
            Assert.That(smr.bounds.size.y, Is.InRange(1.3f, 2.1f), "about a person's height");
            var across = keeper.Bone(HumanBodyBones.RightUpperArm).position - keeper.Bone(HumanBodyBones.LeftUpperArm).position;
            across.y = 0f;
            Assert.Greater(Vector3.Dot(Vector3.Cross(across, Vector3.up), keeper.transform.parent.forward), 0f,
                "the body faces where its owner faces (models exported facing -Z are turned around)");
            keeper.Move(1.3f);
            Assert.AreEqual(1.3f, keeper.Animator.GetFloat(CharacterRig.SpeedId), 1e-4f);
        }

        [Test]
        public void TheGalon_RidesOnYourShoulder_OnThePillion_AndEndsInTheCustomersHands()
        {
            var galon = jobs.Get<GalonMission>();
            Assert.IsTrue(jobs.StartJob(galon));
            Go(galon.Shop.counter + new Vector3(0f, 0f, 1.5f));
            Assert.IsTrue(galon.Carrying);
            Assert.IsNotNull(galon.Galon);
            Assert.IsTrue(galon.Galon.Exists, "a real galon model");
            Assert.AreEqual("view", galon.Galon.Place);
            Assert.AreSame(fx.Player.View, galon.Galon.Model.transform.parent, "on the shoulder, in view");
            Tick(12);
            Assert.Less(Vector3.Distance(galon.Galon.Model.transform.localPosition, CarriedGalon.ShoulderLocal), 0.01f, "lifted up in half a second");

            var bike = Bike(galon.Shop.counter - KampungFixture.Origin + new Vector3(3f, 0f, 3f));
            Assert.IsTrue(bike.Mount(fx.Player));
            Tick();
            Assert.AreEqual("bike", galon.Galon.Place);
            Assert.IsTrue(galon.Galon.Model.transform.IsChildOf(bike.transform), "strapped to the pillion");

            Go(galon.Customer.transform.position + new Vector3(0f, 0f, 1.5f));
            Assert.AreEqual("view", galon.Galon.Place, "off the bike, back on the shoulder");
            var customer = galon.Customer;
            Assert.IsTrue(customer.GetComponent<ActionInteractable>().Interact(fx.Context));
            Assert.AreEqual("held", galon.Galon.Place);
            var rig = MissionContext.RigOf(customer);
            Assert.IsNotNull(rig);
            Assert.AreSame(galon.Galon.Model.transform, rig.Held, "both hands on it");
            Assert.IsTrue(rig.TryGetGrips(out var left, out var right));
            Assert.Less(Vector3.Distance(left, right), 0.5f);
            Tick();
            Assert.AreEqual(MissionStatus.Succeeded, galon.Status);
            Assert.IsTrue(customer != null && customer.activeSelf, "walks in through the gate with it");
            Assert.AreEqual(1, jobs.LeaverCount);
        }

        [Test]
        public void TheOjekFare_SitsOnThePillion_AndGetsOff()
        {
            var ojek = jobs.Get<OjekMission>();
            Assert.IsTrue(jobs.StartJob(ojek));
            var rig = MissionContext.RigOf(ojek.Passenger);
            Assert.IsNotNull(rig);
            var bike = Bike(ojek.Pickup - KampungFixture.Origin + new Vector3(2f, 0f, 0f));
            Assert.IsTrue(bike.Mount(fx.Player));
            Tick();
            TickUntil(() => ojek.Ride.Boarded, 8f);
            Assert.IsTrue(ojek.Ride.Boarded);
            Assert.IsTrue(rig.IsSitting, "Sitting_Enter / Sitting_Idle_Loop");
            bike.Teleport(ojek.Drop + new Vector3(2f, 0f, 0f), 0f);
            fx.Fx.Sync();
            Tick();
            Assert.IsFalse(rig.IsSitting, "Sitting_Exit when getting off");
        }

        [Test]
        public void YourBody_RidesTheBike_InThirdPerson_Only()
        {
            var riders = fx.Director.GetSystem<RiderBodySystem>();
            Assert.IsNotNull(riders.Body);
            Assert.IsFalse(riders.Visible, "on foot the camera is your eyes");
            var bike = Bike(new Vector3(3f, 0f, 0f));
            Assert.IsTrue(bike.Mount(fx.Player));
            Tick();
            if (bike.CameraState.Mode != RideCameraMode.ThirdPerson) bike.CameraState.Toggle();
            Tick();
            Assert.IsTrue(riders.Visible);
            Assert.IsTrue(riders.Body.IsDriving, "Driving_Loop");
            Assert.IsTrue(riders.Body.transform.IsChildOf(bike.transform));
            bike.CameraState.Toggle();
            Tick();
            Assert.IsFalse(riders.Visible, "first person: hidden");
        }
    }
}
