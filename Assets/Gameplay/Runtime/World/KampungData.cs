using System;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>Kind of a baked world spot (spec #6 §3). Append only: the asset stores the value.</summary>
    public enum SpotKind
    {
        ParkingBike,
        ParkingCar,
        Door,
        Kos,
        Mosque,
        Cemetery,
        CampusGate,
        PosRonda
    }

    /// <summary>A shop front: counter on the property line facing the road, plus the slab across the gutter in front of it.</summary>
    [Serializable]
    public sealed class ShopSite
    {
        public string id = "";
        public ShopType type;
        public string name = "";
        /// <summary>Ground point of the counter (on the property line), metres in world XZ with y = 0.</summary>
        public Vector3 counter;
        /// <summary>Yaw (degrees) the counter faces: toward the road.</summary>
        public float yaw;
        /// <summary>Distance from the counter back to the shoulder (the slab spans it); 0 when there is no gutter to cross.</summary>
        public float slabLength;
        public long buildingId;
    }

    [Serializable]
    public sealed class Spot
    {
        public SpotKind kind;
        public Vector3 position;
        public float yaw;
        /// <summary>Building (doors, kos) or OSM feature (landmarks) the spot belongs to; 0 when none.</summary>
        public long refId;
    }

    /// <summary>A walking lane (or a road line for the map): a polyline between two nodes.</summary>
    [Serializable]
    public sealed class Lane
    {
        public Vector2[] points = new Vector2[0];
        public int startNode = -1;
        public int endNode = -1;
        /// <summary>Road width class: -1 a crossing between lane ends, 0 path, 1 gang/residential, 2 main road.</summary>
        public int width;

        public float Length
        {
            get
            {
                float total = 0f;
                for (int i = 0; i + 1 < points.Length; i++) total += Vector2.Distance(points[i], points[i + 1]);
                return total;
            }
        }
    }

    /// <summary>Where lane ends meet: a corner, a crossing or a dead end.</summary>
    [Serializable]
    public sealed class LaneNode
    {
        public Vector2 position;
        public int[] lanes = new int[0];
    }

    /// <summary>
    /// Gameplay data baked from the same pipeline as the generated world (spec #6 §3): shops, parking, doors, landmarks,
    /// walking lanes and the road lines of the phone map. Rebake after regenerating the world.
    /// </summary>
    public sealed class KampungData : ScriptableObject
    {
        public ShopSite[] shops = new ShopSite[0];
        public Spot[] spots = new Spot[0];
        public Lane[] walkLanes = new Lane[0];
        public LaneNode[] walkNodes = new LaneNode[0];
        public Lane[] mapRoads = new Lane[0];
        public Rect bounds;
        public string summary = "";
    }
}
