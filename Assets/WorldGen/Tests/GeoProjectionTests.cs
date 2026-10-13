using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class GeoProjectionTests
    {
        const double Lat0 = -6.97265, Lon0 = 107.63400;
        GeoProjection projection;

        [SetUp]
        public void SetUp() => projection = new GeoProjection(Lat0, Lon0);

        [Test]
        public void Origin_MapsToZero()
        {
            var p = projection.ToLocal(Lat0, Lon0);
            Assert.AreEqual(0f, p.x, 1e-4f);
            Assert.AreEqual(0f, p.y, 1e-4f);
        }

        [Test]
        public void MetersPerDegree_MatchSpec()
        {
            Assert.AreEqual(110590.64, projection.MetersPerDegLat, 0.1);
            Assert.AreEqual(110501.61, projection.MetersPerDegLon, 0.1);
        }

        [Test]
        public void NorthOffset_IsAbout110_59Meters()
        {
            var p = projection.ToLocal(Lat0 + 0.001, Lon0);
            Assert.AreEqual(110.59f, p.y, 0.01f);
            Assert.AreEqual(0f, p.x, 1e-4f);
        }

        [Test]
        public void EastOffset_IsAbout110_50Meters()
        {
            var p = projection.ToLocal(Lat0, Lon0 + 0.001);
            Assert.AreEqual(110.50f, p.x, 0.01f);
            Assert.AreEqual(0f, p.y, 1e-4f);
        }

        [Test]
        public void RoundTrip_ReturnsOriginalCoordinates()
        {
            var p = projection.ToLocal(-6.9771, 107.6311);
            projection.ToGeo(p, out double lat, out double lon);
            Assert.AreEqual(-6.9771, lat, 1e-7);
            Assert.AreEqual(107.6311, lon, 1e-7);
        }

        [Test]
        public void ToLocalRect_MatchesSpecBounds()
        {
            var r = projection.ToLocalRect(new GeoBounds(-6.9850, 107.6255, -6.9675, 107.6380));
            Assert.AreEqual(-939.26f, r.xMin, 0.1f);
            Assert.AreEqual(442.01f, r.xMax, 0.1f);
            Assert.AreEqual(-1365.79f, r.yMin, 0.1f);
            Assert.AreEqual(569.54f, r.yMax, 0.1f);
        }

        [Test]
        public void GeoBounds_Contains()
        {
            var b = new GeoBounds(-6.9850, 107.6255, -6.9675, 107.6380);
            Assert.IsTrue(b.Contains(-6.97265, 107.634));
            Assert.IsFalse(b.Contains(-6.99, 107.634));
        }
    }
}
