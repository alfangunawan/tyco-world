using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>UV mapping for SolidBox: plain metres, or a horizontal band of a stacked atlas (spec street-frontage §10).</summary>
    public readonly struct BoxUv
    {
        public readonly bool Band;
        public readonly float VMin, VMax, BandMeters, TileMeters;

        BoxUv(bool band, float vMin, float vMax, float bandMeters, float tileMeters)
        {
            Band = band;
            VMin = vMin;
            VMax = vMax;
            BandMeters = bandMeters;
            TileMeters = tileMeters;
        }

        /// <summary>Sides: u = metres along, v = metres up. Top: u = metres along, v = metres across.</summary>
        public static BoxUv Meters => new BoxUv(false, 0f, 0f, 1f, 1f);

        /// <summary>Band <paramref name="band"/> of <paramref name="bands"/> stacked bands (band 0 at v = 0), inset 2% against mip bleeding.</summary>
        public static BoxUv AtlasBand(int band, int bands, float bandMeters, float tileMeters) =>
            new BoxUv(true, (band + 0.02f) / bands, (band + 0.98f) / bands, bandMeters, tileMeters);

        public Vector2 Side(float along, float y) =>
            Band ? new Vector2(along / TileMeters, VMin + Mathf.Clamp01(y / BandMeters) * (VMax - VMin)) : new Vector2(along, y);

        public Vector2 Top(float along, float across, float y) =>
            Band ? new Vector2(along / TileMeters, VMin + Mathf.Clamp01(y / BandMeters) * (VMax - VMin)) : new Vector2(along, across);
    }

    /// <summary>Axis-aligned-to-a-segment boxes for walls, coping, posts and colliders. No bottom face.</summary>
    public static class SolidBox
    {
        public static void Add(MeshData m, int sub, Vector2 a, Vector2 b, float y0, float y1, float thickness, BoxUv uv, bool capEnds = true)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 1e-4f || y1 - y0 < 1e-4f || thickness < 1e-4f) return;
            d /= length;
            Vector2 n = Polyline2D.LeftNormal(d);
            Vector2 h = n * (thickness * 0.5f);
            Vector2 al = a + h, ar = a - h, bl = b + h, br = b - h;
            Vector3 left = Xz.Dir(n), forward = Xz.Dir(d);
            m.AddQuad(sub, Xz.At(al, y0), Xz.At(bl, y0), Xz.At(bl, y1), Xz.At(al, y1),
                uv.Side(0f, y0), uv.Side(length, y0), uv.Side(length, y1), uv.Side(0f, y1), left);
            m.AddQuad(sub, Xz.At(ar, y0), Xz.At(br, y0), Xz.At(br, y1), Xz.At(ar, y1),
                uv.Side(0f, y0), uv.Side(length, y0), uv.Side(length, y1), uv.Side(0f, y1), -left);
            m.AddQuad(sub, Xz.At(ar, y1), Xz.At(br, y1), Xz.At(bl, y1), Xz.At(al, y1),
                uv.Top(0f, 0f, y1), uv.Top(length, 0f, y1), uv.Top(length, thickness, y1), uv.Top(0f, thickness, y1), Vector3.up);
            if (!capEnds) return;
            m.AddQuad(sub, Xz.At(ar, y0), Xz.At(al, y0), Xz.At(al, y1), Xz.At(ar, y1),
                uv.Side(0f, y0), uv.Side(thickness, y0), uv.Side(thickness, y1), uv.Side(0f, y1), -forward);
            m.AddQuad(sub, Xz.At(bl, y0), Xz.At(br, y0), Xz.At(br, y1), Xz.At(bl, y1),
                uv.Side(0f, y0), uv.Side(thickness, y0), uv.Side(thickness, y1), uv.Side(0f, y1), forward);
        }

        /// <summary>Square post of side <paramref name="size"/> centred on c, its faces aligned with dir.</summary>
        public static void AddPost(MeshData m, int sub, Vector2 c, Vector2 dir, float size, float y0, float y1, BoxUv uv)
        {
            Vector2 half = dir.normalized * (size * 0.5f);
            Add(m, sub, c - half, c + half, y0, y1, size, uv);
        }
    }
}
