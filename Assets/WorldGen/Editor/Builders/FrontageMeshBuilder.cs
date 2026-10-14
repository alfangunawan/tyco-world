using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public sealed class FrontageMeshResult
    {
        public readonly Dictionary<Vector2Int, MeshData> Render = new Dictionary<Vector2Int, MeshData>();
        public readonly Dictionary<Vector2Int, MeshData> Collider = new Dictionary<Vector2Int, MeshData>();
        public readonly List<WallSegment> Blockers = new List<WallSegment>();
        public readonly SortedDictionary<string, float> MetersByStyle = new SortedDictionary<string, float>(System.StringComparer.Ordinal);
        public float WallMeters;
        public int Gates, Doors, Slabs, Pilasters;
        /// <summary>Placed pilaster centres in placement order.</summary>
        public readonly List<Vector2> PilasterPositions = new List<Vector2>();
        internal readonly SpatialGrid<int> PilasterGrid = new SpatialGrid<int>(2f);
    }

    /// <summary>Walls, coping, pilasters, fence bands, gates and gutter slabs as per-chunk meshes (spec street-frontage §7).</summary>
    public static class FrontageMeshBuilder
    {
        public const string WallsName = "Walls";
        public const int SubWall = 0, SubConcrete = 1, SubFence = 2, SubMetal = 3, SubmeshCount = 4;
        const float RailSize = 0.04f, GateFrame = 0.06f, GateBottom = 0.1f, LeafThickness = 0.05f, GateColliderThickness = 0.06f;

        public static FrontageMeshResult Build(IReadOnlyList<WallRun> runs, RoadEdgeSettings e, FrontageSettings f, float chunkSize)
        {
            var result = new FrontageMeshResult();
            foreach (var run in runs) AddRun(run, e, f, chunkSize, result);
            return result;
        }

        static void AddRun(WallRun run, RoadEdgeSettings e, FrontageSettings f, float chunkSize, FrontageMeshResult r)
        {
            float lineOffset = FrontagePlanner.LineOffset(run.Chain, run.Profile, e, f);
            float centre = lineOffset + f.wallThickness * 0.5f;
            var openings = new List<WallOpening>(run.Openings);
            openings.Sort((a, b) => a.S.CompareTo(b.S));
            float cursor = run.S0;
            foreach (var o in openings)
            {
                AddPiece(run, cursor, o.S - o.Width * 0.5f, centre, f, chunkSize, r);
                AddOpening(run, o, centre, lineOffset, e, f, chunkSize, r);
                cursor = o.S + o.Width * 0.5f;
            }
            AddPiece(run, cursor, run.S1, centre, f, chunkSize, r);
        }

        static void AddPiece(WallRun run, float s0, float s1, float centre, FrontageSettings f, float chunkSize, FrontageMeshResult r)
        {
            if (s1 - s0 < 0.05f) return;
            var chain = run.Chain;
            float copingTop = run.Height + f.copingHeight;
            float fence = run.Style.fenceHeight;
            float total = copingTop + fence;
            var band = BoxUv.AtlasBand(run.Band, f.atlasBands, f.atlasBandMeters, f.atlasTileMeters);
            string style = run.Style.style.ToString();
            var along = new List<float>();
            var center = Polyline2D.Slice(chain.Points, chain.Cum, s0, s1, along);
            var line = Polyline2D.Offset(center, along, run.Side * centre, new List<float>());
            for (int j = 0; j + 1 < line.Count; j++)
            {
                Vector2 a = line[j], b = line[j + 1];
                float length = Vector2.Distance(a, b);
                if (length < 1e-3f) continue;
                var chunk = WorldMeshPipeline.ChunkOf((a + b) * 0.5f, chunkSize);
                var m = MeshIn(r.Render, chunk, SubmeshCount);
                var col = MeshIn(r.Collider, chunk, 1);
                SolidBox.Add(m, SubWall, a, b, 0f, run.Height, f.wallThickness, band);
                SolidBox.Add(m, SubConcrete, a, b, run.Height, copingTop, f.wallThickness + 2f * f.copingOverhang, BoxUv.Meters);
                if (fence > 0f)
                {
                    FenceBand(m, a, b, copingTop, fence - RailSize);
                    SolidBox.Add(m, SubMetal, a, b, total - RailSize, total, RailSize, BoxUv.Meters);
                }
                SolidBox.Add(col, 0, a, b, 0f, total, f.wallThickness, BoxUv.Meters);
                r.Blockers.Add(new WallSegment(a, b));
                r.WallMeters += length;
                r.MetersByStyle.TryGetValue(style, out float meters);
                r.MetersByStyle[style] = meters + length;
            }
            AddPilasters(line, total, f, chunkSize, r);
        }

        /// <summary>Ends always (unless one already stands within 5 cm), interior vertices on turns, then arc-spaced intermediates; non-end ones keep pilasterMinGap.</summary>
        static void AddPilasters(List<Vector2> line, float total, FrontageSettings f, float chunkSize, FrontageMeshResult r)
        {
            int n = line.Count;
            if (n < 2) return;
            var cum = new float[n];
            for (int j = 1; j < n; j++) cum[j] = cum[j - 1] + Vector2.Distance(line[j - 1], line[j]);
            float L = cum[n - 1];
            if (L < 1e-3f) return;
            Vector2 SegDir(int j) { var d = line[j + 1] - line[j]; return d.sqrMagnitude < 1e-8f ? Vector2.zero : d.normalized; }
            // Ends.
            int first = 0; while (first + 1 < n && SegDir(first) == Vector2.zero) first++;
            if (first + 1 < n) Pilaster(line[0], SegDir(first), total, f, chunkSize, r, 0.05f);
            int last = n - 2; while (last > 0 && SegDir(last) == Vector2.zero) last--;
            if (last >= 0 && SegDir(last) != Vector2.zero) Pilaster(line[n - 1], SegDir(last), total, f, chunkSize, r, 0.05f);
            // Interior vertices on turns.
            float minTurn = f.pilasterMinTurnDegrees;
            for (int j = 1; j + 1 < n; j++)
            {
                var din = SegDir(j - 1); var dout = SegDir(j);
                if (din == Vector2.zero || dout == Vector2.zero) continue;
                if (Vector2.Angle(din, dout) >= minTurn) Pilaster(line[j], dout, total, f, chunkSize, r, f.pilasterMinGap);
            }
            // Arc-spaced intermediates.
            int count = Mathf.FloorToInt(L / f.pilasterSpacing);
            int seg = 0;
            for (int k = 1; k <= count; k++)
            {
                float s = k * L / (count + 1);
                while (seg + 2 < n && cum[seg + 1] < s) seg++;
                var d = SegDir(seg);
                if (d == Vector2.zero) continue;
                float t = Mathf.Clamp(s - cum[seg], 0f, cum[seg + 1] - cum[seg]);
                Pilaster(line[seg] + d * t, d, total, f, chunkSize, r, f.pilasterMinGap);
            }
        }

        static void Pilaster(Vector2 p, Vector2 dir, float total, FrontageSettings f, float chunkSize, FrontageMeshResult r, float minDistance)
        {
            var near = new HashSet<int>();
            r.PilasterGrid.Query(new Rect(p.x - minDistance, p.y - minDistance, minDistance * 2f, minDistance * 2f), near);
            foreach (int idx in near) if (Vector2.Distance(r.PilasterPositions[idx], p) < minDistance) return;
            r.PilasterGrid.Insert(new Rect(p.x, p.y, 0f, 0f), r.PilasterPositions.Count);
            r.PilasterPositions.Add(p);
            var chunk = WorldMeshPipeline.ChunkOf(p, chunkSize);
            float top = total + f.pilasterExtra;
            SolidBox.AddPost(MeshIn(r.Render, chunk, SubmeshCount), SubConcrete, p, dir, f.pilasterSize, 0f, top, BoxUv.Meters);
            SolidBox.AddPost(MeshIn(r.Collider, chunk, 1), 0, p, dir, f.pilasterSize, 0f, top, BoxUv.Meters);
            r.Pilasters++;
        }

        /// <summary>Two opposite-facing quads (both sides lit correctly) carrying the alpha-clipped bar texture: u = metres, v = 0..1.</summary>
        static void FenceBand(MeshData m, Vector2 a, Vector2 b, float y0, float height)
        {
            if (height <= 0f) return;
            float length = Vector2.Distance(a, b);
            var normal = Xz.Dir(Polyline2D.LeftNormal((b - a) / length));
            Vector3 p0 = Xz.At(a, y0), p1 = Xz.At(b, y0), p2 = Xz.At(b, y0 + height), p3 = Xz.At(a, y0 + height);
            Vector2 u0 = new Vector2(0f, 0f), u1 = new Vector2(length, 0f), u2 = new Vector2(length, 1f), u3 = new Vector2(0f, 1f);
            m.AddQuad(SubFence, p0, p1, p2, p3, u0, u1, u2, u3, normal);
            m.AddQuad(SubFence, p0, p1, p2, p3, u0, u1, u2, u3, -normal);
        }

        static void AddOpening(WallRun run, WallOpening o, float centre, float lineOffset, RoadEdgeSettings e, FrontageSettings f, float chunkSize,
            FrontageMeshResult r)
        {
            var chain = run.Chain;
            var c = chain.PointAt(o.S, out var t);
            var outward = Polyline2D.LeftNormal(t) * run.Side;
            var mid = c + outward * centre;
            Vector2 g0 = mid - t * (o.Width * 0.5f), g1 = mid + t * (o.Width * 0.5f);
            var chunk = WorldMeshPipeline.ChunkOf(mid, chunkSize);
            var m = MeshIn(r.Render, chunk, SubmeshCount);
            var col = MeshIn(r.Collider, chunk, 1);
            float top = f.gateHeight;
            SolidBox.Add(m, SubMetal, g0, g1, GateBottom, GateBottom + GateFrame, LeafThickness, BoxUv.Meters);
            SolidBox.Add(m, SubMetal, g0, g1, top - GateFrame, top, LeafThickness, BoxUv.Meters);
            SolidBox.AddPost(m, SubMetal, g0 + t * (GateFrame * 0.5f), t, GateFrame, GateBottom, top, BoxUv.Meters);
            SolidBox.AddPost(m, SubMetal, g1 - t * (GateFrame * 0.5f), t, GateFrame, GateBottom, top, BoxUv.Meters);
            FenceBand(m, g0, g1, GateBottom + GateFrame, top - GateBottom - 2f * GateFrame);
            SolidBox.Add(col, 0, g0, g1, 0f, top, GateColliderThickness, BoxUv.Meters);
            r.Blockers.Add(new WallSegment(g0, g1));
            if (o.IsGate) r.Gates++;
            else r.Doors++;
            if (run.Profile != EdgeProfile.Gutter) return;
            Vector2 near = c + outward * (chain.HalfWidth + e.shoulderWidth), far = c + outward * lineOffset;
            float width = o.Width + f.slabExtraWidth;
            SolidBox.Add(m, SubConcrete, near, far, 0f, f.slabTopY, width, BoxUv.Meters);
            SolidBox.Add(col, 0, near, far, 0f, f.slabTopY, width, BoxUv.Meters);
            r.Slabs++;
        }

        static MeshData MeshIn(Dictionary<Vector2Int, MeshData> meshes, Vector2Int chunk, int submeshes)
        {
            if (!meshes.TryGetValue(chunk, out var mesh)) meshes[chunk] = mesh = new MeshData(submeshes);
            return mesh;
        }
    }
}
