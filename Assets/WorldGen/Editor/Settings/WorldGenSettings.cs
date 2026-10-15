using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>All world generator parameters. Defaults come from DefaultSettingsFactory.Populate (spec values).</summary>
    [CreateAssetMenu(menuName = "Tyco World/World Gen Settings", fileName = "WorldGenSettings")]
    public sealed class WorldGenSettings : ScriptableObject
    {
        [Header("Area")]
        public double south;
        public double west;
        public double north;
        public double east;
        public double originLat;
        public double originLon;
        public float chunkSize;

        [Header("Download")]
        public string[] overpassEndpoints = new string[0];
        public int timeoutSeconds;
        public string rawDataPath = "";

        [Header("Roads")]
        public List<RoadRule> roadRules = new List<RoadRule>();
        public float lanesWidth;
        public float roadBaseY;
        public float roadRankStep;

        [Header("Buildings")]
        public float levelHeight;
        public float facadeTileWidth;
        public float parapetHeight;
        public float gableMaxArea;
        public float gableMinRectangularity;
        public float gablePitchRatio;
        public float gableOverhang;
        public float minWallHeight;
        public List<BuildingRule> buildingRules = new List<BuildingRule>();
        public BuildingMaterialSet residentialMaterials = new BuildingMaterialSet();
        public BuildingMaterialSet campusMaterials = new BuildingMaterialSet();

        [Header("Areas")]
        public List<AreaRule> areaRules = new List<AreaRule>();

        [Header("Water")]
        public Material waterMaterial;
        public float waterAreaY;
        public float waterLineY;
        public List<WaterwayWidth> waterwayWidths = new List<WaterwayWidth>();

        [Header("Ground")]
        public Material groundMaterial;

        [Header("Props")]
        public GameObject[] treePrefabs = new GameObject[0];
        public GameObject lampPrefab;
        public int maxTrees;
        public float lampMinSpacing;
        public float lampOffset;

        [Header("Street dressing")]
        public RoadEdgeSettings roadEdges = new RoadEdgeSettings();
        public FrontageSettings frontage = new FrontageSettings();
        public InfillSettings infill = new InfillSettings();
        public RoadClearanceSettings clearance = new RoadClearanceSettings();
        public DeadEndSettings deadEnds = new DeadEndSettings();
        [Tooltip("Dead-end props: 0 potted plant, 1 concrete block, 2 road-closed barricade with its sign.")]
        public GameObject[] barrierPrefabs = new GameObject[0];

        [Header("Key buildings")]
        public List<KeyBuilding> keyBuildings = new List<KeyBuilding>();

        [Header("Other")]
        public Material fallbackMaterial;

        public GeoBounds Bounds => new GeoBounds(south, west, north, east);

        public GeoProjection CreateProjection() => new GeoProjection(originLat, originLon);

        public BuildingMeshParams MeshParams => new BuildingMeshParams
        {
            FacadeTileWidth = facadeTileWidth,
            ParapetHeight = parapetHeight,
            GableMaxArea = gableMaxArea,
            GableMinRectangularity = gableMinRectangularity,
            GablePitchRatio = gablePitchRatio,
            GableOverhang = gableOverhang
        };

        public BuildingMaterialSet MaterialSet(BuildingMaterialSetId id) =>
            id == BuildingMaterialSetId.Campus ? campusMaterials : residentialMaterials;

        void Reset() => DefaultSettingsFactory.Populate(this);
    }
}
