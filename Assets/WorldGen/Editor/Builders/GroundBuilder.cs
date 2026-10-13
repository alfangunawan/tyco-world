using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Single ground quad covering the area; its 1 m thick box collider has its top face at y = 0.</summary>
    public static class GroundBuilder
    {
        public static MeshData Build(Rect bounds)
        {
            var mesh = new MeshData(1);
            Vector2 a = new Vector2(bounds.xMin, bounds.yMin), b = new Vector2(bounds.xMax, bounds.yMin);
            Vector2 c = new Vector2(bounds.xMax, bounds.yMax), d = new Vector2(bounds.xMin, bounds.yMax);
            mesh.AddQuad(0, Xz.At(a, 0f), Xz.At(b, 0f), Xz.At(c, 0f), Xz.At(d, 0f), a, b, c, d, Vector3.up);
            return mesh;
        }

        public static void ColliderBox(Rect bounds, out Vector3 center, out Vector3 size)
        {
            center = new Vector3(bounds.center.x, -0.5f, bounds.center.y);
            size = new Vector3(bounds.width, 1f, bounds.height);
        }
    }
}
