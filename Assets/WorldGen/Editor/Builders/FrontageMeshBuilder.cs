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
        /// <summary>Each drawn gate or door as its stretch of the wall-centre line, in drawing order.</summary>
        public readonly List<List<Vector2>> OpeningSpans = new List<List<Vector2>>();
        internal readonly SpatialGrid<int> PilasterGrid = new SpatialGrid<int>(2f);
    }

    /// <summary>
    /// The wall-centre line of one chain side at one offset: offset once over the whole chain (the inside of tight turns
    /// collapsed) and measured by its own arc length u. Every run, piece and opening on that side is cut from it, so
    /// neighbouring lots share their end point and an opening keeps its width even on a bend (spec frontage-polish §2.3).
    /// </summary>
    sealed class WallLine
    {
        public readonly List<Vector2> Points;
        readonly float[] along, u;

        public WallLine(RoadChain chain, int side, float centre)
        {
            var a = new List<float>();
            Points = Polyline2D.Offset(chain.Points, chain.Cum, side * centre, a);
            along = a.ToArray();
            u = Polyline2D.Cumulative(Points);
        }

        /// <summary>Wall-line arc length at centreline arc length s, interpolated along the offset segment covering s.</summary>
        public float UAt(float s)
        {
            int n = along.Length;
            if (s <= along[0]) return u[0];
            if (s >= along[n - 1]) return u[n - 1];
            int lo = 0, hi = n - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (along[mid] <= s) lo = mid;
                else hi = mid - 1;
            }
            float span = along[lo + 1] - along[lo];
            return span > 1e-6f ? Mathf.Lerp(u[lo], u[lo + 1], (s - along[lo]) / span) : u[lo + 1];
        }

        public List<Vector2> Slice(float u0, float u1) => Polyline2D.Slice(Points, u, u0, u1, new List<float>());

        public Vector2 PointAt(float at) => Polyline2D.PointAt(Points, u, at, out _);

        /// <summary>Direction of the nearest segment of real length to arc length <paramref name="at"/>.</summary>
        public Vector2 TangentAt(float at)
        {
            int i = Polyline2D.SegmentAt(u, at);
            for (int k = 0; k < Points.Count; k++)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    int j = pass == 0 ? i + k : i - k;
                    if (j < 0 || j + 1 >= Points.Count) continue;
                    var d = Points[j + 1] - Points[j];
                    if (d.sqrMagnitude > 1e-8f) return d.normalized;
                }
            }
            return Vector2.right;
        }
    }

    /// <summary>Walls, coping, pilasters, fence bands, gates and gutter slabs as per-chunk meshes (spec street-frontage §7).</summary>
    public static class FrontageMeshBuilder
    {
        public const string WallsName = "Walls";
        public const int SubWall = 0, SubConcrete = 1, SubFence = 2, SubMetal = 3, SubmeshCount = 4;
        const float JointCapDegrees = 10f, RailSize = 0.04f, GateFrame = 0.06f, GateBottom = 0.1f, LeafThickness = 0.05f, GateColliderThickness = 0.06f;
        public static FrontageMeshResult Build(IReadOnlyList<WallRun> runs, RoadEdgeSettings e, FrontageSettings f, float chunkSize)
        {
            var result = new FrontageMeshResult();
            var lines = new Dictionary<(RoadChain chain, int side, int centreMm), WallLine>();
            foreach (var run in runs) AddRun(run, LineOf(run, e, f, lines), e, f, chunkSize, result);
            AddProfileReturns(runs, e, f, chunkSize, result, lines);
            return result;
        }

        static float Centre(WallRun run, RoadEdgeSettings e, FrontageSettings f) =>
            FrontagePlanner.LineOffset(run.Chain, run.Profile, e, f) + f.wallThickness * 0.5f;

        static WallLine LineOf(WallRun run, RoadEdgeSettings e, FrontageSettings f, Dictionary<(RoadChain chain, int side, int centreMm), WallLine> lines)
        {
            float centre = Centre(run, e, f);
            var key = (run.Chain, run.Side, Mathf.RoundToInt(centre * 1000f));
            if (!lines.TryGetValue(key, out var line)) lines[key] = line = new WallLine(run.Chain, run.Side, centre);
            return line;
        }

        /// <summary>
        /// Where one side of a chain changes edge profile, the property line jumps (sidewalk 1.9 m vs gutter 1.2 m past the
        /// asphalt). Two walls that meet there get a short return wall across the jump (spec frontage-polish §2.5).
        /// </summary>
        static void AddProfileReturns(IReadOnlyList<WallRun> runs, RoadEdgeSettings e, FrontageSettings f, float chunkSize, FrontageMeshResult r,
            Dictionary<(RoadChain chain, int side, int centreMm), WallLine> lines)
        {
            var groups = new List<List<WallRun>>();
            var bySide = new Dictionary<(RoadChain chain, int side), List<WallRun>>();
            foreach (var run in runs)
            {
                if (!bySide.TryGetValue((run.Chain, run.Side), out var list))
                {
                    bySide[(run.Chain, run.Side)] = list = new List<WallRun>();
                    groups.Add(list);
                }
                list.Add(run);
            }
            foreach (var list in groups)
            {
                list.Sort((a, b) => a.S0 != b.S0 ? a.S0.CompareTo(b.S0) : a.S1.CompareTo(b.S1));
                for (int i = 0; i + 1 < list.Count; i++)
                {
                    WallRun a = list[i], b = list[i + 1];
                    if (Mathf.Abs(a.S1 - b.S0) > 1e-3f || Mathf.Abs(Centre(a, e, f) - Centre(b, e, f)) < 0.05f) continue;
                    var la = LineOf(a, e, f, lines);
                    var lb = LineOf(b, e, f, lines);
                    Vector2 end = la.PointAt(la.UAt(a.S1)), start = lb.PointAt(lb.UAt(b.S0));
                    if (Vector2.Distance(end, start) < 0.05f) continue;
                    RenderLine(a, new List<Vector2> { end, start }, f, chunkSize, r);
                }
            }
        }

        /// <summary>The run's stretch of its wall line, with each opening kept whole (full width, inside the run's end margins).</summary>
        static void AddRun(WallRun run, WallLine line, RoadEdgeSettings e, FrontageSettings f, float chunkSize, FrontageMeshResult r)
        {
            float u0 = line.UAt(run.S0), u1 = line.UAt(run.S1);
            if (u1 - u0 < 0.05f) return;
            var openings = new List<(WallOpening o, float at)>();
            foreach (var o in run.Openings) openings.Add((o, line.UAt(o.S)));
            openings.Sort((a, b) => a.at.CompareTo(b.at));
            float cursor = u0;
            foreach (var (o, at) in openings)
            {
                float half = o.Width * 0.5f;
                if (u1 - cursor < o.Width) continue;
                float start = OpeningStart(at - half, cursor, u1 - o.Width, f.pilasterMinGap);
                AddPiece(run, line, cursor, start, f, chunkSize, r);
                AddOpening(run, o, line, start + half, e, f, chunkSize, r);
                cursor = start + o.Width;
            }
            AddPiece(run, line, cursor, u1, f, chunkSize, r);
        }

        /// <summary>
        /// Where an opening starts between lo and hi (its start's range): as near <paramref name="wanted"/> as possible, but
        /// either flush with a lot end (sharing its pilaster) or at least <paramref name="minStub"/> from it, so no stub of
        /// wall stands squeezed between two pilasters.
        /// </summary>
        static float OpeningStart(float wanted, float lo, float hi, float minStub)
        {
            float best = lo, bestDistance = Mathf.Abs(wanted - lo);
            void Consider(float x)
            {
                float d = Mathf.Abs(wanted - x);
                if (d < bestDistance - 1e-5f)
                {
                    best = x;
                    bestDistance = d;
                }
            }
            if (lo + minStub <= hi - minStub) Consider(Mathf.Clamp(wanted, lo + minStub, hi - minStub));
            Consider(hi);
            return best;
        }

        static void AddPiece(WallRun run, WallLine line, float u0, float u1, FrontageSettings f, float chunkSize, FrontageMeshResult r)
        {
            if (u1 - u0 < 0.05f) return;
            RenderLine(run, line.Slice(u0, u1), f, chunkSize, r);
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

        static void AddOpening(WallRun run, WallOpening o, WallLine line, float at, RoadEdgeSettings e, FrontageSettings f, float chunkSize,
            FrontageMeshResult r)
        {
            var chain = run.Chain;
            float half = o.Width * 0.5f;
            var span = line.Slice(at - half, at + half);
            Vector2 g0 = span[0], g1 = span[span.Count - 1];
            r.OpeningSpans.Add(span);
            var middle = line.PointAt(at);
            // The slab crosses the gutter square to the wall at the opening centre.
            var outward = Polyline2D.LeftNormal(line.TangentAt(at)) * run.Side;
            var chunk = WorldMeshPipeline.ChunkOf(middle, chunkSize);
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
            float centre = Centre(run, e, f);
            Vector2 near = middle - outward * (centre - chain.HalfWidth - e.shoulderWidth), far = middle - outward * (f.wallThickness * 0.5f);
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
