using System.Linq;
using NUnit.Framework;
using TycoWorld.Vehicles;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    /// <summary>Warga vehicles from the model catalog (spec vehicles-3a §6).</summary>
    public class VehicleFactoryModelTests
    {
        [Test]
        public void ParkedModels_AreDeterministic_AndFromTheRightKind()
        {
            var bikes = new[] { "vario", "supra" };
            var cars = new[] { "sedan", "hatch", "lmpv1", "lmpv2", "mpv2", "van", "l300", "pickup", "carry", "angkot" };
            for (int i = 0; i < 40; i++)
            {
                Assert.AreEqual(VehicleFactory.ParkedModelId(i, VehicleKind.Bike), VehicleFactory.ParkedModelId(i, VehicleKind.Bike));
                CollectionAssert.Contains(bikes, VehicleFactory.ParkedModelId(i, VehicleKind.Bike));
                CollectionAssert.Contains(cars, VehicleFactory.ParkedModelId(i, VehicleKind.Car));
            }
            Assert.AreEqual(2, Enumerable.Range(0, 40).Select(i => VehicleFactory.ParkedModelId(i, VehicleKind.Bike)).Distinct().Count());
            Assert.GreaterOrEqual(Enumerable.Range(0, 40).Select(i => VehicleFactory.ParkedModelId(i, VehicleKind.Car)).Distinct().Count(), 8);
        }

        [Test]
        public void MissingPrefab_FallsBackToTheCodeVehicle()
        {
            var parent = new GameObject("Parked").transform;
            var catalog = ScriptableObject.CreateInstance<VehicleCatalog>();
            var palette = new Palette(null);
            var tint = new VehicleTint();
            var carTuning = VehicleFactory.CarTuning(null);
            try
            {
                var car = VehicleFactory.FromCatalog(parent, catalog, "sedan", tint, new Color32(200, 0, 0, 255), palette, carTuning, null);
                Assert.IsNotNull(car);
                Assert.IsNotNull(car.transform.Find(VehicleFactory.PaintName), "the code-built car");
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(carTuning);
                tint.Dispose();
                palette.Dispose();
            }
        }

        [Test]
        public void IsCar_TellsCarsFromBikes_ModelAndCodeBuilt()
        {
            // Model bikes carry a hull too (spec vehicles-3a §4), so a hull alone no longer means a car.
            var catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(VehiclePaths.CatalogAsset);
            Assert.IsNotNull(catalog, "run VehicleAssetsBuilder.BuildAll");
            var parent = new GameObject("Parked").transform;
            var palette = new Palette(null);
            var carTuning = VehicleFactory.CarTuning(null);
            try
            {
                Assert.IsFalse(VehicleFactory.IsCar(Object.Instantiate(catalog.Get("vario").prefab, parent).GetComponent<Motorbike>()), "Vario");
                Assert.IsFalse(VehicleFactory.IsCar(Object.Instantiate(catalog.Get("supra").prefab, parent).GetComponent<Motorbike>()), "Supra");
                Assert.IsTrue(VehicleFactory.IsCar(Object.Instantiate(catalog.Get("sedan").prefab, parent).GetComponent<Motorbike>()), "sedan");
                Assert.IsTrue(VehicleFactory.IsCar(VehicleFactory.Car(parent, carTuning, palette, new Color32(200, 0, 0, 255))), "code car");
                Assert.IsFalse(VehicleFactory.IsCar(VehicleFactory.Bike(parent, null, palette, new Color32(200, 0, 0, 255))), "code bike");
                Assert.IsFalse(VehicleFactory.IsCar(AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePaths.MotorbikePrefab).GetComponent<Motorbike>()), "kos Vario");
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
                Object.DestroyImmediate(carTuning);
                palette.Dispose();
            }
        }

        [Test]
        public void IsCar_Holds_AfterRideAnchorsAreAskedForACar()
        {
            // A galon job asks RideAnchors.Of for whatever is ridden, cars included; that must not turn a car into a bike.
            var catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(VehiclePaths.CatalogAsset);
            var parent = new GameObject("Parked").transform;
            var palette = new Palette(null);
            var carTuning = VehicleFactory.CarTuning(null);
            try
            {
                var sedan = Object.Instantiate(catalog.Get("sedan").prefab, parent).GetComponent<Motorbike>();
                var code = VehicleFactory.Car(parent, carTuning, palette, new Color32(200, 0, 0, 255));
                RideAnchors.Of(sedan);
                RideAnchors.Of(code);
                Assert.IsTrue(VehicleFactory.IsCar(sedan), "sedan");
                Assert.IsTrue(VehicleFactory.IsCar(code), "code car");
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
                Object.DestroyImmediate(carTuning);
                palette.Dispose();
            }
        }

        [Test]
        public void Noun_FollowsTheClass()
        {
            Assert.AreEqual("angkot", VehicleClasses.Noun(VehicleClass.Angkot));
        }

        [Test]
        public void SetShadows_TogglesEveryRendererOfTheVehicle()
        {
            var go = new GameObject("Car");
            try
            {
                var a = GameObject.CreatePrimitive(PrimitiveType.Cube);
                a.transform.SetParent(go.transform, false);
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.transform.SetParent(a.transform, false);
                VehicleFactory.SetShadows(go, false);
                Assert.IsTrue(go.GetComponentsInChildren<Renderer>().All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off));
                VehicleFactory.SetShadows(go, true);
                Assert.IsTrue(go.GetComponentsInChildren<Renderer>().All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
