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

        /// <summary>Runs the animator (and its IK) and the director, which keeps the hips on the seat, a few times.</summary>
        void Settle(CharacterRig rig)
        {
            var animator = rig.Animator;
            var culling = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int i = 0; i < 6; i++)
            {
                animator.Update(0.2f);
                fx.Director.Tick(0.02f);
            }
            animator.Update(0f);
            animator.cullingMode = culling;
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
        public void EveryBody_HasItsHumanoidHips_AtThePelvis_NotTheGroundRoot()
        {
            foreach (var body in library.bodies.Concat(library.womenBodies ?? new GameObject[0]))
            {
                var avatar = body.GetComponent<Animator>() != null ? body.GetComponent<Animator>().avatar : null;
                if (avatar == null)
                    foreach (var a in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(body)).OfType<Avatar>()) avatar = a;
                Assert.IsNotNull(avatar, body.name);
                string hips = avatar.humanDescription.human.First(h => h.humanName == "Hips").boneName;
                Assert.AreEqual("Hips", hips, body.name + ": the legs hang under the pelvis (Modular Women's hang off Root, at the ground)");
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

        /// <summary>A bare bike: just its <see cref="RideAnchors"/> at the code scooter's layout.</summary>
        static RideAnchors BareBike(Transform parent)
        {
            var bike = new GameObject("BareBike");
            bike.transform.SetParent(parent, false);
            var a = bike.AddComponent<RideAnchors>();
            Transform At(Vector3 local)
            {
                var t = new GameObject("Anchor").transform;
                t.SetParent(bike.transform, false);
                t.localPosition = local;
                return t;
            }
            Vector3 Left(Vector3 v) => new Vector3(-v.x, v.y, v.z);
            a.seat = At(RideAnchors.Seat);
            a.gripRight = At(RideAnchors.BarOnBike + RideAnchors.GripOnBar);
            a.gripLeft = At(Left(RideAnchors.BarOnBike + RideAnchors.GripOnBar));
            a.footRight = At(RideAnchors.Foot);
            a.footLeft = At(Left(RideAnchors.Foot));
            a.pillion = At(RideAnchors.Pillion);
            a.pillionFootRight = At(RideAnchors.PillionFoot);
            a.pillionFootLeft = At(Left(RideAnchors.PillionFoot));
            return a;
        }

        [Test]
        public void OnABike_EveryBody_PutsItsFeetDown_AndARiderTakesTheGrips([Values(false, true)] bool asRider)
        {
            var parent = new GameObject("RideTest").transform;
            try
            {
                foreach (var model in library.bodies.Concat(library.womenBodies ?? new GameObject[0]))
                {
                    var a = BareBike(parent);
                    var one = ScriptableObject.CreateInstance<CharacterLibrary>();
                    one.controller = library.controller;
                    one.bodies = new[] { model };
                    var rig = CharacterFactory.Spawn(one, a.transform, model.name, 0x51UL, fx.Director.Palette);
                    Object.DestroyImmediate(one);
                    rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    rig.Ride(a, asRider);
                    var seat = asRider ? a.seat : a.pillion;
                    for (int i = 0; i < 12; i++)
                    {
                        rig.Animator.Update(0.2f);
                        // As RiderBodySystem and PassengerRide do: hips held on the seat.
                        var hipsLocal = a.transform.InverseTransformPoint(rig.Bone(HumanBodyBones.Hips).position) - rig.Root.localPosition;
                        rig.Root.localPosition = a.transform.InverseTransformPoint(seat.position) - hipsLocal;
                    }
                    rig.Animator.Update(0f);
                    Assert.Greater(rig.IkPasses, 0, model.name + ": the IK pass runs");
                    Assert.Less(Vector3.Distance(rig.Bone(HumanBodyBones.LeftFoot).position, (asRider ? a.footLeft : a.pillionFootLeft).position), 0.06f, model.name + " left foot");
                    Assert.Less(Vector3.Distance(rig.Bone(HumanBodyBones.RightFoot).position, (asRider ? a.footRight : a.pillionFootRight).position), 0.06f, model.name + " right foot");
                    var knee = a.transform.InverseTransformPoint(rig.Bone(HumanBodyBones.LeftLowerLeg).position);
                    var hips = a.transform.InverseTransformPoint(rig.Bone(HumanBodyBones.Hips).position);
                    Assert.Greater(knee.z, hips.z, model.name + ": knees forward of the hips, legs folded over the footboard");
                    if (!asRider) continue;
                    Assert.Less(Vector3.Distance(rig.Bone(HumanBodyBones.LeftHand).position, a.gripLeft.position), 0.12f, model.name + " left hand on its grip");
                    Assert.Less(Vector3.Distance(rig.Bone(HumanBodyBones.RightHand).position, a.gripRight.position), 0.12f, model.name + " right hand on its grip");
                }
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
            }
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
            var anchors = RideAnchors.Of(bike);
            Assert.AreSame(anchors, rig.Riding);
            Assert.IsFalse(rig.IsRider);
            Settle(rig);
            Assert.Less(Vector3.Distance(rig.Bone(HumanBodyBones.Hips).position, anchors.pillion.position), 0.12f, "hips on the pillion");

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
            var body = riders.Body;
            var anchors = RideAnchors.Of(bike);
            Assert.IsTrue(body.IsRider, "sitting astride, not the car's Driving_Loop");
            Assert.IsFalse(body.IsDriving);
            Assert.IsTrue(body.transform.IsChildOf(bike.transform));
            Settle(body);
            Assert.Less(Vector3.Distance(body.Bone(HumanBodyBones.Hips).position, anchors.seat.position), 0.12f, "hips on the front of the seat");
            Assert.Less(Vector3.Distance(body.Bone(HumanBodyBones.LeftHand).position, anchors.gripLeft.position), 0.12f, "left hand on its grip");
            Assert.Less(Vector3.Distance(body.Bone(HumanBodyBones.RightHand).position, anchors.gripRight.position), 0.12f, "right hand on its grip");
            Assert.Less(Vector3.Distance(body.Bone(HumanBodyBones.LeftFoot).position, anchors.footLeft.position), 0.12f, "left foot on the footboard");
            Assert.Less(Vector3.Distance(body.Bone(HumanBodyBones.RightFoot).position, anchors.footRight.position), 0.12f, "right foot on the footboard");
            bike.CameraState.Toggle();
            Tick();
            Assert.IsFalse(riders.Visible, "first person: hidden");
        }
    }
}
