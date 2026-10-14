using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>
    /// Reads the generated hierarchy (Roads|Buildings|Areas|Water|Frontage|RoadEdges|Props/Trees|Props/Lamps → Chunk_x_z) into cullable
    /// groups. Ground, KeyBuildings and WorldEdge are not indexed, so they are always drawn (spec #4 §5.1).
    /// </summary>
    public static class WorldChunkIndex
    {
        const string ChunkPrefix = "Chunk_";

        public static List<ChunkGroup> Build(Transform root)
        {
            var groups = new List<ChunkGroup>();
            if (root == null) return groups;
            Add(root.Find("Roads"), CullCategory.Structure, groups);
            Add(root.Find("Buildings"), CullCategory.Structure, groups);
            Add(root.Find("Areas"), CullCategory.Structure, groups);
            Add(root.Find("Water"), CullCategory.Structure, groups);
            Add(root.Find("Props/Trees"), CullCategory.Prop, groups);
            Add(root.Find("Props/Lamps"), CullCategory.Prop, groups);
            Add(root.Find("RoadEdges"), CullCategory.Detail, groups);
            Add(root.Find("Frontage"), CullCategory.Detail, groups);
            return groups;
        }

        public static bool TryParseChunk(string name, out Vector2Int chunk)
        {
            chunk = default;
            if (name == null || !name.StartsWith(ChunkPrefix, StringComparison.Ordinal)) return false;
            var parts = name.Substring(ChunkPrefix.Length).Split('_');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x)) return false;
            if (!int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int z)) return false;
            chunk = new Vector2Int(x, z);
            return true;
        }

        static void Add(Transform container, CullCategory category, List<ChunkGroup> groups)
        {
            if (container == null) return;
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (!TryParseChunk(child.name, out var chunk)) continue;
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                groups.Add(new ChunkGroup(category, chunk, renderers, bounds));
            }
        }
    }
}
