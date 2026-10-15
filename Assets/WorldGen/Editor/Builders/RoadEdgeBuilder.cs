using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>One continuous stretch of shoulder + gutter, or of sidewalk, along one side of a chain.</summary>
    public sealed class EdgeRun
    {
        public RoadChain Chain;
        public int Side;
        public EdgeProfile Profile;
        public float S0, S1;
        public float Length => S1 - S0;
    }

    public sealed class RoadEdgeResult
    {
        public readonly List<EdgeRun> Runs = new List<EdgeRun>();
        public readonly Dictionary<Vector2Int, MeshData> Gutter = new Dictionary<Vector2Int, MeshData>();
        public readonly Dictionary<Vector2Int, MeshData> Sidewalk = new Dictionary<Vector2Int, MeshData>();
        public float GutterMeters, SidewalkMeters;
    }

    /// <summary>Shoulders, gutters and kerbed sidewalks beside the asphalt (spec street-frontage §4–§5).</summary>
    public static class RoadEdgeBuilder
    {
        public const string GutterName = "Gutter", SidewalkName = "Sidewalk";
        public const int GutterConcrete = 0, GutterChannel = 1, GutterSubmeshes = 2;
        public const int SidewalkBody = 0, SidewalkKerb = 1, SidewalkConcrete = 2, SidewalkSubmeshes = 3;
        const int BisectSteps = 3;

        /// <summary>Horizontal top surface between offsets X0 and X1 (metres from the asphalt edge) at height Y.</summary>
        readonly struct Strip
        {
            public readonly float X0, X1, Y;
            public readonly int Sub;

            public Strip(float x0, float x1, float y, int sub)
            {
                X0 = x0;
                X1 = x1;
                Y = y;
                Sub = sub;
            }
        }

        /// <summary>Vertical face along the road at offset X; Outward faces away from the road.</summary>
        readonly struct Face
        {
            public readonly float X, Y0, Y1;
            public readonly int Sub;
            public readonly bool Outward;

            public Face(float x, float y0, float y1, int sub, bool outward)
            {
                X = x;
                Y0 = y0;
                Y1 = y1;
                Sub = sub;
                Outward = outward;
            }
        }

        /// <summary>End cap across a run (at its start and end) between offsets X0 and X1.</summary>
        readonly struct Cap
        {
            public readonly float X0, X1, Y0, Y1;
            public readonly int Sub;

            public Cap(float x0, float x1, float y0, float y1, int sub)
            {
                X0 = x0;
                X1 = x1;
                Y0 = y0;
                Y1 = y1;
                Sub = sub;
            }
        }

        sealed class Section
        {
            public readonly List<Strip> Strips = new List<Strip>();
            public readonly List<Face> Faces = new List<Face>();
            public readonly List<Cap> Caps = new List<Cap>();
            /// <summary>Every strip boundary offset, ascending; faces and caps only use these values.</summary>
            public readonly List<float> Xs = new List<float>();

            public void Seal()
            {
                var set = new SortedSet<float>();
                foreach (var s in Strips)
                {
                    set.Add(s.X0);
                    set.Add(s.X1);
                }
                Xs.AddRange(set);
            }
        }

        public static RoadEdgeResult Build(RoadNetwork net, StreetContext ctx, RoadEdgeSettings s, float chunkSize)
        {
            var result = new RoadEdgeResult();
            var near = new Dictionary<RoadChain, float>();
            foreach (var chain in net.Chains)
            {
                if (chain.BaseProfile == EdgeProfile.None || chain.Points.Count < 2) continue;
                foreach (int side in RoadNetwork.Sides) CollectRuns(chain, side, net, ctx, s, near, result.Runs);
            }
            var gutter = GutterSection(s);
            var sidewalk = SidewalkSection(s);
            foreach (var run in result.Runs)
            {
                if (run.Profile == EdgeProfile.Sidewalk)
                {
                    Emit(run, sidewalk, result.Sidewalk, SidewalkSubmeshes, chunkSize);
                    result.SidewalkMeters += run.Length;
                }
                else
                {
                    Emit(run, gutter, result.Gutter, GutterSubmeshes, chunkSize);
                    result.GutterMeters += run.Length;
                }
            }
            return result;
        }

        /// <summary>The band from the asphalt edge out to <paramref name="width"/> is free at arc length s (spec §5 rules 1–6).</summary>
        public static bool Keep(RoadChain chain, int side, float s, float width, RoadNetwork net, StreetContext ctx, RoadEdgeSettings set,
            Dictionary<RoadChain, float> near)
        {
            float hw = chain.HalfWidth;
            return Free(chain, side, s, hw + set.underlap, net, ctx, set, near) && Free(chain, side, s, hw + width, net, ctx, set, near);
        }

        static bool Free(RoadChain chain, int side, float s, float offset, RoadNetwork net, StreetContext ctx, RoadEdgeSettings set,
            Dictionary<RoadChain, float> near)
        {
            var p = chain.SidePoint(s, side, offset);
            if (!ctx.InBounds(p) || ctx.InsideBuilding(p) || ctx.InWater(p)) return false;
            if (net.DistanceToChain(chain, p, offset, s - offset - 1f, s + offset + 1f) < offset - 0.05f) return false;
            net.Nearby(p, net.MaxReach, chain, near);
            foreach (var kv in near)
            {
                var other = kv.Key;
                float limit = RoadNetwork.Beats(other, chain) ? other.HalfWidth + other.MaxEdgeWidth : other.HalfWidth + set.mouthMargin;
                if (kv.Value < limit) return false;
            }
            return true;
        }

        static void CollectRuns(RoadChain chain, int side, RoadNetwork net, StreetContext ctx, RoadEdgeSettings s,
            Dictionary<RoadChain, float> near, List<EdgeRun> runs)
        {
            var profiles = chain.Profiles(side);
            int n = profiles.Length;
            float step = s.SafeSampleStep;
            var keep = new bool[n];
            for (int i = 0; i < n; i++)
                keep[i] = profiles[i] != EdgeProfile.None && Keep(chain, side, chain.SampleS(i, step), s.Width(profiles[i]), net, ctx, s, near);

            int start = -1;
            for (int i = 0; i <= n; i++)
            {
                if (i < n && start >= 0 && keep[i] && profiles[i] == profiles[start]) continue;
                if (start >= 0)
                {
                    int last = i - 1;
                    var profile = profiles[start];
                    float width = s.Width(profile);
                    float s0 = chain.SampleS(start, step), s1 = chain.SampleS(last, step);
                    if (start > 0)
                    {
                        float before = chain.SampleS(start - 1, step);
                        s0 = keep[start - 1] ? 0.5f * (before + s0) : Polyline2D.Bisect(s0, before, x => Keep(chain, side, x, width, net, ctx, s, near), BisectSteps);
                    }
                    if (last < n - 1)
                    {
                        float after = chain.SampleS(last + 1, step);
                        s1 = keep[last + 1] ? 0.5f * (s1 + after) : Polyline2D.Bisect(s1, after, x => Keep(chain, side, x, width, net, ctx, s, near), BisectSteps);
                    }
                    if (s1 - s0 >= s.minRunLength) runs.Add(new EdgeRun { Chain = chain, Side = side, Profile = profile, S0 = s0, S1 = s1 });
                    start = -1;
                }
                if (i < n && keep[i]) start = i;
            }
        }

        static Section GutterSection(RoadEdgeSettings s)
        {
            float sh = s.shoulderWidth, lip = s.gutterLipWidth, w = s.GutterProfileWidth, b = s.edgeBaseY, top = s.gutterLipY;
            var x = new Section();
            x.Strips.Add(new Strip(-s.underlap, sh, b, GutterConcrete));
            x.Strips.Add(new Strip(sh, sh + lip, top, GutterConcrete));
            x.Strips.Add(new Strip(sh + lip, w - lip, b, GutterChannel));
            x.Strips.Add(new Strip(w - lip, w, top, GutterConcrete));
            x.Faces.Add(new Face(sh, b, top, GutterConcrete, false));
            x.Faces.Add(new Face(sh + lip, b, top, GutterConcrete, true));
            x.Faces.Add(new Face(w - lip, b, top, GutterConcrete, false));
            x.Faces.Add(new Face(w, 0f, top, GutterConcrete, true));
            x.Caps.Add(new Cap(-s.underlap, w, 0f, b, GutterConcrete));
            x.Caps.Add(new Cap(sh, sh + lip, b, top, GutterConcrete));
            x.Caps.Add(new Cap(w - lip, w, b, top, GutterConcrete));
            x.Seal();
            return x;
        }

        static Section SidewalkSection(RoadEdgeSettings s)
        {
            float k = s.kerbWidth, w = s.sidewalkWidth, b = s.edgeBaseY, top = s.sidewalkTopY;
            var x = new Section();
            x.Strips.Add(new Strip(-s.underlap, 0f, b, SidewalkConcrete));
            x.Strips.Add(new Strip(0f, k, top, SidewalkKerb));
            x.Strips.Add(new Strip(k, w, top, SidewalkBody));
            x.Faces.Add(new Face(0f, b, top, SidewalkKerb, false));
            x.Faces.Add(new Face(w, 0f, top, SidewalkConcrete, true));
            x.Caps.Add(new Cap(-s.underlap, 0f, 0f, b, SidewalkConcrete));
            x.Caps.Add(new Cap(0f, w, 0f, top, SidewalkConcrete));
            x.Seal();
            return x;
        }

        static void Emit(EdgeRun run, Section section, Dictionary<Vector2Int, MeshData> meshes, int submeshes, float chunkSize)
        {
            var chain = run.Chain;
            var along = new List<float>();
            var center = Polyline2D.Slice(chain.Points, chain.Cum, run.S0, run.S1, along);
            var lineAlong = new List<float>();
            var lines = new Dictionary<float, List<Vector2>>();
            foreach (float x in section.Xs) lines[x] = Polyline2D.Offset(center, along, run.Side * (chain.HalfWidth + x), lineAlong);
            var outer = lines[section.Xs[section.Xs.Count - 1]];
            for (int j = 0; j + 1 < outer.Count; j++)
            {
                // The inside of a tight turn collapses the outer lines first (Polyline2D.Offset); the strips nearer the road
                // still need this segment, so it is skipped only when every line has collapsed here.
                Vector2 dir = Vector2.zero, mid = Vector2.zero;
                foreach (float x in section.Xs)
                {
                    var l = lines[x];
                    var d = l[j + 1] - l[j];
                    if (d.sqrMagnitude < 1e-10f) continue;
                    dir = d;
                    mid = (l[j] + l[j + 1]) * 0.5f;
                    break;
                }
                if (dir == Vector2.zero) continue;
                var mesh = MeshIn(meshes, WorldMeshPipeline.ChunkOf(mid, chunkSize), submeshes);
                Vector3 away = Xz.Dir(Polyline2D.LeftNormal(dir.normalized) * run.Side);
                float a0 = lineAlong[j], a1 = lineAlong[j + 1];
                foreach (var st in section.Strips)
                {
                    var l0 = lines[st.X0];
                    var l1 = lines[st.X1];
                    mesh.AddQuad(st.Sub, Xz.At(l0[j], st.Y), Xz.At(l0[j + 1], st.Y), Xz.At(l1[j + 1], st.Y), Xz.At(l1[j], st.Y),
                        new Vector2(a0, st.X0), new Vector2(a1, st.X0), new Vector2(a1, st.X1), new Vector2(a0, st.X1), Vector3.up);
                }
                foreach (var f in section.Faces)
                {
                    var l = lines[f.X];
                    mesh.AddQuad(f.Sub, Xz.At(l[j], f.Y0), Xz.At(l[j + 1], f.Y0), Xz.At(l[j + 1], f.Y1), Xz.At(l[j], f.Y1),
                        new Vector2(a0, f.Y0), new Vector2(a1, f.Y0), new Vector2(a1, f.Y1), new Vector2(a0, f.Y1), f.Outward ? away : -away);
                }
            }
            AddCaps(run, section, lines, meshes, submeshes, chunkSize, true);
            AddCaps(run, section, lines, meshes, submeshes, chunkSize, false);
        }

        static void AddCaps(EdgeRun run, Section section, Dictionary<float, List<Vector2>> lines, Dictionary<Vector2Int, MeshData> meshes,
            int submeshes, float chunkSize, bool atStart)
        {
            run.Chain.PointAt(atStart ? run.S0 : run.S1, out var t);
            Vector3 normal = Xz.Dir(atStart ? -t : t);
            var outer = lines[section.Xs[section.Xs.Count - 1]];
            int j = atStart ? 0 : outer.Count - 1;
            var mesh = MeshIn(meshes, WorldMeshPipeline.ChunkOf(outer[j], chunkSize), submeshes);
            foreach (var c in section.Caps)
            {
                Vector2 p0 = lines[c.X0][j], p1 = lines[c.X1][j];
                mesh.AddQuad(c.Sub, Xz.At(p0, c.Y0), Xz.At(p1, c.Y0), Xz.At(p1, c.Y1), Xz.At(p0, c.Y1),
                    new Vector2(c.X0, c.Y0), new Vector2(c.X1, c.Y0), new Vector2(c.X1, c.Y1), new Vector2(c.X0, c.Y1), normal);
            }
        }

        static MeshData MeshIn(Dictionary<Vector2Int, MeshData> meshes, Vector2Int chunk, int submeshes)
        {
            if (!meshes.TryGetValue(chunk, out var mesh)) meshes[chunk] = mesh = new MeshData(submeshes);
            return mesh;
        }
    }
}
