using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class StreetLampSystemTests
    {
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

        StreetLamp Lamp(LampRole role)
        {
            var lamp = LampFixture.Create(null, LampFixture.PositionWithRole(role, profile));
            created.Add(lamp.gameObject);
            return lamp;
        }

        [Test]
        public void Night_EnablesLight_WithScaledIntensity()
        {
            var lamp = Lamp(LampRole.Normal);
            system.Collect(new[] { lamp });
            system.Apply(0.5f, 0f, 0f);
            Assert.IsTrue(lamp.Light.enabled);
            Assert.AreEqual(1.5f, lamp.Light.intensity, 1e-4f);
            Assert.AreEqual(1, system.LitCount);
        }

        [Test]
        public void Day_DisablesLight_AndDarkensHead()
        {
            var lamp = Lamp(LampRole.Normal);
            system.Collect(new[] { lamp });
            system.Apply(0f, 0f, 0f);
            Assert.IsFalse(lamp.Light.enabled);
            var head = lamp.HeadRenderer.sharedMaterials[1];
            Assert.AreNotSame(LampFixture.HeadAsset, head);
            Assert.AreEqual(Color.black, head.GetColor("_EmissionColor"));
            Assert.AreEqual(0, system.LitCount);
        }

        [Test]
        public void Apply_NeverTouchesSharedHeadMaterial()
        {
            var asset = LampFixture.HeadAsset;
            var before = asset.GetColor("_EmissionColor");
            var lamp = Lamp(LampRole.Normal);
            system.Collect(new[] { lamp });
            system.Apply(1f, 0f, 0f);
            var lit = lamp.HeadRenderer.sharedMaterials[1];
            Assert.AreNotSame(asset, lit);
            Assert.AreEqual(before, lit.GetColor("_EmissionColor"));
            system.Apply(0.25f, 0f, 0f);
            system.Apply(0f, 0f, 0f);
            Assert.AreEqual(before, asset.GetColor("_EmissionColor"));
            Assert.IsFalse(EditorUtility.IsDirty(asset));
            system.Dispose();
            Assert.AreSame(asset, lamp.HeadRenderer.sharedMaterials[1]);
        }

        [Test]
        public void Horror_DeadLampStaysOff_NormalStaysOn()
        {
            var dead = Lamp(LampRole.Dead);
            var normal = Lamp(LampRole.Normal);
            system.Collect(new[] { dead, normal });
            for (float t = 0f; t < 5f; t += 0.05f)
            {
                system.Apply(1f, 1f, t);
                Assert.IsFalse(dead.Light.enabled);
                Assert.IsTrue(normal.Light.enabled);
            }
        }

        [Test]
        public void FlickerLamp_BlinksOnlyDuringHorror()
        {
            var lamp = Lamp(LampRole.Flicker);
            system.Collect(new[] { lamp });
            for (float t = 0f; t < 10f; t += 0.01f)
            {
                system.Apply(1f, 0f, t);
                Assert.IsTrue(lamp.Light.enabled, "tanpa horor lampu tidak boleh berkedip");
            }
            int off = 0, samples = 0;
            for (float t = 0f; t < 10f; t += 0.01f)
            {
                system.Apply(1f, 1f, t);
                samples++;
                if (!lamp.Light.enabled) off++;
            }
            Assert.Greater(off, 0);
            Assert.Less(off, samples / 2);
        }

        [Test]
        public void LampWithoutLight_IsSkipped()
        {
            var go = new GameObject("NoLight");
            created.Add(go);
            var lamp = go.AddComponent<StreetLamp>();
            system.Collect(new[] { lamp });
            Assert.IsTrue(system.Collected);
            Assert.AreEqual(0, system.Count);
            Assert.DoesNotThrow(() => system.Apply(1f, 1f, 0f));
        }

        [Test]
        public void Roles_AreCounted()
        {
            system.Collect(new[] { Lamp(LampRole.Dead), Lamp(LampRole.Flicker), Lamp(LampRole.Normal) });
            Assert.AreEqual(3, system.Count);
            Assert.AreEqual(1, system.RoleCount(LampRole.Dead));
            Assert.AreEqual(1, system.RoleCount(LampRole.Flicker));
            Assert.AreEqual(1, system.RoleCount(LampRole.Normal));
        }
    }
}
