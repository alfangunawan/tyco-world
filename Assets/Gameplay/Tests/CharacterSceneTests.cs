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

        /// <summary>Once animated (out of the T-pose: hands below the shoulders), the body faces its holder's +Z.</summary>
        static void AssertFacesForward(CharacterRig rig)
        {
            var animator = rig.Animator;
            var culling = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int i = 0; i < 4; i++) animator.Update(0.1f);
            animator.cullingMode = culling;
            var hand = rig.Bone(HumanBodyBones.RightHand);
            var shoulder = rig.Bone(HumanBodyBones.RightUpperArm);
            Assert.Less(hand.position.y, shoulder.position.y - 0.25f, rig.Root.name + ": the animation is playing (arms down)");
            var across = shoulder.position - rig.Bone(HumanBodyBones.LeftUpperArm).position;
            across.y = 0f;
            Assert.Greater(Vector3.Dot(Vector3.Cross(across, Vector3.up).normalized, rig.Root.forward), 0.8f,
                rig.Root.name + ": the animated body faces where its holder faces");
        }

        [Test]
        public void EveryBody_IsOneSkinnedMesh_WithEachMaterialOnce()
        {
            foreach (var body in library.bodies.Concat(library.womenBodies ?? new GameObject[0]))
            {
                var parts = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.AreEqual(1, parts.Length, body.name + ": body, head, legs and feet are merged at import (one skinning, fewer draws)");
                var mesh = parts[0].sharedMesh;
                var mats = parts[0].sharedMaterials;
                Assert.AreEqual(mesh.subMeshCount, mats.Length, body.name);
                Assert.AreEqual(mats.Length, mats.Distinct().Count(), body.name + ": one submesh per material");
                Assert.That(mesh.triangles.Length / 3, Is.InRange(4000, 9000), body.name + ": every part's triangles are kept");
                Assert.AreEqual(mesh.vertexCount, mesh.boneWeights.Length, body.name + ": every vertex stays skinned");
                Assert.That(mats.Select(m => m.name.ToLowerInvariant()), Has.Some.StartsWith("skin"), body.name);
            }
        }

        [Test]
        public void TheLibrary_SplitsMenAndWomen_AndDressesThemAsKampungFolk()
        {
            Assert.That(library.bodies.Select(b => b.name), Has.None.StartsWith("Women"), "men are picked from men's bodies only");
            var palette = fx.Director.Palette;
            var parent = new GameObject("DressTest").transform;
            try
            {
                foreach (var body in library.bodies.Concat(library.womenBodies ?? new GameObject[0]))
                {
                    var one = ScriptableObject.CreateInstance<CharacterLibrary>();
                    one.controller = library.controller;
                    one.bodies = new[] { body };
                    var rig = CharacterFactory.Spawn(one, parent, body.name, 0x51UL, palette);
                    Object.DestroyImmediate(one);
                    Assert.IsNotNull(rig, body.name);
                    var mats = rig.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToArray();
                    foreach (var m in mats)
                    {
                        string n = m.name.ToLowerInvariant();
                        Assert.IsFalse(n.StartsWith("skin") || n.StartsWith("hair") || n.StartsWith("eyebrow") || n == "red" && body.name == "Women_Formal",
                            body.name + ": " + m.name + " is still the model's own colour");
                    }
                    Assert.That(mats, Has.Some.Matches<Material>(m => (Color32)m.color is var c && c.r == CharacterFactory.SkinTone(0x51UL).r && c.g == CharacterFactory.SkinTone(0x51UL).g),
                        body.name + " has sawo matang skin");
                    AssertFacesForward(rig);
                }
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
            }
        }

        [Test]
        public void FarPedestrians_StopAnimating_AndCastNoShadows()
        {
            var people = new PedestrianSystem { AutoSpawn = false };
            fx.Director.AddSystem(people);
            var p = people.SpawnAt(0, 0f, 1);
            Assert.IsNotNull(p.Rig, "pedestrians are downloaded bodies");
            var parts = p.Rig.GetComponentsInChildren<Renderer>(true);
            p.SetDetail(false);
            Assert.IsFalse(p.Rig.Animator.enabled);
            Assert.That(parts.Select(r => r.shadowCastingMode), Has.All.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off),
                "past the detail range a body is a few pixels tall: no shadow pass for its skinned meshes");
            p.SetDetail(true);
            Assert.IsTrue(p.Rig.Animator.enabled);
            Assert.That(parts.Select(r => r.shadowCastingMode), Has.All.EqualTo(UnityEngine.Rendering.ShadowCastingMode.On));
        }

        [Test]
        public void ThePeople_AreTheDownloadedBodies_WithTheKampungAnimator()
        {
            var keeper = fx.Director.Stalls[0].KeeperRig;
            Assert.IsNotNull(keeper, "the shop keeper is a real body");
            Assert.AreSame(library.controller, keeper.Animator.runtimeAnimatorController);
            Assert.IsTrue(keeper.Animator.isHuman);
            var parts = keeper.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.IsNotEmpty(parts);
            var bounds = parts[0].bounds;
            foreach (var part in parts) bounds.Encapsulate(part.bounds);
            Assert.That(bounds.max.y - keeper.Root.position.y, Is.InRange(1.4f, 1.9f), "about a person's height (all body parts together)");
            AssertFacesForward(keeper);
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
