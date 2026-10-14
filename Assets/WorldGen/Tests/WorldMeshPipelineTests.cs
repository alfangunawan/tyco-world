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
            var edges = built.Features.Where(f => f.Kind == FeatureKind.RoadEdge).ToList();
            Assert.Greater(edges.Count, 0);
            foreach (var f in edges)
                Assert.LessOrEqual(MeshDataSplitTests.MaxEdge(f.Mesh), WorldMeshPipeline.MaxColliderEdge + 1e-3f, f.Name + " " + f.Chunk);
        }

        BuiltWorld BuildStreet(GenerationReport report, System.Action<TestOsm> extra = null)
        {
            var osm = StreetFixture.Osm();
            extra?.Invoke(osm);
            return WorldMeshPipeline.Build(new FeatureClassifier(settings, report).Classify(osm.Data), settings, report);
        }

        [Test]
        public void Street_EdgesAreChunkObjectsWithoutOsmIdentity()
        {
            var report = new GenerationReport();
            var edges = BuildStreet(report).Features.Where(f => f.Kind == FeatureKind.RoadEdge).ToList();
            Assert.Greater(edges.Count, 0);
            foreach (var f in edges)
            {
                Assert.AreEqual(FeatureGroups.RoadEdges, f.Group);
                Assert.IsFalse(f.HasOsmIdentity);
                Assert.IsTrue(f.AddCollider);
                Assert.IsNull(f.ColliderMesh);
                Assert.Less(f.OsmId, 0);
                Assert.AreEqual(f.Name == RoadEdgeBuilder.SidewalkName ? SurfaceType.Paving : SurfaceType.Concrete, f.Surface);
                Assert.AreEqual(f.Chunk, WorldMeshPipeline.ChunkOf(f.Anchor, 100f));
            }
            Assert.IsTrue(edges.Any(f => f.Name == RoadEdgeBuilder.SidewalkName));
            Assert.IsTrue(edges.Any(f => f.Name == RoadEdgeBuilder.GutterName));
            Assert.AreEqual(edges.Count, edges.Select(f => f.OsmId).Distinct().Count());
            Assert.Greater(report.GetCount("EdgeMeters.Gutter"), 0);
            Assert.Greater(report.GetCount("EdgeMeters.Sidewalk"), 0);
        }

        [Test]
        public void Build_RecordsTheStreetSubStages()
        {
            var report = new GenerationReport();
            BuildStreet(report);
            var names = report.StageMillis.Select(kv => kv.Key).ToList();
            foreach (var n in new[] { "meshes.street", "meshes.edges", "meshes.frontage1", "meshes.infill", "meshes.frontage2" })
                CollectionAssert.Contains(names, n);
        }

        [Test]
        public void Street_WallsAreChunkObjectsWithSeparateColliders()
        {
            var report = new GenerationReport();
            var walls = BuildStreet(report).Features.Where(f => f.Kind == FeatureKind.Frontage).ToList();
            Assert.Greater(walls.Count, 0);
            foreach (var f in walls)
            {
                Assert.AreEqual(FeatureGroups.Frontage, f.Group);
                Assert.AreEqual(FrontageMeshBuilder.WallsName, f.Name);
                Assert.IsFalse(f.HasOsmIdentity);
                Assert.IsTrue(f.AddCollider);
                Assert.IsNotNull(f.ColliderMesh);
                Assert.AreEqual(SurfaceType.Default, f.Surface);
                Assert.AreEqual(FrontageMeshBuilder.SubmeshCount, f.Materials.Length);
            }
            Assert.Greater(report.GetCount("WallMeters"), 0);
            Assert.GreaterOrEqual(report.GetCount("Gates"), 1);
            Assert.GreaterOrEqual(report.GetCount("GutterSlabs"), 1);
            Assert.Greater(report.GetCount("WallMeters.Campus"), 150);
        }

        [Test]
        public void Street_InfillIsOneObjectPerChunk()
        {
            var report = new GenerationReport();
            var built = BuildStreet(report);
            var infill = built.Features.Where(f => f.Kind == FeatureKind.Building && f.Name == "Infill").ToList();
            Assert.Greater(infill.Count, 0);
            Assert.AreEqual(infill.Count, infill.Select(f => f.Chunk).Distinct().Count());
            foreach (var f in infill)
            {
                Assert.AreEqual(FeatureGroups.Buildings, f.Group);
                Assert.AreEqual("Infill", f.Category);
                Assert.IsTrue(f.HasOsmIdentity);
                Assert.IsTrue(f.AddCollider);
                Assert.Less(f.OsmId, 0);
                Assert.IsTrue(f.Tags.TryGetValue("generated", out var generated) && generated == "infill");
                Assert.AreEqual(f.Materials.Length, f.Mesh.SubmeshCount);
                Assert.IsFalse(f.Mesh.HasInvalidValues());
            }
            Assert.Greater(built.Infill.Count, 0);
            Assert.AreEqual(built.Infill.Count, report.GetCount("Infill"));
        }

        [Test]
        public void Street_InfillMaterialsFollowTheVariantRule()
        {
            var upper = Materials(3);
            settings.residentialMaterials = new BuildingMaterialSet
            {
                groundFloor = Materials(2), upper = upper, trim = Materials(3), flatRoof = Materials(1), gableRoof = Materials(1)
            };
            var built = BuildStreet(new GenerationReport());
            var byChunk = built.Features.Where(f => f.Kind == FeatureKind.Building && f.Name == "Infill").ToDictionary(f => f.Chunk);
            foreach (var h in built.Infill)
            {
                var feature = byChunk[WorldMeshPipeline.ChunkOf(h.Centroid, built.ChunkSize)];
                CollectionAssert.Contains(feature.Materials, upper[SplitMix64.Pick(h.Id, "upper", 3)]);
            }
        }

        [Test]
        public void Street_WallsStandInFrontOfSetBackInfill()
        {
            var report = new GenerationReport();
            var built = BuildStreet(report);
            int setBack = built.Infill.Count(h => !h.Flush);
            Assert.Greater(setBack, 0);
            Assert.GreaterOrEqual(report.GetCount("Gates") + report.GetCount("Doors"), 2, "OSM house H1 plus at least one infill house");
        }

        [Test]
        public void LongStraightRoad_WallCollidersStayUnder100m()
        {
            var osm = new TestOsm();
            osm.Way(960, new[] { new Vector2(-600f, -1000f), new Vector2(400f, -1000f) }, false, ("highway", "residential"));
            var report = new GenerationReport();
            var built = WorldMeshPipeline.Build(new FeatureClassifier(settings, report).Classify(osm.Data), settings, report);
            var walls = built.Features.Where(f => f.Kind == FeatureKind.Frontage).ToList();
            Assert.Greater(walls.Count, 0);
            foreach (var f in walls)
            {
                Assert.LessOrEqual(MeshDataSplitTests.MaxEdge(f.Mesh), WorldMeshPipeline.MaxColliderEdge + 1e-3f);
                Assert.LessOrEqual(MeshDataSplitTests.MaxEdge(f.ColliderMesh), WorldMeshPipeline.MaxColliderEdge + 1e-3f);
            }
        }

        [Test]
        public void Street_IsDeterministic()
        {
            var first = new GenerationReport();
            var second = new GenerationReport();
            BuildStreet(first);
            BuildStreet(second);
            CollectionAssert.AreEqual(first.ChunkHashes, second.ChunkHashes);
        }

        [Test]
        public void ChunkFeatureId_IsNegativeAndDistinct()
        {
            long a = WorldMeshPipeline.ChunkFeatureId(FeatureGroups.RoadEdges, RoadEdgeBuilder.GutterName, new Vector2Int(1, 2));
            long b = WorldMeshPipeline.ChunkFeatureId(FeatureGroups.RoadEdges, RoadEdgeBuilder.SidewalkName, new Vector2Int(1, 2));
            long c = WorldMeshPipeline.ChunkFeatureId(FeatureGroups.RoadEdges, RoadEdgeBuilder.GutterName, new Vector2Int(2, 1));
            Assert.Less(a, 0);
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a, c);
            Assert.AreEqual(a, WorldMeshPipeline.ChunkFeatureId(FeatureGroups.RoadEdges, RoadEdgeBuilder.GutterName, new Vector2Int(1, 2)));
        }

        [Test]
        public void LongRoadAndLargeArea_AreSplitBelowColliderEdgeLimit()
        {
            var osm = new TestOsm();
            osm.Rect(900, -400f, -400f, 800f, 800f, ("landuse", "farmland"));
            osm.Way(901, new[] { new Vector2(-600f, 50f), new Vector2(400f, 50f) }, false, ("highway", "residential"));
            var report = new GenerationReport();
            var built = WorldMeshPipeline.Build(new FeatureClassifier(settings, report).Classify(osm.Data), settings, report);
            var walkable = built.Features.Where(f => f.OsmId == 900 || f.OsmId == 901).ToList();
            Assert.AreEqual(2, walkable.Count);
            foreach (var f in walkable)
                Assert.LessOrEqual(MeshDataSplitTests.MaxEdge(f.Mesh), WorldMeshPipeline.MaxColliderEdge + 1e-3f, f.Kind + " " + f.OsmId);
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
        public void MarkedRoad_KeepsItsAsphaltSurface()
        {
            var asphalt = Materials(1)[0];
            asphalt.name = "Road_Asphalt";
            var marking = Materials(1)[0];
            marking.name = "Road_Marking";
            settings.roadRules.Single(r => r.highway == "primary").material = asphalt;
            settings.roadEdges.marking = marking;
            var osm = new TestOsm();
            osm.Way(950, new[] { new Vector2(-300f, -500f), new Vector2(-200f, -500f) }, false, ("highway", "primary"));
            var report = new GenerationReport();
            var built = WorldMeshPipeline.Build(new FeatureClassifier(settings, report).Classify(osm.Data), settings, report);
            var road = built.Features.Single(f => f.Kind == FeatureKind.Road && f.OsmId == 950);
            Assert.AreEqual(SurfaceType.Asphalt, road.Surface);
            Assert.AreSame(asphalt, road.Materials[0]);
            Assert.AreSame(marking, road.Materials[1]);
            Assert.Greater(road.Mesh.Indices(RoadMarkingBuilder.Submesh).Count, 0);
            Assert.AreEqual(12, report.GetCount("MarkingDashes"));
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
