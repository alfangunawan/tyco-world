using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Single source of the spec's default values.</summary>
    public static class DefaultSettingsFactory
    {
        public static WorldGenSettings CreateInMemory()
        {
            var s = ScriptableObject.CreateInstance<WorldGenSettings>();
            Populate(s);
            return s;
        }

        public static WorldGenSettings LoadOrCreateAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WorldGenSettings>(WorldGenPaths.SettingsAsset);
            if (existing != null) return existing;
            AssetFolders.Ensure(WorldGenPaths.ConfigFolder);
            var s = CreateInMemory();
            AssetDatabase.CreateAsset(s, WorldGenPaths.SettingsAsset);
            AssetDatabase.SaveAssets();
            return s;
        }

        public static void Populate(WorldGenSettings s)
        {
            s.south = -6.9850;
            s.west = 107.6255;
            s.north = -6.9675;
            s.east = 107.6380;
            s.originLat = -6.97265;
            s.originLon = 107.63400;
            s.chunkSize = 100f;

            s.overpassEndpoints = new[]
            {
                "https://overpass-api.de/api/interpreter",
                "https://overpass.kumi.systems/api/interpreter",
                "https://overpass.private.coffee/api/interpreter"
            };
            s.timeoutSeconds = 90;
            s.rawDataPath = "WorldGenData/osm_raw.json";

            s.lanesWidth = 3f;
            s.roadBaseY = 0.060f;
            s.roadRankStep = 0.005f;
            s.roadRules = new List<RoadRule>
            {
                new RoadRule("primary", 8f, 8, 30f),
                new RoadRule("secondary", 7f, 7, 30f),
                new RoadRule("tertiary", 6f, 6, 30f),
                new RoadRule("unclassified", 5f, 5, 40f),
                new RoadRule("residential", 5f, 4, 40f),
                new RoadRule("living_street", 4f, 3, 0f),
                new RoadRule("pedestrian", 3f, 3, 0f),
                new RoadRule("service", 3.5f, 2, 50f),
                new RoadRule("track", 3f, 1, 0f),
                new RoadRule("footway", 1.5f, 0, 0f),
                new RoadRule("path", 1.5f, 0, 0f),
                new RoadRule("cycleway", 1.5f, 0, 0f),
                new RoadRule("steps", 1.5f, 0, 0f)
            };

            s.levelHeight = 3.2f;
            s.facadeTileWidth = 3f;
            s.parapetHeight = 0.6f;
            s.gableMaxArea = 200f;
            s.gableMinRectangularity = 0.85f;
            s.gablePitchRatio = 0.25f;
            s.gableOverhang = 0.3f;
            s.minWallHeight = 2.5f;
            s.buildingRules = new List<BuildingRule>
            {
                Rule("Dormitory", BuildingContext.Any, 4, 4, BuildingMaterialSetId.Campus, "dormitory"),
                Rule("Institutional", BuildingContext.Any, 3, 3, BuildingMaterialSetId.Campus, "university", "school", "college", "office", "public", "hospital"),
                Rule("Religious", BuildingContext.Any, 1, 1, BuildingMaterialSetId.Residential, "mosque", "church", "temple", "religious"),
                Rule("Commercial", BuildingContext.Any, 2, 2, BuildingMaterialSetId.Residential, "commercial", "retail", "shop"),
                Rule("Small", BuildingContext.Any, 1, 1, BuildingMaterialSetId.Residential, "garage", "garages", "shed", "hut", "carport", "kiosk"),
                Rule("RoofOnly", BuildingContext.Any, 1, 1, BuildingMaterialSetId.Residential, "roof"),
                Rule("CampusOther", BuildingContext.Campus, 3, 3, BuildingMaterialSetId.Campus),
                Rule("Residential", BuildingContext.NonCampus, 1, 3, BuildingMaterialSetId.Residential)
            };
            s.buildingRules[2].levelHeightOverride = 5f;
            s.buildingRules[4].allowGable = true;
            s.buildingRules[5].roofOnly = true;
            s.buildingRules[7].allowGable = true;
            s.residentialMaterials = new BuildingMaterialSet();
            s.campusMaterials = new BuildingMaterialSet();

            s.areaRules = new List<AreaRule>
            {
                Area("Parking", AreaCategory.Parking, 0.030f, 0f, new TagMatch("amenity", "parking")),
                Area("Cemetery", AreaCategory.Cemetery, 0.030f, 10f, new TagMatch("amenity", "grave_yard"), new TagMatch("landuse", "cemetery")),
                Area("Pitch", AreaCategory.Pitch, 0.030f, 0f, new TagMatch("leisure", "pitch", "track", "sports_centre", "stadium")),
                Area("Farmland", AreaCategory.Farmland, 0.025f, 0f, new TagMatch("landuse", "farmland", "paddy")),
                Area("Wood", AreaCategory.Wood, 0.025f, 4f, new TagMatch("natural", "wood", "scrub"), new TagMatch("landuse", "forest")),
                Area("Grass", AreaCategory.Grass, 0.025f, 12f, new TagMatch("landuse", "grass", "village_green", "meadow", "recreation_ground"), new TagMatch("leisure", "park", "garden"), new TagMatch("natural", "grassland")),
                Area("Hardground", AreaCategory.Hardground, 0.020f, 0f, new TagMatch("landuse", "commercial", "retail", "industrial", "construction", "brownfield")),
                Area("Campus", AreaCategory.Campus, 0.015f, 20f, new TagMatch("amenity", "university")),
                Area("Paving", AreaCategory.Paving, 0.035f, 0f)
            };

            s.waterAreaY = 0.045f;
            s.waterLineY = 0.050f;
            s.waterwayWidths = new List<WaterwayWidth>
            {
                new WaterwayWidth("river", 8f),
                new WaterwayWidth("canal", 4f),
                new WaterwayWidth("stream", 3f),
                new WaterwayWidth("drain", 1.5f),
                new WaterwayWidth("ditch", 1.5f)
            };

            s.treePrefabs = new GameObject[0];
            s.lampPrefab = null;
            s.maxTrees = 4000;
            s.lampMinSpacing = 8f;
            s.lampOffset = 0.6f;

            s.keyBuildings = new List<KeyBuilding>
            {
                new KeyBuilding(1257717375, "Telkom University Landmark Tower (TULT)"),
                new KeyBuilding(197341486, "Gedung Bangkit (Rektorat)"),
                new KeyBuilding(1293369802, "Gedung Tokong Nanas"),
                new KeyBuilding(1293369773, "Asrama Putra Gedung 1"),
                new KeyBuilding(1293369796, "Asrama Putri Gedung A"),
                new KeyBuilding(409467778, "Kos Raflesia 3")
            };
        }

        static BuildingRule Rule(string name, BuildingContext context, int minLevels, int maxLevels, BuildingMaterialSetId set, params string[] values) =>
            new BuildingRule { name = name, context = context, minLevels = minLevels, maxLevels = maxLevels, materialSet = set, buildingValues = values };

        static AreaRule Area(string name, AreaCategory category, float y, float treeSpacing, params TagMatch[] matches) =>
            new AreaRule { name = name, category = category, yOffset = y, treeSpacing = treeSpacing, matches = matches };
    }
}
