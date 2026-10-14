using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>The renderers of one Chunk_x_z container (one category) and their combined world bounds.</summary>
    public sealed class ChunkGroup
    {
        readonly Renderer[] renderers;

        public ChunkGroup(CullCategory category, Vector2Int chunk, Renderer[] renderers, Bounds bounds)
        {
            Category = category;
            Chunk = chunk;
            this.renderers = renderers;
            Bounds = bounds;
            Visible = true;
        }

        public CullCategory Category { get; }
        public Vector2Int Chunk { get; }
        public IReadOnlyList<Renderer> Renderers => renderers;
        public Bounds Bounds { get; }
        public bool Visible { get; private set; }

        /// <summary>Distance from <paramref name="point"/> to the nearest point of the group's bounds (0 inside).</summary>
        public float DistanceTo(Vector3 point) => Mathf.Sqrt(Bounds.SqrDistance(point));

        /// <summary>Enables or disables the renderers only (colliders are untouched); no-op when unchanged.</summary>
        public void SetVisible(bool visible)
        {
            if (Visible == visible) return;
            Visible = visible;
            foreach (var r in renderers)
                if (r != null) r.enabled = visible;
        }
    }
}
