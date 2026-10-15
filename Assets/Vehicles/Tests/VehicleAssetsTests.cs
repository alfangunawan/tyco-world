using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>The saved prefab and its assets (written by "Tyco World/Vehicle Setup" / VehicleAssetsBuilder.BuildAll).</summary>
    public class VehicleAssetsTests
    {
        static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePaths.MotorbikePrefab);
            Assert.IsNotNull(prefab, "Prefab belum ada; jalankan VehicleAssetsBuilder.BuildAll");
            return prefab;
        }

        [Test]
        public void Prefab_UsesOnlySavedAssets()
        {
            var prefab = Prefab();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                Assert.IsTrue(filter.sharedMesh != null && AssetDatabase.Contains(filter.sharedMesh), filter.name);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    Assert.IsTrue(material != null && AssetDatabase.Contains(material), renderer.name);
        }

        [Test]
        public void Prefab_HasTheTuningAndEveryClip()
        {
            var prefab = Prefab();
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<BikeTuning>(VehiclePaths.TuningAsset), prefab.GetComponent<Motorbike>().Tuning);
            var audio = new SerializedObject(prefab.GetComponent<MotorbikeAudio>());
            foreach (var field in new[] { "engineClip", "starterClip", "hornClip", "bumpClip" })
                Assert.IsNotNull(audio.FindProperty(field).objectReferenceValue, field);
        }

        [Test]
        public void KosBike_IsTheVario_AndKeepsItsInteraction()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePaths.MotorbikePrefab);
            Assert.IsNotNull(prefab.transform.Find(VehicleModelBuilder.LeanName + "/" + VehicleModelBuilder.ModelName), "the Vario model");
            Assert.IsNull(prefab.transform.Find(MotorbikeBuilder.LeanPivotName + "/" + MotorbikeBuilder.BodyName), "no code-built body left");
            Assert.AreEqual(MotorbikeBuilder.InteractionId, prefab.GetComponent<MotorbikeInteractable>().InteractionId);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<BikeTuning>(VehiclePaths.TuningAsset), prefab.GetComponent<Motorbike>().Tuning);
            Assert.IsNotNull(prefab.GetComponentInChildren<RideAnchors>(true));
        }

        [Test]
        public void LightMaterials_AreEmissive()
        {
            var visuals = Prefab().GetComponent<MotorbikeVisuals>();
            foreach (var renderer in new[] { visuals.TailLight, visuals.HeadlightLens })
            {
                var material = renderer.sharedMaterial;
                Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), material.name);
                Assert.Greater(material.GetColor("_EmissionColor").maxColorComponent, 0.5f, material.name);
            }
        }
    }
}
