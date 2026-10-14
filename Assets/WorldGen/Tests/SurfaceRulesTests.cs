using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class SurfaceRulesTests
    {
        [TestCase("Road_Asphalt", SurfaceType.Asphalt)]
        [TestCase("Path_Paving", SurfaceType.Paving)]
        [TestCase("Area_Campus", SurfaceType.Paving)]
        [TestCase("Area_Parking", SurfaceType.Concrete)]
        [TestCase("Area_Hardground", SurfaceType.Concrete)]
        [TestCase("Area_Grass", SurfaceType.Grass)]
        [TestCase("Area_Farmland", SurfaceType.Grass)]
        [TestCase("Area_Pitch", SurfaceType.Grass)]
        [TestCase("Ground_Dirt", SurfaceType.Dirt)]
        [TestCase("Area_Cemetery", SurfaceType.Dirt)]
        [TestCase("Edge_Sidewalk", SurfaceType.Paving)]
        [TestCase("Edge_Concrete", SurfaceType.Concrete)]
        [TestCase("Edge_Gutter", SurfaceType.Concrete)]
        [TestCase("Water", SurfaceType.Water)]
        [TestCase("M_Res_Ground_KosDoor", SurfaceType.Default)]
        [TestCase("", SurfaceType.Default)]
        public void ForMaterialName_MapsDrawnMaterial(string name, SurfaceType expected)
        {
            Assert.AreEqual(expected, SurfaceRules.ForMaterialName(name));
        }

        [Test]
        public void For_RoadEdgeWithoutMaterial_UsesItsName()
        {
            Assert.AreEqual(SurfaceType.Paving, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.RoadEdge, Name = RoadEdgeBuilder.SidewalkName }));
            Assert.AreEqual(SurfaceType.Concrete, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.RoadEdge, Name = RoadEdgeBuilder.GutterName }));
            Assert.AreEqual(SurfaceType.Default, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.Frontage, Name = "Walls" }));
        }

        [TestCase("primary", SurfaceType.Asphalt)]
        [TestCase("residential", SurfaceType.Asphalt)]
        [TestCase("living_street", SurfaceType.Asphalt)]
        [TestCase("service", SurfaceType.Asphalt)]
        [TestCase("footway", SurfaceType.Paving)]
        [TestCase("pedestrian", SurfaceType.Paving)]
        [TestCase("steps", SurfaceType.Paving)]
        [TestCase("track", SurfaceType.Dirt)]
        public void ForRoad_FollowsRoadMaterial(string highway, SurfaceType expected)
        {
            Assert.AreEqual(expected, SurfaceRules.ForRoad(highway));
            Assert.AreEqual(SurfaceRules.ForMaterialName(ArtAssetBuilder.RoadMaterialName(highway)), SurfaceRules.ForRoad(highway));
        }

        [TestCase(AreaCategory.Grass, SurfaceType.Grass)]
        [TestCase(AreaCategory.Wood, SurfaceType.Grass)]
        [TestCase(AreaCategory.Farmland, SurfaceType.Grass)]
        [TestCase(AreaCategory.Pitch, SurfaceType.Grass)]
        [TestCase(AreaCategory.Parking, SurfaceType.Concrete)]
        [TestCase(AreaCategory.Hardground, SurfaceType.Concrete)]
        [TestCase(AreaCategory.Campus, SurfaceType.Paving)]
        [TestCase(AreaCategory.Paving, SurfaceType.Paving)]
        [TestCase(AreaCategory.Cemetery, SurfaceType.Dirt)]
        public void ForArea_FollowsAreaMaterial(AreaCategory category, SurfaceType expected)
        {
            Assert.AreEqual(expected, SurfaceRules.ForArea(category));
        }

        [Test]
        public void EveryDefaultRule_IsWalkable()
        {
            var s = DefaultSettingsFactory.CreateInMemory();
            try
            {
                foreach (var road in s.roadRules) Assert.AreNotEqual(SurfaceType.Default, SurfaceRules.ForRoad(road.highway), road.highway);
                foreach (var area in s.areaRules) Assert.AreNotEqual(SurfaceType.Default, SurfaceRules.ForArea(area.category), area.name);
            }
            finally
            {
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void For_PrefersDrawnMaterialOverCategory()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "Area_Parking" };
            try
            {
                var feature = new BuiltFeature { Kind = FeatureKind.Area, Category = "Grass", Materials = new[] { material } };
                Assert.AreEqual(SurfaceType.Concrete, SurfaceRules.For(feature));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void For_FallsBackToKindAndCategory()
        {
            Assert.AreEqual(SurfaceType.Asphalt, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.Road, Category = "residential" }));
            Assert.AreEqual(SurfaceType.Grass, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.Area, Category = "Grass" }));
            Assert.AreEqual(SurfaceType.Water, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.WaterArea }));
            Assert.AreEqual(SurfaceType.Water, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.WaterLine }));
            Assert.AreEqual(SurfaceType.Default, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.Building }));
            Assert.AreEqual(SurfaceType.Default, SurfaceRules.For(new BuiltFeature { Kind = FeatureKind.Area, Category = "Unknown" }));
        }

        [Test]
        public void Ground_IsDirt() => Assert.AreEqual(SurfaceType.Dirt, SurfaceRules.Ground);

        [Test]
        public void GroundSurface_StoresType()
        {
            var go = new GameObject("surface");
            try
            {
                var surface = go.AddComponent<GroundSurface>();
                Assert.AreEqual(SurfaceType.Default, surface.Type);
                surface.Initialize(SurfaceType.Grass);
                Assert.AreEqual(SurfaceType.Grass, surface.Type);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
