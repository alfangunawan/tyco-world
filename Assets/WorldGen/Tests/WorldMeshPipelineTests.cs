using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class WorldMeshPipelineTests
    {
        WorldGenSettings settings;
        readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp() => settings = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Object.DestroyImmediate(settings);
        }

        BuiltWorld Build(string fixture, GenerationReport report) =>
            WorldMeshPipeline.Build(WorldMeshPipeline.Classify(FixtureFiles.Read(fixture), settings, report), settings, report);

        Material[] Materials(int count)
        {
            var result = new Material[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "m" + i };
                created.Add(result[i]);
            }
            return result;
        }

        [TestCase(-0.01f, 0f, -1, 0)]
        [TestCase(100f, 199.99f, 1, 1)]
        [TestCase(-100f, -100f, -1, -1)]
        [TestCase(99.99f, 0f, 0, 0)]
        public void ChunkOf_FloorsCoordinates(float x, float y, int cx, int cy)
        {
            Assert.AreEqual(new Vector2Int(cx, cy), WorldMeshPipeline.ChunkOf(new Vector2(x, y), 100f));
        }

        [Test]
        public void ChunkName_IsInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                Assert.AreEqual("Chunk_-3_5", WorldMeshPipeline.ChunkName(new Vector2Int(-3, 5)));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void Synthetic_BuildsAllKinds()
        {
            var report = new GenerationReport();
            var built = Build("synthetic_world.json", report);
            Assert.IsTrue(built.Features.Any(f => f.Kind == FeatureKind.Road && f.OsmId == 402));
            Assert.IsTrue(built.Features.Any(f => f.Kind == FeatureKind.Building && f.OsmId == 401));
            Assert.IsTrue(built.Features.Any(f => f.Kind == FeatureKind.Building && f.OsmId == 201));
            Assert.IsTrue(built.Features.Any(f => f.Kind == FeatureKind.Area && f.OsmId == 404));
            Assert.IsTrue(built.Features.Any(f => f.Kind == FeatureKind.Area && f.OsmId == 405));
            Assert.IsFalse(built.Ground.IsEmpty);
            Assert.AreEqual(1, report.GetCount("Road"));
            Assert.Greater(report.ChunkHashes.Count, 0);
        }

        [Test]
        public void Synthetic_SurfacesAssigned()
        {
            var built = Build("synthetic_world.json", new GenerationReport());
            Assert.AreEqual(SurfaceType.Asphalt, built.Features.Single(f => f.Kind == FeatureKind.Road && f.OsmId == 402).Surface);
            Assert.AreEqual(SurfaceType.Grass, built.Features.Single(f => f.Kind == FeatureKind.Area && f.OsmId == 404).Surface);
            Assert.AreEqual(SurfaceType.Paving, built.Features.Single(f => f.Kind == FeatureKind.Area && f.OsmId == 405).Surface);
            Assert.AreEqual(SurfaceType.Default, built.Features.Single(f => f.Kind == FeatureKind.Building && f.OsmId == 401).Surface);
        }

        [Test]
        public void Synthetic_WalkableFeaturesHaveColliders()
        {
            var built = Build("synthetic_world.json", new GenerationReport());
            Assert.Greater(built.Features.Count, 0);
            foreach (var f in built.Features.Where(f => f.Mesh != null))
                Assert.IsTrue(f.AddCollider, f.Kind + " " + f.OsmId);
        }

        [Test]
        public void Synthetic_BuildingMaterialsFollowVariantRule()
        {
            var upper = Materials(3);
            var trim = Materials(3);
            var gable = Materials(1);
            settings.residentialMaterials = new BuildingMaterialSet
            {
                groundFloor = Materials(2), upper = upper, trim = trim, flatRoof = Materials(1), gableRoof = gable
            };
            var house = Build("synthetic_world.json", new GenerationReport()).Features.Single(f => f.OsmId == 401);
            int index = SplitMix64.Pick(401, "upper", 3);
            Assert.AreSame(upper[index], house.Materials[BuildingMeshBuilder.SubUpper]);
            Assert.AreSame(trim[index], house.Materials[BuildingMeshBuilder.SubTrim]);
            Assert.AreSame(gable[0], house.Materials[BuildingMeshBuilder.SubRoof]);
        }

        [Test]
        public void KeyBuildingWithPrefab_HasNoMesh()
        {
            var prefab = new GameObject("FakePrefab");
            created.Add(prefab);
            settings.keyBuildings.Add(new KeyBuilding(401, "Rumah Kunci") { prefab = prefab });
            var report = new GenerationReport();
            var feature = Build("synthetic_world.json", report).Features.Single(f => f.OsmId == 401);
            Assert.IsNull(feature.Mesh);
            Assert.AreEqual(FeatureGroups.KeyBuildings, feature.Group);
            Assert.AreEqual(1, report.GetCount("KeyBuildingPrefab"));
        }

        [Test]
        public void Synthetic_PropsStayInBounds()
        {
            var built = Build("synthetic_world.json", new GenerationReport());
            foreach (var t in built.Trees) Assert.IsTrue(built.Bounds.Contains(t.Position));
            foreach (var l in built.Lamps) Assert.IsTrue(built.Bounds.Contains(l.Position));
        }

        [Test]
        public void Sukabirus_IsDeterministic()
        {
            var first = new GenerationReport();
            var second = new GenerationReport();
            Build("sukabirus_small.json", first);
            Build("sukabirus_small.json", second);
            Assert.Greater(first.ChunkHashes.Count, 0);
            CollectionAssert.AreEqual(first.ChunkHashes, second.ChunkHashes);
        }

        [Test]
        public void Sukabirus_NoInvalidGeometry()
        {
            var report = new GenerationReport();
            var built = Build("sukabirus_small.json", report);
            Assert.Greater(report.GetCount("Building"), 0);
            Assert.Greater(report.GetCount("Road"), 0);
            foreach (var f in built.Features)
                if (f.Mesh != null) Assert.IsFalse(f.Mesh.HasInvalidValues(), $"{f.Kind} {f.OsmId}");
        }
    }
}
