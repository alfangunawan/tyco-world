using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Procedural textures for road dressing and frontage (spec street-frontage §10). Deterministic per file.</summary>
    public static class StreetTextureGenerator
    {
        public const string KerbFile = "KerbStripes.png";
        public const int KerbWidth = 128, KerbHeight = 32;

        /// <summary>One metre of kerb along u: a black then a white 0.5 m block, with dirt noise and a darker lower edge.</summary>
        public static Texture2D KerbStripes()
        {
            var px = new Color32[KerbWidth * KerbHeight];
            var rng = new SplitMix64(SplitMix64.Fnv1a64("kerb"));
            for (int y = 0; y < KerbHeight; y++)
                for (int x = 0; x < KerbWidth; x++)
                {
                    float value = x < KerbWidth / 2 ? 0.12f : 0.85f;
                    value *= 1f + ((float)rng.NextDouble() - 0.5f) * 0.15f;
                    if (y < 4) value -= 0.06f;
                    value = Mathf.Clamp01(value);
                    px[y * KerbWidth + x] = new Color(value, value, value * 0.97f, 1f);
                }
            return Finish(px, KerbWidth, KerbHeight, "KerbStripes");
        }

        public const string WallAtlasFile = "WallAtlas.png", FenceFile = "FenceBars.png";
        public const string PlasterId = "Plaster001", BrickId = "Bricks059";
        public const int AtlasWidth = 256, BandHeight = 128, AtlasBands = 8, PixelsPerMeter = 64, FenceSize = 128;
        const float PlasterSourceMeters = 2f, BrickSourceMeters = 1.5f;

        /// <summary>Tints of the two brick bands (6 and 7) over the CC0 brick colour map.</summary>
        public static readonly Color[] BrickTints = { Color.white, new Color(0.8f, 0.72f, 0.68f, 1f) };

        static readonly Color Moss = new Color(0.33f, 0.40f, 0.24f, 1f);
        static readonly Color32 Iron = new Color32(0x2B, 0x2F, 0x33, 255);

        public static string Cc0ColorPath(string id) => WorldGenPaths.Cc0TexturesFolder + "/" + id + "/" + id + "_1K-JPG_Color.jpg";

        /// <summary>
        /// Eight stacked bands (band 0 at the bottom), each 4 m × 2 m of wall at 64 px/m. Bands 0–5 are the plaster colour map
        /// re-tinted with the residential facade palette, bands 6–7 the brick map with BrickTints. Bottom 25 cm get green grime,
        /// and three faint rain streaks per band run down from 1.2 m.
        /// </summary>
        public static Texture2D WallAtlas(Texture2D plaster, Texture2D brick)
        {
            var px = new Color32[AtlasWidth * BandHeight * AtlasBands];
            float plasterMean = MeanLuminance(plaster);
            var palette = FacadeTextureGenerator.ResidentialPalette;
            var rng = new SplitMix64(SplitMix64.Fnv1a64("wall-atlas"));
            var streaks = new int[3];
            for (int band = 0; band < AtlasBands; band++)
            {
                for (int k = 0; k < streaks.Length; k++) streaks[k] = rng.RangeInclusive(0, AtlasWidth - 1);
                bool isBrick = band >= palette.Length;
                for (int y = 0; y < BandHeight; y++)
                {
                    float my = y / (float)PixelsPerMeter;
                    for (int x = 0; x < AtlasWidth; x++)
                    {
                        float mx = x / (float)PixelsPerMeter;
                        Color c;
                        if (isBrick)
                        {
                            c = brick.GetPixelBilinear(mx / BrickSourceMeters, my / BrickSourceMeters) * BrickTints[band - palette.Length];
                        }
                        else
                        {
                            float lum = plaster.GetPixelBilinear(mx / PlasterSourceMeters, my / PlasterSourceMeters).grayscale;
                            float k = plasterMean > 1e-4f ? Mathf.Clamp(lum / plasterMean, 0.6f, 1.4f) : 1f;
                            c = palette[band] * k;
                        }
                        c = Weather(c, x, my, streaks);
                        c.a = 1f;
                        px[(band * BandHeight + y) * AtlasWidth + x] = c;
                    }
                }
            }
            return Finish(px, AtlasWidth, BandHeight * AtlasBands, "WallAtlas");
        }

        /// <summary>One metre of iron fence along u (8 bars), the band height along v: bars and three rails opaque, gaps clear.</summary>
        public static Texture2D FenceBars()
        {
            var px = new Color32[FenceSize * FenceSize];
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < FenceSize; y++)
                for (int x = 0; x < FenceSize; x++)
                {
                    bool bar = x % 16 < 3;
                    bool rail = y < 4 || y >= FenceSize - 4 || (y >= 60 && y < 64);
                    px[y * FenceSize + x] = bar || rail ? Iron : clear;
                }
            return Finish(px, FenceSize, FenceSize, "FenceBars");
        }

        static Color Weather(Color c, int x, float my, int[] streaks)
        {
            if (my < 0.25f)
            {
                float t = 1f - my / 0.25f;
                c = Color.Lerp(c * (1f - 0.3f * t), Moss, 0.25f * t);
            }
            if (my > 1.2f)
                foreach (int sx in streaks)
                    if (Mathf.Abs(x - sx) <= 2) c *= 0.88f;
            return c;
        }

        static float MeanLuminance(Texture2D tex)
        {
            float sum = 0f;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++) sum += tex.GetPixelBilinear((x + 0.5f) / 32f, (y + 0.5f) / 32f).grayscale;
            return sum / (32f * 32f);
        }

        static Texture2D LoadReadable(string assetPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(WorldGenPaths.ToAbsolute(assetPath)));
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        /// <summary>Writes every street texture as PNG into <paramref name="folder"/> and returns the asset paths. Needs the CC0 plaster and brick maps.</summary>
        public static List<string> WriteAll(string folder)
        {
            AssetFolders.Ensure(folder);
            var paths = new List<string>();
            Write(folder, KerbFile, KerbStripes(), paths);
            Write(folder, FenceFile, FenceBars(), paths);
            var plaster = LoadReadable(Cc0ColorPath(PlasterId));
            var brick = LoadReadable(Cc0ColorPath(BrickId));
            try
            {
                Write(folder, WallAtlasFile, WallAtlas(plaster, brick), paths);
            }
            finally
            {
                Object.DestroyImmediate(plaster);
                Object.DestroyImmediate(brick);
            }
            return paths;
        }

        static Texture2D Finish(Color32[] px, int width, int height, string name)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = name };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static void Write(string folder, string file, Texture2D tex, List<string> paths)
        {
            string assetPath = folder + "/" + file;
            File.WriteAllBytes(WorldGenPaths.ToAbsolute(assetPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            paths.Add(assetPath);
        }
    }
}
