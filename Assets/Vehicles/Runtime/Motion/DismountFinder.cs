using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Where the rider can step off (spec #5 §4): beside the bike, left first, on ground with room for the standing capsule.</summary>
    public static class DismountFinder
    {
        public static bool TryFind(Transform bike, float sideOffset, float radius, float height, out Vector3 feet)
        {
            Physics.SyncTransforms();
            foreach (float side in new[] { -1f, 1f })
            {
                var spot = bike.position + bike.right * (side * sideOffset);
                if (!Physics.Raycast(spot + Vector3.up * 0.6f, Vector3.down, out var ground, 1.6f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                    continue;
                var candidate = ground.point;
                var bottom = candidate + Vector3.up * (radius + 0.05f);
                var top = candidate + Vector3.up * (height - radius);
                if (Physics.CheckCapsule(bottom, top, radius * 0.95f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore)) continue;
                feet = candidate;
                return true;
            }
            feet = default;
            return false;
        }
    }
}
