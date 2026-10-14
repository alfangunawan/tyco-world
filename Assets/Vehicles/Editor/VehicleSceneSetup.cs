using System.Collections.Generic;
using System.Text;
using TycoWorld.Player;
using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>
    /// "Tyco World/Vehicle Setup" (spec #5 §2): builds the vehicle assets and puts the rideable Motorbike into World.unity
    /// where the kos placeholder motor stood. Safe to re-run; run it again after "Re-place Kos Frontyard".
    /// </summary>
    public static class VehicleSceneSetup
    {
        public const string MissingPlaceholderError =
            "Motor placeholder di KosFrontyard tidak ditemukan. Jalankan \"Tyco World/Player Setup\" dulu.";
        public const string OverlapWarning = "[Vehicle] Motor menembus objek lain di posisi parkirnya: ";

        [MenuItem("Tyco World/Vehicle Setup")]
        static void SetupMenu()
        {
            if (!Run(true, out string error)) EditorUtility.DisplayDialog("Vehicle Setup gagal", error, "OK");
        }

        public static bool Run(bool interactive, out string error)
        {
            if (!WorldScenes.CheckSaved(interactive, out error)) return false;
            var world = WorldScenes.EnsureWorldSceneOpen();
            var assets = VehicleAssetsBuilder.BuildAll();
            SceneManager.SetActiveScene(world);
            var bike = Wire(world, assets.MotorbikePrefab, out error);
            if (bike == null) return false;
            EditorSceneManager.MarkSceneDirty(world);
            EditorSceneManager.SaveScene(world);
            WorldScenes.OpenGeneratedSceneAdditive();
            SceneManager.SetActiveScene(world);
            WarnOverlaps(bike);
            return true;
        }

        /// <summary>
        /// Creates the Motorbike root (a prefab instance) if needed, moves it to the first placeholder motor's world pose
        /// (yaw only) and deletes every placeholder. Without a placeholder an existing bike keeps its pose.
        /// </summary>
        public static GameObject Wire(Scene world, GameObject prefab, out string error)
        {
            error = null;
            var placeholders = new List<PlaceholderVehicleInteractable>();
            foreach (var go in world.GetRootGameObjects())
                placeholders.AddRange(go.GetComponentsInChildren<PlaceholderVehicleInteractable>(true));
            var bike = FindRoot(world, MotorbikeBuilder.RootName);
            if (bike == null && placeholders.Count == 0)
            {
                error = MissingPlaceholderError;
                return null;
            }
            if (bike == null)
            {
                bike = (GameObject)PrefabUtility.InstantiatePrefab(prefab, world);
                bike.name = MotorbikeBuilder.RootName;
            }
            if (placeholders.Count > 0)
            {
                var anchor = placeholders[0].transform;
                bike.transform.SetPositionAndRotation(anchor.position, Quaternion.Euler(0f, anchor.eulerAngles.y, 0f));
                foreach (var placeholder in placeholders)
                {
                    if (placeholder == null) continue;
                    var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(placeholder.gameObject);
                    Object.DestroyImmediate(instanceRoot != null ? instanceRoot : placeholder.gameObject);
                }
            }
            EditorUtility.SetDirty(bike);
            return bike;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        /// <summary>Warns when the parked bike's body overlaps generated or yard geometry above step height (it is not moved).</summary>
        static void WarnOverlaps(GameObject bike)
        {
            Physics.SyncTransforms();
            var body = bike.GetComponent<CharacterController>();
            var t = bike.transform;
            var bottom = t.position + Vector3.up * (body.radius + body.stepOffset);
            var top = t.position + Vector3.up * (body.height - body.radius);
            var hits = Physics.OverlapCapsule(bottom, top, body.radius * 0.95f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore);
            var sb = new StringBuilder();
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(t)) continue;
                if (hit.GetComponentInParent<GroundSurface>() != null) continue;
                sb.Append(hit.transform.root.name).Append('/').Append(hit.name).Append("; ");
            }
            if (sb.Length > 0) Debug.LogWarning(OverlapWarning + sb);
        }
    }
}
