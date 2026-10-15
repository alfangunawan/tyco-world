using System.Linq;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>All model prefabs, their tuning and the catalog (spec vehicles-3a §4–§5).</summary>
    public class VehicleModelAssetsTests
    {
        [Test]
        public void Catalog_HasEveryModel_WithPrefabAndClassTuning()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(VehiclePaths.CatalogAsset);
            Assert.IsNotNull(catalog, "run VehicleAssetsBuilder.BuildAll");
            foreach (var spec in VehicleModelCatalog.All)
            {
                var e = catalog.Get(spec.Id);
                Assert.IsNotNull(e, spec.Id);
                Assert.AreEqual(spec.Class, e.vehicleClass);
                Assert.IsNotNull(e.prefab, spec.Id);
                var bike = e.prefab.GetComponent<Motorbike>();
                Assert.AreEqual(VehicleClassPresets.MaxKmh(spec.Class) / BikeTuning.KmhPerMs, bike.Tuning.maxForwardSpeed, 1e-3f, spec.Id);
                Assert.IsTrue(AssetDatabase.Contains(bike.Tuning), spec.Id + " tuning is an asset");
                CollectionAssert.AreEqual(spec.PaintMaterials, e.paintMaterials, spec.Id);
                Assert.AreEqual(spec.PaintStrength, e.paintStrength, 1e-4f, spec.Id);
                Assert.IsNull(e.prefab.GetComponent<MotorbikeInteractable>(), "warga vehicles get their lock from gameplay");
            }
        }

        [Test]
        public void EachPrefab_KeepsItsOwnLampMeshes()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(VehiclePaths.CatalogAsset);
            Mesh Tail(string id) => catalog.Get(id).prefab.GetComponentsInChildren<MeshFilter>(true)
                .First(f => f.name == VehicleModelBuilder.TailName).sharedMesh;
            Assert.AreNotSame(Tail("sedan"), Tail("elf"), "lamp meshes are saved per model, not shared by name");
            Assert.AreNotSame(Tail("vario"), Tail("elf"));
        }

        [Test]
        public void MissingSource_IsSkippedAndCounted()
        {
            var spec = new VehicleModelSpec { Id = "does-not-exist", Class = VehicleClass.CarSmall, Length = 4f };
            Assert.IsNull(VehicleModelAssets.BuildOne(spec, null, VehicleMaterials.LoadOrCreate(), new VehicleClipSet(), out bool missing));
            Assert.IsTrue(missing);
        }
    }
}
