using System;
using TycoWorld.Player.Authoring;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Every material the motorbike rig uses.</summary>
    public sealed class VehicleMaterialSet
    {
        public Material Body, Dark, Chrome, TailLight, HeadlightLens, Shirt, Pants, Skin, Helmet;
    }

    /// <summary>
    /// Bike paint and dark parts come from the player's placeholder motor (same look as before); the rest is created
    /// once in Assets/Vehicles/Materials and later edits by the user are kept.
    /// </summary>
    public static class VehicleMaterials
    {
        public const string BodyAsset = PlayerPaths.MaterialsFolder + "/M_Motor_Body.mat";
        public const string DarkAsset = PlayerPaths.MaterialsFolder + "/M_Motor_Dark.mat";
        const string EmissionKeyword = "_EMISSION";

        public static Color TailEmission => new Color(1f, 0.16f, 0.1f, 1f);
        public static Color LensEmission => new Color(1f, 0.96f, 0.86f, 1f);

        public static VehicleMaterialSet LoadOrCreate()
        {
            AssetFolders.Ensure(VehiclePaths.MaterialsFolder);
            return new VehicleMaterialSet
            {
                Body = Require(BodyAsset),
                Dark = Require(DarkAsset),
                Chrome = Create("M_Bike_Chrome", "B9BEC4", 0.8f, Color.black),
                TailLight = Create("M_Bike_TailLight", "5C0B08", 0.5f, TailEmission),
                HeadlightLens = Create("M_Bike_HeadlightLens", "D9D7CF", 0.8f, LensEmission),
                Shirt = Create("M_Rider_Shirt", "2F5D8A", 0.1f, Color.black),
                Pants = Create("M_Rider_Pants", "2A2C33", 0.1f, Color.black),
                Skin = Create("M_Rider_Skin", "C69A74", 0.2f, Color.black),
                Helmet = Create("M_Rider_Helmet", "17181C", 0.7f, Color.black)
            };
        }

        static Material Require(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException(path + " belum ada. Jalankan \"Tyco World/Player Setup\" dulu.");
            return material;
        }

        /// <summary>
        /// URP's material validation re-derives _EMISSION from the GI flags and drops it when they are None, so light
        /// materials use RealtimeEmissive. Repairs older assets without touching colours the user edited.
        /// </summary>
        static void KeepEmission(Material material)
        {
            if (material.globalIlluminationFlags == MaterialGlobalIlluminationFlags.RealtimeEmissive
                && material.IsKeywordEnabled(EmissionKeyword)) return;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.EnableKeyword(EmissionKeyword);
            EditorUtility.SetDirty(material);
        }

        static Material Create(string name, string hex, float smoothness, Color emission)
        {
            string path = VehiclePaths.MaterialsFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                if (emission.maxColorComponent > 0f) KeepEmission(material);
                return material;
            }
            material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword(EmissionKeyword);
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(material, path);
            // CreateAsset drops the emission keyword on a fresh material; enabling it again afterwards makes it stick.
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword(EmissionKeyword);
                EditorUtility.SetDirty(material);
            }
            return material;
        }
    }
}
