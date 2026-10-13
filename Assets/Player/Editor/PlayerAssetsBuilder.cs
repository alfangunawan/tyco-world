using System.Collections.Generic;
using System.IO;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TycoWorld.Player.Authoring
{
    public sealed class PlayerAssets
    {
        public PlayerTuning Tuning;
        public FootstepBank Footsteps;
        public PanelSettings Panel;
        public readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        public readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        public UnityEngine.Mesh UnitBox;
        public GameObject PlayerPrefab, GatePrefab, DoorPrefab, MotorPrefab;
    }

    /// <summary>Creates or updates every player asset in place (re-running is safe).</summary>
    public static class PlayerAssetsBuilder
    {
        const string Theme = "@import url(\"unity-theme://default\");\n";

        [MenuItem("Tyco World/Player/Build Player Assets")]
        static void Menu() => BuildAll();

        public static PlayerAssets BuildAll()
        {
            // Create* builds temporary objects in the active scene; re-save scenes that were clean so Procedure T is not blocked.
            var cleanScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isDirty && s.path.Length > 0) cleanScenes.Add(s);
            }
            foreach (var folder in new[] { PlayerPaths.ConfigFolder, PlayerPaths.UiFolder, PlayerPaths.MaterialsFolder, PlayerPaths.MeshesFolder })
                AssetFolders.Ensure(folder);
            var a = new PlayerAssets
            {
                Tuning = LoadOrCreate<PlayerTuning>(PlayerPaths.TuningAsset),
                Footsteps = LoadOrCreate<FootstepBank>(PlayerPaths.FootstepBankAsset)
            };
            FillFootsteps(a.Footsteps);
            EditorUtility.SetDirty(a.Footsteps);
            a.Panel = BuildPanel();
            foreach (var file in PlayerAudioMap.InteractionFiles)
                a.Clips[file] = AssetDatabase.LoadAssetAtPath<AudioClip>(PlayerAudioMap.InteractionPath(file));

            a.Materials["M_Gate_Metal"] = Mat("M_Gate_Metal", "2B2F33", 0.45f);
            a.Materials["M_Door_Wood"] = Mat("M_Door_Wood", "5A3B22", 0.2f);
            a.Materials["M_Wall_Plaster"] = Mat("M_Wall_Plaster", "B8B2A6", 0.05f);
            a.Materials["M_Motor_Body"] = Mat("M_Motor_Body", "7A1E1E", 0.5f);
            a.Materials["M_Motor_Dark"] = Mat("M_Motor_Dark", "1C1C1C", 0.3f);
            a.Materials["Yard"] = AssetDatabase.LoadAssetAtPath<Material>(WorldGenPaths.MaterialsFolder + "/Area_Hardground.mat");

            a.UnitBox = SaveMesh(PlaceholderPropFactory.UnitBoxMesh());
            a.GatePrefab = PlaceholderPropFactory.SaveAsPrefab(PlaceholderPropFactory.CreateGate(a.Materials["M_Gate_Metal"],
                a.Clips[PlayerAudioMap.GateUnlatch], a.Clips[PlayerAudioMap.GateCreak], a.Clips[PlayerAudioMap.GateLatch]), PlayerPaths.GatePrefab);
            a.DoorPrefab = PlaceholderPropFactory.SaveAsPrefab(PlaceholderPropFactory.CreateDoor(a.Materials["M_Door_Wood"], a.Materials["M_Gate_Metal"],
                a.Clips[PlayerAudioMap.DoorRattle]), PlayerPaths.DoorPrefab);
            a.MotorPrefab = PlaceholderPropFactory.SaveAsPrefab(PlaceholderPropFactory.CreateMotor(a.Materials["M_Motor_Body"], a.Materials["M_Motor_Dark"],
                a.Clips[PlayerAudioMap.VehicleKeys]), PlayerPaths.MotorPrefab);
            a.PlayerPrefab = PlaceholderPropFactory.SaveAsPrefab(PlayerRigBuilder.CreateRig(a.Tuning, a.Footsteps, a.Panel,
                a.Clips[PlayerAudioMap.FlashlightClick]), PlayerPaths.PlayerPrefab);
            AssetDatabase.SaveAssets();
            foreach (var s in cleanScenes)
                if (s.IsValid() && s.isDirty) EditorSceneManager.SaveScene(s);
            return a;
        }

        public static void FillFootsteps(FootstepBank bank)
        {
            bank.surfaces = new List<SurfaceClips>();
            foreach (var set in PlayerAudioMap.Footsteps)
            {
                var clips = new AudioClip[set.Files.Length];
                for (int i = 0; i < clips.Length; i++) clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(PlayerAudioMap.FootstepPath(set.Files[i]));
                bank.surfaces.Add(new SurfaceClips { surface = set.Surface, clips = clips, volume = set.Volume, pitchMin = set.PitchMin, pitchMax = set.PitchMax });
            }
        }

        static PanelSettings BuildPanel()
        {
            string abs = WorldGenPaths.ToAbsolute(PlayerPaths.ThemeAsset);
            if (!File.Exists(abs) || File.ReadAllText(abs) != Theme)
            {
                File.WriteAllText(abs, Theme);
                AssetDatabase.ImportAsset(PlayerPaths.ThemeAsset, ImportAssetOptions.ForceSynchronousImport);
            }
            var panel = LoadOrCreate<PanelSettings>(PlayerPaths.PanelSettingsAsset);
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(PlayerPaths.ThemeAsset);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        static UnityEngine.Mesh SaveMesh(UnityEngine.Mesh mesh)
        {
            string path = PlayerPaths.MeshesFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        static Material Mat(string name, string hex, float smoothness)
        {
            string path = PlayerPaths.MaterialsFolder + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
