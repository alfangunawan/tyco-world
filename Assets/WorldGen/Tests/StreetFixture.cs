using System;
using System.Linq;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    /// <summary>
    /// Synthetic street block for the street-frontage tests (spec street-frontage §14), local metres (x east, z north):
    ///
    ///   z 64..120  campus (amenity=university), x 0..200
    ///   z 60       T1 tertiary, lanes 2 (6 m): (0,60) → (100,60) → (200,60)
    ///   x 100      S1 service (100,0) → (100,60)
    ///   z 4.5      drain (waterway, 1.5 m) x 0..90, north of R1
    ///   kos        x 120..130, z 10..20 (way 409467778, key building without prefab)
    ///   z 0        R1 residential (5 m): (0,0) → (100,0) → (200,0)
    ///   south of R1: H1 setback house x 20..30 (front z -7.7, 4 m behind the property line), footway F1 at x 40,
    ///                H2 flush house x 60..70 (front z -4.2), driveway D1 at x 150,
    ///                park x 160..200 z -40..-4, shed x 180..185 z -10..-3 (over R1's south edge band).
    /// </summary>
    internal sealed class StreetFixture
    {
        public const long Residential = 1001, Tertiary = 1002, Service = 1003, Driveway = 1004, Footway = 1005;
        public const long Campus = 1010, Park = 1011, SetbackHouse = 1020, FlushHouse = 1021, Shed = 1022, Drain = 1030;
        public const long Kos = 409467778;

        public readonly WorldGenSettings Settings;
        public readonly GenerationReport Report = new GenerationReport();
        public readonly ClassifiedWorld World;
        public readonly RoadNetwork Network;
        public readonly StreetContext Context;

        public StreetFixture(WorldGenSettings settings, Action<TestOsm> extra = null)
        {
            Settings = settings;
            var osm = Osm();
            extra?.Invoke(osm);
            World = new FeatureClassifier(settings, Report).Classify(osm.Data);
            var campus = World.Areas.Where(a => a.Rule != null && a.Rule.category == AreaCategory.Campus).Select(a => a.Shape).ToList();
            Network = new RoadNetwork(World.Roads, campus, settings.roadEdges);
            Context = StreetContext.From(World, settings);
        }

        public static TestOsm Osm()
        {
            var osm = new TestOsm();
            osm.Way(Residential, new[] { new Vector2(0f, 0f), new Vector2(100f, 0f), new Vector2(200f, 0f) }, false, ("highway", "residential"));
            osm.Way(Tertiary, new[] { new Vector2(0f, 60f), new Vector2(100f, 60f), new Vector2(200f, 60f) }, false, ("highway", "tertiary"), ("lanes", "2"));
            osm.Way(Service, new[] { new Vector2(100f, 0f), new Vector2(100f, 60f) }, false, ("highway", "service"));
            osm.Way(Driveway, new[] { new Vector2(150f, 0f), new Vector2(150f, -20f) }, false, ("highway", "service"), ("service", "driveway"));
            osm.Way(Footway, new[] { new Vector2(40f, 0f), new Vector2(40f, -30f) }, false, ("highway", "footway"));
            osm.Rect(Campus, 0f, 64f, 200f, 56f, ("amenity", "university"));
            osm.Rect(Park, 160f, -40f, 40f, 36f, ("leisure", "park"));
            osm.Rect(SetbackHouse, 20f, -17.7f, 10f, 10f, ("building", "house"));
            osm.Rect(FlushHouse, 60f, -14.2f, 10f, 10f, ("building", "house"));
            osm.Rect(Shed, 180f, -10f, 5f, 7f, ("building", "shed"));
            osm.Way(Drain, new[] { new Vector2(0f, 4.5f), new Vector2(90f, 4.5f) }, false, ("waterway", "drain"));
            osm.Rect(Kos, 120f, 10f, 10f, 10f, ("building", "house"));
            return osm;
        }

        public RoadChain Chain(long roadId) => Network.Chains.Single(c => c.Members.Any(m => m.Road.Id == roadId));
    }
}
