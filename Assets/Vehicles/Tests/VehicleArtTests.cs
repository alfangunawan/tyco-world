using System.IO;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>The downloaded vehicle models (spec vehicles-3a §1–§2): imported by glTFast, credited in SOURCES.md.</summary>
    public class VehicleArtTests
    {
        [Test]
        public void EveryModel_ImportsAsAGameObjectWithMeshes()
        {
            Assert.AreEqual(14, VehicleModelFiles.Ids.Count);
            foreach (var id in VehicleModelFiles.Ids)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path(id));
                Assert.IsNotNull(go, id + " (" + VehicleModelFiles.Path(id) + ")");
                Assert.IsNotEmpty(go.GetComponentsInChildren<MeshFilter>(true), id + " has meshes");
            }
        }

        [Test]
        public void EveryModel_IsCreditedInItsFolder()
        {
            foreach (var id in VehicleModelFiles.Ids)
            {
                string folder = Path.GetDirectoryName(VehicleModelFiles.Path(id));
                string sources = File.ReadAllText(Path.Combine(folder, "SOURCES.md"));
                StringAssert.Contains(Path.GetFileName(VehicleModelFiles.Path(id)), sources, id);
                StringAssert.Contains("CC Attribution 4.0", sources, id);
                StringAssert.Contains("sketchfab.com/models/", sources, id);
            }
        }
    }
}
