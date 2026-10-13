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
    }
}
