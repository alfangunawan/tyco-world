using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Spawn fallbacks: the desired point first, then rings of candidates nearest-first.</summary>
    public static class SpawnNudge
    {
        public static List<Vector3> Candidates(Vector3 origin, float step = 0.5f, float maxRadius = 2f, int perRing = 8)
        {
            var list = new List<Vector3> { origin };
            int rings = Mathf.FloorToInt(maxRadius / step + 1e-4f);
            for (int ring = 1; ring <= rings; ring++)
            {
                float r = ring * step;
                for (int i = 0; i < perRing; i++)
                {
                    float a = 2f * Mathf.PI * i / perRing;
                    list.Add(origin + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
            }
            return list;
        }
    }
}
