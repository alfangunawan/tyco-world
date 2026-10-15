using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Gameplay
{
    /// <summary>Pure maths for the HUD minimap: world (x, z) to pixels around its centre, turned so the player's heading points up.</summary>
    public static class MinimapMath
    {
        /// <summary>
        /// Pixel offset from the minimap centre (y down, as UI Toolkit draws) of a world point, for a player at
        /// <paramref name="centre"/> facing <paramref name="headingDegrees"/> (0 = north/+z, 90 = east/+x).
        /// </summary>
        public static Vector2 ToMap(Vector2 world, Vector2 centre, float headingDegrees, float pixelsPerMetre)
        {
            var d = world - centre;
            float a = headingDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            // Turn the world by -heading: the facing direction (sin a, cos a) lands on (0, 1), which is up on screen.
            float x = d.x * c - d.y * s;
            float y = d.x * s + d.y * c;
            return new Vector2(x, -y) * pixelsPerMetre;
        }

        /// <summary>The point, or where its direction crosses the rim when it lies beyond <paramref name="radius"/>.</summary>
        public static Vector2 ClampToRim(Vector2 offset, float radius, out bool clamped)
        {
            float m = offset.magnitude;
            clamped = m > radius;
            return clamped ? offset / m * radius : offset;
        }
    }

    /// <summary>
    /// GTA-style round minimap for the gameplay HUD (bottom left): baked roads around the player, turned with the view,
    /// the player's arrow in the middle, markers, and pinned markers (the job target, Pak RT, a taken vehicle) held on
    /// the rim when they are further away, so the way to them is always visible. Show computes everything; the paint
    /// pass only draws what Show cached (tests read the cache).
    /// </summary>
    public sealed class MinimapView : VisualElement
    {
        public const float Size = 210f;
        public const float WalkRadiusMetres = 90f, RideRadiusMetres = 150f;
        const float RimInset = 7f;

        public struct Dot
        {
            public Vector2 Offset;
            public Color Color;
            public float Size;
            public bool OnRim;
        }

        readonly List<Vector2[]> lines = new List<Vector2[]>();
        readonly List<float> lineWidths = new List<float>();
        readonly List<Color> lineColors = new List<Color>();
        readonly List<Dot> dots = new List<Dot>();
        readonly List<Vector2> route = new List<Vector2>();
        readonly Label north;
        KampungData cachedFor;
        Rect[] bounds = new Rect[0];

        public float RadiusMetres { get; private set; } = WalkRadiusMetres;
        public float PixelsPerMetre => (Size * 0.5f - RimInset) / RadiusMetres;
        public IReadOnlyList<Dot> Dots => dots;
        public int LineCount => lines.Count;
        /// <summary>The GPS route to the job target in minimap pixels (empty without one).</summary>
        public IReadOnlyList<Vector2> RoutePixels => route;
        /// <summary>Where north is on the rim (offset from the centre), for the "U" mark.</summary>
        public Vector2 NorthOffset { get; private set; }

        public MinimapView()
        {
            name = "gameplay-minimap";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 24f;
            style.bottom = 24f;
            style.width = Size;
            style.height = Size;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = new Color(0.08f, 0.1f, 0.09f, 0.82f);
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = Size * 0.5f;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 2f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new Color(0f, 0f, 0f, 0.8f);
            generateVisualContent += Draw;
            north = GameplayStyle.HudText("U", 14f, true, "gameplay-minimap-north");
            north.style.position = Position.Absolute;
            north.style.width = 16f;
            north.style.unityTextAlign = TextAnchor.MiddleCenter;
            Add(north);
        }

        /// <summary>Recomputes the picture for the player at <paramref name="player"/> facing <paramref name="headingDegrees"/>.</summary>
        public void Show(KampungData data, Vector2 player, float headingDegrees, bool riding, IEnumerable<MapMarker> markers,
            IReadOnlyList<Vector2> routeWorld = null)
        {
            RadiusMetres = riding ? RideRadiusMetres : WalkRadiusMetres;
            float ppm = PixelsPerMetre;
            float rim = Size * 0.5f - RimInset;
            lines.Clear();
            lineWidths.Clear();
            lineColors.Clear();
            dots.Clear();
            route.Clear();
            if (routeWorld != null)
                foreach (var q in routeWorld) route.Add(MinimapMath.ToMap(q, player, headingDegrees, ppm));
            if (data != null)
            {
                CacheBounds(data);
                var view = new Rect(player.x - RadiusMetres * 1.5f, player.y - RadiusMetres * 1.5f, RadiusMetres * 3f, RadiusMetres * 3f);
                for (int i = 0; i < data.mapRoads.Length; i++)
                {
                    var road = data.mapRoads[i];
                    if (road == null || road.points.Length < 2 || !bounds[i].Overlaps(view)) continue;
                    var pts = new Vector2[road.points.Length];
                    for (int j = 0; j < pts.Length; j++) pts[j] = MinimapMath.ToMap(road.points[j], player, headingDegrees, ppm);
                    lines.Add(pts);
                    lineWidths.Add(road.width >= 2 ? 5f : road.width == 1 ? 3.5f : 1.8f);
                    lineColors.Add(road.width >= 2 ? new Color(0.95f, 0.88f, 0.62f) : road.width == 1 ? new Color(0.75f, 0.75f, 0.72f) : new Color(0.5f, 0.55f, 0.5f));
                }
            }
            if (markers != null)
                foreach (var m in markers)
                {
                    var offset = MinimapMath.ToMap(m.Position, player, headingDegrees, ppm);
                    var at = MinimapMath.ClampToRim(offset, rim, out bool clamped);
                    if (clamped && !m.Pin) continue;
                    dots.Add(new Dot { Offset = at, Color = m.Color, Size = m.Pin ? Mathf.Max(m.Size, 6f) : Mathf.Min(m.Size, 4f), OnRim = clamped });
                }
            NorthOffset = MinimapMath.ClampToRim(MinimapMath.ToMap(player + Vector2.up * 10000f, player, headingDegrees, ppm), rim - 4f, out _);
            north.style.left = Size * 0.5f + NorthOffset.x - 8f - 2f;
            north.style.top = Size * 0.5f + NorthOffset.y - 9f - 2f;
            MarkDirtyRepaint();
        }

        void CacheBounds(KampungData data)
        {
            if (cachedFor == data && bounds.Length == data.mapRoads.Length) return;
            cachedFor = data;
            bounds = new Rect[data.mapRoads.Length];
            for (int i = 0; i < bounds.Length; i++)
            {
                var pts = data.mapRoads[i] != null ? data.mapRoads[i].points : null;
                if (pts == null || pts.Length == 0) continue;
                float minX = pts[0].x, maxX = pts[0].x, minY = pts[0].y, maxY = pts[0].y;
                foreach (var p in pts)
                {
                    minX = Mathf.Min(minX, p.x);
                    maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxY = Mathf.Max(maxY, p.y);
                }
                bounds[i] = Rect.MinMaxRect(minX, minY, maxX, maxY);
            }
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            // Painter space starts at the element's outer corner (the 2 px border included).
            var c = new Vector2(Size * 0.5f, Size * 0.5f);
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            for (int i = 0; i < lines.Count; i++)
            {
                p.strokeColor = lineColors[i];
                p.lineWidth = lineWidths[i];
                p.BeginPath();
                p.MoveTo(c + lines[i][0]);
                for (int j = 1; j < lines[i].Length; j++) p.LineTo(c + lines[i][j]);
                p.Stroke();
            }
            if (route.Count >= 2)
            {
                // The way to the job, like a GPS line: dark under-stroke, then the target colour.
                foreach (var (width, color) in new[] { (7f, new Color(0f, 0f, 0f, 0.6f)), (4.5f, MapColors.Target) })
                {
                    p.strokeColor = color;
                    p.lineWidth = width;
                    p.BeginPath();
                    p.MoveTo(c + route[0]);
                    for (int i = 1; i < route.Count; i++) p.LineTo(c + route[i]);
                    p.Stroke();
                }
            }
            foreach (var d in dots)
            {
                if (d.OnRim)
                {
                    // A dark halo so a pinned marker on the rim reads against the roads.
                    p.fillColor = new Color(0f, 0f, 0f, 0.7f);
                    p.BeginPath();
                    p.Arc(c + d.Offset, d.Size + 2f, 0f, 360f);
                    p.Fill();
                }
                p.fillColor = d.Color;
                p.BeginPath();
                p.Arc(c + d.Offset, d.Size, 0f, 360f);
                p.Fill();
            }
            // The player: always in the middle, always pointing up.
            p.fillColor = new Color(0.95f, 0.3f, 0.25f);
            p.strokeColor = new Color(0f, 0f, 0f, 0.8f);
            p.lineWidth = 1.5f;
            p.BeginPath();
            p.MoveTo(c + new Vector2(0f, -10f));
            p.LineTo(c + new Vector2(7f, 7f));
            p.LineTo(c + new Vector2(0f, 3f));
            p.LineTo(c + new Vector2(-7f, 7f));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }
    }
}
