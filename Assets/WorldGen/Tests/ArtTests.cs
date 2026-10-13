using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class ArtTests
    {
        static float Luminance(Color32 c) => (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;

        static FacadeSpec Spec(string name) => FacadeTextureGenerator.Specs().Single(s => s.Name == name);

        [Test]
        public void Specs_Has22UniqueNames()
        {
            var specs = FacadeTextureGenerator.Specs();
            Assert.AreEqual(22, specs.Count);
            Assert.AreEqual(22, specs.Select(s => s.Name).Distinct().Count());
        }

        [Test]
        public void Render_IsDeterministicAndSized()
        {
            var a = FacadeTextureGenerator.Render(Spec("Res_Upper_0"));
            var b = FacadeTextureGenerator.Render(Spec("Res_Upper_0"));
            try
            {
                Assert.AreEqual(FacadeTextureGenerator.Size, a.width);
                Assert.AreEqual(FacadeTextureGenerator.Size, a.height);
                CollectionAssert.AreEqual(a.GetPixels32(), b.GetPixels32());
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void ResidentialUpper_WindowIsDarkerThanWall()
        {
            var tex = FacadeTextureGenerator.Render(Spec("Res_Upper_0"));
            try
            {
                Assert.Less(Luminance(tex.GetPixel(128, 150)), 0.3f);
                Assert.Greater(Luminance(tex.GetPixel(10, 120)), 0.6f);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void RollingDoor_HasHorizontalStripes()
        {
            var tex = FacadeTextureGenerator.Render(Spec("Res_Ground_RollingDoor"));
            try
            {
                int transitions = 0;
                for (int y = 11; y < 190; y++)
                    if (Mathf.Abs(Luminance(tex.GetPixel(128, y)) - Luminance(tex.GetPixel(128, y - 1))) > 0.05f) transitions++;
                Assert.Greater(transitions, 10);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Cylinder_HasOutwardFaces()
        {
            var m = new MeshData(1);
            PlaceholderMeshes.AddCylinder(m, 0, Vector3.zero, 0.2f, 2f, 6);
            Assert.AreEqual(24, m.TriangleCount);
            AssertOutward(m, new Vector3(0f, 1f, 0f));
        }

        [Test]
        public void Cone_HasOutwardFaces()
        {
            var m = new MeshData(1);
            PlaceholderMeshes.AddCone(m, 0, Vector3.zero, 1.4f, 3.2f, 6);
            Assert.AreEqual(12, m.TriangleCount);
            AssertOutward(m, new Vector3(0f, 0.8f, 0f));
        }

        [Test]
        public void Icosahedron_HasTwentyOutwardFaces()
        {
            var m = new MeshData(1);
            PlaceholderMeshes.AddIcosahedron(m, 0, new Vector3(0f, 3f, 0f), 1.6f);
            Assert.AreEqual(20, m.TriangleCount);
            AssertOutward(m, new Vector3(0f, 3f, 0f));
        }

        [Test]
        public void Box_HasTwelveOutwardFaces()
        {
            var m = new MeshData(1);
            PlaceholderMeshes.AddBox(m, 0, new Vector3(0f, 5.9f, 0.6f), new Vector3(0.08f, 0.08f, 1.2f));
            Assert.AreEqual(12, m.TriangleCount);
            AssertOutward(m, new Vector3(0f, 5.9f, 0.6f));
        }

        [TestCase(AreaCategory.Grass, "Area_Grass")]
        [TestCase(AreaCategory.Wood, "Area_Grass")]
        [TestCase(AreaCategory.Cemetery, "Area_Cemetery")]
        [TestCase(AreaCategory.Paving, "Path_Paving")]
        [TestCase(AreaCategory.Campus, "Area_Campus")]
        public void AreaMaterialName_MapsCategories(AreaCategory category, string expected)
        {
            Assert.AreEqual(expected, ArtAssetBuilder.AreaMaterialName(category));
        }

        static void AssertOutward(MeshData m, Vector3 center)
        {
            Assert.IsFalse(m.HasInvalidValues());
            var idx = m.Indices(0);
            for (int t = 0; t < idx.Count; t += 3)
            {
                Vector3 a = m.Vertices[idx[t]], b = m.Vertices[idx[t + 1]], c = m.Vertices[idx[t + 2]];
                Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - center), 0f);
            }
        }
    }
}
