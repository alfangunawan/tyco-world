using System;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Latitude/longitude bounding box in degrees.</summary>
    [Serializable]
    public struct GeoBounds
    {
        public double South, West, North, East;

        public GeoBounds(double south, double west, double north, double east)
        {
            South = south;
            West = west;
            North = north;
            East = east;
        }

        public bool Contains(double lat, double lon) =>
            lat >= South && lat <= North && lon >= West && lon <= East;
    }
}
