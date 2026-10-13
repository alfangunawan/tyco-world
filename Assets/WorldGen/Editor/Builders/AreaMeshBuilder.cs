using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Triangulated flat polygon at a fixed height. UV = ground position in meters.</summary>
    public static class AreaMeshBuilder
    {
        public static bool AddFlatPolygon(MeshData mesh, int submesh, Polygon polygon, float y, bool faceDown = false)
        {
            var vertices = new List<Vector2>();
            var triangles = new List<int>();
            if (!Triangulator.Triangulate(polygon, vertices, triangles)) return false;
            var normal = faceDown ? Vector3.down : Vector3.up;
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector2 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                mesh.AddTriangle(submesh, Xz.At(a, y), Xz.At(b, y), Xz.At(c, y), a, b, c, normal);
            }
            return true;
        }
    }
}
