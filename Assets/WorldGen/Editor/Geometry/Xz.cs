using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Maps ground-plane Vector2 (x = east, y = north) to Unity 3D (x, height, z = north).</summary>
    public static class Xz
    {
        public static Vector3 At(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        public static Vector3 Dir(Vector2 d) => new Vector3(d.x, 0f, d.y);
    }
}
