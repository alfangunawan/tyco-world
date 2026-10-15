using System;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>What lines one side of a road: nothing, a concrete shoulder + open gutter, or a raised kerbed sidewalk.</summary>
    public enum EdgeProfile { None, Gutter, Sidewalk }

    public enum WallStyle { Plaster, LowFence, Brick, Campus, Cemetery }

    /// <summary>
    /// Road dressing (spec street-frontage §4–§6, §9, §12). Field initializers are the defaults, so settings assets saved
    /// before this class existed pick them up when they load.
    /// </summary>
    [Serializable]
    public sealed class RoadEdgeSettings
    {
        public string[] sidewalkHighways = { "primary", "secondary" };
        public string[] campusSidewalkHighways = { "tertiary" };
        public string[] gutterHighways = { "tertiary", "unclassified", "residential", "living_street", "service" };
        public string[] noEdgeServiceValues = { "driveway", "parking_aisle", "drive-through" };
        public float campusSidewalkDistance = 10f;
        public float minProfileRun = 8f;
        public float sampleStep = 0.5f;
        /// <summary>sampleStep clamped to a sane minimum so a zero or negative setting cannot hang or overflow the sampling loops.</summary>
        public float SafeSampleStep => Mathf.Max(sampleStep, 0.05f);
        public float mouthMargin = 0.3f;
        public float minRunLength = 0.5f;

        [Header("Cross-section")]
        public float edgeBaseY = 0.055f;
        public float gutterLipY = 0.155f;
        public float sidewalkTopY = 0.21f;
        public float underlap = 0.05f;
        public float shoulderWidth = 0.30f;
        public float gutterWidth = 0.50f;
        public float gutterLipWidth = 0.08f;
        public float sidewalkWidth = 1.5f;
        public float kerbWidth = 0.15f;

        [Header("Markings")]
        public string[] markingHighways = { "primary", "secondary" };
        public string[] wideMarkingHighways = { "tertiary" };
        public float markingMinWidth = 7f;
        public float markingWidth = 0.12f;
        public float markingLift = 0.003f;
        public float dashLength = 3f;
        public float dashGap = 5f;
        public float dashStart = 2f;
        public float junctionDashClearance = 3f;

        [Header("Lamps")]
        public float sidewalkLampOffset = 0.45f;
        public float gutterLampOffset = 0.15f;

        [Header("Materials")]
        public Material concrete;
        public Material gutter;
        public Material kerb;
        public Material sidewalk;
        public Material marking;

        public float GutterProfileWidth => shoulderWidth + gutterWidth;

        public float Width(EdgeProfile profile) =>
            profile == EdgeProfile.Sidewalk ? sidewalkWidth : profile == EdgeProfile.Gutter ? GutterProfileWidth : 0f;
    }

    /// <summary>One wall look: residential styles have a share; context styles (Campus, Cemetery) have share 0.</summary>
    [Serializable]
    public sealed class WallStyleRule
    {
        public WallStyle style;
        public float share;
        public float minHeight;
        public float maxHeight;
        public float fenceHeight;
        public int firstBand;
        public int bandCount = 1;

        public WallStyleRule() { }

        public WallStyleRule(WallStyle style, float share, float minHeight, float maxHeight, float fenceHeight, int firstBand, int bandCount)
        {
            this.style = style;
            this.share = share;
            this.minHeight = minHeight;
            this.maxHeight = maxHeight;
            this.fenceHeight = fenceHeight;
            this.firstBand = firstBand;
            this.bandCount = bandCount;
        }
    }

    /// <summary>Property-line walls, gates and slabs (spec street-frontage §7, §12).</summary>
    [Serializable]
    public sealed class FrontageSettings
    {
        public float propertyLineGap = 0.4f;
        public float corridorDepth = 15f;
        public float flushDistance = 1f;
        public float minInterval = 1.5f;
        public float contextProbe = 0.5f;
        public float wallThickness = 0.15f;
        public float copingHeight = 0.05f;
        public float copingOverhang = 0.02f;
        public float pilasterSize = 0.25f;
        public float pilasterSpacing = 3f;
        public float pilasterExtra = 0.1f;
        public float pilasterMinGap = 1f;
        public float pilasterMinTurnDegrees = 20f;
        public float gateWidth = 2f;
        public float doorWidth = 0.9f;
        public float gateHeight = 1.6f;
        public float gateMinLot = 4.5f;
        public float doorMinLot = 2.5f;
        public float gateEndMargin = 0.3f;
        /// <summary>Most centreline turn allowed under a gate and its posts; the gate slides along its lot to find such a stretch (spec frontage-polish §2.3).</summary>
        public float gateMaxTurnDegrees = 5f;
        public float slabTopY = 0.165f;
        public float slabExtraWidth = 0.4f;
        public float kosExclusionRadius = 8f;
        public long kosBuildingId = 409467778;

        [Header("Wall atlas")]
        public int atlasBands = 8;
        public float atlasTileMeters = 4f;
        public float atlasBandMeters = 2f;

        public WallStyleRule[] styles =
        {
            new WallStyleRule(WallStyle.Plaster, 0.55f, 1.6f, 2.0f, 0f, 0, 6),
            new WallStyleRule(WallStyle.LowFence, 0.35f, 0.8f, 0.8f, 0.9f, 0, 6),
            new WallStyleRule(WallStyle.Brick, 0.10f, 1.7f, 1.7f, 0f, 6, 2),
            new WallStyleRule(WallStyle.Campus, 0f, 0.6f, 0.6f, 1.2f, 5, 1),
            new WallStyleRule(WallStyle.Cemetery, 0f, 0.7f, 0.7f, 0f, 5, 1)
        };

        [Header("Materials")]
        public Material wallAtlas;
        public Material fence;
        public Material gateMetal;

        public WallStyleRule Style(WallStyle style)
        {
            if (styles == null) return null;
            foreach (var s in styles)
                if (s != null && s.style == style) return s;
            return null;
        }

        /// <summary>Residential style (share &gt; 0) chosen by a uniform roll in [0, 1).</summary>
        public WallStyleRule PickResidential(double roll)
        {
            double total = 0;
            foreach (var s in styles)
                if (s != null && s.share > 0f) total += s.share;
            double acc = 0;
            WallStyleRule last = null;
            foreach (var s in styles)
            {
                if (s == null || s.share <= 0f) continue;
                acc += s.share / total;
                last = s;
                if (roll < acc) return s;
            }
            return last;
        }
    }

    /// <summary>Made-up houses on empty lots beside roads (spec street-frontage §8, §12).</summary>
    [Serializable]
    public sealed class InfillSettings
    {
        public bool enabled = true;
        public float minLotLength = 6f;
        public float minWidth = 6f;
        public float maxWidth = 10f;
        public float minDepth = 8f;
        public float maxDepth = 12f;
        public float minGap = 0f;
        public float maxGap = 0.6f;
        public float flushShare = 0.35f;
        public float flushInset = 0.1f;
        public float minSetback = 1.5f;
        public float maxSetback = 4f;
        public float twoStoreyShare = 0.4f;
        public float clearance = 0.5f;
        public int maxInfill = 2500;
    }

    /// <summary>
    /// Roads clear of buildings (spec road-clearance §2). Field initializers are the defaults, so settings assets saved
    /// before this class existed pick them up when they load.
    /// </summary>
    [Serializable]
    public sealed class RoadClearanceSettings
    {
        public bool enabled = true;
        /// <summary>Clear distance from a road's centreline = half its width + this.</summary>
        public float margin = 0.3f;
        /// <summary>A house further into the road than this is dropped, not moved.</summary>
        public float maxShift = 2.0f;
        public float shiftExtra = 0.05f;
        /// <summary>Unnamed, non-campus, non-key buildings smaller than this (m²) count as houses.</summary>
        public float houseMaxArea = 300f;
        /// <summary>Step along a road when finding where it runs into a building.</summary>
        public float sampleStep = 0.5f;
        /// <summary>Road pieces left shorter than this after a cut are dropped.</summary>
        public float minPieceLength = 3f;
    }

    /// <summary>Barriers at dead ends (spec road-clearance §3). Field initializers are the defaults.</summary>
    [Serializable]
    public sealed class DeadEndSettings
    {
        public bool enabled = true;
        /// <summary>An end nearer the map edge than this gets the road-closed barrier.</summary>
        public float boundaryDistance = 20f;
        /// <summary>The barrier stands this far back from the end of the road.</summary>
        public float endInset = 0.3f;
        public float minWidth = 1.2f;
        /// <summary>An end within another road's half width + this is a junction, not a dead end.</summary>
        public float joinMargin = 0.5f;
        /// <summary>A building this close past the end already closes it.</summary>
        public float buildingAhead = 2f;
        public float wallHeight = 0.9f;
        public float wallThickness = 0.2f;
        /// <summary>Wall atlas band (plaster) the low walls use.</summary>
        public int wallBand = 1;
        public float portalHeight = 1.0f;
        public float portalPost = 0.1f;
        /// <summary>Spacing of the concrete blocks across a road at the map edge (one block is 1.9 m long).</summary>
        public float blockSpacing = 1.9f;
        public string[] portalHighways = { "service", "track" };
        public Material stripe;
    }
}
