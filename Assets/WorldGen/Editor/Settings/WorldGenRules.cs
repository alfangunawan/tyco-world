using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    [Serializable]
    public sealed class RoadRule
    {
        public string highway = "";
        public float defaultWidth;
        public int rank;
        public float lampSpacing;
        public Material material;

        public RoadRule() { }

        public RoadRule(string highway, float defaultWidth, int rank, float lampSpacing)
        {
            this.highway = highway;
            this.defaultWidth = defaultWidth;
            this.rank = rank;
            this.lampSpacing = lampSpacing;
        }
    }

    public enum BuildingContext { Any, Campus, NonCampus }

    public enum BuildingMaterialSetId { Residential, Campus }

    [Serializable]
    public sealed class BuildingRule
    {
        public string name = "";
        public string[] buildingValues = new string[0];
        public BuildingContext context;
        public int minLevels = 1;
        public int maxLevels = 1;
        public float levelHeightOverride;
        public bool allowGable;
        public bool roofOnly;
        public BuildingMaterialSetId materialSet;

        public bool Matches(string buildingValue, bool isCampus)
        {
            if (context == BuildingContext.Campus && !isCampus) return false;
            if (context == BuildingContext.NonCampus && isCampus) return false;
            if (buildingValues == null || buildingValues.Length == 0) return true;
            foreach (var v in buildingValues)
                if (v == buildingValue) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class BuildingMaterialSet
    {
        public Material[] groundFloor = new Material[0];
        public Material[] upper = new Material[0];
        public Material[] trim = new Material[0];
        public Material[] flatRoof = new Material[0];
        public Material[] gableRoof = new Material[0];
    }

    [Serializable]
    public sealed class TagMatch
    {
        public string key = "";
        public string[] values = new string[0];

        public TagMatch() { }

        public TagMatch(string key, params string[] values)
        {
            this.key = key;
            this.values = values ?? new string[0];
        }

        public bool Matches(IReadOnlyDictionary<string, string> tags)
        {
            if (tags == null || !tags.TryGetValue(key, out var value)) return false;
            if (values == null || values.Length == 0) return true;
            foreach (var v in values)
                if (v == value) return true;
            return false;
        }
    }

    public enum AreaCategory { Campus, Hardground, Grass, Wood, Farmland, Parking, Cemetery, Pitch, Paving }

    [Serializable]
    public sealed class AreaRule
    {
        public string name = "";
        public TagMatch[] matches = new TagMatch[0];
        public AreaCategory category;
        public float yOffset;
        public float treeSpacing;
        public Material material;

        public bool Matches(IReadOnlyDictionary<string, string> tags)
        {
            if (matches == null) return false;
            foreach (var m in matches)
                if (m != null && m.Matches(tags)) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class WaterwayWidth
    {
        public string waterway = "";
        public float width;

        public WaterwayWidth() { }

        public WaterwayWidth(string waterway, float width)
        {
            this.waterway = waterway;
            this.width = width;
        }
    }

    [Serializable]
    public sealed class KeyBuilding
    {
        public long osmId;
        public string label = "";
        public GameObject prefab;
        public int overrideLevels;
        public RoofOverride overrideRoof;

        public KeyBuilding() { }

        public KeyBuilding(long osmId, string label)
        {
            this.osmId = osmId;
            this.label = label;
        }
    }
}
