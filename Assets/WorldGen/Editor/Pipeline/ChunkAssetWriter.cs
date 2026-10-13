using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public readonly struct MeshAsset
    {
        public readonly Mesh Mesh;
        public readonly int[] UsedSubmeshes;

        public MeshAsset(Mesh mesh, int[] usedSubmeshes)
        {
            Mesh = mesh;
            UsedSubmeshes = usedSubmeshes;
        }
    }

    /// <summary>Writes one .asset per 100 m chunk containing every feature mesh of that chunk as sub-assets.</summary>
    public static class ChunkAssetWriter
    {
        public static Dictionary<BuiltFeature, MeshAsset> Write(BuiltWorld world, string folder, out Mesh groundMesh)
        {
            AssetFolders.Ensure(folder);
            var result = new Dictionary<BuiltFeature, MeshAsset>();
            var groups = world.Features
                .Where(f => f.Mesh != null && !f.Mesh.IsEmpty)
                .GroupBy(f => f.Chunk)
                .OrderBy(g => g.Key.x).ThenBy(g => g.Key.y);

            AssetDatabase.StartAssetEditing();
            try
            {
                groundMesh = world.Ground.ToMesh("Ground", out _);
                AssetDatabase.CreateAsset(groundMesh, folder + "/Ground.asset");
                foreach (var group in groups)
                {
                    string path = folder + "/" + WorldMeshPipeline.ChunkName(group.Key) + ".asset";
                    Mesh main = null;
                    foreach (var feature in group.OrderBy(f => f.Kind).ThenBy(f => f.OsmId))
                    {
                        var mesh = feature.Mesh.ToMesh(feature.Group + "_" + feature.OsmId.ToString(CultureInfo.InvariantCulture), out var used);
                        if (main == null)
                        {
                            AssetDatabase.CreateAsset(mesh, path);
                            main = mesh;
                        }
                        else
                        {
                            AssetDatabase.AddObjectToAsset(mesh, main);
                        }
                        result[feature] = new MeshAsset(mesh, used);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            return result;
        }
    }
}
