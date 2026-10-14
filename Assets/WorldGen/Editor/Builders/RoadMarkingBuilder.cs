using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Dashed centre line on main roads, written into submesh 1 of the road meshes (spec street-frontage §6).</summary>
    public static class RoadMarkingBuilder
    {
        public const int Submesh = 1;

        public static bool IsMarked(RoadChain chain, RoadEdgeSettings s) =>
            RoadNetwork.Contains(s.markingHighways, chain.Highway) ||
            (RoadNetwork.Contains(s.wideMarkingHighways, chain.Highway) && chain.Width >= s.markingMinWidth - 1e-3f);

        /// <summary>Adds the dashes of every marked chain to the meshes of the ways they lie on; returns the dash count.</summary>
        public static int Build(RoadNetwork net, RoadEdgeSettings s, IReadOnlyDictionary<long, MeshData> roadMeshes)
        {
            int dashes = 0;
            if (s.dashLength <= 0f || s.dashLength + s.dashGap < 0.05f) return 0;
            var near = new Dictionary<RoadChain, float>();
            var along = new List<float>();
            var lineAlong = new List<float>();
            foreach (var chain in net.Chains)
            {
                if (!IsMarked(chain, s)) continue;
                for (float a = s.dashStart; a + s.dashLength <= chain.Length + 1e-4f; a += s.dashLength + s.dashGap)
                {
                    float b = a + s.dashLength;
                    if (NearJunction(chain, a, b, net, s, near)) continue;
                    foreach (var m in chain.Members)
                    {
                        float s0 = Mathf.Max(a, m.S0), s1 = Mathf.Min(b, m.S1);
                        if (s1 - s0 < 0.05f || !roadMeshes.TryGetValue(m.Road.Id, out var mesh)) continue;
                        AddStrip(mesh, chain, s0, s1, s, along, lineAlong);
                    }
                    dashes++;
                }
            }
            return dashes;
        }

        static bool NearJunction(RoadChain chain, float a, float b, RoadNetwork net, RoadEdgeSettings s, Dictionary<RoadChain, float> near)
        {
            for (float x = a; x <= b + 1e-4f; x += 0.5f)
            {
                var p = chain.PointAt(Mathf.Min(x, b), out _);
                net.Nearby(p, net.MaxHalfWidth + s.junctionDashClearance, chain, near);
                foreach (var kv in near)
                    if (kv.Value < kv.Key.HalfWidth + s.junctionDashClearance) return true;
            }
            return false;
        }

        static void AddStrip(MeshData mesh, RoadChain chain, float s0, float s1, RoadEdgeSettings s, List<float> along, List<float> lineAlong)
        {
            var center = Polyline2D.Slice(chain.Points, chain.Cum, s0, s1, along);
            float half = s.markingWidth * 0.5f, y = chain.Y + s.markingLift;
            var left = Polyline2D.Offset(center, along, half, lineAlong);
            var right = Polyline2D.Offset(center, along, -half, lineAlong);
            for (int j = 0; j + 1 < left.Count; j++)
                mesh.AddQuad(Submesh, Xz.At(right[j], y), Xz.At(right[j + 1], y), Xz.At(left[j + 1], y), Xz.At(left[j], y),
                    new Vector2(0f, lineAlong[j]), new Vector2(0f, lineAlong[j + 1]), new Vector2(s.markingWidth, lineAlong[j + 1]),
                    new Vector2(s.markingWidth, lineAlong[j]), Vector3.up);
        }
    }
}
