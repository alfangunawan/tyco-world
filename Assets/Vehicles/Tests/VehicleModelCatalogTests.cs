using System.Linq;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>The per-model table (spec vehicles-3a §1, §3).</summary>
    public class VehicleModelCatalogTests
    {
        [Test]
        public void Catalog_ListsEveryModel_OnceEach_WithItsClassAndLength()
        {
            var all = VehicleModelCatalog.All;
            CollectionAssert.AreEqual(VehicleModelFiles.Ids, all.Select(s => s.Id).ToList());
            var expected = new (string id, VehicleClass c, float length)[]
            {
                ("vario", VehicleClass.MotorMatic, 1.92f), ("supra", VehicleClass.MotorBebek, 1.89f), ("sedan", VehicleClass.CarSmall, 4.45f),
                ("hatch", VehicleClass.CarSmall, 3.80f), ("lmpv1", VehicleClass.Mpv, 4.40f), ("lmpv2", VehicleClass.Mpv, 4.10f),
                ("mpv2", VehicleClass.Mpv, 4.70f), ("van", VehicleClass.Van, 4.05f), ("l300", VehicleClass.Pickup, 4.30f),
                ("pickup", VehicleClass.Pickup, 5.30f), ("police", VehicleClass.Police, 4.45f), ("angkot", VehicleClass.Angkot, 3.90f),
                ("carry", VehicleClass.Pickup, 3.40f), ("elf", VehicleClass.Truck, 6.00f)
            };
            foreach (var (id, c, length) in expected)
            {
                var s = VehicleModelCatalog.Get(id);
                Assert.IsNotNull(s, id);
                Assert.AreEqual(c, s.Class, id);
                Assert.AreEqual(length, s.Length, 1e-4f, id);
            }
        }

        [Test]
        public void EveryWheelAndSteerNode_ExistsInItsModel()
        {
            foreach (var s in VehicleModelCatalog.All)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path(s.Id));
                Assert.IsNotNull(model, s.Id);
                var names = model.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
                foreach (var n in s.FrontWheels.Concat(s.RearWheels)) CollectionAssert.Contains(names, n, s.Id + " wheel");
                if (!string.IsNullOrEmpty(s.SteerNode)) CollectionAssert.Contains(names, s.SteerNode, s.Id + " steer");
                foreach (var n in s.ExtraSteerNodes) CollectionAssert.Contains(names, n, s.Id + " extra steer");
                if (s.KeepRegion.HasValue) Assert.Greater(s.KeepRegion.Value.size.x * s.KeepRegion.Value.size.y * s.KeepRegion.Value.size.z, 0f, s.Id + " region");
                Assert.IsTrue(s.WheelsMerged || (s.FrontWheels.Length > 0 && s.RearWheels.Length > 0), s.Id + ": wheels named, or marked merged");
                Assert.That(s.PaintStrength, Is.InRange(0f, 1f), s.Id);
            }
        }

        [Test]
        public void Classes_KnowBikesAndNouns()
        {
            Assert.IsTrue(VehicleClasses.IsBike(VehicleClass.MotorMatic));
            Assert.IsTrue(VehicleClasses.IsBike(VehicleClass.MotorBebek));
            Assert.IsFalse(VehicleClasses.IsBike(VehicleClass.Angkot));
            Assert.AreEqual("motor", VehicleClasses.Noun(VehicleClass.MotorBebek));
            Assert.AreEqual("mobil", VehicleClasses.Noun(VehicleClass.Mpv));
            Assert.AreEqual("mobil", VehicleClasses.Noun(VehicleClass.Police));
            Assert.AreEqual("angkot", VehicleClasses.Noun(VehicleClass.Angkot));
            Assert.AreEqual("pikap", VehicleClasses.Noun(VehicleClass.Pickup));
            Assert.AreEqual("truk", VehicleClasses.Noun(VehicleClass.Truck));
        }
    }
}
