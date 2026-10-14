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

        /// <summary>Writes every street texture as PNG into <paramref name="folder"/> and returns the asset paths.</summary>
        public static List<string> WriteAll(string folder)
        {
            AssetFolders.Ensure(folder);
            var paths = new List<string>();
            Write(folder, KerbFile, KerbStripes(), paths);
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
