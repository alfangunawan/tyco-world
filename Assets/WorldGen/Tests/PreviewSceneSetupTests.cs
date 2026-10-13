using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class PreviewSceneSetupTests
    {
        static readonly GeoProjection Projection = new GeoProjection(-6.97265, 107.63400);

        [Test]
        public void StreetPose_OnSukabirus()
        {
            var data = OsmParser.Parse(FixtureFiles.Read("sukabirus_small.json"));
            Assert.IsTrue(PreviewSceneSetup.TryComputeStreetPose(data, Projection, PreviewSceneSetup.SukabirusWayId, out var position, out float yaw));
            Assert.AreEqual(PreviewSceneSetup.EyeHeight, position.y, 1e-5f);
            var bounds = Projection.ToLocalRect(new GeoBounds(-6.9850, 107.6255, -6.9675, 107.6380));
            Assert.IsTrue(bounds.Contains(new Vector2(position.x, position.z)));

            var way = data.Ways[PreviewSceneSetup.SukabirusWayId];
            var ground = new Vector2(position.x, position.z);
            float nearest = float.MaxValue;
            var nearestDir = Vector2.zero;
            for (int i = 0; i + 1 < way.NodeIds.Count; i++)
            {
                var a = data.Nodes[way.NodeIds[i]];
                var b = data.Nodes[way.NodeIds[i + 1]];
                Vector2 pa = Projection.ToLocal(a.Lat, a.Lon), pb = Projection.ToLocal(b.Lat, b.Lon);
                float d = Polygon2D.DistanceToSegment(ground, pa, pb);
                if (d < nearest && (pb - pa).sqrMagnitude > 1e-6f)
                {
                    nearest = d;
                    nearestDir = (pb - pa).normalized;
                }
            }
            Assert.Less(nearest, 0.5f);
            var forward = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
            Assert.Greater(Mathf.Abs(Vector2.Dot(forward, nearestDir)), 0.99f);
        }

        [Test]
        public void StreetPose_MissingWay_ReturnsFalse()
        {
            Assert.IsFalse(PreviewSceneSetup.TryComputeStreetPose(new OsmData(), Projection, 1, out _, out _));
        }
    }
}
