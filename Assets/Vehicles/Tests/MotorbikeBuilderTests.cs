using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class MotorbikeBuilderTests
    {
        VehicleFixture fx;
        BikeTuning tuning;
        VehicleMaterialSet materials;
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            fx = new VehicleFixture();
            tuning = fx.Track(ScriptableObject.CreateInstance<BikeTuning>());
            var plain = fx.Track(new Material(Shader.Find("Universal Render Pipeline/Simple Lit")));
            var glow = fx.Track(new Material(plain));
            materials = new VehicleMaterialSet
            {
                Body = plain, Dark = plain, Chrome = plain, TailLight = glow, HeadlightLens = glow,
                Shirt = plain, Pants = plain, Skin = plain, Helmet = plain
            };
            root = fx.Track(new GameObject("Motorbike"));
            MotorbikeBuilder.Build(root, tuning, materials, new VehicleClipSet());
            TrackMeshes();
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        void TrackMeshes()
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) fx.Track(filter.sharedMesh);
        }

        [Test]
        public void Build_WiresTheRig()
        {
            var bike = root.GetComponent<Motorbike>();
            var visuals = root.GetComponent<MotorbikeVisuals>();
            Assert.IsNotNull(root.GetComponent<CharacterController>());
            Assert.AreSame(tuning, bike.Tuning);
            Assert.AreSame(visuals, bike.Visuals);
            Assert.AreSame(root.GetComponent<MotorbikeAudio>(), bike.Sound);
            Assert.AreEqual(MotorbikeBuilder.InteractionId, root.GetComponent<MotorbikeInteractable>().InteractionId);
            Assert.AreSame(root.transform, bike.Seat.parent, "kursi tidak ikut miring");
            Assert.AreEqual(new Vector3(0f, 0.86f, -0.2f), bike.Seat.localPosition);
            Assert.AreSame(root.transform, visuals.LeanPivot.parent);
            Assert.AreSame(visuals.LeanPivot, visuals.Handlebar.parent);
            Assert.AreSame(visuals.Handlebar, visuals.FrontWheel.parent);
            Assert.AreSame(visuals.LeanPivot, visuals.RearWheel.parent);
            Assert.AreSame(visuals.LeanPivot, visuals.Rider.parent);
            Assert.AreSame(visuals.Handlebar, visuals.Headlight.transform.parent);
            Assert.IsFalse(visuals.Rider.gameObject.activeSelf);
            Assert.AreSame(materials.TailLight, visuals.TailLight.sharedMaterial);
            Assert.AreSame(materials.HeadlightLens, visuals.HeadlightLens.sharedMaterial);
        }

        [Test]
        public void Build_Twice_ReusesTheSameObjects()
        {
            int children = root.transform.childCount;
            var seat = root.GetComponent<Motorbike>().Seat;
            var bar = root.GetComponent<MotorbikeVisuals>().Handlebar;
            var rider = root.GetComponent<MotorbikeVisuals>().Rider;
            MotorbikeBuilder.Build(root, tuning, materials, new VehicleClipSet());
            TrackMeshes();
            Assert.AreEqual(children, root.transform.childCount);
            Assert.AreSame(seat, root.GetComponent<Motorbike>().Seat, "anak dipakai ulang supaya file id prefab tetap");
            Assert.AreSame(bar, root.GetComponent<MotorbikeVisuals>().Handlebar);
            Assert.AreSame(rider, root.GetComponent<MotorbikeVisuals>().Rider);
            Assert.IsFalse(rider.gameObject.activeSelf);
            Assert.AreEqual(1, bar.GetComponents<MeshFilter>().Length);
            Assert.AreEqual(1, root.GetComponents<CharacterController>().Length);
            Assert.AreEqual(1, root.GetComponents<Motorbike>().Length);
            Assert.AreEqual(1, root.GetComponents<MotorbikeVisuals>().Length);
            Assert.AreEqual(1, root.GetComponents<MotorbikeAudio>().Length);
            Assert.AreEqual(1, root.GetComponents<MotorbikeInteractable>().Length);
            Assert.AreEqual(2, root.GetComponentsInChildren<AudioSource>(true).Length);
            Assert.AreEqual(1, root.GetComponentsInChildren<Light>(true).Length);
        }

        [Test]
        public void Wheels_SpinInPlace_AndTouchTheGround()
        {
            var visuals = root.GetComponent<MotorbikeVisuals>();
            foreach (var wheel in new[] { visuals.FrontWheel, visuals.RearWheel })
            {
                var bounds = wheel.GetComponent<MeshFilter>().sharedMesh.bounds;
                Assert.Less(bounds.center.magnitude, 0.01f, wheel.name + " berputar di porosnya");
                Assert.AreEqual(0.1f, bounds.size.x, 0.01f, wheel.name);
                Assert.AreEqual(0.6f, bounds.size.y, 0.01f, wheel.name);
                Assert.AreEqual(0.3f, wheel.position.y - root.transform.position.y, 0.01f, wheel.name + " menyentuh tanah");
            }
            Assert.AreEqual(1.3f, visuals.FrontWheel.position.z - visuals.RearWheel.position.z, 0.01f);
        }

        [Test]
        public void Headlight_AndAudio_FollowTheSpec()
        {
            var light = root.GetComponent<MotorbikeVisuals>().Headlight;
            Assert.AreEqual(LightType.Spot, light.type);
            Assert.AreEqual(30f, light.range, 1e-4f);
            Assert.AreEqual(60f, light.spotAngle, 1e-4f);
            Assert.AreEqual(4f, light.intensity, 1e-4f);
            Assert.AreEqual(LightShadows.None, light.shadows);
            Assert.IsFalse(light.enabled);
            var audio = root.GetComponent<MotorbikeAudio>();
            Assert.AreNotSame(audio.EngineSource, audio.OneShotSource);
            foreach (var source in new[] { audio.EngineSource, audio.OneShotSource })
            {
                Assert.IsFalse(source.playOnAwake);
                Assert.AreEqual(1f, source.spatialBlend);
                Assert.AreEqual(3f, source.minDistance, 1e-4f);
                Assert.AreEqual(40f, source.maxDistance, 1e-4f);
            }
            Assert.IsTrue(audio.EngineSource.loop);
        }

        [Test]
        public void OnlyTheBodyCollides()
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length);
            Assert.IsInstanceOf<CharacterController>(colliders[0]);
            var body = (CharacterController)colliders[0];
            Assert.AreEqual(0.5f, body.radius, 1e-4f);
            Assert.AreEqual(1.4f, body.height, 1e-4f);
        }
    }
}
