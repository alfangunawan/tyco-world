using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public enum FacadeStyle { ResidentialUpper, Plain, RollingDoor, KosDoor, Warung, WallWindow, CampusUpper, CampusGround }

    public readonly struct FacadeSpec
    {
        public readonly string Name;
        public readonly FacadeStyle Style;
        public readonly Color Wall;

        public FacadeSpec(string name, FacadeStyle style, Color wall)
        {
            Name = name;
            Style = style;
            Wall = wall;
        }
    }

    /// <summary>Procedural placeholder facades. One texture = one bay: 3.0 m wide, one floor (3.2 m) high.</summary>
    public static class FacadeTextureGenerator
    {
        public const int Size = 256;

        public static readonly Color[] ResidentialPalette =
            { Hex("D9D4C7"), Hex("E3CFA0"), Hex("A9C3A0"), Hex("9FB8C9"), Hex("D8A9A9"), Hex("A7A7A7") };

        public static readonly Color[] CampusPalette = { Hex("C9C6BE"), Hex("B5B8B9") };

        public static List<FacadeSpec> Specs()
        {
            var list = new List<FacadeSpec>();
            for (int i = 0; i < ResidentialPalette.Length; i++) list.Add(new FacadeSpec("Res_Upper_" + i, FacadeStyle.ResidentialUpper, ResidentialPalette[i]));
            for (int i = 0; i < ResidentialPalette.Length; i++) list.Add(new FacadeSpec("Res_Trim_" + i, FacadeStyle.Plain, ResidentialPalette[i]));
            list.Add(new FacadeSpec("Res_Ground_RollingDoor", FacadeStyle.RollingDoor, ResidentialPalette[0]));
            list.Add(new FacadeSpec("Res_Ground_KosDoor", FacadeStyle.KosDoor, ResidentialPalette[1]));
            list.Add(new FacadeSpec("Res_Ground_Warung", FacadeStyle.Warung, ResidentialPalette[2]));
            list.Add(new FacadeSpec("Res_Ground_WallWindow", FacadeStyle.WallWindow, ResidentialPalette[3]));
            for (int i = 0; i < CampusPalette.Length; i++)
            {
                list.Add(new FacadeSpec("Campus_Upper_" + i, FacadeStyle.CampusUpper, CampusPalette[i]));
                list.Add(new FacadeSpec("Campus_Ground_" + i, FacadeStyle.CampusGround, CampusPalette[i]));
                list.Add(new FacadeSpec("Campus_Trim_" + i, FacadeStyle.Plain, CampusPalette[i]));
            }
            return list;
        }

        public static Texture2D Render(FacadeSpec spec)
        {
            var px = new Color32[Size * Size];
            var rng = new SplitMix64(SplitMix64.Fnv1a64(spec.Name));
            for (int y = 0; y < Size; y++)
            {
                float v = y / (float)(Size - 1);
                float grime = v < 0.2f ? Mathf.Lerp(0.82f, 1f, v / 0.2f) : (v > 0.95f ? 0.95f : 1f);
                for (int x = 0; x < Size; x++)
                {
                    float noise = 1f + ((float)rng.NextDouble() - 0.5f) * 0.08f;
                    px[y * Size + x] = Shade(spec.Wall, grime * noise);
                }
            }
            switch (spec.Style)
            {
                case FacadeStyle.ResidentialUpper:
                    Window(px, 0.30f, 0.70f, 0.35f, 0.80f, spec.Wall);
                    Fill(px, 0.27f, 0.73f, 0.31f, 0.33f, Shade(spec.Wall, 0.75f));
                    break;
                case FacadeStyle.WallWindow:
                    Window(px, 0.30f, 0.70f, 0.30f, 0.75f, spec.Wall);
                    break;
                case FacadeStyle.RollingDoor:
                    RollingDoor(px);
                    break;
                case FacadeStyle.KosDoor:
                    KosDoor(px, spec.Wall);
                    break;
                case FacadeStyle.Warung:
                    Warung(px);
                    break;
                case FacadeStyle.CampusUpper:
                    CampusUpper(px);
                    break;
                case FacadeStyle.CampusGround:
                    CampusGround(px);
                    break;
            }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = spec.Name };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static List<string> WriteAll(string folder)
        {
            AssetFolders.Ensure(folder);
            var paths = new List<string>();
            foreach (var spec in Specs())
            {
                var tex = Render(spec);
                string assetPath = folder + "/" + spec.Name + ".png";
                File.WriteAllBytes(WorldGenPaths.ToAbsolute(assetPath), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                paths.Add(assetPath);
            }
            return paths;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        static Color32 Shade(Color c, float k) =>
            new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

        static void Fill(Color32[] px, float x0, float x1, float y0, float y1, Color32 color)
        {
            int ix0 = Mathf.Clamp(Mathf.RoundToInt(x0 * Size), 0, Size), ix1 = Mathf.Clamp(Mathf.RoundToInt(x1 * Size), 0, Size);
            int iy0 = Mathf.Clamp(Mathf.RoundToInt(y0 * Size), 0, Size), iy1 = Mathf.Clamp(Mathf.RoundToInt(y1 * Size), 0, Size);
            for (int y = iy0; y < iy1; y++)
                for (int x = ix0; x < ix1; x++)
                    px[y * Size + x] = color;
        }

        static void Window(Color32[] px, float x0, float x1, float y0, float y1, Color wall)
        {
            Fill(px, x0 - 0.02f, x1 + 0.02f, y0 - 0.02f, y1 + 0.02f, Shade(wall, 1.15f));
            int iy0 = Mathf.RoundToInt(y0 * Size), iy1 = Mathf.RoundToInt(y1 * Size);
            Color bottom = Hex("1C2228"), top = Hex("2E3A44");
            for (int y = iy0; y < iy1; y++)
            {
                float t = (y - iy0) / (float)Mathf.Max(1, iy1 - iy0);
                Fill(px, x0, x1, y / (float)Size, (y + 1) / (float)Size, Color.Lerp(bottom, top, t));
            }
            for (float bx = x0 + 0.05f; bx < x1 - 0.01f; bx += 0.055f) Fill(px, bx, bx + 0.008f, y0, y1, Hex("2A2A2A"));
        }

        static void RollingDoor(Color32[] px)
        {
            Fill(px, 0.08f, 0.92f, 0f, 0.78f, Hex("8C8F93"));
            int top = Mathf.RoundToInt(0.78f * Size);
            for (int y = 0; y < top; y += 6) Fill(px, 0.08f, 0.92f, y / (float)Size, (y + 2) / (float)Size, Hex("6E7175"));
            Fill(px, 0.06f, 0.94f, 0.78f, 0.86f, Hex("5A5D61"));
        }

        static void KosDoor(Color32[] px, Color wall)
        {
            Fill(px, 0.10f, 0.40f, 0f, 0.74f, Shade(wall, 1.15f));
            Fill(px, 0.12f, 0.38f, 0f, 0.72f, Hex("6B4A32"));
            Fill(px, 0.33f, 0.35f, 0.34f, 0.37f, Hex("C8B060"));
            Window(px, 0.52f, 0.86f, 0.35f, 0.75f, wall);
        }

        static void Warung(Color32[] px)
        {
            Fill(px, 0.06f, 0.94f, 0.15f, 0.70f, Hex("141414"));
            Fill(px, 0.06f, 0.94f, 0.15f, 0.32f, Hex("7A5C3A"));
            Fill(px, 0.06f, 0.94f, 0.74f, 0.92f, Hex("D8D2C0"));
        }

        static void CampusUpper(Color32[] px)
        {
            Fill(px, 0.04f, 0.96f, 0.30f, 0.85f, Hex("1E2A33"));
            for (float x = 0.04f; x < 0.97f; x += 0.23f) Fill(px, x, x + 0.012f, 0.30f, 0.85f, Hex("9A9EA2"));
        }

        static void CampusGround(Color32[] px)
        {
            Fill(px, 0.05f, 0.95f, 0f, 0.85f, Hex("22303A"));
            foreach (float x in new[] { 0.05f, 0.35f, 0.65f, 0.94f }) Fill(px, x, x + 0.012f, 0f, 0.85f, Hex("9A9EA2"));
            Fill(px, 0.42f, 0.58f, 0f, 0.80f, Hex("2C3E4A"));
        }
    }
}
