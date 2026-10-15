using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Imports CC0 textures, generates facades, creates materials/prefabs and assigns them to WorldGenSettings.</summary>
    public static class ArtAssetBuilder
    {
        public static readonly string[] Cc0Ids =
        {
            "Asphalt033", "PavingStones070", "PavingStones138", "Ground037", "Ground106",
            "Grass004", "Grass005", "Concrete034", "Concrete046", "RoofingTiles006",
            StreetTextureGenerator.PlasterId, StreetTextureGenerator.BrickId
        };

        [MenuItem("Tyco World/World Generator/Build Art Assets")]
        static void BuildAllMenu()
        {
            BuildAll();
            EditorUtility.DisplayDialog("World Generator", "Art assets selesai dibuat dan dipasang ke Settings.", "OK");
        }

        public static WorldGenSettings BuildAll()
        {
            AssetFolders.Ensure(WorldGenPaths.MaterialsFolder);
            AssetFolders.Ensure(WorldGenPaths.FacadeTexturesFolder);
            AssetFolders.Ensure(WorldGenPaths.StreetTexturesFolder);
            AssetFolders.Ensure(WorldGenPaths.PrefabsFolder);
            CheckCc0Present();
            foreach (var id in Cc0Ids)
            {
                Configure(Cc0Path(id, "Color"), false, 512);
                Configure(Cc0Path(id, "NormalGL"), true, 512);
            }
            foreach (var path in FacadeTextureGenerator.WriteAll(WorldGenPaths.FacadeTexturesFolder))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Configure(path, false, 256);
            }
            foreach (var path in StreetTextureGenerator.WriteAll(WorldGenPaths.StreetTexturesFolder))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Configure(path, false, path.EndsWith(StreetTextureGenerator.WallAtlasFile) ? 1024 : 256);
                if (path.EndsWith(StreetTextureGenerator.FenceFile)) ConfigureAlpha(path);
            }
            var materials = CreateMaterials();
            var prefabs = PlaceholderPrefabFactory.CreateAll(materials);
            var settings = DefaultSettingsFactory.LoadOrCreateAsset();
            Assign(settings, materials, prefabs);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static string AreaMaterialName(AreaCategory category)
        {
            switch (category)
            {
                case AreaCategory.Campus: return "Area_Campus";
                case AreaCategory.Hardground: return "Area_Hardground";
                case AreaCategory.Grass:
                case AreaCategory.Wood: return "Area_Grass";
                case AreaCategory.Farmland: return "Area_Farmland";
                case AreaCategory.Parking: return "Area_Parking";
                case AreaCategory.Cemetery: return "Area_Cemetery";
                case AreaCategory.Pitch: return "Area_Pitch";
                default: return "Path_Paving";
            }
        }

        public static string RoadMaterialName(string highway) =>
            highway is "footway" or "path" or "cycleway" or "steps" or "pedestrian" ? "Path_Paving"
            : highway == "track" ? "Ground_Dirt" : "Road_Asphalt";

        static string Cc0Path(string id, string map) => WorldGenPaths.Cc0TexturesFolder + "/" + id + "/" + id + "_1K-JPG_" + map + ".jpg";

        static void CheckCc0Present()
        {
            var missing = new List<string>();
            foreach (var id in Cc0Ids)
                foreach (var map in new[] { "Color", "NormalGL" })
                    if (!File.Exists(WorldGenPaths.ToAbsolute(Cc0Path(id, map)))) missing.Add(Cc0Path(id, map));
            if (missing.Count > 0)
                throw new FileNotFoundException("Tekstur CC0 belum didownload (Task 15 Step 1):\n" + string.Join("\n", missing));
        }

        static Texture2D Tex(string id, string map) => AssetDatabase.LoadAssetAtPath<Texture2D>(Cc0Path(id, map));

        static void Configure(string path, bool normalMap, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool changed = false;
            var type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type) { importer.textureType = type; changed = true; }
            if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; changed = true; }
            if (importer.sRGBTexture == normalMap) { importer.sRGBTexture = !normalMap; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; changed = true; }
            if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        /// <summary>Alpha-tested texture: keep transparency and preserve coverage in the mips so thin bars do not vanish far away.</summary>
        static void ConfigureAlpha(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool changed = false;
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
            if (!importer.mipMapsPreserveCoverage) { importer.mipMapsPreserveCoverage = true; changed = true; }
            if (Mathf.Abs(importer.alphaTestReferenceValue - 0.5f) > 1e-4f) { importer.alphaTestReferenceValue = 0.5f; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        static Texture2D StreetTexture(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(WorldGenPaths.StreetTexturesFolder + "/" + file);

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        static Material Mat(string name, Texture2D baseMap, Texture2D normalMap, Color tint, float tileMeters, float smoothness = 0.1f)
        {
            string path = WorldGenPaths.MaterialsFolder + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BumpMap", normalMap);
            if (normalMap != null) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
            float scale = tileMeters > 0f ? 1f / tileMeters : 1f;
            material.SetTextureScale("_BaseMap", new Vector2(scale, scale));
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Dictionary<string, Material> CreateMaterials()
        {
            var m = new Dictionary<string, Material>();
            void Cc0(string name, string id, string tint, float tile) => m[name] = Mat(name, Tex(id, "Color"), Tex(id, "NormalGL"), Hex(tint), tile);

            Cc0("Road_Asphalt", "Asphalt033", "FFFFFF", 4f);
            Cc0("Path_Paving", "PavingStones070", "FFFFFF", 2f);
            Cc0("Ground_Dirt", "Ground037", "FFFFFF", 4f);
            Cc0("Area_Grass", "Grass004", "FFFFFF", 4f);
            Cc0("Area_Farmland", "Grass005", "D8D890", 4f);
            Cc0("Area_Pitch", "Grass005", "C8F0A0", 4f);
            Cc0("Area_Parking", "Concrete034", "FFFFFF", 3f);
            Cc0("Area_Hardground", "Concrete034", "B0B0B0", 3f);
            Cc0("Area_Cemetery", "Ground106", "707070", 4f);
            Cc0("Area_Campus", "PavingStones138", "E0E0E0", 2f);
            Cc0("Roof_Genteng_Merah", "RoofingTiles006", "FFFFFF", 2f);
            Cc0("Roof_Genteng_Coklat", "RoofingTiles006", "8A6A5A", 2f);
            Cc0("Roof_Dak_A", "Concrete046", "FFFFFF", 3f);
            Cc0("Roof_Dak_B", "Concrete046", "A8A8A8", 3f);
            Cc0("Edge_Concrete", "Concrete034", "B4B2AA", 2f);
            Cc0("Edge_Sidewalk", "PavingStones070", "E8E8E8", 1.5f);
            m["Edge_Gutter"] = Mat("Edge_Gutter", Tex("Concrete046", "Color"), Tex("Concrete046", "NormalGL"), Hex("3A3A36"), 2f, 0.4f);
            m["Edge_Kerb"] = Mat("Edge_Kerb", AssetDatabase.LoadAssetAtPath<Texture2D>(WorldGenPaths.StreetTexturesFolder + "/" + StreetTextureGenerator.KerbFile),
                null, Color.white, 1f);
            m["Road_Marking"] = Mat("Road_Marking", null, null, Hex("DCDCD2"), 1f, 0.2f);
            m["Frontage_WallAtlas"] = Mat("Frontage_WallAtlas", StreetTexture(StreetTextureGenerator.WallAtlasFile), null, Color.white, 1f);
            var fence = Mat("Frontage_Fence", StreetTexture(StreetTextureGenerator.FenceFile), null, Color.white, 1f, 0.3f);
            fence.SetFloat("_AlphaClip", 1f);
            fence.SetFloat("_Cutoff", 0.5f);
            fence.EnableKeyword("_ALPHATEST_ON");
            fence.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(fence);
            m["Frontage_Fence"] = fence;
            m["Frontage_GateMetal"] = Mat("Frontage_GateMetal", null, null, Hex("2B2F33"), 1f, 0.45f);
            m["Barrier_Stripe"] = Mat("Barrier_Stripe", StreetTexture(StreetTextureGenerator.StripeFile), null, Color.white, 1f, 0.3f);
            m["Barrier_Board"] = Mat("Barrier_Board", null, null, Hex("F2F2EE"), 1f, 0.2f);
            m["Water"] = Mat("Water", null, null, Hex("10161A"), 1f, 0.9f);
            m["WorldGen_Fallback"] = Mat("WorldGen_Fallback", null, null, Hex("808080"), 1f);

            foreach (var spec in FacadeTextureGenerator.Specs())
            {
                string name = "M_" + spec.Name;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldGenPaths.FacadeTexturesFolder + "/" + spec.Name + ".png");
                m[name] = Mat(name, tex, null, Color.white, 1f);
            }

            m["M_Tree_Trunk"] = Mat("M_Tree_Trunk", null, null, Hex("5A4030"), 1f);
            m["M_Tree_Leaves"] = Mat("M_Tree_Leaves", null, null, Hex("3E5A2E"), 1f);
            m["M_Lamp_Metal"] = Mat("M_Lamp_Metal", null, null, Hex("4A4A4A"), 1f, 0.4f);
            var head = Mat("M_Lamp_Head", null, null, Hex("FFE8B0"), 1f);
            head.EnableKeyword("_EMISSION");
            head.SetColor("_EmissionColor", Hex("FFE8B0") * 2f);
            head.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(head);
            m["M_Lamp_Head"] = head;
            return m;
        }

        static Material[] Seq(IReadOnlyDictionary<string, Material> m, string prefix, int count)
        {
            var result = new Material[count];
            for (int i = 0; i < count; i++) result[i] = m[prefix + i];
            return result;
        }

        static void Assign(WorldGenSettings s, IReadOnlyDictionary<string, Material> m, PlaceholderPrefabs prefabs)
        {
            foreach (var rule in s.roadRules) rule.material = m[RoadMaterialName(rule.highway)];

            s.residentialMaterials = new BuildingMaterialSet
            {
                groundFloor = new[] { m["M_Res_Ground_RollingDoor"], m["M_Res_Ground_KosDoor"], m["M_Res_Ground_Warung"], m["M_Res_Ground_WallWindow"] },
                upper = Seq(m, "M_Res_Upper_", 6),
                trim = Seq(m, "M_Res_Trim_", 6),
                flatRoof = new[] { m["Roof_Dak_A"], m["Roof_Dak_B"] },
                gableRoof = new[] { m["Roof_Genteng_Merah"], m["Roof_Genteng_Coklat"] }
            };
            s.campusMaterials = new BuildingMaterialSet
            {
                groundFloor = Seq(m, "M_Campus_Ground_", 2),
                upper = Seq(m, "M_Campus_Upper_", 2),
                trim = Seq(m, "M_Campus_Trim_", 2),
                flatRoof = new[] { m["Roof_Dak_A"] },
                gableRoof = new[] { m["Roof_Genteng_Merah"] }
            };
            foreach (var rule in s.areaRules) rule.material = m[AreaMaterialName(rule.category)];
            s.waterMaterial = m["Water"];
            s.groundMaterial = m["Ground_Dirt"];
            s.fallbackMaterial = m["WorldGen_Fallback"];
            s.treePrefabs = new[] { prefabs.TreeA, prefabs.TreeB };
            s.lampPrefab = prefabs.Lamp;
            var e = s.roadEdges;
            e.concrete = m["Edge_Concrete"];
            e.gutter = m["Edge_Gutter"];
            e.sidewalk = m["Edge_Sidewalk"];
            e.kerb = m["Edge_Kerb"];
            e.marking = m["Road_Marking"];
            var f = s.frontage;
            f.wallAtlas = m["Frontage_WallAtlas"];
            f.fence = m["Frontage_Fence"];
            f.gateMetal = m["Frontage_GateMetal"];
            s.deadEnds.stripe = m["Barrier_Stripe"];
            s.barrierPrefabs = BarrierArt.BuildPrefabs(m["Barrier_Board"]);
            EditorUtility.SetDirty(s);
        }
    }
}
