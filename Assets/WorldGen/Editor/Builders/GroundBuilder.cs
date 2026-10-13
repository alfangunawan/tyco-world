using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Single ground quad covering the area; its 1 m thick box collider has its top face at y = 0.</summary>
    public static class GroundBuilder
    {
        public const float EdgeWallHeight = 30f;
        public const float EdgeWallThickness = 2f;

        public struct WallBox
        {
            public Vector3 Center;
            public Vector3 Size;
        }

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

        /// <summary>Four boxes (West, East, South, North) whose inner faces lie exactly on the bounds, from y = -1 to <paramref name="height"/>.</summary>
        public static WallBox[] EdgeWalls(Rect bounds, float height, float thickness)
        {
            float cy = (height - 1f) * 0.5f, sy = height + 1f, half = thickness * 0.5f;
            float spanX = bounds.width + 2f * thickness, spanZ = bounds.height + 2f * thickness;
            return new[]
            {
                new WallBox { Center = new Vector3(bounds.xMin - half, cy, bounds.center.y), Size = new Vector3(thickness, sy, spanZ) },
                new WallBox { Center = new Vector3(bounds.xMax + half, cy, bounds.center.y), Size = new Vector3(thickness, sy, spanZ) },
                new WallBox { Center = new Vector3(bounds.center.x, cy, bounds.yMin - half), Size = new Vector3(spanX, sy, thickness) },
                new WallBox { Center = new Vector3(bounds.center.x, cy, bounds.yMax + half), Size = new Vector3(spanX, sy, thickness) }
            };
        }
    }
}
