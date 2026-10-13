using System;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Local equirectangular projection around an origin. x = east, y = north, in meters.</summary>
    public sealed class GeoProjection
    {
        public double OriginLat { get; }
        public double OriginLon { get; }
        public double MetersPerDegLat { get; }
        public double MetersPerDegLon { get; }

        public GeoProjection(double originLat, double originLon)
        {
            OriginLat = originLat;
            OriginLon = originLon;
            double phi = originLat * Math.PI / 180.0;
            MetersPerDegLat = 111132.92 - 559.82 * Math.Cos(2 * phi) + 1.175 * Math.Cos(4 * phi);
            MetersPerDegLon = 111412.84 * Math.Cos(phi) - 93.5 * Math.Cos(3 * phi) + 0.118 * Math.Cos(5 * phi);
        }

        public Vector2 ToLocal(double lat, double lon) =>
            new Vector2((float)((lon - OriginLon) * MetersPerDegLon), (float)((lat - OriginLat) * MetersPerDegLat));

        public void ToGeo(Vector2 local, out double lat, out double lon)
        {
            lat = OriginLat + local.y / MetersPerDegLat;
            lon = OriginLon + local.x / MetersPerDegLon;
        }

        public Rect ToLocalRect(GeoBounds bounds)
        {
            var min = ToLocal(bounds.South, bounds.West);
            var max = ToLocal(bounds.North, bounds.East);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
