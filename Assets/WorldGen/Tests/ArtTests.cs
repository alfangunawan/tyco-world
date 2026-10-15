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

        [Test]
        public void BarrierStripes_AreYellowAndBlackDiagonals()
        {
            var a = StreetTextureGenerator.BarrierStripes();
            var b = StreetTextureGenerator.BarrierStripes();
            try
            {
                CollectionAssert.AreEqual(a.GetPixels32(), b.GetPixels32());
                Color yellow = a.GetPixel(4, 4), black = a.GetPixel(4 + a.width / 4, 4);
                Assert.Greater(yellow.r, 0.7f);
                Assert.Greater(yellow.g, 0.55f);
                Assert.Less(yellow.b, 0.3f);
                Assert.Less(black.grayscale, 0.15f);
                var along = a.GetPixel(4 + a.width / 4, 4 - a.width / 4 + a.width);
                Assert.IsTrue(along.r > 0.7f && along.b < 0.3f, "diagonal: the same yellow band one step along x and back along y");
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void BarrierPrefabs_AreBuilt_Solid_AndOfASensibleSize()
        {
            var prefabs = BarrierArt.BuildPrefabs(null);
            Assert.AreEqual(3, prefabs.Length);
            float[,] height = { { 0.5f, 0.9f }, { 0.6f, 1.0f }, { 1.2f, 2.0f } };
            for (int i = 0; i < 3; i++)
            {
                var p = prefabs[i];
                Assert.IsNotNull(p, "prefab " + i);
                Assert.IsNotNull(p.GetComponent<Collider>(), p.name + " is solid");
                var renderers = p.GetComponentsInChildren<Renderer>();
                Assert.IsNotEmpty(renderers, p.name);
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                Assert.That(b.size.y, Is.InRange(height[i, 0], height[i, 1]), p.name + " height");
                Assert.AreEqual(0f, b.min.y, 0.02f, p.name + " stands on the ground");
            }
            var signs = prefabs[BarrierBuilder.RoadClosedVariant].GetComponentsInChildren<TextMesh>();
            Assert.AreEqual(1, signs.Length, "one line of text: the text shader draws both sides, two would overlap mirrored");
            Assert.AreEqual("JALAN DITUTUP", signs[0].text);
            Assert.Greater(Vector3.Dot(signs[0].transform.forward, Vector3.back), 0.99f,
                "readable from the prefab's +Z side, which BarrierBuilder turns toward the road");
        }

        [Test]
        public void SavedSettings_UseTheBarrierPrefabs_AndTheStripeMaterial()
        {
            var s = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
            Assert.IsNotNull(s);
            Assert.AreEqual(3, s.barrierPrefabs.Length);
            Assert.IsTrue(s.barrierPrefabs.All(p => p != null));
            Assert.IsNotNull(s.deadEnds.stripe);
            Assert.IsNotNull(s.deadEnds.stripe.GetTexture("_BaseMap"));
        }

        [Test]
        public void KerbStripes_AreDarkThenLightAlongU()
        {
            var a = StreetTextureGenerator.KerbStripes();
            var b = StreetTextureGenerator.KerbStripes();
            try
            {
                Assert.AreEqual(StreetTextureGenerator.KerbWidth, a.width);
                Assert.AreEqual(StreetTextureGenerator.KerbHeight, a.height);
                CollectionAssert.AreEqual(a.GetPixels32(), b.GetPixels32());
                Assert.Less(a.GetPixel(16, 16).grayscale, 0.3f);
                Assert.Greater(a.GetPixel(96, 16).grayscale, 0.6f);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        static Texture2D Flat(float gray)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(gray, gray, gray, 1f);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Color AtlasPixel(Texture2D atlas, int band, int y) => atlas.GetPixel(10, band * StreetTextureGenerator.BandHeight + y);

        [Test]
        public void WallAtlas_BandsCarryTheirTint()
        {
            var plaster = Flat(0.5f);
            var brick = Flat(0.5f);
            var atlas = StreetTextureGenerator.WallAtlas(plaster, brick);
            try
            {
                Assert.AreEqual(StreetTextureGenerator.AtlasWidth, atlas.width);
                Assert.AreEqual(StreetTextureGenerator.BandHeight * StreetTextureGenerator.AtlasBands, atlas.height);
                // y = 50 px = 0.78 m: above the 25 cm grime, below the streaks that start at 1.2 m.
                for (int band = 0; band < 6; band++)
                {
                    var expected = FacadeTextureGenerator.ResidentialPalette[band];
                    var c = AtlasPixel(atlas, band, 50);
                    Assert.AreEqual(expected.r, c.r, 0.02f, "band " + band);
                    Assert.AreEqual(expected.g, c.g, 0.02f, "band " + band);
                    Assert.AreEqual(expected.b, c.b, 0.02f, "band " + band);
                }
                for (int i = 0; i < 2; i++)
                {
                    var expected = StreetTextureGenerator.BrickTints[i] * 0.5f;
                    var c = AtlasPixel(atlas, 6 + i, 50);
                    Assert.AreEqual(expected.r, c.r, 0.02f);
                    Assert.AreEqual(expected.g, c.g, 0.02f);
                }
                Assert.Less(AtlasPixel(atlas, 0, 2).grayscale, AtlasPixel(atlas, 0, 50).grayscale, "grime at the foot");
            }
            finally
            {
                Object.DestroyImmediate(plaster);
                Object.DestroyImmediate(brick);
                Object.DestroyImmediate(atlas);
            }
        }

        [Test]
        public void WallAtlas_IsDeterministic()
        {
            var plaster = Flat(0.6f);
            var brick = Flat(0.4f);
            var a = StreetTextureGenerator.WallAtlas(plaster, brick);
            var b = StreetTextureGenerator.WallAtlas(plaster, brick);
            try { CollectionAssert.AreEqual(a.GetPixels32(), b.GetPixels32()); }
            finally
            {
                Object.DestroyImmediate(plaster);
                Object.DestroyImmediate(brick);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void FenceBars_HaveOpaqueBarsAndClearGaps()
        {
            var f = StreetTextureGenerator.FenceBars();
            try
            {
                Assert.AreEqual(1f, f.GetPixel(1, 30).a, 1e-3f, "bar");
                Assert.AreEqual(0f, f.GetPixel(8, 30).a, 1e-3f, "gap");
                Assert.AreEqual(1f, f.GetPixel(8, 1).a, 1e-3f, "bottom rail");
                Assert.AreEqual(1f, f.GetPixel(8, 62).a, 1e-3f, "middle rail");
                Assert.AreEqual(1f, f.GetPixel(8, 126).a, 1e-3f, "top rail");
            }
            finally { Object.DestroyImmediate(f); }
        }

        [Test]
        public void FenceMaterial_ClipsAlpha()
        {
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(WorldGenPaths.MaterialsFolder + "/Frontage_Fence.mat");
            Assert.IsNotNull(m);
            Assert.AreEqual(1f, m.GetFloat("_AlphaClip"));
            Assert.IsTrue(m.IsKeywordEnabled("_ALPHATEST_ON"));
            Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.AlphaTest, m.renderQueue);
        }
    }
}
