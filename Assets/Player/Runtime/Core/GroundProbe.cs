using System.Collections.Generic;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Player
{
    public struct GroundInfo
    {
        public bool Hit;
        public SurfaceType Surface;
        public OsmFeature Feature;
        public Collider Collider;
        public Vector3 Point;
    }

    /// <summary>Downward ray that reports the walkable surface and OSM feature under a point (cached per collider).</summary>
    public static class GroundProbe
    {
        struct Entry
        {
            public SurfaceType Surface;
            public OsmFeature Feature;
        }

        static readonly Dictionary<Collider, Entry> cache = new Dictionary<Collider, Entry>();
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            hooked = false;
        }

        public static bool Probe(Vector3 from, float distance, out GroundInfo info)
        {
            EnsureHooked();
            info = default;
            if (!Physics.Raycast(from, Vector3.down, out var hit, distance, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                return false;
            if (!cache.TryGetValue(hit.collider, out var entry))
            {
                var surface = hit.collider.GetComponentInParent<GroundSurface>();
                entry = new Entry
                {
                    Surface = surface != null ? surface.Type : SurfaceType.Default,
                    Feature = hit.collider.GetComponentInParent<OsmFeature>()
                };
                cache[hit.collider] = entry;
            }
            info = new GroundInfo { Hit = true, Surface = entry.Surface, Feature = entry.Feature, Collider = hit.collider, Point = hit.point };
            return true;
        }

        public static void ClearCache() => cache.Clear();

        static void EnsureHooked()
        {
            if (hooked) return;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            hooked = true;
        }

        static void OnSceneUnloaded(Scene scene) => cache.Clear();
    }
}
