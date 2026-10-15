using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Instantiates the generated hierarchy (spec section 11) inside the generated scene.</summary>
    public static class SceneObjectFactory
    {
        const StaticEditorFlags Flags = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

        public static GameObject Populate(Scene scene, BuiltWorld world, IReadOnlyDictionary<BuiltFeature, MeshAsset> meshes,
            Mesh groundMesh, WorldGenSettings settings, GenerationReport report, Action<string, float> progress)
        {
            var root = new GameObject(WorldGenPaths.GeneratedSceneName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var containers = new Dictionary<string, Transform>();

            Transform Container(string path)
            {
                if (containers.TryGetValue(path, out var existing)) return existing;
                int slash = path.LastIndexOf('/');
                Transform parent = slash < 0 ? root.transform : Container(path.Substring(0, slash));
                var go = new GameObject(slash < 0 ? path : path.Substring(slash + 1));
                go.transform.SetParent(parent, false);
                containers[path] = go.transform;
                return go.transform;
            }

            var ground = new GameObject("Ground");
            ground.transform.SetParent(root.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = Resolve(settings.groundMaterial, settings, report, "Ground");
            GroundBuilder.ColliderBox(world.Bounds, out var center, out var size);
            var box = ground.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            GameObjectUtility.SetStaticEditorFlags(ground, Flags);
            SetSurface(ground, SurfaceRules.Ground);
            AddEdgeWalls(Container("WorldEdge"), world.Bounds);

            for (int i = 0; i < world.Features.Count; i++)
            {
                if (i % 250 == 0) progress?.Invoke("Membuat objek " + i + "/" + world.Features.Count, (float)i / world.Features.Count);
                var feature = world.Features[i];
                GameObject go;
                if (feature.Mesh == null)
                {
                    if (feature.Key == null || feature.Key.prefab == null) continue;
                    go = (GameObject)PrefabUtility.InstantiatePrefab(feature.Key.prefab, scene);
                    go.name = feature.Key.label;
                    go.transform.SetParent(Container(FeatureGroups.KeyBuildings), false);
                    go.transform.SetPositionAndRotation(Xz.At(feature.Anchor, 0f),
                        Quaternion.Euler(0f, ObbCalculator.YawDegreesForLocalX(feature.Obb.AxisU), 0f));
                }
                else
                {
                    if (!meshes.TryGetValue(feature, out var asset)) continue;
                    go = new GameObject(ObjectName(feature));
                    string path = feature.Group == FeatureGroups.KeyBuildings
                        ? FeatureGroups.KeyBuildings
                        : feature.Group + "/" + WorldMeshPipeline.ChunkName(feature.Chunk);
                    go.transform.SetParent(Container(path), false);
                    go.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
                    var materials = new Material[asset.UsedSubmeshes.Length];
                    for (int k = 0; k < materials.Length; k++)
                    {
                        int sub = asset.UsedSubmeshes[k];
                        var material = sub < feature.Materials.Length ? feature.Materials[sub] : null;
                        materials[k] = Resolve(material, settings, report, feature.Group + "/" + feature.Category);
                    }
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = materials;
                    // Kerb strips are at most ~0.2 m tall: their shadows are invisible but cost shadow-pass batches.
                    if (feature.Kind == FeatureKind.RoadEdge) renderer.shadowCastingMode = ShadowCastingMode.Off;
                    if (feature.AddCollider) go.AddComponent<MeshCollider>().sharedMesh = asset.Collider != null ? asset.Collider : asset.Mesh;
                }

                if (feature.HasOsmIdentity)
                {
                    var osm = go.GetComponent<OsmFeature>();
                    if (osm == null) osm = go.AddComponent<OsmFeature>();
                    osm.Initialize(feature.OsmId, feature.ElementType, feature.Kind, feature.Name ?? "", feature.Tags);
                }
                ApplySurface(go, feature.Surface);
                if (feature.Key != null)
                {
                    var marker = go.GetComponent<KeyBuildingMarker>();
                    if (marker == null) marker = go.AddComponent<KeyBuildingMarker>();
                    marker.Initialize(feature.OsmId, feature.Key.label);
                }
                GameObjectUtility.SetStaticEditorFlags(go, Flags);
            }

            PlaceProps(scene, world.Trees, settings.treePrefabs, WorldMeshPipeline.TreesGroup, world.ChunkSize, Container, report, "pohon");
            var lampPrefabs = settings.lampPrefab != null ? new[] { settings.lampPrefab } : new GameObject[0];
            PlaceProps(scene, world.Lamps, lampPrefabs, WorldMeshPipeline.LampsGroup, world.ChunkSize, Container, report, "lampu");
            PlaceProps(scene, world.Barriers, settings.barrierPrefabs, WorldMeshPipeline.BarrierPropsGroup, world.ChunkSize, Container, report, "pembatas");
            return root;
        }

        public static void AddEdgeWalls(Transform parent, Rect bounds)
        {
            string[] names = { "Edge_West", "Edge_East", "Edge_South", "Edge_North" };
            var walls = GroundBuilder.EdgeWalls(bounds, GroundBuilder.EdgeWallHeight, GroundBuilder.EdgeWallThickness);
            for (int i = 0; i < walls.Length; i++)
            {
                var wall = new GameObject(names[i]);
                wall.transform.SetParent(parent, false);
                var box = wall.AddComponent<BoxCollider>();
                box.center = walls[i].Center;
                box.size = walls[i].Size;
                GameObjectUtility.SetStaticEditorFlags(wall, Flags);
            }
        }

        /// <summary>Adds or updates <see cref="GroundSurface"/>; Default means "not a walkable surface" and adds nothing.</summary>
        public static void ApplySurface(GameObject go, SurfaceType surface)
        {
            if (surface == SurfaceType.Default) return;
            SetSurface(go, surface);
        }

        static void SetSurface(GameObject go, SurfaceType surface)
        {
            var component = go.GetComponent<GroundSurface>();
            if (component == null) component = go.AddComponent<GroundSurface>();
            component.Initialize(surface);
        }

        static string ObjectName(BuiltFeature f)
        {
            if (f.Key != null && !string.IsNullOrEmpty(f.Key.label)) return f.Key.label;
            if (!string.IsNullOrEmpty(f.Name)) return f.Name;
            string id = f.OsmId.ToString(CultureInfo.InvariantCulture);
            switch (f.Kind)
            {
                case FeatureKind.Road: return "Road_" + id;
                case FeatureKind.Building: return "Building_" + id;
                case FeatureKind.Area: return "Area_" + f.Category + "_" + id;
                default: return "Water_" + id;
            }
        }

        static void PlaceProps(Scene scene, List<PropPlacement> placements, GameObject[] prefabs, string group, float chunkSize,
            Func<string, Transform> container, GenerationReport report, string label)
        {
            if (placements.Count == 0) return;
            if (prefabs == null || prefabs.Length == 0 || prefabs.All(p => p == null))
            {
                report.Warn("Prefab " + label + " belum diisi di Settings; " + label + " dilewati.");
                return;
            }
            var fallback = prefabs.First(p => p != null);
            foreach (var placement in placements)
            {
                var prefab = prefabs[placement.Variant % prefabs.Length];
                if (prefab == null) prefab = fallback;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var chunk = WorldMeshPipeline.ChunkOf(placement.Position, chunkSize);
                go.transform.SetParent(container(group + "/" + WorldMeshPipeline.ChunkName(chunk)), false);
                go.transform.SetPositionAndRotation(Xz.At(placement.Position, 0f), Quaternion.Euler(0f, placement.YawDegrees, 0f));
                go.transform.localScale = prefab.transform.localScale * placement.Scale;
                GameObjectUtility.SetStaticEditorFlags(go, Flags);
            }
        }

        static Material Resolve(Material material, WorldGenSettings settings, GenerationReport report, string context)
        {
            if (material != null) return material;
            report.Warn("Material kosong untuk " + context + "; memakai fallback.");
            if (settings.fallbackMaterial != null) return settings.fallbackMaterial;
            var pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline != null ? pipeline.defaultMaterial : null;
        }
    }
}
