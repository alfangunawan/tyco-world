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
        const float JointCapDegrees = 10f, RailSize = 0.04f, GateFrame = 0.06f, GateBottom = 0.1f, LeafThickness = 0.05f, GateColliderThickness = 0.06f;
        /// <summary>An opening whose wall-line span is shorter than this share of its width sits on the inside of a bend that folds it away; the wall runs on instead.</summary>
        const float MinOpeningShare = 0.6f;

        /// <summary>Straight-line length of the opening's span on the wall line (after the offset folds the inside of a bend away).</summary>
        static float SpanChord(WallRun run, WallOpening o, float centre)
        {
            var along = new List<float>();
            var span = Polyline2D.Offset(Polyline2D.Slice(run.Chain.Points, run.Chain.Cum, o.S - o.Width * 0.5f, o.S + o.Width * 0.5f, along),
                along, run.Side * centre, new List<float>());
            return Vector2.Distance(span[0], span[span.Count - 1]);
        }

        public static FrontageMeshResult Build(IReadOnlyList<WallRun> runs, RoadEdgeSettings e, FrontageSettings f, float chunkSize)
        {
            var result = new FrontageMeshResult();
            foreach (var run in runs) AddRun(run, e, f, chunkSize, result);
            AddProfileReturns(runs, e, f, chunkSize, result);
            return result;
        }

        /// <summary>
        /// Where one side of a chain changes edge profile, the property line jumps (sidewalk 1.9 m vs gutter 1.2 m past the
        /// asphalt). Two walls that meet there get a short return wall across the jump (spec frontage-polish §2.4).
        /// </summary>
        static void AddProfileReturns(IReadOnlyList<WallRun> runs, RoadEdgeSettings e, FrontageSettings f, float chunkSize, FrontageMeshResult r)
        {
            var bySide = new Dictionary<(int chain, int side), List<WallRun>>();
            foreach (var run in runs)
            {
                var key = (run.Chain.Index, run.Side);
                if (!bySide.TryGetValue(key, out var list)) bySide[key] = list = new List<WallRun>();
                list.Add(run);
            }
            var keys = new List<(int chain, int side)>(bySide.Keys);
            keys.Sort();
            foreach (var key in keys)
            {
                var list = bySide[key];
                list.Sort((a, b) => a.S0.CompareTo(b.S0));
                for (int i = 0; i + 1 < list.Count; i++)
                {
                    WallRun a = list[i], b = list[i + 1];
                    if (Mathf.Abs(a.S1 - b.S0) > 1e-3f) continue;
                    float ca = FrontagePlanner.LineOffset(a.Chain, a.Profile, e, f) + f.wallThickness * 0.5f;
                    float cb = FrontagePlanner.LineOffset(b.Chain, b.Profile, e, f) + f.wallThickness * 0.5f;
                    if (Mathf.Abs(ca - cb) < 0.05f) continue;
                    var end = LineEnd(a, Mathf.Max(a.S0, a.S1 - 0.5f), a.S1, ca, false);
                    var start = LineEnd(b, b.S0, Mathf.Min(b.S1, b.S0 + 0.5f), cb, true);
                    if (Vector2.Distance(end, start) < 0.05f) continue;
                    RenderLine(a, new List<Vector2> { end, start }, f, chunkSize, r);
                }
            }
        }

        /// <summary>First or last point of the wall line over [s0, s1]; equal to the matching end of any longer slice ending there.</summary>
        static Vector2 LineEnd(WallRun run, float s0, float s1, float centre, bool first)
        {
            var along = new List<float>();
            var line = Polyline2D.Offset(Polyline2D.Slice(run.Chain.Points, run.Chain.Cum, s0, s1, along), along, run.Side * centre, new List<float>());
            return first ? line[0] : line[line.Count - 1];
        }

        static void AddRun(WallRun run, RoadEdgeSettings e, FrontageSettings f, float chunkSize, FrontageMeshResult r)
        {
            float lineOffset = FrontagePlanner.LineOffset(run.Chain, run.Profile, e, f);
            float centre = lineOffset + f.wallThickness * 0.5f;
            var openings = new List<WallOpening>();
            foreach (var o in run.Openings)
                if (SpanChord(run, o, centre) >= MinOpeningShare * o.Width) openings.Add(o);
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
            var along = new List<float>();
            var center = Polyline2D.Slice(chain.Points, chain.Cum, s0, s1, along);
            RenderLine(run, Polyline2D.Offset(center, along, run.Side * centre, new List<float>()), f, chunkSize, r);
        }

        /// <summary>Wall boxes, coping, fence band, collider, blockers and pilasters along one wall-centre polyline in the run's look.</summary>
        static void RenderLine(WallRun run, List<Vector2> line, FrontageSettings f, float chunkSize, FrontageMeshResult r)
        {
            float copingTop = run.Height + f.copingHeight;
            float fence = run.Style.fenceHeight;
            float total = copingTop + fence;
            var band = BoxUv.AtlasBand(run.Band, f.atlasBands, f.atlasBandMeters, f.atlasTileMeters);
            string style = run.Style.style.ToString();
            for (int j = 0; j + 1 < line.Count; j++)
            {
                Vector2 a = line[j], b = line[j + 1];
                float length = Vector2.Distance(a, b);
                if (length < 1e-3f) continue;
                var ends = EndsFor(line, j);
                var chunk = WorldMeshPipeline.ChunkOf((a + b) * 0.5f, chunkSize);
                var m = MeshIn(r.Render, chunk, SubmeshCount);
                var col = MeshIn(r.Collider, chunk, 1);
                // Render only: the wall top is hidden under the coping and gentle interior joints need no end faces (colliders stay closed solids).
                SolidBox.Add(m, SubWall, a, b, 0f, run.Height, f.wallThickness, band, ends, false);
                SolidBox.Add(m, SubConcrete, a, b, run.Height, copingTop, f.wallThickness + 2f * f.copingOverhang, BoxUv.Meters, ends, true);
                if (fence > 0f)
                {
                    FenceBand(m, a, b, copingTop, fence - RailSize);
                    SolidBox.Add(m, SubMetal, a, b, total - RailSize, total, RailSize, BoxUv.Meters, ends, true);
                }
                SolidBox.Add(col, 0, a, b, 0f, total, f.wallThickness, BoxUv.Meters);
                r.Blockers.Add(new WallSegment(a, b));
                r.WallMeters += length;
                r.MetersByStyle.TryGetValue(style, out float meters);
                r.MetersByStyle[style] = meters + length;
            }
            AddPilasters(line, total, f, chunkSize, r);
        }

        /// <summary>Piece ends keep their end faces; an interior joint keeps them only on a real turn, where the wedge between two boxes would otherwise show their inside.</summary>
        static BoxEnds EndsFor(List<Vector2> line, int j)
        {
            var ends = BoxEnds.None;
            if (j == 0 || TurnAt(line, j) >= JointCapDegrees) ends |= BoxEnds.Start;
            if (j + 2 >= line.Count || TurnAt(line, j + 1) >= JointCapDegrees) ends |= BoxEnds.End;
            return ends;
        }

        static float TurnAt(List<Vector2> line, int i)
        {
            Vector2 din = line[i] - line[i - 1], dout = line[i + 1] - line[i];
            return din.sqrMagnitude < 1e-8f || dout.sqrMagnitude < 1e-8f ? 180f : Vector2.Angle(din, dout);
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
            // Ends; an end within one pilaster width of a standing pilaster (a shared lot boundary, a gate post) reuses it.
            float endGap = f.pilasterSize + 0.05f;
            int first = 0; while (first + 1 < n && SegDir(first) == Vector2.zero) first++;
            if (first + 1 < n) Pilaster(line[0], SegDir(first), total, f, chunkSize, r, endGap);
            int last = n - 2; while (last > 0 && SegDir(last) == Vector2.zero) last--;
            if (last >= 0 && SegDir(last) != Vector2.zero) Pilaster(line[n - 1], SegDir(last), total, f, chunkSize, r, endGap);
            // Interior vertices on turns.
            float minTurn = f.pilasterMinTurnDegrees;
            for (int j = 1; j + 1 < n; j++)
            {
                var din = SegDir(j - 1); var dout = SegDir(j);
                if (din == Vector2.zero || dout == Vector2.zero) continue;
                if (Vector2.Angle(din, dout) >= minTurn) Pilaster(line[j], dout, total, f, chunkSize, r, f.pilasterMinGap);
            }
            // Arc-spaced intermediates.
            int count = f.pilasterSpacing > 0.05f ? Mathf.FloorToInt(L / f.pilasterSpacing) : 0;
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
            var c = chain.PointAt(o.S, out var tangent);
            // Same slice + mitred offset as the wall pieces, so the leaf meets their ends exactly even across a bend.
            var along = new List<float>();
            var span = Polyline2D.Offset(Polyline2D.Slice(chain.Points, chain.Cum, o.S - o.Width * 0.5f, o.S + o.Width * 0.5f, along),
                along, run.Side * centre, new List<float>());
            Vector2 g0 = span[0], g1 = span[span.Count - 1];
            if ((g1 - g0).sqrMagnitude < 1e-8f) return;
            // The slab crosses the gutter square to the road at the opening centre.
            var outward = Polyline2D.LeftNormal(tangent) * run.Side;
            var chunk = WorldMeshPipeline.ChunkOf((g0 + g1) * 0.5f, chunkSize);
            var m = MeshIn(r.Render, chunk, SubmeshCount);
            var col = MeshIn(r.Collider, chunk, 1);
            float top = f.gateHeight;
            for (int j = 0; j + 1 < span.Count; j++)
            {
                Vector2 a = span[j], b = span[j + 1];
                if (Vector2.Distance(a, b) < 1e-3f) continue;
                SolidBox.Add(m, SubMetal, a, b, GateBottom, GateBottom + GateFrame, LeafThickness, BoxUv.Meters);
                SolidBox.Add(m, SubMetal, a, b, top - GateFrame, top, LeafThickness, BoxUv.Meters);
                FenceBand(m, a, b, GateBottom + GateFrame, top - GateBottom - 2f * GateFrame);
                SolidBox.Add(col, 0, a, b, 0f, top, GateColliderThickness, BoxUv.Meters);
                r.Blockers.Add(new WallSegment(a, b));
            }
            Vector2 startDir = SpanDir(span, true), endDir = SpanDir(span, false);
            SolidBox.AddPost(m, SubMetal, g0 + startDir * (GateFrame * 0.5f), startDir, GateFrame, GateBottom, top, BoxUv.Meters);
            SolidBox.AddPost(m, SubMetal, g1 - endDir * (GateFrame * 0.5f), endDir, GateFrame, GateBottom, top, BoxUv.Meters);
            if (o.IsGate) r.Gates++;
            else r.Doors++;
            if (run.Profile != EdgeProfile.Gutter) return;
            Vector2 near = c + outward * (chain.HalfWidth + e.shoulderWidth), far = c + outward * lineOffset;
            float width = o.Width + f.slabExtraWidth;
            SolidBox.Add(m, SubConcrete, near, far, 0f, f.slabTopY, width, BoxUv.Meters);
            SolidBox.Add(col, 0, near, far, 0f, f.slabTopY, width, BoxUv.Meters);
            r.Slabs++;
        }

        static Vector2 SpanDir(List<Vector2> span, bool first)
        {
            if (first) { for (int j = 0; j + 1 < span.Count; j++) if (Vector2.Distance(span[j], span[j + 1]) > 1e-3f) return (span[j + 1] - span[j]).normalized; }
            else { for (int j = span.Count - 1; j > 0; j--) if (Vector2.Distance(span[j], span[j - 1]) > 1e-3f) return (span[j] - span[j - 1]).normalized; }
            return (span[span.Count - 1] - span[0]).normalized;
        }

        static MeshData MeshIn(Dictionary<Vector2Int, MeshData> meshes, Vector2Int chunk, int submeshes)
        {
            if (!meshes.TryGetValue(chunk, out var mesh)) meshes[chunk] = mesh = new MeshData(submeshes);
            return mesh;
        }
    }
}
