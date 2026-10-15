using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class DefaultSettingsTests
    {
        WorldGenSettings s;

        [SetUp]
        public void SetUp() => s = DefaultSettingsFactory.CreateInMemory();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(s);

        [Test]
        public void Area_MatchesSpec()
        {
            Assert.AreEqual(-6.9850, s.south);
            Assert.AreEqual(107.6255, s.west);
            Assert.AreEqual(-6.9675, s.north);
            Assert.AreEqual(107.6380, s.east);
            Assert.AreEqual(-6.97265, s.originLat);
            Assert.AreEqual(107.63400, s.originLon);
            Assert.AreEqual(100f, s.chunkSize);
        }

        [Test]
        public void Download_MatchesSpec()
        {
            CollectionAssert.AreEqual(new[]
            {
                "https://overpass-api.de/api/interpreter",
                "https://overpass.kumi.systems/api/interpreter",
                "https://overpass.private.coffee/api/interpreter"
            }, s.overpassEndpoints);
            Assert.AreEqual(90, s.timeoutSeconds);
            Assert.AreEqual("WorldGenData/osm_raw.json", s.rawDataPath);
        }

        [Test]
        public void RoadRules_MatchSpecTable()
        {
            Assert.AreEqual(13, s.roadRules.Count);
            var tertiary = s.roadRules.Single(r => r.highway == "tertiary");
            Assert.AreEqual(6f, tertiary.defaultWidth);
            Assert.AreEqual(6, tertiary.rank);
            Assert.AreEqual(30f, tertiary.lampSpacing);
            var service = s.roadRules.Single(r => r.highway == "service");
            Assert.AreEqual(3.5f, service.defaultWidth);
            Assert.AreEqual(50f, service.lampSpacing);
            var footway = s.roadRules.Single(r => r.highway == "footway");
            Assert.AreEqual(1.5f, footway.defaultWidth);
            Assert.AreEqual(0, footway.rank);
            Assert.AreEqual(0f, footway.lampSpacing);
            Assert.AreEqual(3f, s.lanesWidth);
            Assert.AreEqual(0.060f, s.roadBaseY, 1e-6f);
            Assert.AreEqual(0.005f, s.roadRankStep, 1e-6f);
        }

        [Test]
        public void BuildingRules_MatchSpecTable()
        {
            Assert.AreEqual(8, s.buildingRules.Count);
            var religious = s.buildingRules[2];
            CollectionAssert.Contains(religious.buildingValues, "mosque");
            Assert.AreEqual(5f, religious.levelHeightOverride);
            Assert.IsTrue(s.buildingRules[5].roofOnly);
            var residential = s.buildingRules[7];
            Assert.AreEqual(BuildingContext.NonCampus, residential.context);
            Assert.AreEqual(1, residential.minLevels);
            Assert.AreEqual(3, residential.maxLevels);
            Assert.IsTrue(residential.allowGable);
            Assert.AreEqual(BuildingMaterialSetId.Residential, residential.materialSet);
            Assert.AreEqual(BuildingMaterialSetId.Campus, s.buildingRules[6].materialSet);
        }

        [Test]
        public void BuildingScalars_MatchSpec()
        {
            Assert.AreEqual(3.2f, s.levelHeight);
            Assert.AreEqual(3f, s.facadeTileWidth);
            Assert.AreEqual(0.6f, s.parapetHeight);
            Assert.AreEqual(200f, s.gableMaxArea);
            Assert.AreEqual(0.85f, s.gableMinRectangularity);
            Assert.AreEqual(0.25f, s.gablePitchRatio);
            Assert.AreEqual(0.3f, s.gableOverhang);
            Assert.AreEqual(2.5f, s.minWallHeight);
        }

        [Test]
        public void AreaRules_MatchSpecTable()
        {
            Assert.AreEqual(9, s.areaRules.Count);
            var campus = s.areaRules.Single(a => a.category == AreaCategory.Campus);
            Assert.AreEqual(0.015f, campus.yOffset, 1e-6f);
            Assert.AreEqual(20f, campus.treeSpacing);
            var cemetery = s.areaRules.Single(a => a.category == AreaCategory.Cemetery);
            Assert.IsTrue(cemetery.Matches(new Dictionary<string, string> { { "amenity", "grave_yard" } }));
            Assert.IsTrue(cemetery.Matches(new Dictionary<string, string> { { "landuse", "cemetery" } }));
            Assert.AreEqual(0, s.areaRules.Single(a => a.category == AreaCategory.Paving).matches.Length);
        }

        [Test]
        public void WaterAndProps_MatchSpec()
        {
            Assert.AreEqual(0.045f, s.waterAreaY, 1e-6f);
            Assert.AreEqual(0.050f, s.waterLineY, 1e-6f);
            Assert.AreEqual(8f, s.waterwayWidths.Single(w => w.waterway == "river").width);
            Assert.AreEqual(1.5f, s.waterwayWidths.Single(w => w.waterway == "ditch").width);
            Assert.AreEqual(4000, s.maxTrees);
            Assert.AreEqual(8f, s.lampMinSpacing);
            Assert.AreEqual(0.6f, s.lampOffset);
        }

        [Test]
        public void KeyBuildings_MatchSpec()
        {
            CollectionAssert.AreEqual(new long[] { 1257717375, 197341486, 1293369802, 1293369773, 1293369796, 409467778 },
                s.keyBuildings.Select(k => k.osmId).ToArray());
            Assert.AreEqual("Telkom University Landmark Tower (TULT)", s.keyBuildings[0].label);
            Assert.AreEqual("Kos Raflesia 3", s.keyBuildings[5].label);
            Assert.AreEqual(0, s.keyBuildings[5].overrideLevels);
            Assert.AreEqual(RoofOverride.Auto, s.keyBuildings[5].overrideRoof);
        }

        [Test]
        public void RoadEdges_MatchStreetFrontageSpec()
        {
            var e = s.roadEdges;
            CollectionAssert.AreEqual(new[] { "primary", "secondary" }, e.sidewalkHighways);
            CollectionAssert.AreEqual(new[] { "tertiary" }, e.campusSidewalkHighways);
            CollectionAssert.AreEqual(new[] { "tertiary", "unclassified", "residential", "living_street", "service" }, e.gutterHighways);
            CollectionAssert.AreEqual(new[] { "driveway", "parking_aisle", "drive-through" }, e.noEdgeServiceValues);
            Assert.AreEqual(10f, e.campusSidewalkDistance);
            Assert.AreEqual(8f, e.minProfileRun);
            Assert.AreEqual(0.5f, e.sampleStep);
            Assert.AreEqual(0.3f, e.mouthMargin, 1e-6f);
            Assert.AreEqual(0.055f, e.edgeBaseY, 1e-6f);
            Assert.AreEqual(0.155f, e.gutterLipY, 1e-6f);
            Assert.AreEqual(0.21f, e.sidewalkTopY, 1e-6f);
            Assert.AreEqual(0.05f, e.underlap, 1e-6f);
            Assert.AreEqual(0.3f, e.shoulderWidth, 1e-6f);
            Assert.AreEqual(0.5f, e.gutterWidth, 1e-6f);
            Assert.AreEqual(0.08f, e.gutterLipWidth, 1e-6f);
            Assert.AreEqual(1.5f, e.sidewalkWidth, 1e-6f);
            Assert.AreEqual(0.15f, e.kerbWidth, 1e-6f);
            Assert.AreEqual(0.8f, e.Width(EdgeProfile.Gutter), 1e-6f);
            Assert.AreEqual(1.5f, e.Width(EdgeProfile.Sidewalk), 1e-6f);
            Assert.AreEqual(0f, e.Width(EdgeProfile.None));
            CollectionAssert.AreEqual(new[] { "primary", "secondary" }, e.markingHighways);
            CollectionAssert.AreEqual(new[] { "tertiary" }, e.wideMarkingHighways);
            Assert.AreEqual(7f, e.markingMinWidth);
            Assert.AreEqual(0.12f, e.markingWidth, 1e-6f);
            Assert.AreEqual(0.003f, e.markingLift, 1e-6f);
            Assert.AreEqual(3f, e.dashLength);
            Assert.AreEqual(5f, e.dashGap);
            Assert.AreEqual(2f, e.dashStart);
            Assert.AreEqual(3f, e.junctionDashClearance);
            Assert.AreEqual(0.45f, e.sidewalkLampOffset, 1e-6f);
            Assert.AreEqual(0.15f, e.gutterLampOffset, 1e-6f);
        }

        [Test]
        public void Frontage_MatchesStreetFrontageSpec()
        {
            var f = s.frontage;
            Assert.AreEqual(0.4f, f.propertyLineGap, 1e-6f);
            Assert.AreEqual(15f, f.corridorDepth);
            Assert.AreEqual(1f, f.flushDistance);
            Assert.AreEqual(1.5f, f.minInterval);
            Assert.AreEqual(0.5f, f.contextProbe);
            Assert.AreEqual(0.15f, f.wallThickness, 1e-6f);
            Assert.AreEqual(0.25f, f.pilasterSize, 1e-6f);
            Assert.AreEqual(3f, f.pilasterSpacing);
            Assert.AreEqual(0.1f, f.pilasterExtra, 1e-6f);
            Assert.AreEqual(1f, f.pilasterMinGap);
            Assert.AreEqual(20f, f.pilasterMinTurnDegrees);
            Assert.AreEqual(5f, f.gateMaxTurnDegrees);
            Assert.AreEqual(2f, f.gateWidth);
            Assert.AreEqual(0.9f, f.doorWidth, 1e-6f);
            Assert.AreEqual(1.6f, f.gateHeight, 1e-6f);
            Assert.AreEqual(4.5f, f.gateMinLot);
            Assert.AreEqual(2.5f, f.doorMinLot);
            Assert.AreEqual(0.165f, f.slabTopY, 1e-6f);
            Assert.AreEqual(0.4f, f.slabExtraWidth, 1e-6f);
            Assert.AreEqual(8f, f.kosExclusionRadius);
            Assert.AreEqual(409467778L, f.kosBuildingId);
            Assert.AreEqual(8, f.atlasBands);
            Assert.AreEqual(5, f.styles.Length);
            var plaster = f.Style(WallStyle.Plaster);
            Assert.AreEqual(0.55f, plaster.share, 1e-6f);
            Assert.AreEqual(1.6f, plaster.minHeight, 1e-6f);
            Assert.AreEqual(2.0f, plaster.maxHeight, 1e-6f);
            Assert.AreEqual(0, plaster.firstBand);
            Assert.AreEqual(6, plaster.bandCount);
            var low = f.Style(WallStyle.LowFence);
            Assert.AreEqual(0.8f, low.maxHeight, 1e-6f);
            Assert.AreEqual(0.9f, low.fenceHeight, 1e-6f);
            var brick = f.Style(WallStyle.Brick);
            Assert.AreEqual(1.7f, brick.minHeight, 1e-6f);
            Assert.AreEqual(6, brick.firstBand);
            Assert.AreEqual(2, brick.bandCount);
            Assert.AreEqual(1.2f, f.Style(WallStyle.Campus).fenceHeight, 1e-6f);
            Assert.AreEqual(0.6f, f.Style(WallStyle.Campus).maxHeight, 1e-6f);
            Assert.AreEqual(0.7f, f.Style(WallStyle.Cemetery).maxHeight, 1e-6f);
            Assert.AreEqual(1f, f.styles.Where(x => x.share > 0f).Sum(x => x.share), 1e-5f);
        }

        [TestCase(0.0, WallStyle.Plaster)]
        [TestCase(0.549, WallStyle.Plaster)]
        [TestCase(0.6, WallStyle.LowFence)]
        [TestCase(0.95, WallStyle.Brick)]
        [TestCase(0.9999, WallStyle.Brick)]
        public void PickResidential_FollowsShares(double roll, WallStyle expected)
        {
            Assert.AreEqual(expected, s.frontage.PickResidential(roll).style);
        }

        [Test]
        public void Infill_MatchesStreetFrontageSpec()
        {
            var i = s.infill;
            Assert.IsTrue(i.enabled);
            Assert.AreEqual(6f, i.minLotLength);
            Assert.AreEqual(6f, i.minWidth);
            Assert.AreEqual(10f, i.maxWidth);
            Assert.AreEqual(8f, i.minDepth);
            Assert.AreEqual(12f, i.maxDepth);
            Assert.AreEqual(0f, i.minGap);
            Assert.AreEqual(0.6f, i.maxGap, 1e-6f);
            Assert.AreEqual(0.35f, i.flushShare, 1e-6f);
            Assert.AreEqual(0.1f, i.flushInset, 1e-6f);
            Assert.AreEqual(1.5f, i.minSetback);
            Assert.AreEqual(4f, i.maxSetback);
            Assert.AreEqual(0.4f, i.twoStoreyShare, 1e-6f);
            Assert.AreEqual(0.5f, i.clearance, 1e-6f);
            Assert.AreEqual(2500, i.maxInfill);
        }

        [Test]
        public void Defaults_RoadClearanceAndDeadEnds_MatchTheSpec()
        {
            var s = DefaultSettingsFactory.CreateInMemory();
            try
            {
                Assert.IsTrue(s.clearance.enabled);
                Assert.AreEqual(0.3f, s.clearance.margin, 1e-6f);
                Assert.AreEqual(2.0f, s.clearance.maxShift, 1e-6f);
                Assert.AreEqual(0.05f, s.clearance.shiftExtra, 1e-6f);
                Assert.AreEqual(300f, s.clearance.houseMaxArea, 1e-6f);
                Assert.AreEqual(3f, s.clearance.minPieceLength, 1e-6f);
                Assert.IsTrue(s.deadEnds.enabled);
                Assert.AreEqual(20f, s.deadEnds.boundaryDistance, 1e-6f);
                Assert.AreEqual(0.3f, s.deadEnds.endInset, 1e-6f);
                Assert.AreEqual(1.2f, s.deadEnds.minWidth, 1e-6f);
                Assert.AreEqual(0.9f, s.deadEnds.wallHeight, 1e-6f);
                Assert.AreEqual(2f, s.deadEnds.buildingAhead, 1e-6f);
                CollectionAssert.AreEquivalent(new[] { "service", "track" }, s.deadEnds.portalHighways);
                Assert.IsNotNull(s.barrierPrefabs);
            }
            finally
            {
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void SavedAsset_HasRoadClearanceAndDeadEnds()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
            Assert.IsNotNull(asset);
            Assert.IsNotNull(asset.clearance);
            Assert.IsNotNull(asset.deadEnds);
            Assert.AreEqual(0.9f, asset.deadEnds.wallHeight, 1e-6f, "an asset saved before the field existed gets the initializer defaults");
            Assert.AreEqual(0.3f, asset.clearance.margin, 1e-6f);
        }

        [Test]
        public void SavedAsset_HasStreetDressing()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
            Assert.IsNotNull(asset);
            Assert.IsNotNull(asset.roadEdges);
            Assert.AreEqual(1.5f, asset.roadEdges.sidewalkWidth, 1e-6f);
            Assert.IsNotNull(asset.frontage);
            Assert.AreEqual(409467778L, asset.frontage.kosBuildingId);
            Assert.AreEqual(5, asset.frontage.styles.Length);
            Assert.AreEqual(1f, asset.frontage.pilasterMinGap, 1e-6f);
            Assert.AreEqual(20f, asset.frontage.pilasterMinTurnDegrees, 1e-6f);
            string yaml = System.IO.File.ReadAllText(WorldGenPaths.SettingsAsset);
            StringAssert.Contains("pilasterMinGap:", yaml, "the saved file itself carries the field, not only the initializer");
            StringAssert.Contains("pilasterMinTurnDegrees:", yaml);
            Assert.AreEqual(5f, asset.frontage.gateMaxTurnDegrees, 1e-6f);
            StringAssert.Contains("gateMaxTurnDegrees:", yaml);
            Assert.IsNotNull(asset.infill);
            Assert.AreEqual(2500, asset.infill.maxInfill);
        }

        [Test]
        public void SavedAsset_HasKosRaflesia3()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
            Assert.IsNotNull(asset);
            var kos = asset.keyBuildings.SingleOrDefault(k => k.osmId == 409467778);
            Assert.IsNotNull(kos);
            Assert.AreEqual("Kos Raflesia 3", kos.label);
        }

        [Test]
        public void BuildingRule_Matches()
        {
            var rule = new BuildingRule { buildingValues = new[] { "shed" }, context = BuildingContext.Any };
            Assert.IsTrue(rule.Matches("shed", false));
            Assert.IsFalse(rule.Matches("house", false));
            var campusOnly = new BuildingRule { buildingValues = new string[0], context = BuildingContext.Campus };
            Assert.IsTrue(campusOnly.Matches("yes", true));
            Assert.IsFalse(campusOnly.Matches("yes", false));
        }

        [Test]
        public void TagMatch_EmptyValuesMatchesAnyValue()
        {
            var any = new TagMatch("highway");
            Assert.IsTrue(any.Matches(new Dictionary<string, string> { { "highway", "x" } }));
            Assert.IsFalse(any.Matches(new Dictionary<string, string> { { "building", "x" } }));
        }

        static string NameOf(Material m) => m != null ? m.name : null;

        [Test]
        public void SavedAsset_HasEdgeMaterials()
        {
            var e = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset).roadEdges;
            Assert.AreEqual("Edge_Concrete", NameOf(e.concrete));
            Assert.AreEqual("Edge_Gutter", NameOf(e.gutter));
            Assert.AreEqual("Edge_Kerb", NameOf(e.kerb));
            Assert.AreEqual("Edge_Sidewalk", NameOf(e.sidewalk));
            Assert.AreEqual("Road_Marking", NameOf(e.marking));
        }

        [Test]
        public void SavedAsset_HasFrontageMaterials()
        {
            var f = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset).frontage;
            Assert.AreEqual("Frontage_WallAtlas", NameOf(f.wallAtlas));
            Assert.AreEqual("Frontage_Fence", NameOf(f.fence));
            Assert.AreEqual("Frontage_GateMetal", NameOf(f.gateMetal));
        }
    }
}
