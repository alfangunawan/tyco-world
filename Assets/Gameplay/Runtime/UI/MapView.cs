using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Gameplay
{
    /// <summary>A map marker in world XZ metres.</summary>
    public struct MapMarker
    {
        public Vector2 Position;
        public Color Color;
        public float Size;
        /// <summary>Kept on the minimap's rim when out of range (the job target, Pak RT, a taken vehicle).</summary>
        public bool Pin;
    }

    /// <summary>
    /// The phone map (spec #6 §2): the baked road lines drawn with Painter2D around the player, north up, with shop dots,
    /// the kos, mission markers and the player's arrow. Zoom in steps.
    /// </summary>
    /// <summary>Marker colours on the phone map, shared by the systems that add markers and the legend that explains them.</summary>
    public static class MapColors
    {
        public static readonly Color Shop = new Color(0.3f, 0.7f, 1f);
        /// <summary>Konter and minimarket: where the data package for the Kerjaan app is sold.</summary>
        public static readonly Color DataShop = new Color(0.8f, 0.45f, 1f);
        public static readonly Color Kos = new Color(1f, 0.85f, 0.2f);
        public static readonly Color Target = new Color(1f, 0.6f, 0.1f);
        public static readonly Color PakRt = new Color(0.35f, 0.95f, 0.45f);
        public static readonly Color Stolen = new Color(0.9f, 0.25f, 0.25f);
    }

    public sealed class MapView : VisualElement
    {
        readonly List<Vector2> route = new List<Vector2>();
        static readonly float[] Zooms = { 0.35f, 0.6f, 1f, 1.6f };

        KampungData data;
        Vector2 centre;
        float heading;
        int zoom = 1;
        readonly List<MapMarker> markers = new List<MapMarker>();

        public MapView()
        {
            name = "phone-map";
            style.flexGrow = 1f;
            style.backgroundColor = new Color(0.16f, 0.2f, 0.16f, 1f);
            style.overflow = Overflow.Hidden;
            GameplayStyle.Round(this, 6f);
            generateVisualContent += Draw;
        }

        public float Scale => Zooms[zoom];

        public void ZoomIn()
        {
            zoom = Mathf.Min(zoom + 1, Zooms.Length - 1);
            MarkDirtyRepaint();
        }

        public void ZoomOut()
        {
            zoom = Mathf.Max(zoom - 1, 0);
            MarkDirtyRepaint();
        }

        public void Show(KampungData kampung, Vector2 player, float yawDegrees, IEnumerable<MapMarker> extra, IReadOnlyList<Vector2> gpsRoute = null)
        {
            route.Clear();
            if (gpsRoute != null) route.AddRange(gpsRoute);
            data = kampung;
            centre = player;
            heading = yawDegrees;
            markers.Clear();
            if (extra != null) markers.AddRange(extra);
            MarkDirtyRepaint();
        }

        Vector2 ToScreen(Vector2 world, Rect rect)
        {
            var d = (world - centre) * Scale;
            return new Vector2(rect.width * 0.5f + d.x, rect.height * 0.5f - d.y);
        }

        void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (data == null || rect.width < 4f) return;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            float reach = Mathf.Max(rect.width, rect.height) / Scale;
            foreach (var road in data.mapRoads)
            {
                if (road.points.Length < 2 || !Near(road.points, reach)) continue;
                p.strokeColor = road.width >= 2 ? new Color(0.95f, 0.9f, 0.7f) : road.width == 1 ? new Color(0.82f, 0.82f, 0.8f) : new Color(0.55f, 0.6f, 0.55f);
                p.lineWidth = (road.width >= 2 ? 7f : road.width == 1 ? 4.5f : 2f) * Mathf.Sqrt(Scale);
                p.BeginPath();
                p.MoveTo(ToScreen(road.points[0], rect));
                for (int i = 1; i < road.points.Length; i++) p.LineTo(ToScreen(road.points[i], rect));
                p.Stroke();
            }
            if (route.Count >= 2)
            {
                p.strokeColor = MapColors.Target;
                p.lineWidth = 4f;
                p.BeginPath();
                p.MoveTo(ToScreen(route[0], rect));
                for (int i = 1; i < route.Count; i++) p.LineTo(ToScreen(route[i], rect));
                p.Stroke();
            }
            foreach (var m in markers) Dot(p, ToScreen(m.Position, rect), m.Size, m.Color);
            // The player: an arrow pointing where they face (north up).
            var c = ToScreen(centre, rect);
            float a = heading * Mathf.Deg2Rad;
            var fwd = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            var side = new Vector2(-fwd.y, fwd.x);
            p.fillColor = new Color(0.95f, 0.3f, 0.25f);
            p.BeginPath();
            p.MoveTo(c + fwd * 11f);
            p.LineTo(c - fwd * 7f + side * 7f);
            p.LineTo(c - fwd * 3f);
            p.LineTo(c - fwd * 7f - side * 7f);
            p.ClosePath();
            p.Fill();
        }

        /// <summary>The line's bounding box overlaps the view square: a long straight road passing by is drawn even when both ends are off screen.</summary>
        bool Near(Vector2[] points, float reach)
        {
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            foreach (var q in points)
            {
                minX = Mathf.Min(minX, q.x);
                maxX = Mathf.Max(maxX, q.x);
                minY = Mathf.Min(minY, q.y);
                maxY = Mathf.Max(maxY, q.y);
            }
            return maxX > centre.x - reach && minX < centre.x + reach && maxY > centre.y - reach && minY < centre.y + reach;
        }

        static void Dot(Painter2D p, Vector2 at, float size, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.Arc(at, size, 0f, 360f);
            p.Fill();
        }
    }
}
