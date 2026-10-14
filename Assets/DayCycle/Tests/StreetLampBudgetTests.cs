using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class StreetLampBudgetTests
    {
        static readonly Vector3 Base = new Vector3(5000f, 1000f, 5000f);
        readonly List<GameObject> created = new List<GameObject>();
        DayCycleProfile profile;
        StreetLampSystem system;

        [SetUp]
        public void SetUp()
        {
            profile = DayCycleProfile.CreateDefault();
            system = new StreetLampSystem(profile);
        }

        [TearDown]
        public void TearDown()
        {
            system.Dispose();
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            Object.DestroyImmediate(profile);
        }

        StreetLamp LampAt(float x) => Track(LampFixture.Create(null, Base + new Vector3(x, 0f, 0f)));

        StreetLamp Track(StreetLamp lamp)
        {
            created.Add(lamp.gameObject);
            return lamp;
        }

        float Full => profile.lampIntensity;

        [Test]
        public void NoBudget_EveryLitLampEmits()
        {
            var a = LampAt(10f);
            var b = LampAt(20f);
            system.Collect(new[] { a, b });
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f);
            Assert.IsTrue(a.Light.enabled && b.Light.enabled);
            Assert.AreEqual(2, system.LightsOn);
        }

        [Test]
        public void Budget_OnlyTheNearestEmit_HeadsStayLit()
        {
            var lamps = new[] { LampAt(10f), LampAt(20f), LampAt(30f), LampAt(40f), LampAt(50f) };
            system.Collect(lamps);
            system.LightBudget = 2;
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f);
            Assert.IsTrue(lamps[0].Light.enabled);
            Assert.IsTrue(lamps[1].Light.enabled);
            for (int i = 2; i < lamps.Length; i++) Assert.IsFalse(lamps[i].Light.enabled, "lampu " + i);
            Assert.AreEqual(2, system.LightsOn);
            Assert.AreEqual(5, system.LitCount);
            Assert.AreSame(lamps[0].HeadRenderer.sharedMaterials[1], lamps[4].HeadRenderer.sharedMaterials[1], "kepala lampu jauh tetap menyala");
        }

        [Test]
        public void MaxLightDistance_LimitsTheBudget()
        {
            var lamps = new[] { LampAt(10f), LampAt(20f), LampAt(30f) };
            system.Collect(lamps);
            system.LightBudget = 10;
            system.MaxLightDistance = 25f;
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f);
            Assert.IsTrue(lamps[0].Light.enabled);
            Assert.IsTrue(lamps[1].Light.enabled);
            Assert.IsFalse(lamps[2].Light.enabled);
        }

        [Test]
        public void Selection_FadesOverHalfASecond()
        {
            var a = LampAt(0f);
            var b = LampAt(40f);
            system.Collect(new[] { a, b });
            system.LightBudget = 1;
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f);
            Assert.AreEqual(Full, a.Light.intensity, 1e-4f);
            Assert.IsFalse(b.Light.enabled);

            system.Viewer = Base + new Vector3(40f, 0f, 0f);
            system.Apply(1f, 0f, 0f, 0.25f);
            Assert.IsTrue(a.Light.enabled);
            Assert.IsTrue(b.Light.enabled);
            Assert.AreEqual(Full * 0.5f, a.Light.intensity, 1e-4f);
            Assert.AreEqual(Full * 0.5f, b.Light.intensity, 1e-4f);

            system.Apply(1f, 0f, 0f, 0.25f);
            Assert.IsFalse(a.Light.enabled);
            Assert.AreEqual(Full, b.Light.intensity, 1e-4f);
        }

        [Test]
        public void ViewerJump_ReselectsImmediately()
        {
            var a = LampAt(0f);
            var c = LampAt(60f);
            system.Collect(new[] { a, c });
            system.LightBudget = 1;
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f);
            Assert.IsFalse(c.Light.enabled);
            system.Viewer = Base + new Vector3(60f, 0f, 0f);
            system.Apply(1f, 0f, 0f, 0.01f);
            Assert.IsTrue(c.Light.enabled);
        }

        [Test]
        public void FirstFrameAfterCollect_DoesNotFade()
        {
            var a = LampAt(0f);
            var b = LampAt(30f);
            system.Collect(new[] { a, b });
            system.LightBudget = 1;
            system.Viewer = Base;
            system.Apply(1f, 0f, 0f, 0.016f);
            Assert.IsTrue(a.Light.enabled);
            Assert.IsFalse(b.Light.enabled, "lampu di luar jatah langsung mati, tidak memudar dari menyala");
        }

        [Test]
        public void HorrorDeadLamps_DoNotUseTheBudget()
        {
            var dead = Track(LampFixture.Create(null, LampFixture.PositionWithRole(LampRole.Dead, profile)));
            var normal = Track(LampFixture.Create(null, LampFixture.PositionWithRole(LampRole.Normal, profile)));
            system.Collect(new[] { dead, normal });
            system.LightBudget = 1;
            system.Viewer = dead.transform.position;
            system.Apply(1f, 1f, 0f);
            Assert.IsFalse(dead.Light.enabled);
            Assert.IsTrue(normal.Light.enabled);
        }

        [Test]
        public void NoViewer_KeepsEveryLampEmitting()
        {
            var a = LampAt(0f);
            var b = LampAt(30f);
            system.Collect(new[] { a, b });
            system.LightBudget = 1;
            system.Viewer = null;
            system.Apply(1f, 0f, 0f);
            Assert.IsTrue(a.Light.enabled && b.Light.enabled);
        }

        [Test]
        public void Daytime_NoLightsEvenWithABudget()
        {
            var a = LampAt(0f);
            system.Collect(new[] { a });
            system.LightBudget = 16;
            system.Viewer = Base;
            system.Apply(0f, 0f, 0f);
            Assert.IsFalse(a.Light.enabled);
            Assert.AreEqual(0, system.LightsOn);
        }

        [Test]
        public void Intensity_FollowsTheCycle()
        {
            var a = LampAt(0f);
            system.Collect(new[] { a });
            system.LightBudget = 1;
            system.Viewer = Base;
            system.Apply(0.5f, 0f, 0f);
            Assert.AreEqual(Full * 0.5f, a.Light.intensity, 1e-4f);
        }
    }
}
