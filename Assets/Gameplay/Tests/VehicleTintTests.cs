using System.Linq;
using NUnit.Framework;
using TycoWorld.Vehicles;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    /// <summary>Repaint by tinting the model's body material (spec vehicles-3a §4), one material per colour.</summary>
    public class VehicleTintTests
    {
        GameObject car;
        Material body;
        VehicleTint tint;

        [SetUp]
        public void SetUp()
        {
            body = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Body" };
            body.SetColor("_BaseColor", Color.white);
            car = GameObject.CreatePrimitive(PrimitiveType.Cube);
            car.GetComponent<MeshRenderer>().sharedMaterial = body;
            tint = new VehicleTint();
        }

        [TearDown]
        public void TearDown()
        {
            tint.Dispose();
            Object.DestroyImmediate(car);
            Object.DestroyImmediate(body);
        }

        /// <summary>Material colours go through a gamma/linear round trip in a linear project, so they match to 1e-3.</summary>
        static void AssertColour(Color expected, Color actual)
        {
            for (int i = 0; i < 4; i++) Assert.AreEqual(expected[i], actual[i], 1e-3f, "channel " + i);
        }

        [Test]
        public void BodyMaterial_IsTintedFully_AndShared()
        {
            var entry = new VehicleCatalog.Entry { paintMaterials = new[] { "Body" }, paintStrength = 1f };
            var red = new Color32(180, 40, 40, 255);
            tint.Apply(car, entry, red);
            var first = car.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreNotSame(body, first, "never edits the imported material");
            AssertColour(red, first.GetColor("_BaseColor"));
            var other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                other.GetComponent<MeshRenderer>().sharedMaterial = body;
                tint.Apply(other, entry, red);
                Assert.AreSame(first, other.GetComponent<MeshRenderer>().sharedMaterial, "one material per (source, colour)");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void AtlasModel_IsTintedAtPaintStrength()
        {
            var entry = new VehicleCatalog.Entry { paintMaterials = new[] { "Body" }, paintStrength = 0.6f };
            tint.Apply(car, entry, new Color32(0, 0, 255, 255));
            var c = car.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor");
            Assert.AreEqual(0.4f, c.r, 0.01f, "lerp(white, blue, 0.6)");
            Assert.AreEqual(1f, c.b, 0.01f);
        }

        [Test]
        public void GltfMaterial_IsTintedThroughBaseColorFactor()
        {
            // glTFast imports to its own shader graph, whose colour is "baseColorFactor" (there is no _BaseColor).
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/Vehicles/Art/ThirdParty/Han66st/lmpv2.glb").OfType<Material>().First(m => m.name == "body.001");
            Assert.IsFalse(source.HasProperty("_BaseColor"));
            car.GetComponent<MeshRenderer>().sharedMaterial = source;
            var red = new Color32(180, 40, 40, 255);
            tint.Apply(car, new VehicleCatalog.Entry { paintMaterials = new[] { "body.001" }, paintStrength = 1f }, red);
            var copy = car.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreNotSame(source, copy);
            AssertColour(red, copy.GetColor("baseColorFactor"));
        }

        [Test]
        public void FixedLivery_IsLeftAlone()
        {
            tint.Apply(car, new VehicleCatalog.Entry(), new Color32(0, 255, 0, 255));
            Assert.AreSame(body, car.GetComponent<MeshRenderer>().sharedMaterial);
        }
    }
}
