using System.Collections.Generic;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Joins multipolygon member ways (node id sequences) into closed rings.</summary>
    public static class RingAssembler
    {
        public static bool TryAssemble(IReadOnlyList<IReadOnlyList<long>> ways, out List<List<long>> rings)
        {
            rings = new List<List<long>>();
            var pool = new List<List<long>>();
            foreach (var way in ways)
                if (way != null && way.Count >= 2) pool.Add(new List<long>(way));

            while (pool.Count > 0)
            {
                var ring = pool[0];
                pool.RemoveAt(0);
                while (!(ring.Count >= 4 && ring[0] == ring[ring.Count - 1]))
                {
                    long tail = ring[ring.Count - 1];
                    int found = -1;
                    bool reverse = false;
                    for (int i = 0; i < pool.Count; i++)
                    {
                        if (pool[i][0] == tail) { found = i; break; }
                        if (pool[i][pool[i].Count - 1] == tail) { found = i; reverse = true; break; }
                    }
                    if (found < 0) return false;
                    var next = pool[found];
                    pool.RemoveAt(found);
                    if (reverse) next.Reverse();
                    for (int k = 1; k < next.Count; k++) ring.Add(next[k]);
                }
                rings.Add(ring);
            }
            return true;
        }
    }
}
