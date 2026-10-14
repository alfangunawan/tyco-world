using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class MotorbikeVisualsTests
    {
        static readonly Color TailEmission = new Color(1f, 0.16f, 0.1f, 1f);

        VehicleFixture fx;
        MotorbikeVisuals visuals;
        Transform lean, bar, front, rear, rider;
        Light headlight;
        Renderer tail, lens;
        Material tailMaterial, lensMaterial;

        [SetUp]
        public void SetUp()
        {
            fx = new VehicleFixture();
            var root = fx.Track(new GameObject("Bike"));
            lean = Child("LeanPivot", root.transform);
            bar = Child("Handlebar", lean);
            front = Child("FrontWheel", bar);
            rear = Child("RearWheel", lean);
            rider = Child("Rider", lean);
            headlight = Child("Headlight", bar).gameObject.AddComponent<Light>();
            headlight.type = LightType.Spot;
            headlight.enabled = false;
            tailMaterial = fx.Track(EmissiveMaterial(TailEmission));
            lensMaterial = fx.Track(EmissiveMaterial(Color.white));
            tail = Child("TailLight", lean).gameObject.AddComponent<MeshRenderer>();
            tail.sharedMaterial = tailMaterial;
            lens = Child("HeadlightLens", bar).gameObject.AddComponent<MeshRenderer>();
            lens.sharedMaterial = lensMaterial;
            visuals = root.AddComponent<MotorbikeVisuals>();
            visuals.Configure(lean, bar, front, rear, rider, headlight, tail, lens);
            visuals.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            visuals.Shutdown();
            fx.Dispose();
        }

        static Transform Child(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Material EmissiveMaterial(Color emission)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        [Test]
        public void Initialize_HidesTheRiderAndTheHeadlight()
        {
            Assert.IsFalse(rider.gameObject.activeSelf);
            Assert.IsFalse(headlight.enabled);
            Assert.IsFalse(visuals.TailBright);
            Assert.IsFalse(visuals.LensOn);
        }

        [Test]
        public void Apply_SpinsWheels_SteersAndLeans()
        {
            visuals.Apply(3f, 20f, 10f, false, false, true, 0.1f);
            Assert.AreEqual(20f, bar.localEulerAngles.y, 1e-3f);
            Assert.AreEqual(350f, lean.localEulerAngles.z, 1e-3f, "miring kanan = rotasi z negatif");
            float expected = 3f / 0.3f * Mathf.Rad2Deg * 0.1f;
            Assert.AreEqual(expected, Quaternion.Angle(Quaternion.identity, front.localRotation), 0.01f);
            Assert.AreEqual(expected, Quaternion.Angle(Quaternion.identity, rear.localRotation), 0.01f);
            Assert.IsTrue(rider.gameObject.activeSelf);
        }

        [Test]
        public void Headlight_SwitchesTheLightAndTheLens()
        {
            visuals.Apply(0f, 0f, 0f, false, true, false, 0.02f);
            Assert.IsTrue(headlight.enabled);
            Assert.IsTrue(visuals.LensOn);
            Assert.AreNotSame(lensMaterial, lens.sharedMaterial);
            visuals.Apply(0f, 0f, 0f, false, false, false, 0.02f);
            Assert.IsFalse(headlight.enabled);
            Assert.IsFalse(visuals.LensOn);
        }

        [Test]
        public void TailLight_BrightensWhenBraking_WithoutTouchingTheMaterial()
        {
            visuals.Apply(5f, 0f, 0f, true, false, true, 0.02f);
            Assert.IsTrue(visuals.TailBright);
            Assert.AreNotSame(tailMaterial, tail.sharedMaterial);
            Assert.AreEqual(TailEmission, tail.sharedMaterial.GetColor("_EmissionColor"));
            Assert.AreNotEqual(0, (int)(tail.sharedMaterial.hideFlags & HideFlags.DontSave));
            visuals.Apply(5f, 0f, 0f, false, false, true, 0.02f);
            Assert.IsFalse(visuals.TailBright);
            Assert.AreEqual(TailEmission * 0.35f, tail.sharedMaterial.GetColor("_EmissionColor"));
            Assert.AreEqual(TailEmission, tailMaterial.GetColor("_EmissionColor"), "material asli tidak berubah");
        }

        [Test]
        public void Shutdown_RestoresTheOriginalMaterials()
        {
            visuals.Apply(5f, 0f, 0f, true, true, true, 0.02f);
            visuals.Shutdown();
            Assert.AreSame(tailMaterial, tail.sharedMaterial);
            Assert.AreSame(lensMaterial, lens.sharedMaterial);
        }
    }
}
