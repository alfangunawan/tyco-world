using System.IO;
using System.Text;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Puts the player and the kos front yard into World.unity (manual content; idempotent).</summary>
    public static class PlayerSceneSetup
    {
        public const long KosOsmId = 409467778;
        public const string FrontyardName = "KosFrontyard";
        public const string PlayerName = "Player";

        [MenuItem("Tyco World/Player Setup")]
        static void SetupMenu()
        {
            if (!Run(true, false, Vector2.zero, out string error)) EditorUtility.DisplayDialog("Player Setup gagal", error, "OK");
        }

        [MenuItem("Tyco World/Re-place Kos Frontyard")]
        static void ReplaceMenu()
        {
            if (!Run(true, true, Vector2.zero, out string error)) EditorUtility.DisplayDialog("Re-place gagal", error, "OK");
        }

        public static bool Run(bool interactive, bool replaceFrontyard, Vector2 preferredOutward, out string error)
        {
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            var world = WorldScenes.EnsureWorldSceneOpen();
            var assets = PlayerAssetsBuilder.BuildAll();

            var frontyard = FindRoot(world, FrontyardName);
            if (frontyard == null || replaceFrontyard)
            {
                if (!TryPlanKos(preferredOutward, out var plan, out error)) return false;
                if (frontyard != null) Object.DestroyImmediate(frontyard);
                SceneManager.SetActiveScene(world);
                frontyard = KosFrontyardBuilder.Build(plan, assets.GatePrefab, assets.DoorPrefab, assets.MotorPrefab,
                    assets.Materials["Yard"], assets.Materials["M_Wall_Plaster"], assets.UnitBox);
                Debug.Log("[Player] Halaman kos: " + KosSitePlanner.Describe(plan));
            }
            var spawn = frontyard.GetComponentInChildren<PlayerSpawnPoint>(true);

            var playerGo = FindRoot(world, PlayerName);
            if (playerGo == null)
            {
                playerGo = (GameObject)PrefabUtility.InstantiatePrefab(assets.PlayerPrefab, world);
                playerGo.name = PlayerName;
            }
            playerGo.transform.SetPositionAndRotation(spawn.Feet, Quaternion.Euler(0f, spawn.Yaw, 0f));
            var spawner = playerGo.GetComponent<PlayerSpawner>();
            spawner.Configure(playerGo.GetComponent<FirstPersonPlayer>(), spawn);
            PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);

            var preview = FindRoot(world, "PreviewCam");
            if (preview != null) preview.SetActive(false);

            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            WarnOverlaps(frontyard);
            return true;
        }

        static bool TryPlanKos(Vector2 preferredOutward, out KosSitePlan plan, out string error)
        {
            plan = default;
            var settings = DefaultSettingsFactory.LoadOrCreateAsset();
            string raw = WorldGenPaths.ToAbsolute(settings.rawDataPath);
            if (!File.Exists(raw)) { error = "Data OSM belum ada (" + settings.rawDataPath + ")."; return false; }
            var classified = WorldMeshPipeline.Classify(File.ReadAllText(raw), settings, new GenerationReport());
            if (!KosSiteSource.TryBuild(classified, KosOsmId, 60f, out var footprint, out var roads, out var neighbours))
            {
                error = "Kos Raflesia 3 (way 409467778) tidak ada di data OSM.";
                return false;
            }
            if (!KosSitePlanner.TryPlan(footprint, roads, neighbours, new KosSiteOptions { preferredOutward = preferredOutward }, out plan, out string reason))
            {
                error = "Tidak bisa menata halaman kos: " + reason;
                return false;
            }
            error = null;
            return true;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        /// <summary>Warns about generated props or buildings that intrude into the front yard (they are not moved).</summary>
        static void WarnOverlaps(GameObject frontyard)
        {
            Physics.SyncTransforms();
            var yard = frontyard.transform.Find("Yard").GetComponent<BoxCollider>();
            var b = yard.bounds;
            var hits = Physics.OverlapBox(b.center + Vector3.up * 1f, new Vector3(b.extents.x, 0.9f, b.extents.z) * 0.95f,
                Quaternion.identity, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore);
            var sb = new StringBuilder();
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(frontyard.transform)) continue;
                if (hit.GetComponentInParent<GroundSurface>() != null) continue;
                var marker = hit.GetComponentInParent<KeyBuildingMarker>();
                if (marker != null && marker.OsmId == KosOsmId) continue;
                sb.Append(hit.transform.root.name).Append('/').Append(hit.name).Append("; ");
            }
            if (sb.Length > 0) Debug.LogWarning("[Player] Objek generate menembus halaman kos: " + sb);
        }
    }
}
